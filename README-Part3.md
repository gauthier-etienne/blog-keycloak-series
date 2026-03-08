# Blog Keycloak Series - Part 3: Securing APIs with Bearer Tokens (JWT)

## Overview

This is **Part 3** of the Blog Keycloak Series. Building on the foundation from [Part 1](README-Part1.md) (OIDC authentication) and [Part 2](README-Part2.md) (Social Login), this part focuses on the backend: **securing API endpoints with JWT Bearer token validation**.

By the end of this part, your `ApiService` will reject unauthenticated requests with a `401 Unauthorized`, and your Blazor frontend will transparently forward the user's Keycloak access token to each API call — all without the user ever noticing.

## What You'll Learn

- ✅ How JWT Bearer authentication works in a .NET Minimal API
- ✅ How to validate Keycloak-issued tokens using `Aspire.Keycloak.Authentication`
- ✅ How the access token flows from Keycloak → Blazor → ApiService
- ✅ How to protect endpoints with `.RequireAuthorization()`
- ✅ How to configure Scalar and OpenAPI with Bearer token support
- ✅ How to configure a Keycloak audience mapper for production

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Keycloak)
- IDE: Visual Studio 2022+ or VS Code with C# DevKit
- Completion of [Part 1](README-Part1.md) and [Part 2](README-Part2.md)

## Understanding JWT Bearer Authentication

When a user logs in through your Blazor app, Keycloak issues three tokens:

| Token | Purpose | Lifetime |
|-------|---------|---------|
| **ID Token** | Who the user is (identity claims) | Short |
| **Access Token** | What the user can do (authorization) | Short (5 min default) |
| **Refresh Token** | Used to get new access tokens silently | Longer (30 min default) |

The **access token** is a signed JWT (JSON Web Token) that your API service can independently validate — **without calling Keycloak** — by checking the signature against Keycloak's public keys (JWKS endpoint).

A decoded Keycloak access token looks like this:

```json
{
  "exp": 1735000000,
  "iat": 1734999700,
  "iss": "http://localhost:8888/realms/movie-library",
  "aud": ["account"],
  "sub": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "preferred_username": "etienne",
  "email": "etienne@example.com",
  "realm_access": {
    "roles": ["offline_access", "uma_authorization", "default-roles-movie-library"]
  }
}
```

The API validates:
1. **Signature** — was this signed by Keycloak? (using JWKS public keys)
2. **Issuer** — does `iss` match our Keycloak realm?
3. **Expiration** — has the token expired?
4. **Audience** — is this token meant for our API? (covered later)

## The Token Flow

Here's the complete flow showing how a JWT Bearer token reaches your API:

```
┌──────────┐          ┌──────────────────┐          ┌───────────────┐          ┌──────────────────┐
│  Browser │          │   Blazor Server  │          │   Keycloak    │          │   ApiService     │
└────┬─────┘          └────────┬─────────┘          └──────┬────────┘          └────────┬─────────┘
     │                         │                           │                            │
     │  1. Visit /movies page  │                           │                            │
     │────────────────────────▶│                           │                            │
     │                         │                           │                            │
     │                         │  2. User not authenticated                             │
     │  3. Redirect to login   │                           │                            │
     │◀────────────────────────│                           │                            │
     │                         │                           │                            │
     │  4. OIDC + PKCE login ─────────────────────────────▶│                            │
     │◀─────────────────────────────────── 5. Access Token │                            │
     │                         │           (stored in      │                            │
     │                         │            server cookie) │                            │
     │                         │                           │                            │
     │  6. Request /movies     │                           │                            │
     │────────────────────────▶│                           │                            │
     │                         │                           │                            │
     │                         │  7. AuthenticatedHttpMessageHandler                   │
     │                         │     reads access_token from cookie                    │
     │                         │     adds: Authorization: Bearer <token>               │
     │                         │                                                        │
     │                         │  8. GET /api/movies/popular ──────────────────────────▶│
     │                         │     Authorization: Bearer eyJhbGci...                 │
     │                         │                                                        │
     │                         │                                          9. Validate JWT:
     │                         │                                             - Signature ✓
     │                         │                                             - Issuer ✓
     │                         │                                             - Expiry ✓
     │                         │                                                        │
     │                         │  10. 200 OK + Movie data ◀────────────────────────────│
     │                         │                                                        │
     │  11. Rendered page      │                                                        │
     │◀────────────────────────│                                                        │
```

> **Key insight:** The `AuthenticatedHttpMessageHandler` (already in place from Part 1!) is the bridge between the Blazor cookie session and the API's Bearer scheme. The user never sees a token — it's all handled server-side.

## Reviewing the AuthenticatedHttpMessageHandler

Before writing new code, let's appreciate what's already in `blog-keycloak-series.Web/AuthenticatedHttpMessageHandler.cs`:

```csharp
public class AuthenticatedHttpMessageHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticatedHttpMessageHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.User.Identity?.IsAuthenticated == true)
        {
            var accessToken = await httpContext.GetTokenAsync("access_token");

            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

This handler:
1. Checks if the current Blazor user is authenticated
2. Retrieves the `access_token` stored in the OIDC cookie (thanks to `SaveTokens = true` from Part 1)
3. Appends it as a `Bearer` token to every outgoing API request

This was already wired up in Part 1 — we just need the **API side** to validate it.

## What Changes in Part 3

| File | Change |
|------|--------|
| `ApiService/Program.cs` | Add JWT Bearer auth, authorization middleware, OpenAPI Bearer scheme, update Scalar config |
| `ApiService/Endpoints/Movies/*.cs` | Add `.RequireAuthorization()` to each endpoint |
| Keycloak Admin | Add Audience mapper to the `movielibraryweb` client (recommended for production) |

## Implementation

### Step 1: Configure JWT Bearer Authentication in the ApiService

Open `blog-keycloak-series.ApiService/Program.cs` and add the following after `builder.AddServiceDefaults()`:

To embed a GitHub Gist, use this HTML syntax:


<script src="https://gist.github.com/gauthier-etienne/9b43cf8f2fc387c82e00380cbbd21361.js"></script>



```csharp
// Add JWT Bearer authentication via Aspire Keycloak integration
builder.AddKeycloakJwtBearer(
    serviceName: "keycloak",
    realm: "movie-library",
    configureOptions: options =>
    {
        options.RequireHttpsMetadata = false; // ⚠️ Dev only — set to true in production
    });

// Add authorization services
builder.Services.AddAuthorization();
```

The `AddKeycloakJwtBearer` extension method (from `Aspire.Keycloak.Authentication`) automatically:
- Reads the Keycloak URL from the Aspire service discovery connection string (`"keycloak"`)
- Sets the **Authority** to `{keycloakUrl}/realms/movie-library`
- Configures **JWKS** signature validation using Keycloak's public keys
- Sets the **Issuer** to match the realm URL

No `appsettings.json` changes needed — Aspire handles service discovery!

> **Why does this work?** In `AppHost.cs`, the `apiservice` already has `.WithReference(keycloak)`, which injects the Keycloak connection string into the ApiService's configuration. `AddKeycloakJwtBearer` picks it up by service name.

### Step 2: Add Authentication & Authorization Middleware

In the same `Program.cs`, add the middleware **after** `app.MapDefaultEndpoints()` and **before** `app.UseHttpsRedirection()`:

```csharp
// ⚠️ Order matters! Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();
```

The full middleware pipeline should look like:

```csharp
var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.MapOpenApi();
app.MapScalarApiReference(/* ... */);

// Map your versioned endpoints
var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.MapGetNowPlayingMoviesEndpoint(versionSet);
app.MapGetPopularMoviesEndpoint(versionSet);
app.MapGetTopRatedMoviesEndpoint(versionSet);
app.MapGetUpcomingMoviesEndpoint(versionSet);

// ✅ Add these in the correct order
app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();

app.Run();
```

### Step 3: Configure OpenAPI with a Bearer Security Scheme

To test protected endpoints via the Scalar UI, the OpenAPI document needs to declare the Bearer security scheme. In `Program.cs`, update `AddOpenApi()`:

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        // Declare the Bearer security scheme
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your Keycloak JWT access token"
        };

        // Apply security globally to all operations
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = "Bearer",
                    Type = ReferenceType.SecurityScheme
                }
            }] = []
        });

        return Task.CompletedTask;
    });
});
```

You'll need these using statements at the top of `Program.cs`:

```csharp
using Microsoft.OpenApi.Models;
```

### Step 4: Update Scalar to Enable Bearer Authentication

Update the `MapScalarApiReference` call in `Program.cs`:

```csharp
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Movie Library API")
        .WithTheme(ScalarTheme.Mars)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .AddPreferredSecuritySchemes("Bearer")   // ✅ Uncomment this
        .SortTagsAlphabetically()
        .WithDotNetFlag();

    if (app.Environment.IsProduction() && app.Environment.IsStaging())
    {
        options.HideTestRequestButton();
    }
});
```

### Step 5: Protect the Endpoints

Add `.RequireAuthorization()` to each endpoint. Here is the updated pattern for all four movie endpoints:

**`GetNowPlayingMoviesEndpoint.cs`:**

```csharp
app.MapGet(ApiEndpoints.Movies.NowPlaying, async Task<Results<Ok<ApiResponse>, BadRequest<List<string>>>>
    ([FromServices] HybridCache cache, [FromServices] IHttpClientFactory httpClientFactory, CancellationToken token) =>
    {
        // ... existing handler code unchanged ...
    })
    .WithName(Name)
    .WithApiVersionSet(versionSet)
    .HasApiVersion(1.0)
    .RequireAuthorization()                    // ✅ Add this
    .Produces<ApiResponse>()
    .Produces(StatusCodes.Status401Unauthorized) // ✅ Add this for OpenAPI docs
    .Produces(StatusCodes.Status403Forbidden)    // ✅ Add this for OpenAPI docs
    .Produces(StatusCodes.Status404NotFound)
    .WithTags(ApiEndpoints.Movies.Tag);
```

Apply the same change to `GetPopularMoviesEndpoint.cs`, `GetTopRatedMoviesEndpoint.cs`, and `GetUpcomingMoviesEndpoint.cs`.

> **Tip:** If you want all endpoints to be protected by default, you can add a global policy instead:
> ```csharp
> builder.Services.AddAuthorizationBuilder()
>     .SetFallbackPolicy(new AuthorizationPolicyBuilder()
>         .RequireAuthenticatedUser()
>         .Build());
> ```
> This protects every endpoint automatically. Individual endpoints can opt out with `.AllowAnonymous()`.

## Keycloak Audience Mapper (Recommended for Production)

By default, a Keycloak access token's `aud` (audience) claim only contains `"account"`. When .NET validates the JWT, it checks that your API's name is in the audience. Without the right audience, you'd need to **disable audience validation** — which is a security risk in production.

The proper fix is an **Audience Mapper** in Keycloak:

### Adding the Audience Mapper

1. Open the **Keycloak Admin Console** at `http://localhost:8888`
2. Go to the **`movie-library`** realm
3. Navigate to **Clients** → **`movielibraryweb`** → **Client scopes** tab
4. Click on **`movielibraryweb-dedicated`**
5. Click **Add mapper** → **By configuration** → **Audience**
6. Configure the mapper:

| Field | Value |
|-------|-------|
| Name | `movie-library-api-audience` |
| Included Client Audience | *(leave empty)* |
| Included Custom Audience | `movie-library-api` |
| Add to ID token | Off |
| Add to access token | **On** |
| Add to lightweight access token | Off |

7. Click **Save**

### Update JWT Validation to Require the Audience

Now update the `AddKeycloakJwtBearer` call to validate the audience:

```csharp
builder.AddKeycloakJwtBearer(
    serviceName: "keycloak",
    realm: "movie-library",
    configureOptions: options =>
    {
        options.RequireHttpsMetadata = false; // ⚠️ Dev only
        options.TokenValidationParameters.ValidAudience = "movie-library-api";
    });
```

After this change, Keycloak access tokens will include `"movie-library-api"` in their `aud` claim, and the API will only accept tokens specifically intended for it.

## Testing

### Testing with Scalar UI

The Scalar UI is available at `/scalar/v1` when your ApiService is running.

**Step 1: Get an access token from Keycloak**

Use the Keycloak token endpoint directly (replace values as needed):

```bash
curl -X POST "http://localhost:8888/realms/movie-library/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=movielibraryweb" \
  -d "client_secret=YOUR_CLIENT_SECRET" \
  -d "username=YOUR_TEST_USER" \
  -d "password=YOUR_TEST_PASSWORD"
```

Copy the `access_token` value from the JSON response.

**Step 2: Use the token in Scalar**

1. Open Scalar at the ApiService URL + `/scalar/v1`
2. Click the **Authentication** panel (lock icon)
3. Paste your `access_token` into the Bearer token field
4. Try any protected endpoint — it should now return `200 OK`

Without a token, you'll get `401 Unauthorized`.

### Testing with curl

```bash
# Without token — should return 401
curl -i http://localhost:PORT/api/movies/popular

# With token — should return 200
curl -i http://localhost:PORT/api/movies/popular \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwi..."
```

### Verifying End-to-End from Blazor

The existing `AuthenticatedHttpMessageHandler` already forwards the access token. Once you protect the endpoints:

1. Run the full stack via `dotnet run` in `AppHost`
2. Log in through the Blazor frontend
3. Navigate to the movies page
4. The movies load — the JWT was silently forwarded ✅

Log out and try to access movies directly via the API — you'll get `401` ✅

## Troubleshooting

### `401 Unauthorized` even with a valid token

- **Check middleware order**: `UseAuthentication()` must come **before** `UseAuthorization()` in the pipeline
- **Check the token issuer**: Ensure the token's `iss` claim matches `http://localhost:8888/realms/movie-library`
- **Check `RequireHttpsMetadata`**: In development, it must be `false` since Keycloak runs over HTTP

### `WWW-Authenticate: Bearer error="invalid_token"` in response

- The token may be **expired** — Keycloak access tokens expire in 5 minutes by default
- The token may have the **wrong audience** — check if you've added the audience mapper
- Run `dotnet user-jwts print <token>` to decode and inspect the JWT locally

### `InvalidOperationException` on startup about Keycloak connection string

- Verify that `AppHost.cs` has `.WithReference(keycloak)` on the `apiservice`
- Confirm the service name `"keycloak"` in `AddKeycloakJwtBearer` matches the name in `AppHost.cs` (`AddKeycloak("keycloak", ...)`)

### Blazor app shows movies but API returns `401` when called directly

This is expected! The `AuthenticatedHttpMessageHandler` only runs inside the Blazor server process. Direct API calls (from browser, curl, Postman) need a Bearer token manually.

### Token not forwarded from Blazor to API

- Confirm `SaveTokens = true` is set in the OIDC options in `blog-keycloak-series.Web/Program.cs`
- Confirm `AuthenticatedHttpMessageHandler` is registered as a transient service
- Confirm `.AddHttpMessageHandler<AuthenticatedHttpMessageHandler>()` is on the `MoviesApiClient` HTTP client registration

## Updated Program.cs (Complete)

Here's the complete updated `ApiService/Program.cs` for reference:

```csharp
using Asp.Versioning;
using blog_keycloak_series.ApiService.Endpoints.Movies;
using blog_keycloak_series.Domain.Converters;
using blog_keycloak_series.Domain.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.OpenApi.Models;
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

// ✅ Part 3: JWT Bearer authentication via Aspire Keycloak integration
builder.AddKeycloakJwtBearer(
    serviceName: "keycloak",
    realm: "movie-library",
    configureOptions: options =>
    {
        options.RequireHttpsMetadata = false; // ⚠️ Dev only
        // Uncomment after adding the audience mapper in Keycloak:
        // options.TokenValidationParameters.ValidAudience = "movie-library-api";
    });

// ✅ Part 3: Authorization services
builder.Services.AddAuthorization();

builder.AddRedisDistributedCache(connectionName: "cache");
builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024 * 100;
    options.MaximumKeyLength = 1024;
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});

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

// ✅ Part 3: OpenAPI with Bearer security scheme
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your Keycloak JWT access token"
        };

        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme }
            }] = []
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

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Movie Library API")
        .WithTheme(ScalarTheme.Mars)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .AddPreferredSecuritySchemes("Bearer") // ✅ Part 3: Enable Bearer in Scalar
        .SortTagsAlphabetically()
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

app.MapGetNowPlayingMoviesEndpoint(versionSet);
app.MapGetPopularMoviesEndpoint(versionSet);
app.MapGetTopRatedMoviesEndpoint(versionSet);
app.MapGetUpcomingMoviesEndpoint(versionSet);

// ✅ Part 3: Authentication + Authorization middleware (order matters!)
app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();

app.Run();
```

## Best Practices

### Security

✅ **Always use HTTPS in production** — set `RequireHttpsMetadata = true`

✅ **Add an Audience Mapper** — don't skip this for production; it prevents token reuse across services

✅ **Keep access token lifetime short** — Keycloak defaults to 5 minutes; adjust in realm settings under **Tokens**

✅ **Never log access tokens** — they are credentials; treat them like passwords

✅ **Return minimal error details on 401/403** — don't expose internal implementation details to unauthenticated callers

✅ **Use HTTPS for Keycloak JWKS endpoint in production** — the public keys used for signature validation must be fetched securely

### Architecture

✅ **Validate tokens at the API boundary** — the API should never trust a caller just because they came from the internal network

✅ **Use service-specific audiences** — if you add more APIs in the future (e.g., `notification-api`), each should have its own audience

✅ **Cache JWKS keys** — .NET's JWT Bearer middleware already caches Keycloak's public keys and refreshes them automatically; you don't need to manage this

✅ **Consider token introspection for high-security scenarios** — for cases where you need to check if a token was revoked before expiry, use Keycloak's introspection endpoint instead of local validation

## What's Next?

In **Part 4**, we'll explore:
- Defining **roles and permissions** in Keycloak
- Using `[Authorize(Roles = "admin")]` in API endpoints
- Reading roles from the JWT and mapping them to .NET's claim system
- Protecting different endpoints with different role requirements (e.g., only admins can delete)

---

## Resources

- [Aspire.Keycloak.Authentication NuGet](https://www.nuget.org/packages/Aspire.Keycloak.Authentication)
- [Keycloak JWT Bearer Docs](https://www.keycloak.org/docs/latest/server_admin/#_client-credentials)
- [JWT.io — Decode tokens](https://jwt.io/)
- [Keycloak Token Endpoint Reference](https://www.keycloak.org/docs/latest/server_admin/#token-endpoint)
- [ASP.NET Core JWT Bearer Auth Docs](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn)
- [Scalar.AspNetCore](https://github.com/scalar/scalar)

---

**Questions or feedback?** Feel free to open an issue on [GitHub](https://github.com/gauthier-etienne/blog-keycloak-series/issues) or drop a comment on the blog post.
