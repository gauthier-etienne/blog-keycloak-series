using System.Text.Json;
using System.Text.Json.Serialization;

namespace blog_keycloak_series.Domain.Converters;

public class JsonGuidConverter : JsonConverter<Guid>
{
    public override Guid Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
                Guid.TryParse(reader.GetString(), out var result) ? result : Guid.Empty;

    public override void Write(
            Utf8JsonWriter writer,
            Guid value,
            JsonSerializerOptions options) =>
                writer.WriteStringValue(value.ToString());
}
