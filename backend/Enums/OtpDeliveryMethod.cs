using System.Text.Json.Serialization;

namespace backend.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OtpDeliveryMethod
{
    Email = 0,
    Sms = 1
}
