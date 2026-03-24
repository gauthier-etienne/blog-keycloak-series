# Blog Keycloak Series - Part 3: Securing APIs with JWT Bearer Tokens and Role-Based Access Control

## Overview

This is **Part 3** of the Blog Keycloak Series. Building on the foundation from [Part 1](README-Part1.md) (OIDC authentication) and [Part 2](README-Part2.md) (Social Login), this part focuses on the backend: **securing API endpoints with JWT Bearer token validation and role-based authorization**.

By the end of this part, your `ApiService` will:
- Reject unauthenticated requests with `401 Unauthorized`
- Reject authenticated users without the right role with `403 Forbidden`
- Silently receive the user's Keycloak access token forwarded from the Blazor frontend

## What You'll Learn

- ✅ How JWT Bearer authentication works in a .NET Minimal API
- ✅ How to configure `AddJwtBearer` manually with Keycloak settings
- ✅ How `MapInboundClaims = false` preserves JWT claim names as-is
- ✅ How to define named authorization policies with role requirements
- ✅ How to protect endpoints with `.RequireAuthorization("PolicyName")`
- ✅ How the access token flows from Keycloak → Blazor → ApiService
- ✅ How to configure Scalar and OpenAPI with Bearer token support
- ✅ How to configure Keycloak: realm roles, audience mapper, and role mapper

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

After the Keycloak configuration in this part, a decoded access token will look like this:

```json
{
  "exp": 1735000000,
  "iat": 1734999700,
  "iss": "http://localhost:8888/realms/movie-library",
  "aud": ["movielibraryapi", "account"],
  "sub": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "preferred_username": "etienne",
  "email": "etienne@example.com",
  "roles": ["movie-user", "offline_access", "default-roles-movie-library"],
  "realm_access": {
    "roles": ["movie-user", "offline_access", "default-roles-movie-library"]
  }
}
```

The API validates:
1. **Signature** — was this signed by Keycloak? (using JWKS public keys)
2. **Issuer** — does `iss` match our Keycloak realm URL?
3. **Expiration** — has the token expired?
4. **Audience** — does `aud` contain `"movielibraryapi"`?
5. **Role** — does the `roles` array contain `"movie-user"`?

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
     │                         │     (stored in server     │                            │
     │                         │      cookie via           │                            │
     │                         │      SaveTokens = true)   │                            │
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
     │                         │                                             - Audience ✓
     │                         │                                             - Role ✓
     │                         │                                                        │
     │                         │  10. 200 OK + Movie data ◀────────────────────────────│
     │                         │                                                        │
     │  11. Rendered page      │                                                        │
     │◀────────────────────────│                                                        │
```

> **Key insight:** The `AuthenticatedHttpMessageHandler` (already in place from Part 1!) is the bridge between the Blazor cookie session and the API's Bearer scheme. The user never sees a token — it's all handled server-side.

## What Changes in Part 3

| File | Change |
|------|--------|
| `ApiService/Program.cs` | Add JWT Bearer auth with manual config, named authorization policies, OpenAPI Bearer scheme, Scalar Bearer config |
| `ApiService/Endpoints/Movies/*.cs` | Add `.RequireAuthorization("MovieUser")` to each endpoint |
| `Web/Program.cs` | Add `movielibrary_api.all` scope to the OIDC request + claim mapping |
| Keycloak Admin | Create realm roles, create `movielibrary_api.all` client scope with audience and role mappers |

## Reviewing the AuthenticatedHttpMessageHandler

Before looking at the new code, let's appreciate what's already in `blog-keycloak-series.Web/AuthenticatedHttpMessageHandler.cs` — this was set up in Part 1 and requires no changes:

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

## Implementation

### Step 1: Configure JWT Bearer Authentication in the ApiService

Open `blog-keycloak-series.ApiService/Program.cs` and add the following after `builder.AddServiceDefaults()`:

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme) // ← registers JWT Bearer as the default scheme
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8888/realms/movie-library";
        options.Audience = "movielibraryapi";
        options.RequireHttpsMetadata = builder.Environment.IsProduction(); // ⚠️ false in dev, true in prod
        options.MapInboundClaims = false; // Keep JWT claim names as-is — no remapping to ClaimTypes.* URIs
        options.TokenValidationParameters.RoleClaimType = "roles"; // Keycloak emits roles as a flat "roles" array
    });
```

**Why manual `AddJwtBearer` instead of `AddKeycloakJwtBearer`?**

The manual approach gives explicit control over each setting:

| Setting | Purpose |
|---------|---------|
| `Authority` | The Keycloak realm URL — used to discover the JWKS endpoint for signature validation |
| `Audience` | Must match the custom audience set in Keycloak; prevents tokens issued for other services from being accepted |
| `RequireHttpsMetadata` | Disabled in dev (Keycloak runs on HTTP locally); always enable in production |
| `MapInboundClaims = false` | Prevents .NET from remapping `sub` → `ClaimTypes.NameIdentifier`, `roles` → `ClaimTypes.Role`, etc. JWT claim names stay as-is |
| `RoleClaimType = "roles"` | Tells .NET which JWT claim holds the user's roles; pairs with the Keycloak role mapper configured below |

### Step 2: Add Named Authorization Policies

Immediately after the JWT Bearer configuration, define named policies:

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("MovieUser", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("movie-user"))
    .AddPolicy("MovieAdmin", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("movie-admin"));
```

`AddAuthorizationBuilder()` is the modern fluent API (equivalent to the older `AddAuthorization(options => ...)` pattern). Each policy:
- Requires the user to be authenticated
- Requires a specific role from the `roles` JWT claim

### Step 3: Configure OpenAPI with a Bearer Security Scheme

To test protected endpoints via the Scalar UI, the OpenAPI document needs to declare the Bearer security scheme. In `Program.cs`, update `AddOpenApi()`:

```csharp
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
```

> **Note:** `OpenApiSecuritySchemeReference` is the .NET 10 way of referencing a security scheme by name. It replaces the older `new OpenApiSecurityScheme { Reference = new OpenApiReference { ... } }` pattern.

You'll need this using statement:

```csharp
using Microsoft.OpenApi;
```

### Step 4: Enable Bearer Authentication in Scalar

Update the `MapScalarApiReference` call:

```csharp
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Movie Library API")
        .WithTheme(ScalarTheme.Mars)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .AddPreferredSecuritySchemes("Bearer")   // ✅ Shows the Bearer token input in the Scalar UI
        .SortTagsAlphabetically()
        .WithDotNetFlag();

    if (app.Environment.IsProduction() && app.Environment.IsStaging())
    {
        options.HideTestRequestButton();
    }
});
```

### Step 5: Add Authentication & Authorization Middleware

Add the middleware **after** `app.MapDefaultEndpoints()` and **before** `app.MapOpenApi()`:

```csharp
app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// ✅ Order matters: Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(/* ... */);
// ... endpoint mapping
```

> **Order matters!** `UseAuthentication()` must always precede `UseAuthorization()`. Placing them early in the pipeline ensures the user identity is resolved before any endpoint logic runs.

### Step 6: Protect the Movie Endpoints

Add `.RequireAuthorization("MovieUser")` to each endpoint. Apply this pattern to all four movie endpoints:

```csharp
app.MapGet(ApiEndpoints.Movies.NowPlaying, async Task<Results<Ok<ApiResponse>, BadRequest<List<string>>>>
    ([FromServices] HybridCache cache, [FromServices] IHttpClientFactory httpClientFactory, CancellationToken token) =>
    {
        // ... handler code unchanged ...
    })
    .WithName(Name)
    .WithApiVersionSet(versionSet)
    .HasApiVersion(1.0)
    .RequireAuthorization("MovieUser")               // ✅ Requires authenticated user with "movie-user" role
    .Produces<ApiResponse>()
    .Produces(StatusCodes.Status401Unauthorized)     // ✅ Documents 401 in OpenAPI
    .Produces(StatusCodes.Status403Forbidden)        // ✅ Documents 403 in OpenAPI
    .Produces(StatusCodes.Status404NotFound)
    .WithTags(ApiEndpoints.Movies.Tag);
```

Apply the same `.RequireAuthorization("MovieUser")` to `GetPopularMoviesEndpoint.cs`, `GetTopRatedMoviesEndpoint.cs`, and `GetUpcomingMoviesEndpoint.cs`.

> **`"MovieUser"` vs `.RequireAuthorization()`:** Using a named policy (`"MovieUser"`) is more explicit than the bare `.RequireAuthorization()`. It enforces both authentication *and* a specific role. The bare form only requires authentication. For a future admin endpoint, you'd use `.RequireAuthorization("MovieAdmin")`.

### Step 7: Update the Web Frontend to Request the API Scope

In `blog-keycloak-series.Web/Program.cs`, the OIDC options must request the `movielibrary_api.all` scope. This triggers the audience and role mappers configured in Keycloak, ensuring the access token contains both `"movielibraryapi"` in `aud` and the user's roles in `roles`:

```csharp
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    // ... existing settings ...

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("movielibrary_api.all");  // ✅ Triggers audience + role mappers in Keycloak

    options.SaveTokens = true; // Required — stores the access_token in the cookie for forwarding

    // Map the flat "roles" claim from the userinfo endpoint to the "role" claim type
    options.ClaimActions.MapJsonKey("role", "roles");
    options.ClaimActions.MapJsonKey("preferred_username", "preferred_username");
    options.ClaimActions.MapJsonKey("email", "email");
    options.ClaimActions.MapJsonKey("given_name", "given_name");
    options.ClaimActions.MapJsonKey("family_name", "family_name");
});
```

## Keycloak Configuration

Four things need to be configured in Keycloak for the above code to work.

### 1 — Create Realm Roles

Go to **Keycloak Admin** → `movie-library` realm → **Realm roles** → **Create role**:

- Create role `movie-user` (for regular users — access to all movie endpoints)
- Create role `movie-admin` (for administrators — reserved for future admin endpoints)

### 2 — Assign Roles to Test Users

Go to **Users** → select your test user → **Role mapping** tab → **Assign role** → select `movie-user` → **Assign**.

Without this step, authenticated users will get `403 Forbidden` because their token won't contain the `movie-user` role.

### 3 — Create the `movielibrary_api.all` Client Scope

This scope is what the Blazor frontend requests. When Keycloak processes a token request with this scope, it triggers the mappers defined below — adding the API audience and the user's roles to the access token.

1. Go to **Client Scopes** → **Create client scope**
2. Configure:

| Field | Value |
|-------|-------|
| Name | `movielibrary_api.all` |
| Type | `Optional` |
| Include in token scope | On |

3. Click **Save**

Now add two mappers to this scope. Go to the scope → **Mappers** tab → **Add mapper** → **By configuration**:

#### Mapper A: Audience

| Field | Value |
|-------|-------|
| Type | Audience |
| Name | `movie-library-api-audience` |
| Included Custom Audience | `movielibraryapi` |
| Add to ID token | Off |
| Add to access token | **On** |

This adds `"movielibraryapi"` to the `aud` claim of the access token, satisfying `options.Audience = "movielibraryapi"` in the ApiService.

#### Mapper B: Realm Roles (flat `roles` claim)

| Field | Value |
|-------|-------|
| Type | User Realm Role |
| Name | `realm-roles-flat` |
| Token Claim Name | `roles` |
| Add to ID token | Off |
| Add to access token | **On** |
| Multivalued | **On** |

This adds a flat `"roles": ["movie-user", ...]` array to the access token, satisfying `RoleClaimType = "roles"` in the ApiService. Without this mapper, roles would only exist nested under `realm_access.roles` and .NET's role check would fail.

### 4 — Add the Scope to the `movielibraryweb` Client

Go to **Clients** → `movielibraryweb` → **Client scopes** tab → **Add client scope** → select `movielibrary_api.all` → **Add** (as Optional).

This makes the scope available for the Blazor frontend to request.

### Verify the Token

After configuration, you can verify the token contents at [jwt.io](https://jwt.io). The access token should contain:

```json
{
  "aud": ["movielibraryapi", "account"],
  "roles": ["movie-user", "default-roles-movie-library", "offline_access"],
  "iss": "http://localhost:8888/realms/movie-library"
}
```

## Testing

### Testing with the Scalar UI

The Scalar UI is available at `/scalar/v1` when your ApiService is running.

**Step 1: Get an access token from Keycloak**

```bash
curl -X POST "http://localhost:8888/realms/movie-library/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=movielibraryweb" \
  -d "client_secret=YOUR_CLIENT_SECRET" \
  -d "username=YOUR_TEST_USER" \
  -d "password=YOUR_TEST_PASSWORD" \
  -d "scope=openid movielibrary_api.all"
```

Copy the `access_token` value from the JSON response.

**Step 2: Use the token in Scalar**

1. Open Scalar at the ApiService URL + `/scalar/v1`
2. Click the **Authentication** panel (lock icon)
3. Paste your `access_token` into the Bearer token field
4. Try any protected endpoint — it should return `200 OK`

Without a token → `401 Unauthorized`. With a valid token but no `movie-user` role → `403 Forbidden`.

### Testing with curl

```bash
# Without token — should return 401
curl -i http://localhost:PORT/api/movies/popular

# With valid token — should return 200
curl -i http://localhost:PORT/api/movies/popular \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwi..."
```

### Verifying End-to-End from Blazor

1. Run the full stack via `dotnet run` in `AppHost`
2. Log in through the Blazor frontend as a user with the `movie-user` role
3. Navigate to the movies page — movies load, JWT was silently forwarded ✅
4. Log out — navigating to movies redirects to login ✅

## Complete Program.cs (ApiService)

```csharp
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

builder.AddServiceDefaults();

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(60);
});

// JWT Bearer authentication — manually configured for full control over each setting
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8888/realms/movie-library";
        options.Audience = "movielibraryapi";
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.MapInboundClaims = false; // Keep JWT claim names as-is
        options.TokenValidationParameters.RoleClaimType = "roles"; // Keycloak flat roles array
    });

// Named authorization policies
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

// OpenAPI with Bearer security scheme
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

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Authentication + Authorization middleware — must be in this order
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

app.UseHttpsRedirection();

app.Run();
```

## Troubleshooting

### `401 Unauthorized` even with a valid token

- **Check middleware order**: `UseAuthentication()` must come **before** `UseAuthorization()` in the pipeline
- **Check the token issuer**: Ensure the token's `iss` claim matches `http://localhost:8888/realms/movie-library`
- **Check `RequireHttpsMetadata`**: In development it must be `false` since Keycloak runs over HTTP
- **Check the audience**: The token's `aud` claim must contain `"movielibraryapi"` — confirm the Keycloak audience mapper is saving to the access token

### `403 Forbidden` with a valid token

- The user doesn't have the `movie-user` realm role assigned — go to **Users** → select user → **Role mapping** → assign `movie-user`
- The `roles` flat claim is missing from the token — confirm the "User Realm Role" mapper is configured on the `movielibrary_api.all` scope with **Add to access token = On**
- The `movielibrary_api.all` scope wasn't requested — confirm the Web frontend has `options.Scope.Add("movielibrary_api.all")` and the scope is assigned as Optional to the `movielibraryweb` client in Keycloak

### `WWW-Authenticate: Bearer error="invalid_token"` in response

- The token may be **expired** — Keycloak access tokens expire in 5 minutes by default
- The token may have the **wrong audience** — decode at [jwt.io](https://jwt.io) and check the `aud` claim

### Token not forwarded from Blazor to API

- Confirm `SaveTokens = true` is set in the OIDC options in `blog-keycloak-series.Web/Program.cs`
- Confirm `AuthenticatedHttpMessageHandler` is registered as a transient service
- Confirm `.AddHttpMessageHandler<AuthenticatedHttpMessageHandler>()` is on the `MoviesApiClient` registration

### Blazor app shows movies but API returns `401` when called directly

This is expected. The `AuthenticatedHttpMessageHandler` only runs inside the Blazor server process. Direct API calls (curl, Postman, Scalar) require a Bearer token manually.

## Best Practices

### Security

✅ **Always use HTTPS in production** — set `RequireHttpsMetadata = true`

✅ **Always validate the audience** — `options.Audience = "movielibraryapi"` prevents tokens issued for other services from being accepted by this API

✅ **Keep access token lifetime short** — Keycloak defaults to 5 minutes; adjust in realm settings under **Tokens**

✅ **Never log access tokens** — they are credentials; treat them like passwords

✅ **Use named policies over bare `.RequireAuthorization()`** — named policies are explicit about both authentication and role requirements

✅ **Return minimal error details on 401/403** — don't expose internal implementation details to unauthenticated callers

### Architecture

✅ **Validate tokens at the API boundary** — the API should never trust a caller just because the request came from the internal network

✅ **Use service-specific audiences** — if you add more APIs (e.g., `notification-api`), each should have its own audience and its own Keycloak client scope

✅ **Cache JWKS keys** — .NET's JWT Bearer middleware already caches Keycloak's public keys and refreshes them automatically

## What's Next?

In **Part 4**, we'll explore:
- Persisting user data in PostgreSQL on first login using the Keycloak user `sub` claim
- Associating data with users and scoping API responses per authenticated user
- Using the `ClaimsPrincipal` inside Minimal API endpoint handlers

---

## Resources

- [JWT.io — Decode tokens](https://jwt.io/)
- [Keycloak Protocol Mappers](https://www.keycloak.org/docs/latest/server_admin/#_protocol-mappers)
- [ASP.NET Core JWT Bearer Auth Docs](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn)
- [ASP.NET Core Authorization Policies](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies)
- [Scalar.AspNetCore](https://github.com/scalar/scalar)
- [OpenAPI Security Schemes](https://spec.openapis.org/oas/v3.1.0#security-scheme-object)

---

**Questions or feedback?** Feel free to open an issue on [GitHub](https://github.com/gauthier-etienne/blog-keycloak-series/issues) or drop a comment on the blog post.
