using Asp.Versioning;
using blog_keycloak_series.ApiService.Endpoints.Movies;
using blog_keycloak_series.Domain.Converters;
using blog_keycloak_series.Domain.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(60);
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme) // ← tells UseAuthentication() which handler to invoke
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8888/realms/movie-library";
        options.Audience = "movielibraryapi";
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.MapInboundClaims = false; // Keep JWT claim names as-is (no remapping to ClaimTypes.* URIs)
        options.TokenValidationParameters.RoleClaimType = "roles"; // Keycloak emits roles as "roles" array
    });

//builder.Services.AddAuthorization();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("MovieUser", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("movie-user"))
    .AddPolicy("MovieAdmin", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("movie-admin"));

builder.AddRedisDistributedCache(connectionName: "cache");
builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024 * 100; // 100MB
    options.MaximumKeyLength = 1024;
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});

// Add services to the container.
builder.Services.AddProblemDetails();

builder.Services.AddNpgsqlDataSource("keycloakpkceDb");

var tmdbInfo = new TheMovieDbInfo(
    builder.Configuration["TheMovieDb:ApiKey"] ?? throw new InvalidOperationException(), 
    builder.Configuration["TheMovieDb:ApiReadAccessKey"] ?? throw new InvalidOperationException()
);

builder.Services.AddSingleton(tmdbInfo);

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new HeaderApiVersionReader("x-api-version");
});

builder.Services.AddHttpClient("TMDB", opt =>
{
    opt.BaseAddress = new Uri("https://api.themoviedb.org/3/");
    opt.DefaultRequestHeaders.Add("Accept", "application/json");
    opt.DefaultRequestHeaders.Add("Authorization", $"Bearer {tmdbInfo.ApiReadAccessKey}");
    opt.DefaultRequestHeaders.Add("User-Agent", "MovieLibrary");
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes?["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your Keycloak JWT access token"
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer")] = []
        });

        return Task.CompletedTask;
    });
});
builder.Services.AddEndpointsApiExplorer();

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.AllowTrailingCommas = false;
    options.SerializerOptions.MaxDepth = 100;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.Converters.Add(new JsonGuidConverter());
});

builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Movie Library API")
        .WithTheme(ScalarTheme.Mars)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .AddPreferredSecuritySchemes("Bearer")
        .SortTagsAlphabetically()
        // .WithDocumentDownloadType(DocumentDownloadType.Json)
        .WithDotNetFlag();

    if (app.Environment.IsProduction() && app.Environment.IsStaging())
    {
        options.HideTestRequestButton();
    }
});

var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

// app.MapApiEndpoints(versionSet);
app.MapGetNowPlayingMoviesEndpoint(versionSet);
app.MapGetPopularMoviesEndpoint(versionSet);
app.MapGetTopRatedMoviesEndpoint(versionSet);
app.MapGetUpcomingMoviesEndpoint(versionSet);

app.UseHttpsRedirection();

app.Run();