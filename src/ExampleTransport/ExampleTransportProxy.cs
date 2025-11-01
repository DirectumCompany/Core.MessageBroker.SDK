using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Core.MessageBroker.Transport;
using Core.MessageBroker.Transport.Exceptions;
using Core.MessageBroker.Transport.Models;
using Core.MessageBroker.Transport.Plugin;
using Core.MessageBroker.Transport.Utils;
using Core.MessageBroker.Utilities;
using ExampleTransport.Models;
using Microsoft.Net.Http.Headers;
using Polly;
using static System.Net.Mime.MediaTypeNames;

namespace ExampleTransport
{
  /// <summary>
  /// Прокси-сервис провайдера отправки сообщений.
  /// </summary>
  public class ExampleTransportProxy : IMessageTransportProxy
  {
    /// <summary>
    /// Наименование плагина.
    /// </summary>
    private readonly string _pluginType = nameof(Plugin.ExampleTransportPlugin);

    /// <summary>
    /// Адрес подключения к сервису отправки сообщений.
    /// </summary>
    private readonly Uri _endpoint;

    /// <summary>
    /// Настройки транспортного посредника для Example.
    /// </summary>
    private readonly Configuration _proxyOptions;

    /// <summary>
    /// Логгер.
    /// </summary>
    private readonly IPluginLogger _logger;

    /// <summary>
    /// Утилиты для номера телефона.
    /// </summary>
    private readonly IPhoneNumberUtilities _phoneNumberUtilities;

    /// <summary>
    /// Системные часы.
    /// </summary>
    private readonly ISystemClock _systemClock;

    /// <summary>
    /// Фабрика HTTP клиентов.
    /// </summary>
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Инициализирует прокси-сервис провайдера отправки сообщений.
    /// </summary>
    /// <param name="logger">Логгер.</param>
    /// <param name="configurationService">Сервис получения конфигурации плагина.</param>
    /// <param name="phoneNumberUtilities">Утилиты для номера телефона.</param>
    /// <param name="systemClock">Системные часы.</param>
    /// <param name="httpClientFactory">Фабрика HTTP клиентов.</param>
    public ExampleTransportProxy(
      IPluginLogger logger,
      IConfigurationService configurationService,
      IPhoneNumberUtilities phoneNumberUtilities,
      ISystemClock systemClock,
      IHttpClientFactory httpClientFactory)
    {
      _logger = logger;
      _proxyOptions = configurationService.Get<Configuration>();

      SettingsValidator<Configuration>.Validate(
        _proxyOptions,
        new ConfigurationValidator(),
        $"Transport:Proxies:{_pluginType}");

      _systemClock = systemClock;
      _phoneNumberUtilities = phoneNumberUtilities;

      _endpoint = new UriBuilder(
        _proxyOptions.UseSsl ? "https" : "http",
        _proxyOptions.Host,
        _proxyOptions.Port,
        _proxyOptions.Path).Uri;

      _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Передаёт сообщение.
    /// </summary>
    /// <param name="message">Передаваемое сообщение.</param>
    public async Task<TransmitResult> TransmitAsync(Message message)
    {
      try
      {
        _logger.LogDebug(LogHelper.StartMessageTemplate, message?.Id);

        CheckIdentityCredential(message);
        var messageText = GetMessageText(message);
        var phoneNumber = _phoneNumberUtilities.Normalize(message.Identity.CredentialValue);
        using var request = PrepareHttpRequestMessage(messageText, phoneNumber);
        using var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        using var httpResponse = await client.SendAsync(request);
        await HandleResponseAsync(message, httpResponse);

        _logger.LogDebug(LogHelper.EndMessageTemplate, message.Id);

        return new TransmitResult
        {
          IsSuccess = true,
        };
      }
      catch (PluginAppException ex)
      {
        _logger.LogWarning(LogHelper.ErrorMessageTemplate, message?.Id, ex.Message);

        return new TransmitResult
        {
          IsSuccess = ex.IsSuccess,
          Error = ex.Error,
        };
      }
      catch (IncorrectPhoneNumberException ex)
      {
        _logger.LogWarning(LogHelper.ErrorMessageTemplate, message?.Id, ex.Message);

        return new TransmitResult
        {
          IsSuccess = false,
          Error = new Error(ex.Message, ErrorCode.IncorrectPhoneNumberError),
        };
      }
      catch (Exception ex)
      {
        _logger.LogWarning(LogHelper.ExceptionMessageTemplate, message?.Id, ex.Message, ex.StackTrace);

        return new TransmitResult
        {
          IsSuccess = false,
          Error = new Error(ex.Message, ex.StackTrace),
        };
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
    public int GetMessagesPerSecond() => _proxyOptions.MessagesPerSecond;

    /// <summary>
    /// Возвращает количество попыток отправить сообщение.
    /// </summary>
    /// <returns>
    /// Количество попыток отправить сообщение.
    /// </returns>
    /// <remarks>
    /// Значение <c>null</c> означает "без ограничений"
    /// </remarks>
    public int? GetTrySendCount() => _proxyOptions.MaxTransmitRetryCount;

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
      try
      {
        _logger.LogDebug(LogHelper.HealthCheckStartMessage);

        var result = await Task.FromResult(HealthCheckResult.Healthy());

        _logger.LogDebug(LogHelper.HealthCheckEndMessage);

        return result;
      }
      catch (Exception ex)
      {
        _logger.LogError(LogHelper.HealthCheckExceptionTemplate, ex.Message, ex.StackTrace);

        return HealthCheckResult.Unhealthy(ex.Message);
      }
    }

    /// <summary>
    /// Возвращает текст сообщения.
    /// </summary>
    /// <param name="message">Модель данных сообщения.</param>
    /// <returns>Текст сообщения.</returns>
    private static string GetMessageText(Message message)
    {
      if (string.IsNullOrWhiteSpace(message.Title) && string.IsNullOrWhiteSpace(message.Content))
      {
        throw new PluginAppException(new Error(
          "Не задан ни заголовок, ни содержимое сообщения.",
          ErrorCode.IncorrectMessageDataError));
      }

      return string.IsNullOrWhiteSpace(message.Content)
        ? message.Title
        : message.Content;
    }

    /// <summary>
    /// Проверяет, что реквизит получателя действительно задает и содержит номер телефона.
    /// </summary>
    /// <param name="message">Модель данных сообщения.</param>
    private void CheckIdentityCredential(Message message)
    {
      var identityCredentialType = message.Identity.CredentialType;

      if (!_phoneNumberUtilities.IsPhoneCredentialType(identityCredentialType))
      {
        throw new PluginAppException(new Error(
          $"Тип реквизита получателя '{identityCredentialType}' в сообщении {message.Id} является некорректным для плагина {_pluginType}.",
          ErrorCode.InvalidCredentialTypeError));
      }

      if (string.IsNullOrWhiteSpace(message.Identity.CredentialValue))
      {
        throw new PluginAppException(new Error(
          "Сообщение не содержит информацию о номере телефона.",
          ErrorCode.IncorrectMessageDataError));
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
      var body = new Request
      {
        PhoneNumber = phoneNumber,
        Content = messageText,
        Sender = _proxyOptions.Sender,
      };
      var jsonData = JsonSerializer.Serialize(body);
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
    /// Обрабатывает ошибки отправки сообщения.
    /// </summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="httpResponse">Ответ на отправку сообщения.</param>
    private async Task HandleResponseAsync(Message message, HttpResponseMessage httpResponse)
    {
      var responseBody = await httpResponse?.Content?.ReadAsStringAsync();
      try
      {
        _ = JsonSerializer.Deserialize<Response>(responseBody);
      }
      catch (Exception ex)
      {
        throw new PluginAppException(new Error(
          $"Пришел неожиданный ответ. Не удалось десериализовать ответ {_pluginType}: {ex.Message}. ID сообщения: {message.Id}",
          ErrorCode.MessageDeliveryError));
      }

      if (httpResponse?.StatusCode != HttpStatusCode.OK)
      {
        responseBody = responseBody[..499] + (responseBody.Length > 500 ? "..." : default);
        throw (httpResponse?.StatusCode ?? default) switch
        {
          HttpStatusCode.ServiceUnavailable => new PluginAppException(new Error(
            $"Ошибка ожидания транспорта {_pluginType} {responseBody}. ID сообщения: {message.Id}.",
            ErrorCode.TransportPendingError)),
          HttpStatusCode.TooManyRequests => new PluginAppException(new Error(
            $"Ошибка ожидания транспорта {_pluginType} {responseBody}. ID сообщения: {message.Id}.",
            ErrorCode.TransportPendingError)),
          HttpStatusCode.InternalServerError => new PluginAppException(new Error(
            $"Не удалось отправить сообщение {_pluginType} {responseBody}. ID сообщения: {message.Id}.",
            ErrorCode.MessageTransmitError)),
          HttpStatusCode.BadRequest => new PluginAppException(new Error(
            $"Ошибка в реквизитах сообщения {_pluginType} {responseBody}. ID сообщения: {message.Id}.",
            ErrorCode.IncorrectMessageDataError)),
          HttpStatusCode.NotAcceptable => new PluginAppException(new Error(
            $"Ошибка в реквизитах сообщения {_pluginType} {responseBody}. ID сообщения: {message.Id}.",
            ErrorCode.IncorrectMessageDataError)),
          HttpStatusCode.Unauthorized => new PluginAppException(new Error(
            $"Ошибка авторизации транспорта {_pluginType} {responseBody}. ID сообщения: {message.Id}.",
            ErrorCode.TransportAuthorizeError)),
          HttpStatusCode.Forbidden => new PluginAppException(new Error(
            $"Ошибка авторизации транспорта {_pluginType} {responseBody}. ID сообщения: {message.Id}.",
            ErrorCode.TransportAuthorizeError)),
          HttpStatusCode.OK => new PluginAppException(new Error(
            $"Не удалось отправить сообщение т.к. пришел неожиданный ответ {_pluginType} {responseBody}. ID сообщения: {message.Id}.",
            ErrorCode.MessageDeliveryError)),
          _ => new PluginAppException(new Error(
            $"Ошибка во время доставки сообщения {_pluginType} {responseBody}. ID сообщения: {message.Id}.",
            ErrorCode.MessageDeliveryError)),
        };
      }
    }
  }
}
