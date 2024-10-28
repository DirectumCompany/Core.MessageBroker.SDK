using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Core.MessageBroker.ConfigurationValidation;
using Core.MessageBroker.Transport;
using Core.MessageBroker.Transport.Exceptions;
using Core.MessageBroker.Transport.Models;
using Core.MessageBroker.Transport.Plugin;
using Core.MessageBroker.Transport.Utils;
using ExampleTransport.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Polly;
using static System.Net.Mime.MediaTypeNames;

namespace ExampleTransport
{
  /// <summary>
  /// Транспортный посредник Example для отправки сообщений.
  /// </summary>
  public class ExampleTransportProxy : IMessageTransportProxy
  {
    /// <summary>
    /// Наименование плагина.
    /// </summary>
    private readonly string _pluginType = nameof(Plugin.ExampleTransportPlugin);

    /// <summary>
    /// Количество сообщений в секунду, которые позволяет отправлять транспорт.
    /// </summary>
    private readonly int _messagesPerSecond;

    /// <summary>
    /// Адрес подключения к сервису отправки сообщений.
    /// </summary>
    private readonly Uri _endpoint;

    /// <summary>
    /// Настройки транспортного посредника для Example.
    /// </summary>
    private readonly Configuration _proxyOptions;

    /// <summary>
    /// Утилиты для номера телефона.
    /// </summary>
    private readonly IPhoneNumberUtilities _phoneNumberUtilities;

    /// <summary>
    /// Логгер.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Системные часы.
    /// </summary>
    private readonly ISystemClock _systemClock;

    /// <summary>
    /// Фабрика HTTP клиентов.
    /// </summary>
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Инициализирует транспортный посредник.
    /// </summary>
    /// <param name="configurationService">Сервис получения конфигурации плагина.</param>
    /// <param name="phoneNumberUtilities">Утилиты для номера телефона.</param>
    /// <param name="loggerFactory">Фабрика логов.</param>
    /// <param name="systemClock">Системные часы.</param>
    /// <param name="httpClientFactory">Фабрика HTTP клиентов.</param>
    public ExampleTransportProxy(
      IConfigurationService configurationService,
      IPhoneNumberUtilities phoneNumberUtilities,
      ILoggerFactory loggerFactory,
      ISystemClock systemClock,
      IHttpClientFactory httpClientFactory = null)
    {
      _proxyOptions = configurationService.Get<Configuration>();

      SettingsValidator<Configuration>.Validate(
        _proxyOptions,
        new ConfigurationValidator(),
        $"Transport:Proxies:{_pluginType}");

      _logger = loggerFactory.CreateLogger(_pluginType);
      _systemClock = systemClock;
      _phoneNumberUtilities = phoneNumberUtilities;

      _endpoint = new UriBuilder(
        _proxyOptions.UseSsl ? "https" : "http",
        _proxyOptions.Host,
        _proxyOptions.Port,
        _proxyOptions.Path).Uri;

      _httpClientFactory = httpClientFactory;
      _messagesPerSecond = _proxyOptions.MessagesPerSecond;
    }

    /// <summary>
    /// Передаёт сообщение.
    /// </summary>
    /// <param name="message">Передаваемое сообщение.</param>
    public async Task TransmitAsync(Message message)
    {
      var requestQuota = ChooseQuotaPriority(message.Priority);

      CheckIdentityCredential(message);

      var messageText = GetMessageText(message);
      var phoneNumber = _phoneNumberUtilities.Normalize(message.Identity.CredentialValue);

      try
      {
        using var request = PrepareHttpRequestMessage(messageText, phoneNumber);
        using var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        using var response = await client.SendAsync(request);
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        var responseText = await reader.ReadToEndAsync();

        if (response.StatusCode != HttpStatusCode.OK)
        {
          var settings = new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } };
          var errorInfo = JsonConvert.DeserializeObject<ErrorInfo>(responseText, settings) ?? new ErrorInfo();

          throw errorInfo.Code switch
          {
            _ => new MessageDeliveryException(
              $"Ошибка доставки сообщения: {_pluginType} {errorInfo.Code} {errorInfo.Message}."),
          };
        }
      }
      catch (Exception ex) when
        (ex is IncorrectMessageDataException ||
         ex is TransportAuthorizeException ||
         ex is PriorityLimitException ||
         ex is MessageDeliveryException)
      {
        throw;
      }
      catch (Exception ex)
      {
        throw new MessageDeliveryException(
          $"Произошла непредвиденная ошибка при отправке сообщения: {_pluginType} {message.Id}.", ex);
      }
    }

    /// <summary>
    /// Возвращает количество сообщений в секунду, которые позволяет отправлять транспорт.
    /// </summary>
    /// <returns>
    /// Количество сообщений в секунду, которые позволяет отправлять транспорт.
    /// </returns>
    /// <remarks>
    /// Значение <c>0</c> означает "без ограничений".
    /// </remarks>
    public int GetMessagesPerSecond() => _messagesPerSecond;

    /// <summary>
    /// Возвращает количество попыток отправить сообщение.
    /// </summary>
    /// <returns>
    /// Количество попыток отправить сообщение.
    /// </returns>
    /// <remarks>
    /// Значение <c>null</c> означает "без ограничений"
    /// </remarks>
    public int? GetTrySendCount() => default;

    /// <summary>
    /// Выполняет проверку здоровья.
    /// </summary>
    /// <returns>
    /// Результат проверки здоровья.
    /// </returns>
    /// <remarks>
    /// Метод вызывается в общей проверки готовности хоста (/ready) и позволяет быстро определить
    /// все ли в порядке с транспортным посредником.
    /// </remarks>
    public async Task<HealthCheckResult> HealthCheck()
    {
      return await Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <summary>
    /// Возвращает текст сообщения.
    /// </summary>
    /// <param name="message">Модель данных сообщения.</param>
    /// <returns>Текст сообщения.</returns>
    private string GetMessageText(Message message)
    {
      if (string.IsNullOrWhiteSpace(message.Title) && string.IsNullOrWhiteSpace(message.Content))
      {
        throw new IncorrectMessageDataException($"Не задан ни заголовок, ни содержимое сообщения: {_pluginType} {message.Id}.");
      }

      return string.IsNullOrWhiteSpace(message.Content)
        ? message.Title
        : message.Content;
    }

    /// <summary>
    /// Проверяет, что реквизит получателя действительно задает и содержит номер телефона.
    /// </summary>
    /// <param name="message">Модель данных сообщения.</param>
    /// <exception cref="InvalidCredentialTypeException">Неверный тип реквизита получателя.</exception>
    /// <exception cref="IncorrectMessageDataException">Сообщение не содержит информацию о номере телефона.</exception>
    private void CheckIdentityCredential(Message message)
    {
      var identityCredentialType = message.Identity.CredentialType;

      if (!_phoneNumberUtilities.IsPhoneCredentialType(identityCredentialType))
      {
        throw new InvalidCredentialTypeException(identityCredentialType, _pluginType, message.Id);
      }

      if (string.IsNullOrWhiteSpace(message.Identity.CredentialValue))
      {
        throw new IncorrectMessageDataException(
          $"Сообщение не содержит информацию о номере телефона: {_pluginType} {message.Id}.");
      }
    }

    /// <summary>
    /// Подготавливает модель запроса.
    /// </summary>
    /// <param name="messageText">Текст сообщения.</param>
    /// <param name="phoneNumber">Номер телефона.</param>
    /// <returns>Модель запроса.</returns>
    private HttpRequestMessage PrepareHttpRequestMessage(string messageText, string phoneNumber)
    {
      var body = new MessageInfo
      {
        PhoneNumber = phoneNumber,
        Content = messageText,
        Sender = _proxyOptions.Sender,
      };
      var jsonData = JsonConvert.SerializeObject(body);
      var httpContent = new StringContent(jsonData, Encoding.UTF8, Application.Json);
      var authenticationString = $"{_proxyOptions.Username}:{_proxyOptions.Password}";
      var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.UTF8.GetBytes(authenticationString));

      var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
      {
        Content = httpContent,
      };
      request.SetPolicyExecutionContext(new Context { [HeaderNames.Host] = _endpoint.Host });
      request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);

      return request;
    }

    /// <summary>
    /// Определяет квота какого приоритета будет израсходована при попытки отправить сообщения с данным приоритетом.
    /// </summary>
    /// <param name="messagePriority">Приоритет отправляемого сообщения</param>
    /// <returns>Приоритет, чью квоту следует расходовать.</returns>
    /// <exception cref="PriorityLimitException">Ошибка из-за достижения лимита сообщений.</exception>
    /// <exception cref="TransportPendingException">Ошибка ожидания транспорта.</exception>
    private MessagePriority ChooseQuotaPriority(MessagePriority messagePriority)
    {
      return messagePriority;
    }
  }
}
