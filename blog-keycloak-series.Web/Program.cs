using blog_keycloak_series.Web.Components;
using blog_keycloak_series.Web;
using blog_keycloak_series.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Text.Json.Serialization;
using blog_keycloak_series.Domain.Converters;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

// Add HTTP context accessor
builder.Services.AddHttpContextAccessor();

// Add authentication services
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        var keycloakSettings = builder.Configuration.GetSection("Keycloak");
        options.Authority = keycloakSettings["Authority"];
        options.ClientId = keycloakSettings["ClientId"];
        options.ClientSecret = keycloakSettings["ClientSecret"];
        options.MetadataAddress = keycloakSettings["MetadataAddress"];

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.Query;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("movielibrary_api.all");

        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.RequireHttpsMetadata = false; // Set to true in production
        options.UsePkce = true;

        // Configure logout
        options.SignedOutRedirectUri = "/Account/LogoutCallback";
        options.SignedOutCallbackPath = "/Account/LogoutCallback";

        // Map Keycloak claims to standard claims
        options.ClaimActions.MapJsonKey("role", "roles");
        options.ClaimActions.MapJsonKey("preferred_username", "preferred_username");
        options.ClaimActions.MapJsonKey("email", "email");
        options.ClaimActions.MapJsonKey("given_name", "given_name");
        options.ClaimActions.MapJsonKey("family_name", "family_name");
    });

builder.Services.AddAuthorizationBuilder();

// Add authentication state provider for Blazor
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

// Register the authenticated HTTP message handler
builder.Services.AddTransient<AuthenticatedHttpMessageHandler>();

// Configure HTTP clients with authentication
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddHttpClient<MoviesApiClient>(client =>
{
    client.BaseAddress = new("https+http://apiservice");
    client.DefaultRequestVersion = new Version(1, 0);
})
.AddHttpMessageHandler<AuthenticatedHttpMessageHandler>()
.AddServiceDiscovery();

// Add Cascading Authentication State
builder.Services.AddCascadingAuthenticationState();

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

// Add Controllers for Account management
builder.Services.AddControllers();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Add authentication middleware
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map controllers for Account login/logout
app.MapControllers();

app.Run();