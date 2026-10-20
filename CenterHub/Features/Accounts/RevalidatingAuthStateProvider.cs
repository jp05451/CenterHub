// CenterHub/Features/Accounts/RevalidatingAuthStateProvider.cs
using System.Security.Claims;
using CenterHub.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CenterHub.Features.Accounts;

// An open Blazor circuit keeps the login it started with. Re-checking the security stamp makes a
// disable / role change / password reset (all of which rotate the stamp) take effect without
// waiting for the user to reload the page.
public sealed class RevalidatingAuthStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IdentityOptions _options;

    public RevalidatingAuthStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> options)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.GetUserAsync(authenticationState.User);
        if (user is null)
            return false;

        var principalStamp = authenticationState.User.FindFirstValue(_options.ClaimsIdentity.SecurityStampClaimType);
        return principalStamp == await users.GetSecurityStampAsync(user);
    }
}
