using System.Text.Json;
using Asp.Versioning.Builder;
using blog_keycloak_series.Domain.Model;
using blog_keycloak_series.Domain.Model.Api;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;

namespace blog_keycloak_series.ApiService.Endpoints.Movies;

public static class GetNowPlayingMoviesEndpoint
{
    private const string Name = "GetNowPlayingMovies";
    
    public static IEndpointRouteBuilder MapGetNowPlayingMoviesEndpoint(this IEndpointRouteBuilder app, ApiVersionSet versionSet)
    {
        app.MapGet(ApiEndpoints.Movies.NowPlaying, async Task<Results<Ok<ApiResponse>, BadRequest<List<string>>>>
        ([FromServices] HybridCache cache, [FromServices] IHttpClientFactory httpClientFactory, CancellationToken token) =>
        {
            //var options = new RestClientOptions("https://api.themoviedb.org/3/movie/now_playing?language=en-US&page=1");
            var httpClient = httpClientFactory.CreateClient("TMDB");
            var response = await httpClient.GetAsync("movie/now_playing?language=en-US&page=1", token);

            response.EnsureSuccessStatusCode();

            var stringResponse = await response.Content.ReadAsStringAsync(token);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<TmdbMovieResponse>(stringResponse, options);

            var results = data?.Results;

            return TypedResults.Ok(new ApiResponse(
                Data: results,
                Messages:
                [
                    new ApiMessage(ApiMessageType.Success, "Movies have been retrieved")
                ]
            ));
        })
        .WithName(Name)
        .WithApiVersionSet(versionSet)
        .HasApiVersion(1.0)
        .RequireAuthorization("MovieUser")
        .Produces<ApiResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags(ApiEndpoints.Movies.Tag);

        return app;
    }
}