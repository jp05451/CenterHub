using CenterHub.Data;
using CenterHub.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Local, gitignored override file for secrets such as Seed:AdminPassword —
// keeps plaintext credentials out of source control while still letting an
// operator set them without env vars. Loaded last so it wins over
// appsettings.json / appsettings.{Environment}.json.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Cookie logins are re-checked against the security stamp every minute, so a disabled or
// password-reset account is kicked out quickly (Identity's default is 30 minutes).
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CenterHub.Features.Accounts.RevalidatingAuthStateProvider>();

builder.Services.AddScoped<CenterHub.Features.Tickets.TicketService>();
builder.Services.AddScoped<CenterHub.Features.Accounts.AccountService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<CenterHub.Components.App>()
    .AddInteractiveServerRenderMode();

// Ensure database + roles exist on startup (dev convenience; fine for this
// single-server internal tool — no need for a separate migration pipeline).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in Roles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    if (await userManager.FindByNameAsync("admin") is null)
    {
        var admin = new ApplicationUser
        {
            UserName = "admin",
            DisplayName = "系統管理員",
            Email = "admin@example.edu" // placeholder; replace with a real mailbox before deployment
        };

        // Prefer an operator-supplied password (appsettings.json, environment
        // variable Seed__AdminPassword, or user-secrets in development). If none is
        // configured, generate one with AdminSeedPassword
        // and log it once so an operator can retrieve it from console/log output on
        // first run — never hardcode a literal password in source control.
        var configuredPassword = builder.Configuration["Seed:AdminPassword"];
        IdentityResult result;
        if (!string.IsNullOrEmpty(configuredPassword))
        {
            result = await userManager.CreateAsync(admin, configuredPassword);
        }
        else
        {
            // Generate() draws uniformly across character classes and is not
            // guaranteed to satisfy Identity's per-class requirements (e.g. a
            // draw with zero digits) — retry with a fresh password on failure
            // instead of silently ending up with no admin account at all, and
            // only log the password once we know it actually worked.
            const int maxAttempts = 5;
            var attempt = 0;
            string generatedPassword;
            do
            {
                attempt++;
                generatedPassword = AdminSeedPassword.Generate();
                result = await userManager.CreateAsync(admin, generatedPassword);
            } while (!result.Succeeded && attempt < maxAttempts);

            if (result.Succeeded)
            {
                app.Logger.LogInformation(
                    "Seeded initial admin password: {Password} — change this after first login.",
                    generatedPassword);
            }
        }

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, Roles.Admin);
        }
        else
        {
            app.Logger.LogError(
                "Failed to seed the initial admin account: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}

app.Run();
