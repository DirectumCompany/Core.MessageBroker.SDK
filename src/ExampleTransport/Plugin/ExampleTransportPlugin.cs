using System.Threading.Tasks;
using Core.MessageBroker.Transport;
using Core.MessageBroker.Transport.Models;
using Core.MessageBroker.Transport.Plugin;
using Prise.Plugin;

namespace ExampleTransport.Plugin
{
  /// <summary>
  /// Транспортный плагин.
  /// </summary>
  /// <remarks>
  /// Точка входа для работы с плагином.
  /// </remarks>
  [Plugin(PluginType = typeof(IMessageTransportPlugin))]
  public class ExampleTransportPlugin : IMessageTransportPlugin
  {
    /// <summary>
    /// Сервис транспортного посредника.
    /// </summary>
    /// <remarks>
    /// Атрибут <see cref=" PluginServiceAttribute"/> говорит о том,
    /// что нужно получить эту зависимость из загрузчика <see cref="ExampleTransportPluginBootstrapper"/>.
    /// </remarks>
    [PluginService(ProvidedBy = ProvidedBy.Plugin, ServiceType = typeof(IMessageTransportProxy))]
    private readonly IMessageTransportProxy _messageTransportProxy;

    /// <summary>
    /// Передаёт сообщение.
    /// </summary>
    /// <param name="message">Передаваемое сообщение.</param>
    public async Task TransmitAsync(Message message) => await _messageTransportProxy.TransmitAsync(message);

    /// <summary>
    /// Возвращает количество сообщений в секунду, которые позволяет отправлять транспорт.
    /// </summary>
    /// <returns>
    /// Количество сообщений в секунду, которые позволяет отправлять транспорт.
    /// </returns>
    /// <remarks>
    /// Значение <c>0</c> означает "без ограничений".
    /// </remarks>
    public int GetMessagesPerSecond() => _messageTransportProxy.GetMessagesPerSecond();

    /// <summary>
    /// Возвращает количество попыток отправить сообщение.
    /// </summary>
    /// <returns>
    /// Количество попыток отправить сообщение.
    /// </returns>
    /// <remarks>
    /// Значение <c>null</c> означает "без ограничений"
    /// </remarks>
    public int? GetTrySendCount() => _messageTransportProxy.GetTrySendCount();

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
    public async Task<HealthCheckResult> HealthCheck() => await _messageTransportProxy.HealthCheck();
  }
}
