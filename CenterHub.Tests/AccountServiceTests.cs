// CenterHub.Tests/AccountServiceTests.cs
using CenterHub.Data;
using CenterHub.Features.Accounts;
using CenterHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CenterHub.Tests;

public class AccountServiceTests : IDisposable
{
    private const string Password = "Passw0rd!";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AccountService _service;

    public AccountServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        var db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        var roles = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
            roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();

        _users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _service = new AccountService(_users, db);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }

    private async Task<ApplicationUser> SeedUserAsync(string userName, string role)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            DisplayName = $"顯示-{userName}",
            Email = $"{userName}@example.edu"
        };
        var created = await _users.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
        await _users.AddToRoleAsync(user, role);
        return user;
    }

    private static NewAccount MakeNewAccount(string userName = "newbie", string role = Roles.WorkStudy) =>
        new(userName, "王小明", $"{userName}@example.edu", Password, role);

    [Fact]
    public async Task CreateAccountAsync_ByAdmin_CreatesUserWithRole()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);

        var result = await _service.CreateAccountAsync(admin.Id, MakeNewAccount());

        Assert.True(result.Succeeded);
        var created = await _users.FindByNameAsync("newbie");
        Assert.NotNull(created);
        Assert.Equal("王小明", created.DisplayName);
        Assert.True(await _users.IsInRoleAsync(created, Roles.WorkStudy));
    }

    [Fact]
    public async Task CreateAccountAsync_ByNonAdmin_Throws()
    {
        var student = await SeedUserAsync("student1", Roles.WorkStudy);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAccountAsync(student.Id, MakeNewAccount()));
        Assert.Null(await _users.FindByNameAsync("newbie"));
    }

    [Fact]
    public async Task CreateAccountAsync_WeakPassword_ReturnsFailureAndCreatesNothing()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);

        var result = await _service.CreateAccountAsync(admin.Id, MakeNewAccount() with { Password = "short" });

        Assert.False(result.Succeeded);
        Assert.Null(await _users.FindByNameAsync("newbie"));
    }

    [Fact]
    public async Task CreateAccountAsync_UnknownRole_ReturnsFailureAndCreatesNothing()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);

        var result = await _service.CreateAccountAsync(admin.Id, MakeNewAccount(role: "Superuser"));

        Assert.False(result.Succeeded);
        Assert.Null(await _users.FindByNameAsync("newbie"));
    }

    [Fact]
    public async Task GetAccountsAsync_ListsEveryAccountWithRoleAndStatus()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);
        var student = await SeedUserAsync("student1", Roles.WorkStudy);
        await _service.SetDisabledAsync(admin.Id, student.Id, disabled: true);

        var accounts = await _service.GetAccountsAsync(admin.Id);

        Assert.Equal(2, accounts.Count);
        var adminInfo = accounts.Single(a => a.UserName == "admin1");
        Assert.Equal(Roles.Admin, adminInfo.Role);
        Assert.False(adminInfo.IsDisabled);
        var studentInfo = accounts.Single(a => a.UserName == "student1");
        Assert.Equal(Roles.WorkStudy, studentInfo.Role);
        Assert.True(studentInfo.IsDisabled);
    }

    [Fact]
    public async Task GetAccountsAsync_ByNonAdmin_Throws()
    {
        var student = await SeedUserAsync("student1", Roles.WorkStudy);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetAccountsAsync(student.Id));
    }

    [Fact]
    public async Task UpdateAccountAsync_ChangesDisplayNameEmailAndRole_AndRotatesSecurityStamp()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);
        var student = await SeedUserAsync("student1", Roles.WorkStudy);
        var stampBefore = await _users.GetSecurityStampAsync(student);

        var result = await _service.UpdateAccountAsync(
            admin.Id, student.Id, new AccountEdit("李大華", "new@example.edu", Roles.Admin));

        Assert.True(result.Succeeded);
        var updated = await _users.FindByIdAsync(student.Id);
        Assert.NotNull(updated);
        Assert.Equal("李大華", updated.DisplayName);
        Assert.Equal("new@example.edu", updated.Email);
        Assert.True(await _users.IsInRoleAsync(updated, Roles.Admin));
        Assert.False(await _users.IsInRoleAsync(updated, Roles.WorkStudy));
        Assert.NotEqual(stampBefore, await _users.GetSecurityStampAsync(updated));
    }

    [Fact]
    public async Task UpdateAccountAsync_AdminEditingOwnNameAndEmail_KeepingRole_Succeeds()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);

        var result = await _service.UpdateAccountAsync(
            admin.Id, admin.Id, new AccountEdit("新名字", "me@example.edu", Roles.Admin));

        Assert.True(result.Succeeded);
        Assert.Equal("新名字", (await _users.FindByIdAsync(admin.Id))!.DisplayName);
    }

    [Fact]
    public async Task UpdateAccountAsync_AdminDemotingSelf_Fails()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);

        var result = await _service.UpdateAccountAsync(
            admin.Id, admin.Id, new AccountEdit("x", "x@example.edu", Roles.WorkStudy));

        Assert.False(result.Succeeded);
        Assert.True(await _users.IsInRoleAsync(admin, Roles.Admin));
    }

    [Fact]
    public async Task UpdateAccountAsync_BlankDisplayNameOrEmail_Fails()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);
        var student = await SeedUserAsync("student1", Roles.WorkStudy);

        var blankName = await _service.UpdateAccountAsync(
            admin.Id, student.Id, new AccountEdit(" ", "a@example.edu", Roles.WorkStudy));
        var blankEmail = await _service.UpdateAccountAsync(
            admin.Id, student.Id, new AccountEdit("name", "", Roles.WorkStudy));

        Assert.False(blankName.Succeeded);
        Assert.False(blankEmail.Succeeded);
    }

    [Fact]
    public async Task UpdateAccountAsync_UnknownTarget_Fails()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);

        var result = await _service.UpdateAccountAsync(
            admin.Id, "no-such-id", new AccountEdit("n", "n@example.edu", Roles.WorkStudy));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ResetPasswordAsync_ByAdmin_SetsNewPasswordAndRotatesSecurityStamp()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);
        var student = await SeedUserAsync("student1", Roles.WorkStudy);
        var stampBefore = await _users.GetSecurityStampAsync(student);

        var result = await _service.ResetPasswordAsync(admin.Id, student.Id, "BrandNew123");

        Assert.True(result.Succeeded);
        var reloaded = await _users.FindByIdAsync(student.Id);
        Assert.True(await _users.CheckPasswordAsync(reloaded!, "BrandNew123"));
        Assert.False(await _users.CheckPasswordAsync(reloaded!, Password));
        Assert.NotEqual(stampBefore, await _users.GetSecurityStampAsync(reloaded!));
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakPassword_FailsAndKeepsOldPassword()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);
        var student = await SeedUserAsync("student1", Roles.WorkStudy);

        var result = await _service.ResetPasswordAsync(admin.Id, student.Id, "short");

        Assert.False(result.Succeeded);
        Assert.True(await _users.CheckPasswordAsync((await _users.FindByIdAsync(student.Id))!, Password));
    }

    [Fact]
    public async Task ResetPasswordAsync_ByNonAdmin_Throws()
    {
        var student = await SeedUserAsync("student1", Roles.WorkStudy);
        var other = await SeedUserAsync("student2", Roles.WorkStudy);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ResetPasswordAsync(student.Id, other.Id, "BrandNew123"));
    }

    [Fact]
    public async Task SetDisabledAsync_Disable_LocksAccountAndRotatesSecurityStamp()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);
        var student = await SeedUserAsync("student1", Roles.WorkStudy);
        var stampBefore = await _users.GetSecurityStampAsync(student);

        var result = await _service.SetDisabledAsync(admin.Id, student.Id, disabled: true);

        Assert.True(result.Succeeded);
        var reloaded = await _users.FindByIdAsync(student.Id);
        Assert.True(await _users.IsLockedOutAsync(reloaded!));
        Assert.NotEqual(stampBefore, await _users.GetSecurityStampAsync(reloaded!));
    }

    [Fact]
    public async Task SetDisabledAsync_Enable_RestoresAccount()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);
        var student = await SeedUserAsync("student1", Roles.WorkStudy);
        await _service.SetDisabledAsync(admin.Id, student.Id, disabled: true);

        var result = await _service.SetDisabledAsync(admin.Id, student.Id, disabled: false);

        Assert.True(result.Succeeded);
        var reloaded = await _users.FindByIdAsync(student.Id);
        Assert.False(await _users.IsLockedOutAsync(reloaded!));
        var info = (await _service.GetAccountsAsync(admin.Id)).Single(a => a.UserName == "student1");
        Assert.False(info.IsDisabled);
    }

    [Fact]
    public async Task SetDisabledAsync_DisablingSelf_Fails()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);

        var result = await _service.SetDisabledAsync(admin.Id, admin.Id, disabled: true);

        Assert.False(result.Succeeded);
        Assert.False(await _users.IsLockedOutAsync((await _users.FindByIdAsync(admin.Id))!));
    }

    [Fact]
    public async Task SetDisabledAsync_ByNonAdmin_Throws()
    {
        var student = await SeedUserAsync("student1", Roles.WorkStudy);
        var other = await SeedUserAsync("student2", Roles.WorkStudy);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetDisabledAsync(student.Id, other.Id, disabled: true));
    }

    [Fact]
    public async Task AdminActions_ByAnAdminWhoWasDisabled_Throw_SoTwoAdminsCannotLockEachOtherOut()
    {
        var adminA = await SeedUserAsync("adminA", Roles.Admin);
        var adminB = await SeedUserAsync("adminB", Roles.Admin);
        await _service.SetDisabledAsync(adminA.Id, adminB.Id, disabled: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetDisabledAsync(adminB.Id, adminA.Id, disabled: true));
        Assert.False(await _users.IsLockedOutAsync((await _users.FindByIdAsync(adminA.Id))!));
    }

    [Fact]
    public async Task GetAccountsAsync_TemporaryLoginLockout_IsNotReportedAsDisabled()
    {
        var admin = await SeedUserAsync("admin1", Roles.Admin);
        var student = await SeedUserAsync("student1", Roles.WorkStudy);
        await _users.SetLockoutEndDateAsync(student, DateTimeOffset.UtcNow.AddMinutes(5));

        var info = (await _service.GetAccountsAsync(admin.Id)).Single(a => a.UserName == "student1");

        Assert.False(info.IsDisabled);
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_WithCorrectCurrentPassword_ChangesIt()
    {
        var student = await SeedUserAsync("student1", Roles.WorkStudy);

        var result = await _service.ChangeOwnPasswordAsync(student.Id, Password, "BrandNew123");

        Assert.True(result.Succeeded);
        Assert.True(await _users.CheckPasswordAsync((await _users.FindByIdAsync(student.Id))!, "BrandNew123"));
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_WithWrongCurrentPassword_FailsAndKeepsPassword()
    {
        var student = await SeedUserAsync("student1", Roles.WorkStudy);

        var result = await _service.ChangeOwnPasswordAsync(student.Id, "WrongPass123", "BrandNew123");

        Assert.False(result.Succeeded);
        Assert.Equal("目前密碼不正確。", result.Error);
        Assert.True(await _users.CheckPasswordAsync((await _users.FindByIdAsync(student.Id))!, Password));
    }
}
