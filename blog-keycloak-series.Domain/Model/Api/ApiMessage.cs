using System.Text.Json.Serialization;

namespace blog_keycloak_series.Domain.Model.Api;

public record ApiMessage([property: JsonPropertyName("messageType")] ApiMessageType MessageType, string Message);