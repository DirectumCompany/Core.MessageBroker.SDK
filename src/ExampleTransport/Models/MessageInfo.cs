using System.Text.Json.Serialization;

namespace ExampleTransport.Models
{
  /// <summary>
  /// Сообщение.
  /// </summary>
  internal class MessageInfo
  {
    /// <summary>
    /// Отправитель сообщения.
    /// </summary>
    [JsonPropertyName("phone")]
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Контент сообщения.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; }

    /// <summary>
    /// Отправитель сообщения.
    /// </summary>
    [JsonPropertyName("sender")]
    public string Sender { get; set; }
  }
}
