using System.Text.Json.Serialization;

namespace blog_keycloak_series.Domain.Model;

public record Movie(
    int Id,
    string Title,
    string ImdbId,
    [property: JsonPropertyName("genre_ids")] int[] GenreIds,
    [property: JsonPropertyName("release_date")] string ReleaseDate,
    [property: JsonPropertyName("poster_path")] string PosterPath,
    [property: JsonPropertyName("backdrop_path")] string BackdropPath,
    string Overview,
    [property: JsonPropertyName("original_language")] string OriginalLanguage,
    [property: JsonPropertyName("original_title")] string OriginalTitle
);