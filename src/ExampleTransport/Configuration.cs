using Core.MessageBroker.Logger;
using Core.MessageBroker.Transport.Models;

namespace ExampleTransport
{
  /// <summary>
  /// Настройки транспортного посредника для Example.
  /// </summary>
  internal class Configuration : ConfigurationBase
  {
    /// <summary>
    /// Имя отправителя.
    /// </summary>
    [LoggableOption]
    public string Sender { get; set; }
  }
}
