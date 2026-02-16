using blog_keycloak_series.Domain.Model.Api;
using System.Text.Json;

namespace blog_keycloak_series.Web;

public class MoviesApiClient(HttpClient httpClient)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<ApiResponse?> GetUpcomingMoviesAsync()
    {
        var response = await httpClient.GetAsync("api/movies/upcoming");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse>(json, _jsonOptions);
    }

    public async Task<ApiResponse?> GetNowPlayingMoviesAsync()
    {
        var response = await httpClient.GetAsync("api/movies/nowplaying");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse>(json, _jsonOptions);
    }

    public async Task<ApiResponse?> GetPopularMoviesAsync()
    {
        var response = await httpClient.GetAsync("api/movies/popular");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse>(json, _jsonOptions);
    }

    public async Task<ApiResponse?> GetTopRatedMoviesAsync()
    {
        var response = await httpClient.GetAsync("api/movies/toprated");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse>(json, _jsonOptions);
    }
}