using System.Text.Json.Serialization;
using backend.Enums;

namespace backend.Dtos.Auth
{
    public class RequestTwoFactorDto
    {
        public Guid UserId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public OtpDeliveryMethod Method { get; set; } = OtpDeliveryMethod.Sms;
    }
}
