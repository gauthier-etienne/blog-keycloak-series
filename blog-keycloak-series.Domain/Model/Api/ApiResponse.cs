using System.Text.Json.Serialization;

namespace blog_keycloak_series.Domain.Model.Api;

public record ApiResponse(
    [property: JsonPropertyName("data")] object? Data, 
    [property: JsonPropertyName("messages")] List<ApiMessage>? Messages
);