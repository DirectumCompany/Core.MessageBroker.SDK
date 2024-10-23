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
  [Plugin(PluginType = typeof(IMessageTransportPlugin))]
  public class ExampleTransportPlugin : IMessageTransportPlugin
  {
    /// <summary>
    /// Сервис транспортного посредника.
    /// </summary>
    [PluginService(ProvidedBy = ProvidedBy.Plugin, ServiceType = typeof(IMessageTransportProxy))]
    private readonly IMessageTransportProxy _messageTransportProxy;

    /// <inheritdoc/>
    public async Task TransmitAsync(Message message) => await _messageTransportProxy.TransmitAsync(message);

    /// <inheritdoc/>
    public int GetMessagesPerSecond() => _messageTransportProxy.GetMessagesPerSecond();

    /// <inheritdoc/>
    public int? GetTrySendCount() => _messageTransportProxy.GetTrySendCount();

    /// <inheritdoc/>
    public async Task<double> HealthCheck(HealthCheckErrorMessage errorMessage = null) => await _messageTransportProxy.HealthCheck(errorMessage);
  }
}
