using System.Text.Json.Serialization;

namespace ExampleTransport.Models
{
  /// <summary>
  /// Информация об ошибке.
  /// </summary>
  public class ErrorInfo
  {
    /// <summary>
    /// Код ошибки.
    /// </summary>
    [JsonPropertyName("error_code")]
    public int Code { get; set; }

    /// <summary>
    /// Сообщение об ошибке.
    /// </summary>
    [JsonPropertyName("error_message")]
    public string Message { get; set; }
  }
}
