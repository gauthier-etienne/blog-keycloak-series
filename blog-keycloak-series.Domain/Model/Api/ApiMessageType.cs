using System.Text.Json.Serialization;

namespace blog_keycloak_series.Domain.Model.Api;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApiMessageType
{
    Warning,
    Error,
    Info,
    Success
}