namespace blog_keycloak_series.ApiService.Endpoints;

public static class ApiEndpoints
{
    private const string ApiBase = "api";
    
    public static class Movies
    {
        private const string Base = $"{ApiBase}/movies";

        public const string Tag = "Movies";

        public const string Get = Base;

        public const string Upcoming = $"{Base}/upcoming";
        public const string TopRated = $"{Base}/toprated";
        public const string Popular = $"{Base}/popular";
        public const string NowPlaying = $"{Base}/nowplaying";
    }
}