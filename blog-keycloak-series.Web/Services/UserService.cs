using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace blog_keycloak_series.Web.Services;

public class UserService : IUserService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public UserService(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<ClaimsPrincipal?> GetCurrentUserAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return authState.User.Identity?.IsAuthenticated == true ? authState.User : null;
    }

    public async Task<string?> GetUserIdAsync()
    {
        var user = await GetCurrentUserAsync();
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               user?.FindFirst("sub")?.Value;
    }

    public async Task<string?> GetUserNameAsync()
    {
        var user = await GetCurrentUserAsync();
        return user?.FindFirst(ClaimTypes.Name)?.Value ??
               user?.FindFirst("preferred_username")?.Value ??
               user?.FindFirst("name")?.Value;
    }

    public async Task<string?> GetUserEmailAsync()
    {
        var user = await GetCurrentUserAsync();
        return user?.FindFirst(ClaimTypes.Email)?.Value ??
               user?.FindFirst("email")?.Value;
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Enumerable.Empty<string>();

        return user.FindAll(ClaimTypes.Role)
            .Concat(user.FindAll("role"))
            .Select(c => c.Value)
            .Distinct();
    }
}

public interface IUserService
{
    Task<ClaimsPrincipal?> GetCurrentUserAsync();
    Task<string?> GetUserIdAsync();
    Task<string?> GetUserNameAsync();
    Task<string?> GetUserEmailAsync();
    Task<IEnumerable<string>> GetUserRolesAsync();
}