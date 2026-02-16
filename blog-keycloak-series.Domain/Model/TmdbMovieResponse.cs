using System.Text.Json.Serialization;

namespace blog_keycloak_series.Domain.Model;
public class TmdbMovieResponse
{
    [JsonPropertyName("results")]
    public List<Movie> Results { get; set; }
}
