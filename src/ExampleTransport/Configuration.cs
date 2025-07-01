using Core.MessageBroker.Transport.Models;
using Core.MessageBroker.Utilities.Logger;

namespace ExampleTransport
{
  /// <summary>
  /// Настройки транспортного посредника для Example.
  /// </summary>
  /// <remarks>
  /// Расширяет базовый класс настроек <see cref="ConfigurationBase"/>.
  /// Базовый класс содержит:
  /// * Host - имя хоста;
  /// * Port - номер порта;
  /// * Path - путь для подключения к сервису;
  /// * UseSsl - признак необходимости использования SSL;
  /// * Username - имя пользователя;
  /// * Password - пароль пользователя;
  /// * MessagesPerSecond - количество сообщений в секунду, которые позволяет отправлять транспорт. 0, если неограниченно.
  /// * MaxTransmitRetryCount - количество попыток отправить сообщение. <c>null</c>, если неограниченно.
  /// </remarks>
  internal class Configuration : ConfigurationBase
  {
    /// <summary>
    /// Имя отправителя.
    /// </summary>
    [LoggableOption]
    public string Sender { get; set; }
  }
}
