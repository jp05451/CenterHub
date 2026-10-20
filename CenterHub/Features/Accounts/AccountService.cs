// CenterHub/Features/Accounts/AccountService.cs
using CenterHub.Data;
using CenterHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CenterHub.Features.Accounts;

public sealed record AccountInfo(string Id, string UserName, string DisplayName, string Email, string Role, bool IsDisabled);

public sealed record NewAccount(string UserName, string DisplayName, string Email, string Password, string Role);

public sealed record AccountEdit(string DisplayName, string Email, string Role);

public sealed record AccountResult(bool Succeeded, string? Error = null)
{
    public static AccountResult Ok { get; } = new(true);

    public static AccountResult Fail(string error) => new(false, error);

    public static AccountResult From(IdentityResult result) =>
        result.Succeeded ? Ok : Fail(string.Join("; ", result.Errors.Select(e => e.Description)));
}

public class AccountService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;

    public AccountService(UserManager<ApplicationUser> users, AppDbContext db)
    {
        _users = users;
        _db = db;
    }

    // "Disabled" is Identity's lockout pinned to MaxValue. Temporary lockouts from too many
    // failed logins end on their own and must not be shown as disabled.
    private static bool IsDisabled(ApplicationUser user) => user.LockoutEnd == DateTimeOffset.MaxValue;

    public async Task<List<AccountInfo>> GetAccountsAsync(string requestingUserId)
    {
        await RequireActiveAdminAsync(requestingUserId);

        var users = await _users.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        var accounts = new List<AccountInfo>(users.Count);
        foreach (var user in users)
        {
            accounts.Add(new AccountInfo(
                user.Id,
                user.UserName ?? string.Empty,
                user.DisplayName,
                user.Email ?? string.Empty,
                await GetRoleAsync(user),
                IsDisabled(user)));
        }
        return accounts;
    }

    public async Task<AccountResult> CreateAccountAsync(string requestingUserId, NewAccount model)
    {
        await RequireActiveAdminAsync(requestingUserId);

        if (string.IsNullOrWhiteSpace(model.UserName)) return AccountResult.Fail("請輸入帳號。");
        if (string.IsNullOrWhiteSpace(model.DisplayName)) return AccountResult.Fail("請輸入顯示名稱。");
        if (string.IsNullOrWhiteSpace(model.Email)) return AccountResult.Fail("請輸入 Email。");
        if (!Roles.All.Contains(model.Role)) return AccountResult.Fail("角色不正確。");

        var user = new ApplicationUser
        {
            UserName = model.UserName.Trim(),
            DisplayName = model.DisplayName.Trim(),
            Email = model.Email.Trim()
        };

        var created = await _users.CreateAsync(user, model.Password);
        if (!created.Succeeded) return AccountResult.From(created);

        var roleResult = await _users.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded)
        {
            // A brand-new account has no tickets yet, so removing it is safe and avoids a role-less user.
            await _users.DeleteAsync(user);
            return AccountResult.From(roleResult);
        }

        return AccountResult.Ok;
    }

    public async Task<AccountResult> UpdateAccountAsync(string requestingUserId, string targetUserId, AccountEdit model)
    {
        await RequireActiveAdminAsync(requestingUserId);

        if (string.IsNullOrWhiteSpace(model.DisplayName)) return AccountResult.Fail("請輸入顯示名稱。");
        if (string.IsNullOrWhiteSpace(model.Email)) return AccountResult.Fail("請輸入 Email。");
        if (!Roles.All.Contains(model.Role)) return AccountResult.Fail("角色不正確。");

        var user = await _users.FindByIdAsync(targetUserId);
        if (user is null) return AccountResult.Fail("找不到這個帳號。");

        var currentRole = await GetRoleAsync(user);
        var roleChanged = model.Role != currentRole;
        if (roleChanged && targetUserId == requestingUserId)
            return AccountResult.Fail("不能變更自己的角色。");

        user.DisplayName = model.DisplayName.Trim();
        user.Email = model.Email.Trim();
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded) return AccountResult.From(updated);

        if (!roleChanged) return AccountResult.Ok;

        if (currentRole.Length > 0)
        {
            var removed = await _users.RemoveFromRoleAsync(user, currentRole);
            if (!removed.Succeeded) return AccountResult.From(removed);
        }

        var added = await _users.AddToRoleAsync(user, model.Role);
        if (!added.Succeeded) return AccountResult.From(added);

        // Role claims live in the login cookie; rotating the stamp forces the user to sign in again.
        return AccountResult.From(await _users.UpdateSecurityStampAsync(user));
    }

    public async Task<AccountResult> ResetPasswordAsync(string requestingUserId, string targetUserId, string newPassword)
    {
        await RequireActiveAdminAsync(requestingUserId);

        var user = await _users.FindByIdAsync(targetUserId);
        if (user is null) return AccountResult.Fail("找不到這個帳號。");

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        return AccountResult.From(await _users.ResetPasswordAsync(user, token, newPassword));
    }

    public async Task<AccountResult> SetDisabledAsync(string requestingUserId, string targetUserId, bool disabled)
    {
        await RequireActiveAdminAsync(requestingUserId);

        // The requester is always an active admin, so forbidding self-disable also guarantees
        // the system never ends up without one.
        if (disabled && targetUserId == requestingUserId)
            return AccountResult.Fail("不能停用自己的帳號。");

        var user = await _users.FindByIdAsync(targetUserId);
        if (user is null) return AccountResult.Fail("找不到這個帳號。");

        var result = await _users.SetLockoutEnabledAsync(user, true);
        if (result.Succeeded)
            result = await _users.SetLockoutEndDateAsync(user, disabled ? DateTimeOffset.MaxValue : null);
        if (result.Succeeded)
            result = await _users.ResetAccessFailedCountAsync(user);
        if (result.Succeeded)
            result = await _users.UpdateSecurityStampAsync(user);

        return AccountResult.From(result);
    }

    public async Task<AccountResult> ChangeOwnPasswordAsync(string userId, string currentPassword, string newPassword)
    {
        _db.ChangeTracker.Clear();

        var user = await _users.FindByIdAsync(userId);
        if (user is null) return AccountResult.Fail("找不到這個帳號。");

        var result = await _users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            return AccountResult.Fail("目前密碼不正確。");

        return AccountResult.From(result);
    }

    private async Task RequireActiveAdminAsync(string requestingUserId)
    {
        // Blazor Server shares one AppDbContext per circuit, so a tracked copy of the user would hide
        // a disable/role change made from another session. Start every admin action from fresh rows.
        _db.ChangeTracker.Clear();

        var requester = await _users.FindByIdAsync(requestingUserId);
        if (requester is null || IsDisabled(requester) || !await _users.IsInRoleAsync(requester, Roles.Admin))
            throw new InvalidOperationException("Only an active admin may manage accounts.");
    }

    private async Task<string> GetRoleAsync(ApplicationUser user) =>
        (await _users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
}
