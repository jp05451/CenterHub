# CenterHub MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Phase 1 MVP of CenterHub — a single Blazor Server app replacing Evernote (service tickets) + HackMD (shift summaries) + ad-hoc schedule tracking for the computer center's work-study desk.

**Architecture:** One ASP.NET Core Blazor Server project (`CenterHub`), organized by feature folder under `Features/`, backed by one EF Core `AppDbContext` over SQLite. `ShiftSummary` is a read-only computed service over `Ticket` rows, not its own table. ASP.NET Core Identity supplies auth with two roles (`Admin`, `WorkStudy`).

**Tech Stack:** .NET 10, ASP.NET Core Blazor Server, EF Core 10 (SQLite provider), ASP.NET Core Identity, xUnit for tests.

**Spec:** `docs/superpowers/specs/2026-09-08-centerhub-design.md`

## Global Constraints

- Deploy target is a Windows host at the computer center; develop on macOS using the `dotnet` CLI (cross-platform) — no macOS-only or Windows-only APIs.
- Two roles only: `Admin`, `WorkStudy`. No finer-grained permission model.
- S (self-resolved) / F (referred) in ShiftSummary are derived directly from `Ticket.ResolutionStatus` (`Completed` → S, `Incomplete` → F) — no separate field, no extra business rule.
- Notifications are email-only; a failed send must never block the operation that triggered it (per spec's error-handling section).
- No data migration from Evernote/HackMD/Excel — the app starts with an empty database.
- Charts and Word export are explicitly out of scope for this plan (Phase 2, spec §分期規劃).
- Desktop-first UI; mobile only needs to not be broken, no responsive optimization work.
- Per spec's testing strategy: aggregation/state-machine logic gets unit/integration tests; CRUD Razor pages get a manual verification step, not automated UI tests.

---

## File Structure

```
CenterHub.sln
CenterHub/
├── CenterHub.csproj
├── Program.cs
├── appsettings.json
├── Data/
│   └── AppDbContext.cs
├── Models/
│   ├── ApplicationUser.cs
│   ├── Roles.cs
│   ├── Enums.cs
│   ├── ServiceCategory.cs
│   ├── TicketCategory.cs
│   ├── Ticket.cs
│   ├── SopCategory.cs
│   ├── SopArticle.cs
│   ├── SopArticleRevision.cs
│   ├── ShiftSlot.cs
│   └── ShiftChangeRequest.cs
├── Features/
│   ├── Tickets/
│   │   ├── TicketFormModel.cs
│   │   ├── TicketFilter.cs
│   │   └── TicketService.cs
│   ├── ShiftSummary/
│   │   ├── ShiftSummaryResult.cs
│   │   ├── UnitCategoryBreakdown.cs
│   │   └── ShiftSummaryService.cs
│   ├── SopWiki/
│   │   ├── SopEditConflictException.cs
│   │   └── SopArticleService.cs
│   ├── Schedule/
│   │   └── ShiftChangeRequestService.cs
│   ├── Notifications/
│   │   ├── EmailMessage.cs
│   │   ├── IEmailSender.cs
│   │   ├── SmtpEmailSender.cs
│   │   └── EmailNotificationQueue.cs
│   └── Tools/
│       └── PasswordGeneratorService.cs
└── Components/
    ├── Layout/
    │   ├── MainLayout.razor
    │   └── NavMenu.razor
    └── Pages/
        ├── Tickets/
        │   ├── TicketList.razor
        │   └── TicketForm.razor
        ├── ShiftSummary/
        │   └── ShiftSummaryPage.razor
        ├── SopWiki/
        │   ├── SopList.razor
        │   ├── SopArticleCreate.razor
        │   ├── SopArticleView.razor
        │   └── SopArticleEdit.razor
        ├── Schedule/
        │   ├── ScheduleView.razor
        │   └── ShiftChangeRequests.razor
        └── Tools/
            └── PasswordGenerator.razor

CenterHub.Tests/
├── CenterHub.Tests.csproj
├── TicketServiceTests.cs
├── ShiftSummaryServiceTests.cs
├── SopArticleServiceTests.cs
├── ShiftChangeRequestServiceTests.cs
├── EmailNotificationQueueTests.cs
└── PasswordGeneratorServiceTests.cs
```

---

### Task 1: Solution & Project Scaffolding

**Files:**
- Create: `CenterHub.sln`
- Create: `CenterHub/CenterHub.csproj`
- Create: `CenterHub/Program.cs`
- Create: `CenterHub/appsettings.json`
- Create: `CenterHub.Tests/CenterHub.Tests.csproj`

**Interfaces:**
- Produces: a runnable empty Blazor Server app (`CenterHub`) and an xUnit test project (`CenterHub.Tests`) that references it — every later task builds inside these two projects.

- [ ] **Step 1: Scaffold the Blazor Server project**

```bash
dotnet new blazor -o CenterHub --interactivity Server --empty
dotnet new sln -n CenterHub
dotnet sln add CenterHub/CenterHub.csproj
```

- [ ] **Step 2: Add the NuGet packages this plan depends on**

```bash
cd CenterHub
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet tool install --global dotnet-ef
cd ..
```

- [ ] **Step 3: Scaffold the test project and wire it to the app project**

```bash
dotnet new xunit -o CenterHub.Tests
dotnet sln add CenterHub.Tests/CenterHub.Tests.csproj
cd CenterHub.Tests
dotnet add reference ../CenterHub/CenterHub.csproj
dotnet add package Microsoft.EntityFrameworkCore.InMemory
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
cd ..
```

- [ ] **Step 4: Make `CenterHub`'s internal members visible to the test project**

Task 16 and Task 17 add `internal` methods to `EmailNotificationQueue` for direct testing (`ProcessMessageAsync`, `TryReadForTest`). Without this, those tests fail to compile with `CS0122: is inaccessible due to its protection level`. Add this now so no later task needs to touch project settings.

Open `CenterHub/CenterHub.csproj` and add a new `<ItemGroup>` (any location inside the root `<Project>` element is fine):

```xml
<ItemGroup>
  <InternalsVisibleTo Include="CenterHub.Tests" />
</ItemGroup>
```

- [ ] **Step 5: Verify both projects build**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` for both `CenterHub` and `CenterHub.Tests`.

- [ ] **Step 6: Verify the app runs**

Run: `dotnet run --project CenterHub`
Expected: console prints `Now listening on: http://localhost:5xxx`; visiting that URL in a browser shows the default Blazor template page. Stop the server (Ctrl+C) once confirmed.

- [ ] **Step 7: Commit**

```bash
git add CenterHub.sln CenterHub CenterHub.Tests
git commit -m "chore: scaffold CenterHub Blazor Server app and test project"
```

---

### Task 2: Identity & AppDbContext Foundation

**Files:**
- Create: `CenterHub/Models/ApplicationUser.cs`
- Create: `CenterHub/Models/Roles.cs`
- Create: `CenterHub/Data/AppDbContext.cs`
- Modify: `CenterHub/Program.cs`
- Modify: `CenterHub/appsettings.json`

**Interfaces:**
- Consumes: nothing (first domain code).
- Produces: `ApplicationUser` (extends `IdentityUser`, adds `DisplayName`), `Roles.Admin` / `Roles.WorkStudy` constants, `AppDbContext : IdentityDbContext<ApplicationUser>` — every later task's `DbSet<T>` additions and every service's constructor (`AppDbContext db`) build on this.

- [ ] **Step 1: Add the connection string**

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=centerhub.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```
Replace the contents of `CenterHub/appsettings.json` with the above (keep it minimal for now; later tasks won't touch this file except Task 16, which adds an `Smtp` section).

- [ ] **Step 2: Write `ApplicationUser` and `Roles`**

```csharp
// CenterHub/Models/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace CenterHub.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
```

```csharp
// CenterHub/Models/Roles.cs
namespace CenterHub.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string WorkStudy = "WorkStudy";

    public static readonly string[] All = { Admin, WorkStudy };
}
```

- [ ] **Step 3: Write `AppDbContext`**

```csharp
// CenterHub/Data/AppDbContext.cs
using CenterHub.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CenterHub.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
    }
}
```

- [ ] **Step 4: Wire Identity + EF Core into `Program.cs`**

```csharp
// CenterHub/Program.cs
using CenterHub.Data;
using CenterHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

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
}

app.Run();
```

Note: `Components.App` refers to the root component the `dotnet new blazor` template already generated at `CenterHub/Components/App.razor` — leave that file as-is for now (Task 20 modifies it).

- [ ] **Step 5: Create the initial migration**

```bash
cd CenterHub
dotnet ef migrations add InitialIdentitySchema
dotnet ef database update
cd ..
```

Expected: `CenterHub/Data/Migrations/` now contains a migration creating the standard `AspNetUsers`, `AspNetRoles`, etc. tables, and `centerhub.db` is created in `CenterHub/`.

- [ ] **Step 6: Verify the app still starts and seeds roles**

Run: `dotnet run --project CenterHub`
Expected: no exceptions on startup; stop the server, then check the seeded roles:

```bash
sqlite3 CenterHub/centerhub.db "SELECT Name FROM AspNetRoles;"
```
Expected output: `Admin` and `WorkStudy`, one per line.

- [ ] **Step 7: Commit**

```bash
git add CenterHub
git commit -m "feat: add Identity, AppDbContext, and role seeding"
```

---

### Task 3: Ticket & ServiceCategory Data Model

**Files:**
- Create: `CenterHub/Models/Enums.cs`
- Create: `CenterHub/Models/ServiceCategory.cs`
- Create: `CenterHub/Models/TicketCategory.cs`
- Create: `CenterHub/Models/Ticket.cs`
- Modify: `CenterHub/Data/AppDbContext.cs`

**Interfaces:**
- Consumes: `AppDbContext` (Task 2), `ApplicationUser.Id` as the FK type (`string`).
- Produces: `Ticket`, `ServiceCategory`, `TicketCategory`, and the enums `ServiceMode`, `ResolutionStatus`, `ShiftPeriod` — `TicketService` (Task 4) and `ShiftSummaryService` (Task 7) both query these directly.

- [ ] **Step 1: Write the enums**

```csharp
// CenterHub/Models/Enums.cs
namespace CenterHub.Models;

public enum ServiceMode { OnSite, Phone }
public enum ResolutionStatus { Completed, Incomplete }
public enum ShiftPeriod { AM, PM }
public enum ShiftChangeType { Swap, Leave }
public enum ShiftChangeStatus { Pending, Approved, Rejected }
```

- [ ] **Step 2: Write `ServiceCategory` and the `Ticket`/`ServiceCategory` join entity**

```csharp
// CenterHub/Models/ServiceCategory.cs
namespace CenterHub.Models;

public class ServiceCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
```

```csharp
// CenterHub/Models/TicketCategory.cs
namespace CenterHub.Models;

public class TicketCategory
{
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public int ServiceCategoryId { get; set; }
    public ServiceCategory ServiceCategory { get; set; } = null!;
}
```

- [ ] **Step 3: Write `Ticket`**

```csharp
// CenterHub/Models/Ticket.cs
namespace CenterHub.Models;

public class Ticket
{
    public int Id { get; set; }

    // Per-day sequence number, computed by TicketService — see Task 4.
    public int DailySeq { get; set; }

    public DateOnly ShiftDate { get; set; }
    public ShiftPeriod ShiftPeriod { get; set; }
    public TimeOnly ContactTime { get; set; }

    public string RequestingUnit { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string? RequesterContact { get; set; }

    public ServiceMode ServiceMode { get; set; }
    public ResolutionStatus ResolutionStatus { get; set; }
    public int? SatisfactionRating { get; set; }
    public int ServiceDurationMinutes { get; set; }
    public string? OperatingSystem { get; set; }

    public string ProblemDescription { get; set; } = string.Empty;
    public string? SolutionDescription { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TicketCategory> Categories { get; set; } = new();
}
```

- [ ] **Step 4: Register the new entities and configure keys/enum storage in `AppDbContext`**

```csharp
// CenterHub/Data/AppDbContext.cs — replace OnModelCreating and add DbSets
using CenterHub.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CenterHub.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TicketCategory>()
            .HasKey(tc => new { tc.TicketId, tc.ServiceCategoryId });

        builder.Entity<TicketCategory>()
            .HasOne(tc => tc.Ticket)
            .WithMany(t => t.Categories)
            .HasForeignKey(tc => tc.TicketId);

        builder.Entity<TicketCategory>()
            .HasOne(tc => tc.ServiceCategory)
            .WithMany()
            .HasForeignKey(tc => tc.ServiceCategoryId);

        builder.Entity<Ticket>()
            .Property(t => t.ServiceMode)
            .HasConversion<string>();

        builder.Entity<Ticket>()
            .Property(t => t.ResolutionStatus)
            .HasConversion<string>();

        builder.Entity<Ticket>()
            .Property(t => t.ShiftPeriod)
            .HasConversion<string>();

        builder.Entity<ServiceCategory>().HasData(
            new ServiceCategory { Id = 1, Name = "硬體" },
            new ServiceCategory { Id = 2, Name = "軟體" },
            new ServiceCategory { Id = 3, Name = "網路" },
            new ServiceCategory { Id = 4, Name = "郵件" },
            new ServiceCategory { Id = 5, Name = "重灌" },
            new ServiceCategory { Id = 6, Name = "中毒" },
            new ServiceCategory { Id = 7, Name = "無線" },
            new ServiceCategory { Id = 8, Name = "諮詢" },
            new ServiceCategory { Id = 9, Name = "印表機" },
            new ServiceCategory { Id = 10, Name = "其他" }
        );
    }
}
```

- [ ] **Step 5: Generate and apply the migration**

```bash
cd CenterHub
dotnet ef migrations add AddTicketsAndServiceCategories
dotnet ef database update
cd ..
```

- [ ] **Step 6: Verify the seed data landed**

```bash
sqlite3 CenterHub/centerhub.db "SELECT Id, Name FROM ServiceCategories ORDER BY Id;"
```
Expected: 10 rows, `1|硬體` through `10|其他`.

- [ ] **Step 7: Commit**

```bash
git add CenterHub
git commit -m "feat: add Ticket and ServiceCategory data model"
```

---

### Task 4: TicketService — Create with Auto Daily Sequence

**Files:**
- Create: `CenterHub/Features/Tickets/TicketFormModel.cs`
- Create: `CenterHub/Features/Tickets/TicketService.cs`
- Test: `CenterHub.Tests/TicketServiceTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `Ticket`, `ServiceCategory`, `TicketCategory` (Task 3).
- Produces: `TicketFormModel` (validated DTO with `[Required]` annotations) and `TicketService.CreateTicketAsync(TicketFormModel model, string createdByUserId) : Task<Ticket>` — the Razor form (Task 6) binds to `TicketFormModel` and calls this method directly.

- [ ] **Step 1: Write `TicketFormModel`**

```csharp
// CenterHub/Features/Tickets/TicketFormModel.cs
using System.ComponentModel.DataAnnotations;
using CenterHub.Models;

namespace CenterHub.Features.Tickets;

public class TicketFormModel
{
    [Required]
    public DateOnly ShiftDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    [Required]
    public ShiftPeriod ShiftPeriod { get; set; }

    [Required]
    public TimeOnly ContactTime { get; set; } = TimeOnly.FromDateTime(DateTime.Now);

    [Required(ErrorMessage = "請輸入申請單位")]
    public string RequestingUnit { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入申請人")]
    public string RequesterName { get; set; } = string.Empty;

    public string? RequesterContact { get; set; }

    [Required]
    public ServiceMode ServiceMode { get; set; }

    [Required(ErrorMessage = "請至少選擇一個服務類別")]
    [MinLength(1, ErrorMessage = "請至少選擇一個服務類別")]
    public List<int> SelectedCategoryIds { get; set; } = new();

    [Required]
    public ResolutionStatus ResolutionStatus { get; set; }

    [Range(1, 5)]
    public int? SatisfactionRating { get; set; }

    [Range(0, 1440, ErrorMessage = "服務時間需介於 0 到 1440 分鐘")]
    public int ServiceDurationMinutes { get; set; }

    public string? OperatingSystem { get; set; }

    [Required(ErrorMessage = "請填寫問題簡述")]
    public string ProblemDescription { get; set; } = string.Empty;

    public string? SolutionDescription { get; set; }
}
```

- [ ] **Step 2: Write the failing integration test for daily sequence assignment**

```csharp
// CenterHub.Tests/TicketServiceTests.cs
using CenterHub.Data;
using CenterHub.Features.Tickets;
using CenterHub.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CenterHub.Tests;

public class TicketServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public TicketServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static TicketFormModel MakeModel(DateOnly date, ShiftPeriod period) => new()
    {
        ShiftDate = date,
        ShiftPeriod = period,
        ContactTime = new TimeOnly(14, 25),
        RequestingUnit = "材料系",
        RequesterName = "馬同學",
        RequesterContact = "M11404145",
        ServiceMode = ServiceMode.OnSite,
        SelectedCategoryIds = new List<int> { 8 },
        ResolutionStatus = ResolutionStatus.Completed,
        SatisfactionRating = 5,
        ServiceDurationMinutes = 5,
        OperatingSystem = "Windows",
        ProblemDescription = "忘記M365密碼",
        SolutionDescription = "重設M365密碼"
    };

    [Fact]
    public async Task CreateTicketAsync_FirstTicketOfDay_GetsSeqOne()
    {
        var service = new TicketService(_db);
        var date = new DateOnly(2026, 9, 8);

        var ticket = await service.CreateTicketAsync(MakeModel(date, ShiftPeriod.AM), "user-1");

        Assert.Equal(1, ticket.DailySeq);
    }

    [Fact]
    public async Task CreateTicketAsync_SecondTicketSameDay_GetsSeqTwo_EvenAcrossPeriods()
    {
        var service = new TicketService(_db);
        var date = new DateOnly(2026, 9, 8);

        await service.CreateTicketAsync(MakeModel(date, ShiftPeriod.AM), "user-1");
        var second = await service.CreateTicketAsync(MakeModel(date, ShiftPeriod.PM), "user-2");

        Assert.Equal(2, second.DailySeq);
    }

    [Fact]
    public async Task CreateTicketAsync_TicketOnDifferentDay_RestartsAtOne()
    {
        var service = new TicketService(_db);

        await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM), "user-1");
        var nextDay = await service.CreateTicketAsync(MakeModel(new DateOnly(2026, 9, 9), ShiftPeriod.AM), "user-1");

        Assert.Equal(1, nextDay.DailySeq);
    }

    [Fact]
    public async Task CreateTicketAsync_PersistsSelectedCategories()
    {
        var service = new TicketService(_db);
        var model = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM);
        model.SelectedCategoryIds = new List<int> { 1, 3 };

        var ticket = await service.CreateTicketAsync(model, "user-1");

        var saved = await _db.Tickets
            .Include(t => t.Categories)
            .FirstAsync(t => t.Id == ticket.Id);
        Assert.Equal(new[] { 1, 3 }, saved.Categories.Select(c => c.ServiceCategoryId).OrderBy(x => x));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter TicketServiceTests`
Expected: FAIL to compile — `TicketService` does not exist yet.

- [ ] **Step 4: Write `TicketService.CreateTicketAsync`**

```csharp
// CenterHub/Features/Tickets/TicketService.cs
using CenterHub.Data;
using CenterHub.Models;
using Microsoft.EntityFrameworkCore;

namespace CenterHub.Features.Tickets;

public class TicketService
{
    private readonly AppDbContext _db;

    public TicketService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Ticket> CreateTicketAsync(TicketFormModel model, string createdByUserId)
    {
        var maxSeq = await _db.Tickets
            .Where(t => t.ShiftDate == model.ShiftDate)
            .Select(t => (int?)t.DailySeq)
            .MaxAsync() ?? 0;

        var ticket = new Ticket
        {
            DailySeq = maxSeq + 1,
            ShiftDate = model.ShiftDate,
            ShiftPeriod = model.ShiftPeriod,
            ContactTime = model.ContactTime,
            RequestingUnit = model.RequestingUnit,
            RequesterName = model.RequesterName,
            RequesterContact = model.RequesterContact,
            ServiceMode = model.ServiceMode,
            ResolutionStatus = model.ResolutionStatus,
            SatisfactionRating = model.SatisfactionRating,
            ServiceDurationMinutes = model.ServiceDurationMinutes,
            OperatingSystem = model.OperatingSystem,
            ProblemDescription = model.ProblemDescription,
            SolutionDescription = model.SolutionDescription,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            Categories = model.SelectedCategoryIds
                .Select(categoryId => new TicketCategory { ServiceCategoryId = categoryId })
                .ToList()
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();
        return ticket;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --filter TicketServiceTests`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 6: Commit**

```bash
git add CenterHub CenterHub.Tests
git commit -m "feat: add TicketService with auto daily sequence"
```

---

### Task 5: TicketService — Filtering / List

**Files:**
- Create: `CenterHub/Features/Tickets/TicketFilter.cs`
- Modify: `CenterHub/Features/Tickets/TicketService.cs`
- Test: `CenterHub.Tests/TicketServiceTests.cs`

**Interfaces:**
- Consumes: `TicketService` (Task 4), `Ticket`, `TicketCategory`.
- Produces: `TicketFilter` and `TicketService.GetTicketsAsync(TicketFilter filter) : Task<List<Ticket>>` — the Ticket list page (Task 6) calls this directly.

- [ ] **Step 1: Write `TicketFilter`**

```csharp
// CenterHub/Features/Tickets/TicketFilter.cs
using CenterHub.Models;

namespace CenterHub.Features.Tickets;

public class TicketFilter
{
    public DateOnly? ShiftDate { get; set; }
    public string? RequestingUnit { get; set; }
    public int? ServiceCategoryId { get; set; }
    public ResolutionStatus? ResolutionStatus { get; set; }
}
```

- [ ] **Step 2: Add the failing test for filtering**

```csharp
// Append to CenterHub.Tests/TicketServiceTests.cs, inside the TicketServiceTests class

[Fact]
public async Task GetTicketsAsync_FiltersByShiftDateAndResolutionStatus()
{
    var service = new TicketService(_db);
    var wanted = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM);
    wanted.ResolutionStatus = ResolutionStatus.Completed;
    await service.CreateTicketAsync(wanted, "user-1");

    var wrongDate = MakeModel(new DateOnly(2026, 9, 9), ShiftPeriod.AM);
    await service.CreateTicketAsync(wrongDate, "user-1");

    var wrongStatus = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.PM);
    wrongStatus.ResolutionStatus = ResolutionStatus.Incomplete;
    await service.CreateTicketAsync(wrongStatus, "user-1");

    var results = await service.GetTicketsAsync(new TicketFilter
    {
        ShiftDate = new DateOnly(2026, 9, 8),
        ResolutionStatus = ResolutionStatus.Completed
    });

    var ticket = Assert.Single(results);
    Assert.Equal(new DateOnly(2026, 9, 8), ticket.ShiftDate);
    Assert.Equal(ResolutionStatus.Completed, ticket.ResolutionStatus);
}

[Fact]
public async Task GetTicketsAsync_FiltersByServiceCategory()
{
    var service = new TicketService(_db);
    var withCategory1 = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM);
    withCategory1.SelectedCategoryIds = new List<int> { 1 };
    await service.CreateTicketAsync(withCategory1, "user-1");

    var withCategory2 = MakeModel(new DateOnly(2026, 9, 8), ShiftPeriod.AM);
    withCategory2.SelectedCategoryIds = new List<int> { 2 };
    await service.CreateTicketAsync(withCategory2, "user-1");

    var results = await service.GetTicketsAsync(new TicketFilter { ServiceCategoryId = 2 });

    Assert.Single(results);
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter TicketServiceTests`
Expected: FAIL to compile — `GetTicketsAsync` does not exist.

- [ ] **Step 4: Implement `GetTicketsAsync`**

```csharp
// Append inside the TicketService class in CenterHub/Features/Tickets/TicketService.cs

public async Task<List<Ticket>> GetTicketsAsync(TicketFilter filter)
{
    var query = _db.Tickets
        .Include(t => t.Categories)
        .AsQueryable();

    if (filter.ShiftDate is { } shiftDate)
        query = query.Where(t => t.ShiftDate == shiftDate);

    if (!string.IsNullOrWhiteSpace(filter.RequestingUnit))
        query = query.Where(t => t.RequestingUnit.Contains(filter.RequestingUnit));

    if (filter.ServiceCategoryId is { } categoryId)
        query = query.Where(t => t.Categories.Any(c => c.ServiceCategoryId == categoryId));

    if (filter.ResolutionStatus is { } status)
        query = query.Where(t => t.ResolutionStatus == status);

    return await query
        .OrderByDescending(t => t.ShiftDate)
        .ThenByDescending(t => t.DailySeq)
        .ToListAsync();
}
```

- [ ] **Step 5: Also add a lookup helper the form will need for the category checkboxes**

```csharp
// Append inside the TicketService class

public async Task<List<ServiceCategory>> GetActiveCategoriesAsync()
{
    return await _db.ServiceCategories
        .Where(c => c.IsActive)
        .OrderBy(c => c.Id)
        .ToListAsync();
}

public async Task<List<string>> GetKnownRequestingUnitsAsync()
{
    return await _db.Tickets
        .Select(t => t.RequestingUnit)
        .Distinct()
        .OrderBy(u => u)
        .ToListAsync();
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --filter TicketServiceTests`
Expected: `Passed! - Failed: 0, Passed: 6`.

- [ ] **Step 7: Commit**

```bash
git add CenterHub CenterHub.Tests
git commit -m "feat: add ticket filtering and lookup helpers"
```

---

### Task 6: Ticket Razor Pages

**Files:**
- Create: `CenterHub/Components/Pages/Tickets/TicketList.razor`
- Create: `CenterHub/Components/Pages/Tickets/TicketForm.razor`
- Modify: `CenterHub/Program.cs`

**Interfaces:**
- Consumes: `TicketService`, `TicketFormModel`, `TicketFilter` (Tasks 4-5).
- Produces: two routed pages (`/tickets`, `/tickets/new`) — Task 19's `NavMenu` links to `/tickets`.

- [ ] **Step 1: Register `TicketService` for DI**

```csharp
// CenterHub/Program.cs — add alongside the other builder.Services calls, before builder.Build()
builder.Services.AddScoped<CenterHub.Features.Tickets.TicketService>();
```

- [ ] **Step 2: Write the ticket list page**

```razor
@* CenterHub/Components/Pages/Tickets/TicketList.razor *@
@page "/tickets"
@rendermode InteractiveServer
@using CenterHub.Features.Tickets
@using CenterHub.Models
@inject TicketService TicketService
@attribute [Microsoft.AspNetCore.Authorization.Authorize]

<h3>服務工單</h3>

<a href="/tickets/new">+ 新增工單</a>

<div>
    <label>日期: <input type="date" @bind="_filterDate" /></label>
    <label>單位: <input type="text" @bind="_filterUnit" /></label>
    <label>類別:
        <select @bind="_filterCategoryId">
            <option value="">全部</option>
            @foreach (var category in _categories)
            {
                <option value="@category.Id">@category.Name</option>
            }
        </select>
    </label>
    <label>處理結果:
        <select @bind="_filterStatus">
            <option value="">全部</option>
            <option value="Completed">已完成</option>
            <option value="Incomplete">未完成</option>
        </select>
    </label>
    <button @onclick="LoadTickets">篩選</button>
</div>

@if (_tickets is null)
{
    <p>載入中...</p>
}
else if (_tickets.Count == 0)
{
    <p>沒有符合條件的工單。</p>
}
else
{
    <table>
        <thead>
            <tr>
                <th>編號</th><th>日期</th><th>班別</th><th>單位</th><th>申請人</th>
                <th>類別</th><th>處理結果</th><th>時間(分)</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var ticket in _tickets)
            {
                <tr>
                    <td>@ticket.DailySeq</td>
                    <td>@ticket.ShiftDate</td>
                    <td>@ticket.ShiftPeriod</td>
                    <td>@ticket.RequestingUnit</td>
                    <td>@ticket.RequesterName</td>
                    <td>@string.Join(", ", ticket.Categories.Select(c => c.ServiceCategoryId))</td>
                    <td>@(ticket.ResolutionStatus == ResolutionStatus.Completed ? "已完成" : "未完成")</td>
                    <td>@ticket.ServiceDurationMinutes</td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private List<Ticket>? _tickets;
    private List<ServiceCategory> _categories = new();
    private DateTime? _filterDate;
    private string _filterUnit = "";
    private string _filterCategoryId = "";
    private string _filterStatus = "";

    protected override async Task OnInitializedAsync()
    {
        _categories = await TicketService.GetActiveCategoriesAsync();
        await LoadTickets();
    }

    private async Task LoadTickets()
    {
        var filter = new TicketFilter
        {
            ShiftDate = _filterDate is { } d ? DateOnly.FromDateTime(d) : null,
            RequestingUnit = string.IsNullOrWhiteSpace(_filterUnit) ? null : _filterUnit,
            ServiceCategoryId = int.TryParse(_filterCategoryId, out var categoryId) ? categoryId : null,
            ResolutionStatus = _filterStatus switch
            {
                "Completed" => ResolutionStatus.Completed,
                "Incomplete" => ResolutionStatus.Incomplete,
                _ => null
            }
        };
        _tickets = await TicketService.GetTicketsAsync(filter);
    }
}
```

- [ ] **Step 3: Write the ticket creation form**

```razor
@* CenterHub/Components/Pages/Tickets/TicketForm.razor *@
@page "/tickets/new"
@rendermode InteractiveServer
@using System.ComponentModel.DataAnnotations
@using CenterHub.Features.Tickets
@using CenterHub.Models
@using Microsoft.AspNetCore.Components.Authorization
@inject TicketService TicketService
@inject NavigationManager Nav
@inject AuthenticationStateProvider AuthProvider
@attribute [Microsoft.AspNetCore.Authorization.Authorize]

<h3>新增工單</h3>

<EditForm Model="_model" OnValidSubmit="Submit">
    <DataAnnotationsValidator />
    <ValidationSummary />

    <div><label>值班日期: <InputDate @bind-Value="_model.ShiftDate" /></label></div>
    <div>
        <label>班別:
            <InputSelect @bind-Value="_model.ShiftPeriod">
                <option value="AM">AM</option>
                <option value="PM">PM</option>
            </InputSelect>
        </label>
    </div>
    <div><label>聯絡時間: <InputText @bind-Value="_contactTimeText" placeholder="HH:mm" /></label></div>
    <div><label>申請單位: <InputText @bind-Value="_model.RequestingUnit" list="known-units" /></label>
        <datalist id="known-units">
            @foreach (var unit in _knownUnits)
            {
                <option value="@unit" />
            }
        </datalist>
    </div>
    <div><label>申請人: <InputText @bind-Value="_model.RequesterName" /></label></div>
    <div><label>分機/學號: <InputText @bind-Value="_model.RequesterContact" /></label></div>
    <div>
        <label>服務性質:
            <InputSelect @bind-Value="_model.ServiceMode">
                <option value="OnSite">到場處理</option>
                <option value="Phone">電話線上</option>
            </InputSelect>
        </label>
    </div>
    <div>
        <p>服務類別:</p>
        @foreach (var category in _categories)
        {
            <label>
                <input type="checkbox"
                       checked="@_model.SelectedCategoryIds.Contains(category.Id)"
                       @onchange="e => ToggleCategory(category.Id, (bool)e.Value!)" />
                @category.Name
            </label>
        }
    </div>
    <div>
        <label>處理結果:
            <InputSelect @bind-Value="_model.ResolutionStatus">
                <option value="Completed">已完成</option>
                <option value="Incomplete">未完成</option>
            </InputSelect>
        </label>
    </div>
    <div><label>服務滿意度(1-5): <InputNumber @bind-Value="_model.SatisfactionRating" /></label></div>
    <div><label>服務時間(分): <InputNumber @bind-Value="_model.ServiceDurationMinutes" /></label></div>
    <div><label>作業系統: <InputText @bind-Value="_model.OperatingSystem" /></label></div>
    <div><label>問題簡述: <InputTextArea @bind-Value="_model.ProblemDescription" /></label></div>
    <div><label>解決方法: <InputTextArea @bind-Value="_model.SolutionDescription" /></label></div>

    <button type="submit">送出</button>
</EditForm>

@code {
    private readonly TicketFormModel _model = new();
    private List<ServiceCategory> _categories = new();
    private List<string> _knownUnits = new();
    private string _contactTimeText = DateTime.Now.ToString("HH:mm");

    protected override async Task OnInitializedAsync()
    {
        _categories = await TicketService.GetActiveCategoriesAsync();
        _knownUnits = await TicketService.GetKnownRequestingUnitsAsync();
    }

    private void ToggleCategory(int categoryId, bool isChecked)
    {
        if (isChecked && !_model.SelectedCategoryIds.Contains(categoryId))
            _model.SelectedCategoryIds.Add(categoryId);
        else if (!isChecked)
            _model.SelectedCategoryIds.Remove(categoryId);
    }

    private async Task Submit()
    {
        _model.ContactTime = TimeOnly.Parse(_contactTimeText);

        var authState = await AuthProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("No authenticated user id found.");

        await TicketService.CreateTicketAsync(_model, userId);
        Nav.NavigateTo("/tickets");
    }
}
```

- [ ] **Step 4: Verify the build succeeds**

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 5: Manual verification**

Run: `dotnet run --project CenterHub`, then:
1. Navigate to `/tickets/new` (you won't be able to log in yet — Task 19 adds login; for now, temporarily comment out the `[Authorize]` attribute on both pages, verify the flow, then restore it before committing).
2. Fill in the form matching the sample ticket from the spec (材料系, 馬同學, 到場處理, 諮詢, 已完成, 5 分鐘, Windows, 忘記M365密碼 / 重設M365密碼) and submit.
3. Confirm you land on `/tickets` and the new row appears with `DailySeq = 1`.

Expected: the ticket appears in the list with the values you entered.

- [ ] **Step 6: Commit**

```bash
git add CenterHub
git commit -m "feat: add ticket list and creation pages"
```

---

### Task 7: ShiftSummaryService — Core Aggregation

**Files:**
- Create: `CenterHub/Features/ShiftSummary/ShiftSummaryResult.cs`
- Create: `CenterHub/Features/ShiftSummary/UnitCategoryBreakdown.cs`
- Create: `CenterHub/Features/ShiftSummary/ShiftSummaryService.cs`
- Test: `CenterHub.Tests/ShiftSummaryServiceTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `Ticket`, `TicketCategory`, `ResolutionStatus` (Task 3).
- Produces: `ShiftSummaryResult` (`TotalCount`, `SelfResolvedCount`, `ReferredCount`, `DefenseRate`, `TotalMinutes`, `CrossTab`) and `ShiftSummaryService.GetSummaryAsync(DateOnly shiftDate, ShiftPeriod period, string userId) : Task<ShiftSummaryResult>` — the ShiftSummary page (Task 9) calls this directly. This is the spec's highest-priority tested logic (§測試策略).

- [ ] **Step 1: Write the result DTOs**

```csharp
// CenterHub/Features/ShiftSummary/ShiftSummaryResult.cs
namespace CenterHub.Features.ShiftSummary;

public class ShiftSummaryResult
{
    public int TotalCount { get; set; }
    public int SelfResolvedCount { get; set; }
    public int ReferredCount { get; set; }
    public double DefenseRate { get; set; }
    public int TotalMinutes { get; set; }
    public List<UnitCategoryBreakdown> CrossTab { get; set; } = new();
}
```

```csharp
// CenterHub/Features/ShiftSummary/UnitCategoryBreakdown.cs
namespace CenterHub.Features.ShiftSummary;

public class UnitCategoryBreakdown
{
    public string Unit { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int Count { get; set; }
    public int TotalMinutes { get; set; }
}
```

- [ ] **Step 2: Write the failing unit tests for the core numbers**

```csharp
// CenterHub.Tests/ShiftSummaryServiceTests.cs
using CenterHub.Data;
using CenterHub.Features.ShiftSummary;
using CenterHub.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CenterHub.Tests;

public class ShiftSummaryServiceTests : IDisposable
{
    private readonly AppDbContext _db;

    public ShiftSummaryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private Ticket AddTicket(DateOnly date, ShiftPeriod period, string userId,
        ResolutionStatus status, int minutes, string unit, int categoryId)
    {
        var ticket = new Ticket
        {
            ShiftDate = date,
            ShiftPeriod = period,
            CreatedByUserId = userId,
            ResolutionStatus = status,
            ServiceDurationMinutes = minutes,
            RequestingUnit = unit,
            RequesterName = "test",
            ProblemDescription = "test",
            Categories = new List<TicketCategory>
            {
                new() { ServiceCategoryId = categoryId }
            }
        };
        _db.Tickets.Add(ticket);
        return ticket;
    }

    [Fact]
    public async Task GetSummaryAsync_CountsTotalSelfResolvedAndReferred()
    {
        var date = new DateOnly(2026, 9, 8);
        AddTicket(date, ShiftPeriod.AM, "user-1", ResolutionStatus.Completed, 10, "主計室", 1);
        AddTicket(date, ShiftPeriod.AM, "user-1", ResolutionStatus.Completed, 5, "主計室", 1);
        AddTicket(date, ShiftPeriod.AM, "user-1", ResolutionStatus.Incomplete, 20, "應科所", 2);
        await _db.SaveChangesAsync();

        var service = new ShiftSummaryService(_db);
        var result = await service.GetSummaryAsync(date, ShiftPeriod.AM, "user-1");

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.SelfResolvedCount);
        Assert.Equal(1, result.ReferredCount);
        Assert.Equal(35, result.TotalMinutes);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesDefenseRateAsSelfResolvedOverTotal()
    {
        var date = new DateOnly(2026, 9, 8);
        AddTicket(date, ShiftPeriod.AM, "user-1", ResolutionStatus.Completed, 10, "主計室", 1);
        AddTicket(date, ShiftPeriod.AM, "user-1", ResolutionStatus.Incomplete, 10, "主計室", 1);
        AddTicket(date, ShiftPeriod.AM, "user-1", ResolutionStatus.Incomplete, 10, "主計室", 1);
        AddTicket(date, ShiftPeriod.AM, "user-1", ResolutionStatus.Incomplete, 10, "主計室", 1);
        await _db.SaveChangesAsync();

        var service = new ShiftSummaryService(_db);
        var result = await service.GetSummaryAsync(date, ShiftPeriod.AM, "user-1");

        Assert.Equal(0.25, result.DefenseRate, precision: 4);
    }

    [Fact]
    public async Task GetSummaryAsync_WithNoTickets_ReturnsZeroCountsAndZeroDefenseRate()
    {
        var service = new ShiftSummaryService(_db);
        var result = await service.GetSummaryAsync(new DateOnly(2026, 9, 8), ShiftPeriod.AM, "user-1");

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.DefenseRate);
    }

    [Fact]
    public async Task GetSummaryAsync_OnlyIncludesTicketsForGivenDatePeriodAndUser()
    {
        var date = new DateOnly(2026, 9, 8);
        AddTicket(date, ShiftPeriod.AM, "user-1", ResolutionStatus.Completed, 10, "主計室", 1);
        AddTicket(date, ShiftPeriod.PM, "user-1", ResolutionStatus.Completed, 10, "主計室", 1); // wrong period
        AddTicket(date.AddDays(1), ShiftPeriod.AM, "user-1", ResolutionStatus.Completed, 10, "主計室", 1); // wrong date
        AddTicket(date, ShiftPeriod.AM, "user-2", ResolutionStatus.Completed, 10, "主計室", 1); // wrong user
        await _db.SaveChangesAsync();

        var service = new ShiftSummaryService(_db);
        var result = await service.GetSummaryAsync(date, ShiftPeriod.AM, "user-1");

        Assert.Equal(1, result.TotalCount);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter ShiftSummaryServiceTests`
Expected: FAIL to compile — `ShiftSummaryService` does not exist.

- [ ] **Step 4: Implement the core aggregation (cross-tab left empty for now, filled in Task 8)**

```csharp
// CenterHub/Features/ShiftSummary/ShiftSummaryService.cs
using CenterHub.Data;
using CenterHub.Models;
using Microsoft.EntityFrameworkCore;

namespace CenterHub.Features.ShiftSummary;

public class ShiftSummaryService
{
    private readonly AppDbContext _db;

    public ShiftSummaryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ShiftSummaryResult> GetSummaryAsync(DateOnly shiftDate, ShiftPeriod period, string userId)
    {
        var tickets = await _db.Tickets
            .Include(t => t.Categories)
            .Where(t => t.ShiftDate == shiftDate
                        && t.ShiftPeriod == period
                        && t.CreatedByUserId == userId)
            .ToListAsync();

        var total = tickets.Count;
        var selfResolved = tickets.Count(t => t.ResolutionStatus == ResolutionStatus.Completed);
        var referred = tickets.Count(t => t.ResolutionStatus == ResolutionStatus.Incomplete);

        return new ShiftSummaryResult
        {
            TotalCount = total,
            SelfResolvedCount = selfResolved,
            ReferredCount = referred,
            DefenseRate = total == 0 ? 0 : (double)selfResolved / total,
            TotalMinutes = tickets.Sum(t => t.ServiceDurationMinutes),
            CrossTab = new List<UnitCategoryBreakdown>()
        };
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --filter ShiftSummaryServiceTests`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 6: Commit**

```bash
git add CenterHub CenterHub.Tests
git commit -m "feat: add ShiftSummaryService core aggregation (T/S/F/defense rate)"
```

---

### Task 8: ShiftSummaryService — Unit × Category Cross-Tab

**Files:**
- Modify: `CenterHub/Features/ShiftSummary/ShiftSummaryService.cs`
- Test: `CenterHub.Tests/ShiftSummaryServiceTests.cs`

**Interfaces:**
- Consumes: `ShiftSummaryService.GetSummaryAsync` (Task 7), `ServiceCategory.Name`.
- Produces: a populated `ShiftSummaryResult.CrossTab` — one `UnitCategoryBreakdown` row per distinct (unit, category) pair present in the matching tickets.

- [ ] **Step 1: Write the failing test**

```csharp
// Append to CenterHub.Tests/ShiftSummaryServiceTests.cs, inside the class

[Fact]
public async Task GetSummaryAsync_BuildsCrossTabByUnitAndCategory()
{
    var date = new DateOnly(2026, 9, 8);
    _db.ServiceCategories.Add(new ServiceCategory { Id = 1, Name = "硬體" });
    _db.ServiceCategories.Add(new ServiceCategory { Id = 2, Name = "軟體" });
    AddTicket(date, ShiftPeriod.AM, "user-1", ResolutionStatus.Completed, 10, "主計室", 1);
    AddTicket(date, ShiftPeriod.AM, "user-1", ResolutionStatus.Completed, 20, "主計室", 1);
    AddTicket(date, ShiftPeriod.AM, "user-1", ResolutionStatus.Completed, 15, "應科所", 2);
    await _db.SaveChangesAsync();

    var service = new ShiftSummaryService(_db);
    var result = await service.GetSummaryAsync(date, ShiftPeriod.AM, "user-1");

    Assert.Equal(2, result.CrossTab.Count);
    var mainOffice = result.CrossTab.Single(r => r.Unit == "主計室");
    Assert.Equal("硬體", mainOffice.CategoryName);
    Assert.Equal(2, mainOffice.Count);
    Assert.Equal(30, mainOffice.TotalMinutes);
    var appliedSci = result.CrossTab.Single(r => r.Unit == "應科所");
    Assert.Equal("軟體", appliedSci.CategoryName);
    Assert.Equal(1, appliedSci.Count);
    Assert.Equal(15, appliedSci.TotalMinutes);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter GetSummaryAsync_BuildsCrossTabByUnitAndCategory`
Expected: FAIL — `result.CrossTab.Count` is `0`, not `2`.

- [ ] **Step 3: Implement the cross-tab**

```csharp
// CenterHub/Features/ShiftSummary/ShiftSummaryService.cs — replace GetSummaryAsync
public async Task<ShiftSummaryResult> GetSummaryAsync(DateOnly shiftDate, ShiftPeriod period, string userId)
{
    var tickets = await _db.Tickets
        .Include(t => t.Categories)
        .Where(t => t.ShiftDate == shiftDate
                    && t.ShiftPeriod == period
                    && t.CreatedByUserId == userId)
        .ToListAsync();

    var total = tickets.Count;
    var selfResolved = tickets.Count(t => t.ResolutionStatus == ResolutionStatus.Completed);
    var referred = tickets.Count(t => t.ResolutionStatus == ResolutionStatus.Incomplete);

    var categoryIds = tickets.SelectMany(t => t.Categories.Select(c => c.ServiceCategoryId)).Distinct().ToList();
    var categoryNames = await _db.ServiceCategories
        .Where(c => categoryIds.Contains(c.Id))
        .ToDictionaryAsync(c => c.Id, c => c.Name);

    var crossTab = tickets
        .SelectMany(t => t.Categories.Select(c => (t.RequestingUnit, c.ServiceCategoryId, t.ServiceDurationMinutes)))
        .GroupBy(row => (row.RequestingUnit, row.ServiceCategoryId))
        .Select(group => new UnitCategoryBreakdown
        {
            Unit = group.Key.RequestingUnit,
            CategoryName = categoryNames.GetValueOrDefault(group.Key.ServiceCategoryId, "未知類別"),
            Count = group.Count(),
            TotalMinutes = group.Sum(row => row.ServiceDurationMinutes)
        })
        .OrderBy(row => row.Unit)
        .ToList();

    return new ShiftSummaryResult
    {
        TotalCount = total,
        SelfResolvedCount = selfResolved,
        ReferredCount = referred,
        DefenseRate = total == 0 ? 0 : (double)selfResolved / total,
        TotalMinutes = tickets.Sum(t => t.ServiceDurationMinutes),
        CrossTab = crossTab
    };
}
```

- [ ] **Step 4: Run all ShiftSummaryService tests to verify they pass**

Run: `dotnet test --filter ShiftSummaryServiceTests`
Expected: `Passed! - Failed: 0, Passed: 5`.

- [ ] **Step 5: Commit**

```bash
git add CenterHub CenterHub.Tests
git commit -m "feat: add unit x category cross-tab to ShiftSummaryService"
```

---

### Task 9: ShiftSummary Razor Page

**Files:**
- Create: `CenterHub/Components/Pages/ShiftSummary/ShiftSummaryPage.razor`
- Modify: `CenterHub/Program.cs`

**Interfaces:**
- Consumes: `ShiftSummaryService.GetSummaryAsync` (Tasks 7-8).
- Produces: a routed page at `/shift-summary`.

- [ ] **Step 1: Register `ShiftSummaryService` for DI**

```csharp
// CenterHub/Program.cs — add alongside the TicketService registration
builder.Services.AddScoped<CenterHub.Features.ShiftSummary.ShiftSummaryService>();
```

- [ ] **Step 2: Write the page**

```razor
@* CenterHub/Components/Pages/ShiftSummary/ShiftSummaryPage.razor *@
@page "/shift-summary"
@rendermode InteractiveServer
@using CenterHub.Features.ShiftSummary
@using CenterHub.Models
@using Microsoft.AspNetCore.Components.Authorization
@inject ShiftSummaryService SummaryService
@inject AuthenticationStateProvider AuthProvider
@attribute [Microsoft.AspNetCore.Authorization.Authorize]

<h3>班別彙總</h3>

<div>
    <label>日期: <input type="date" @bind="_date" /></label>
    <label>班別:
        <select @bind="_period">
            <option value="AM">AM</option>
            <option value="PM">PM</option>
        </select>
    </label>
    <button @onclick="Load">查詢</button>
</div>

@if (_result is not null)
{
    <ul>
        <li>總服務案件數(T): @_result.TotalCount</li>
        <li>自行解決(S): @_result.SelfResolvedCount</li>
        <li>轉介其他單位(F): @_result.ReferredCount</li>
        <li>防衛率(S/T): @_result.DefenseRate.ToString("P0")</li>
        <li>總服務時間: @_result.TotalMinutes 分</li>
    </ul>

    <table>
        <thead><tr><th>單位</th><th>類別</th><th>件數</th><th>時間(分)</th></tr></thead>
        <tbody>
            @foreach (var row in _result.CrossTab)
            {
                <tr>
                    <td>@row.Unit</td>
                    <td>@row.CategoryName</td>
                    <td>@row.Count</td>
                    <td>@row.TotalMinutes</td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private DateTime _date = DateTime.Now;
    private string _period = "AM";
    private ShiftSummaryResult? _result;

    protected override async Task OnInitializedAsync() => await Load();

    private async Task Load()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("No authenticated user id found.");

        var period = _period == "AM" ? ShiftPeriod.AM : ShiftPeriod.PM;
        _result = await SummaryService.GetSummaryAsync(DateOnly.FromDateTime(_date), period, userId);
    }
}
```

- [ ] **Step 3: Manual verification**

Run: `dotnet run --project CenterHub`. After Task 19 adds login, log in as a work-study user who created the ticket from Task 6's manual test, navigate to `/shift-summary`, pick the same date/period, and confirm the counts match the ticket you entered (T=1, S=1 or F=1 depending on its resolution status, defense rate 100% or 0%).

Expected: numbers match what you entered by hand.

- [ ] **Step 4: Commit**

```bash
git add CenterHub
git commit -m "feat: add shift summary page"
```

---

### Task 10: SOP Data Model

**Files:**
- Create: `CenterHub/Models/SopCategory.cs`
- Create: `CenterHub/Models/SopArticle.cs`
- Create: `CenterHub/Models/SopArticleRevision.cs`
- Modify: `CenterHub/Data/AppDbContext.cs`

**Interfaces:**
- Consumes: `AppDbContext` (Task 2).
- Produces: `SopCategory`, `SopArticle` (with `RowVersion` concurrency token), `SopArticleRevision` — `SopArticleService` (Task 11) operates on these.

- [ ] **Step 1: Write the models**

```csharp
// CenterHub/Models/SopCategory.cs
namespace CenterHub.Models;

public class SopCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

```csharp
// CenterHub/Models/SopArticle.cs
namespace CenterHub.Models;

public class SopArticle
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public SopCategory Category { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Who most recently wrote the CURRENT content — distinct from CreatedByUserId,
    // which never changes after creation. Null means "still the creator's content"
    // (no edits yet). SopArticleService uses this to correctly attribute each archived
    // SopArticleRevision to whoever actually wrote the content being superseded, not
    // always the original creator — see Task 11's UpdateAsync.
    public string? LastEditedByUserId { get; set; }

    // Concurrency token. NOT store-generated: SQLite has no native rowversion/timestamp
    // type (unlike SQL Server), so `[Timestamp]`/`IsRowVersion()` would leave this column
    // NULL on insert and never change it on update, silently defeating optimistic
    // concurrency. Instead, SopArticleService assigns a fresh Guid-derived value here on
    // every create/update (see Task 11) and AppDbContext marks it `IsConcurrencyToken()`
    // (plain, not store-generated) so EF Core still uses it in the UPDATE's WHERE clause.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
```

```csharp
// CenterHub/Models/SopArticleRevision.cs
namespace CenterHub.Models;

public class SopArticleRevision
{
    public int Id { get; set; }
    public int SopArticleId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string EditedByUserId { get; set; } = string.Empty;
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Register the DbSets in `AppDbContext`**

```csharp
// CenterHub/Data/AppDbContext.cs — add to the DbSet list
public DbSet<SopCategory> SopCategories => Set<SopCategory>();
public DbSet<SopArticle> SopArticles => Set<SopArticle>();
public DbSet<SopArticleRevision> SopArticleRevisions => Set<SopArticleRevision>();
```

`AppDbContext` needs one more piece of Fluent API config, since `SqlArticle.RowVersion` has no `[Timestamp]` attribute (SQLite doesn't support store-generated rowversion columns — see the comment on the model). Add this inside `OnModelCreating`, after the existing `Ticket`/`ServiceCategory` configuration:

```csharp
// CenterHub/Data/AppDbContext.cs — add inside OnModelCreating
builder.Entity<SopArticle>()
    .Property(a => a.RowVersion)
    .IsConcurrencyToken();
```

This marks `RowVersion` as a concurrency token EF Core will include in every UPDATE's WHERE clause, WITHOUT expecting the database to auto-generate its value (that part is `SopArticleService`'s job in Task 11 — it assigns a fresh value on every create/update). The FK conventions for `SopArticle.CategoryId` and `SopArticleRevision.SopArticleId` are inferred automatically and need no explicit configuration.

- [ ] **Step 3: Generate and apply the migration**

```bash
cd CenterHub
dotnet ef migrations add AddSopWiki
dotnet ef database update
cd ..
```

- [ ] **Step 4: Verify the tables exist**

```bash
sqlite3 CenterHub/centerhub.db ".tables"
```
Expected: output includes `SopCategories`, `SopArticles`, `SopArticleRevisions`.

- [ ] **Step 5: Commit**

```bash
git add CenterHub
git commit -m "feat: add SOP wiki data model"
```

---

### Task 11: SopArticleService — Optimistic Concurrency + Revisions

**Files:**
- Create: `CenterHub/Features/SopWiki/SopEditConflictException.cs`
- Create: `CenterHub/Features/SopWiki/SopArticleService.cs`
- Test: `CenterHub.Tests/SopArticleServiceTests.cs`

**Interfaces:**
- Consumes: `SopArticle`, `SopArticleRevision`, `SopCategory` (Task 10).
- Produces: `SopArticleService.CreateAsync(...)`, `.UpdateAsync(int articleId, string newContent, byte[] originalRowVersion, string userId)` (throws `SopEditConflictException` on a stale `RowVersion`), `.GetRevisionsAsync(int articleId)` — the SOP edit page (Task 12) calls these and catches `SopEditConflictException`.

This test needs real relational concurrency-token behavior, which the EF Core InMemory provider does not enforce — use the SQLite in-memory connection pattern from Task 4's tests.

- [ ] **Step 1: Write `SopEditConflictException`**

```csharp
// CenterHub/Features/SopWiki/SopEditConflictException.cs
namespace CenterHub.Features.SopWiki;

public class SopEditConflictException : Exception
{
    public SopEditConflictException()
        : base("這篇文章已被其他人修改，請重新整理後再編輯。")
    {
    }
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
// CenterHub.Tests/SopArticleServiceTests.cs
using CenterHub.Data;
using CenterHub.Features.SopWiki;
using CenterHub.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CenterHub.Tests;

public class SopArticleServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SopArticleServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
        db.SopCategories.Add(new SopCategory { Id = 1, Name = "帳號問題" });
        db.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task CreateAsync_CreatesArticleWithGivenContent()
    {
        using var db = new AppDbContext(_options);
        var service = new SopArticleService(db);

        var article = await service.CreateAsync("重設 M365 密碼流程", 1, "1. 到後台重設\n2. 通知使用者", "user-1");

        Assert.NotEqual(0, article.Id);
        Assert.Equal("重設 M365 密碼流程", article.Title);
    }

    [Fact]
    public async Task UpdateAsync_WithCurrentRowVersion_UpdatesContentAndRecordsRevision()
    {
        using var db = new AppDbContext(_options);
        var service = new SopArticleService(db);
        var article = await service.CreateAsync("流程A", 1, "原始內容", "user-1");
        var currentVersion = article.RowVersion;

        await service.UpdateAsync(article.Id, "修改後內容", currentVersion, "user-2");

        var updated = await db.SopArticles.FindAsync(article.Id);
        Assert.Equal("修改後內容", updated!.Content);

        var revisions = await service.GetRevisionsAsync(article.Id);
        var revision = Assert.Single(revisions);
        Assert.Equal("原始內容", revision.Content);
        Assert.Equal("user-1", revision.EditedByUserId);
    }

    [Fact]
    public async Task UpdateAsync_WithStaleRowVersion_ThrowsSopEditConflictException()
    {
        using var db = new AppDbContext(_options);
        var service = new SopArticleService(db);
        var article = await service.CreateAsync("流程B", 1, "原始內容", "user-1");
        var staleVersion = (byte[])article.RowVersion.Clone();

        // Someone else edits it first, changing the RowVersion in the database.
        await service.UpdateAsync(article.Id, "別人先改的版本", article.RowVersion, "user-2");

        await Assert.ThrowsAsync<SopEditConflictException>(
            () => service.UpdateAsync(article.Id, "我的修改", staleVersion, "user-3"));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter SopArticleServiceTests`
Expected: FAIL to compile — `SopArticleService` does not exist.

- [ ] **Step 4: Implement `SopArticleService`**

```csharp
// CenterHub/Features/SopWiki/SopArticleService.cs
using CenterHub.Data;
using CenterHub.Models;
using Microsoft.EntityFrameworkCore;

namespace CenterHub.Features.SopWiki;

public class SopArticleService
{
    private readonly AppDbContext _db;

    public SopArticleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SopArticle> CreateAsync(string title, int categoryId, string content, string userId)
    {
        var article = new SopArticle
        {
            Title = title,
            CategoryId = categoryId,
            Content = content,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        _db.SopArticles.Add(article);
        await _db.SaveChangesAsync();
        return article;
    }

    public async Task UpdateAsync(int articleId, string newContent, byte[] originalRowVersion, string userId)
    {
        var article = await _db.SopArticles.FindAsync(articleId)
            ?? throw new InvalidOperationException($"SOP article {articleId} not found.");

        var previousContent = article.Content;
        // The content now being archived was written by whoever last edited it — the
        // creator if this is the first edit (LastEditedByUserId still null), or the
        // previous editor otherwise. Must be read BEFORE LastEditedByUserId is
        // overwritten below, or this would misattribute every revision to the current
        // editor instead of the actual author of the superseded content.
        var previousContentAuthor = article.LastEditedByUserId ?? article.CreatedByUserId;
        _db.Entry(article).Property(a => a.RowVersion).OriginalValue = originalRowVersion;

        article.Content = newContent;
        article.UpdatedAt = DateTime.UtcNow;
        article.LastEditedByUserId = userId;
        // Assign a fresh concurrency-token value on every successful update — RowVersion
        // is not store-generated (see the Task 10 model comment), so the app must change
        // it itself for the next editor's optimistic-concurrency check to have something
        // to compare against.
        article.RowVersion = Guid.NewGuid().ToByteArray();

        var revision = new SopArticleRevision
        {
            SopArticleId = articleId,
            Content = previousContent,
            EditedByUserId = previousContentAuthor,
            EditedAt = DateTime.UtcNow
        };
        _db.SopArticleRevisions.Add(revision);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Detach both entries before rethrowing. AppDbContext is shared per Blazor
            // Server circuit (same scoped instance as the calling page's own DbContext
            // injection), so a failed SopArticle stays tracked as Modified with its
            // rejected in-memory values. Left tracked, any later FindAsync/query for the
            // same Id — from this service OR the calling page's own reload — returns
            // that stale, still-conflicted instance straight from the identity map
            // instead of re-querying the database, silently defeating "reload and
            // retry." Detaching forces the next lookup to hit the database for real.
            _db.Entry(article).State = EntityState.Detached;
            _db.Entry(revision).State = EntityState.Detached;
            throw new SopEditConflictException();
        }
    }

    public async Task<List<SopArticleRevision>> GetRevisionsAsync(int articleId)
    {
        return await _db.SopArticleRevisions
            .Where(r => r.SopArticleId == articleId)
            .OrderByDescending(r => r.EditedAt)
            .ToListAsync();
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --filter SopArticleServiceTests`
Expected: `Passed! - Failed: 0, Passed: 3`.

- [ ] **Step 6: Commit**

```bash
git add CenterHub CenterHub.Tests
git commit -m "feat: add SopArticleService with optimistic concurrency and revision history"
```

---

### Task 12: SOP Razor Pages

**Files:**
- Create: `CenterHub/Components/Pages/SopWiki/SopList.razor`
- Create: `CenterHub/Components/Pages/SopWiki/SopArticleCreate.razor`
- Create: `CenterHub/Components/Pages/SopWiki/SopArticleView.razor`
- Create: `CenterHub/Components/Pages/SopWiki/SopArticleEdit.razor`
- Modify: `CenterHub/Program.cs`

**Interfaces:**
- Consumes: `SopArticleService` (Task 11).
- Produces: routed pages `/sop` (list + search), `/sop/new` (create), `/sop/{id}` (view), `/sop/{id}/edit` — satisfies the spec's §頁面結構 requirement of "分類瀏覽、全文搜尋、文章檢視/編輯、修改歷史".

- [ ] **Step 1: Register `SopArticleService` for DI**

```csharp
// CenterHub/Program.cs
builder.Services.AddScoped<CenterHub.Features.SopWiki.SopArticleService>();
```

- [ ] **Step 2: Write the list page**

```razor
@* CenterHub/Components/Pages/SopWiki/SopList.razor *@
@page "/sop"
@rendermode InteractiveServer
@using CenterHub.Data
@using Microsoft.EntityFrameworkCore
@inject AppDbContext Db
@attribute [Microsoft.AspNetCore.Authorization.Authorize]

<h3>SOP 知識庫</h3>

<a href="/sop/new">+ 新增文章</a>

<div>
    <label>搜尋: <input type="text" @bind="_searchTerm" @bind:event="oninput" placeholder="標題或內容關鍵字" /></label>
</div>

@if (_articles is null)
{
    <p>載入中...</p>
}
else
{
    <ul>
        @foreach (var group in FilteredArticles().GroupBy(a => a.Category.Name))
        {
            <li>
                <strong>@group.Key</strong>
                <ul>
                    @foreach (var article in group)
                    {
                        <li><a href="/sop/@article.Id">@article.Title</a></li>
                    }
                </ul>
            </li>
        }
    </ul>
}

@code {
    private List<CenterHub.Models.SopArticle>? _articles;
    private string _searchTerm = "";

    protected override async Task OnInitializedAsync()
    {
        _articles = await Db.SopArticles.Include(a => a.Category).OrderBy(a => a.Title).ToListAsync();
    }

    private IEnumerable<CenterHub.Models.SopArticle> FilteredArticles()
    {
        if (_articles is null) return Enumerable.Empty<CenterHub.Models.SopArticle>();
        if (string.IsNullOrWhiteSpace(_searchTerm)) return _articles;

        return _articles.Where(a =>
            a.Title.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
            a.Content.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase));
    }
}
```

- [ ] **Step 3: Write the create page**

```razor
@* CenterHub/Components/Pages/SopWiki/SopArticleCreate.razor *@
@page "/sop/new"
@rendermode InteractiveServer
@using CenterHub.Data
@using CenterHub.Features.SopWiki
@using CenterHub.Models
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.EntityFrameworkCore
@inject AppDbContext Db
@inject SopArticleService SopService
@inject NavigationManager Nav
@inject AuthenticationStateProvider AuthProvider
@attribute [Microsoft.AspNetCore.Authorization.Authorize]

<h3>新增 SOP 文章</h3>

<div><label>標題: <input type="text" @bind="_title" /></label></div>
<div>
    <label>分類:
        <select @bind="_categoryId">
            @foreach (var category in _categories)
            {
                <option value="@category.Id">@category.Name</option>
            }
        </select>
    </label>
</div>
<div><textarea rows="15" cols="80" @bind="_content" placeholder="內容"></textarea></div>
<button @onclick="Save">建立</button>

@code {
    private List<SopCategory> _categories = new();
    private string _title = "";
    private int _categoryId;
    private string _content = "";

    protected override async Task OnInitializedAsync()
    {
        _categories = await Db.SopCategories.OrderBy(c => c.Name).ToListAsync();
        if (_categories.Count > 0)
            _categoryId = _categories[0].Id;
    }

    private async Task Save()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("No authenticated user id found.");

        var article = await SopService.CreateAsync(_title, _categoryId, _content, userId);
        Nav.NavigateTo($"/sop/{article.Id}");
    }
}
```

- [ ] **Step 4: Write the view page**

```razor
@* CenterHub/Components/Pages/SopWiki/SopArticleView.razor *@
@page "/sop/{Id:int}"
@rendermode InteractiveServer
@using CenterHub.Data
@using CenterHub.Features.SopWiki
@using Microsoft.EntityFrameworkCore
@inject AppDbContext Db
@inject SopArticleService SopService
@attribute [Microsoft.AspNetCore.Authorization.Authorize]

@if (_article is null)
{
    <p>找不到這篇文章。</p>
}
else
{
    <h3>@_article.Title</h3>
    <a href="/sop/@Id/edit">編輯</a>
    <pre>@_article.Content</pre>

    <h4>修改歷史</h4>
    <ul>
        @foreach (var revision in _revisions)
        {
            <li>@revision.EditedAt.ToLocalTime() — @revision.EditedByUserId</li>
        }
    </ul>
}

@code {
    [Parameter] public int Id { get; set; }
    private CenterHub.Models.SopArticle? _article;
    private List<CenterHub.Models.SopArticleRevision> _revisions = new();

    protected override async Task OnInitializedAsync()
    {
        _article = await Db.SopArticles.FindAsync(Id);
        _revisions = await SopService.GetRevisionsAsync(Id);
    }
}
```

- [ ] **Step 5: Write the edit page, handling the conflict exception**

```razor
@* CenterHub/Components/Pages/SopWiki/SopArticleEdit.razor *@
@page "/sop/{Id:int}/edit"
@rendermode InteractiveServer
@using CenterHub.Data
@using CenterHub.Features.SopWiki
@using Microsoft.AspNetCore.Components.Authorization
@inject AppDbContext Db
@inject SopArticleService SopService
@inject NavigationManager Nav
@inject AuthenticationStateProvider AuthProvider
@attribute [Microsoft.AspNetCore.Authorization.Authorize]

@if (_article is null)
{
    <p>載入中...</p>
}
else
{
    <h3>編輯: @_article.Title</h3>

    @if (_conflictMessage is not null)
    {
        <p style="color:red">@_conflictMessage</p>
    }

    <textarea rows="15" cols="80" @bind="_content"></textarea>
    <br />
    <button @onclick="Save">儲存</button>
}

@code {
    [Parameter] public int Id { get; set; }
    private CenterHub.Models.SopArticle? _article;
    private string _content = string.Empty;
    private string? _conflictMessage;

    protected override async Task OnInitializedAsync()
    {
        _article = await Db.SopArticles.FindAsync(Id);
        _content = _article?.Content ?? string.Empty;
    }

    private async Task Save()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("No authenticated user id found.");

        try
        {
            await SopService.UpdateAsync(Id, _content, _article!.RowVersion, userId);
            Nav.NavigateTo($"/sop/{Id}");
        }
        catch (SopEditConflictException ex)
        {
            _conflictMessage = ex.Message;
            _article = await Db.SopArticles.FindAsync(Id); // reload latest version for retry
        }
    }
}
```

- [ ] **Step 6: Manual verification**

Run: `dotnet run --project CenterHub`. Create a category row directly (`sqlite3 CenterHub/centerhub.db "INSERT INTO SopCategories (Name) VALUES ('測試分類');"`), then:
1. Visit `/sop/new`, create an article in that category.
2. Visit `/sop`, confirm the article is listed under its category, and confirm typing a keyword from its title or content into the search box narrows the list to just that article (and clearing the search box shows it again).
3. Visit `/sop/{id}`, confirm content and (empty) revision history show.
4. Edit and save; revisit `/sop/{id}` and confirm the revision history now has one entry with the old content.

Expected: article creation, search filtering, and edit/revision history all work as described.

- [ ] **Step 7: Commit**

```bash
git add CenterHub
git commit -m "feat: add SOP wiki pages with search and concurrency-conflict handling"
```

---

### Task 13: Schedule Data Model

**Files:**
- Create: `CenterHub/Models/ShiftSlot.cs`
- Create: `CenterHub/Models/ShiftChangeRequest.cs`
- Modify: `CenterHub/Data/AppDbContext.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `ShiftPeriod`, `ShiftChangeType`, `ShiftChangeStatus` (Tasks 2-3).
- Produces: `ShiftSlot`, `ShiftChangeRequest` — `ShiftChangeRequestService` (Task 14) operates on these.

- [ ] **Step 1: Write the models**

```csharp
// CenterHub/Models/ShiftSlot.cs
namespace CenterHub.Models;

public class ShiftSlot
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public ShiftPeriod Period { get; set; }
    public string AssignedUserId { get; set; } = string.Empty;
}
```

```csharp
// CenterHub/Models/ShiftChangeRequest.cs
namespace CenterHub.Models;

public class ShiftChangeRequest
{
    public int Id { get; set; }
    public string RequestingUserId { get; set; } = string.Empty;
    public int ShiftSlotId { get; set; }
    public ShiftSlot ShiftSlot { get; set; } = null!;
    public ShiftChangeType Type { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ShiftChangeStatus Status { get; set; } = ShiftChangeStatus.Pending;
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Register the DbSets and enum conversions**

```csharp
// CenterHub/Data/AppDbContext.cs — add to DbSet list
public DbSet<ShiftSlot> ShiftSlots => Set<ShiftSlot>();
public DbSet<ShiftChangeRequest> ShiftChangeRequests => Set<ShiftChangeRequest>();
```

```csharp
// CenterHub/Data/AppDbContext.cs — add inside OnModelCreating, after the existing Ticket conversions
builder.Entity<ShiftSlot>()
    .Property(s => s.Period)
    .HasConversion<string>();

builder.Entity<ShiftChangeRequest>()
    .Property(r => r.Type)
    .HasConversion<string>();

builder.Entity<ShiftChangeRequest>()
    .Property(r => r.Status)
    .HasConversion<string>();

// EF Core's default for a required FK is cascade delete, which would silently wipe
// every ShiftChangeRequest (an audit trail of who requested/reviewed what, and when)
// if its ShiftSlot were ever deleted. Nothing in this plan deletes a ShiftSlot today,
// but Restrict makes that an explicit future decision instead of a silent data-loss
// default.
builder.Entity<ShiftChangeRequest>()
    .HasOne(r => r.ShiftSlot)
    .WithMany()
    .HasForeignKey(r => r.ShiftSlotId)
    .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 3: Generate and apply the migration**

```bash
cd CenterHub
dotnet ef migrations add AddSchedule
dotnet ef database update
cd ..
```

- [ ] **Step 4: Verify the tables exist**

```bash
sqlite3 CenterHub/centerhub.db ".tables"
```
Expected: output includes `ShiftSlots`, `ShiftChangeRequests`.

- [ ] **Step 5: Commit**

```bash
git add CenterHub
git commit -m "feat: add schedule and shift change request data model"
```

---

### Task 14: ShiftChangeRequestService — Approval State Machine

**Files:**
- Create: `CenterHub/Features/Schedule/ShiftChangeRequestService.cs`
- Test: `CenterHub.Tests/ShiftChangeRequestServiceTests.cs`

**Interfaces:**
- Consumes: `ShiftSlot`, `ShiftChangeRequest`, `ShiftChangeStatus`, `ShiftChangeType` (Task 13).
- Produces: `ShiftChangeRequestService.CreateRequestAsync(...)`, `.ApproveAsync(int requestId, string reviewerId)`, `.RejectAsync(int requestId, string reviewerId)` (both throw `InvalidOperationException` if the request is not `Pending`) — the Schedule pages (Task 15) and the notification wiring (Task 17) call these.

This task builds the service **without** email notifications first — Task 17 wires those in once `EmailNotificationQueue` exists (Task 16), keeping this task's tests focused on the state machine.

- [ ] **Step 1: Write the failing tests**

```csharp
// CenterHub.Tests/ShiftChangeRequestServiceTests.cs
using CenterHub.Data;
using CenterHub.Features.Schedule;
using CenterHub.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CenterHub.Tests;

public class ShiftChangeRequestServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ShiftSlot _slot;

    public ShiftChangeRequestServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _slot = new ShiftSlot { Date = new DateOnly(2026, 9, 10), Period = ShiftPeriod.AM, AssignedUserId = "user-1" };
        _db.ShiftSlots.Add(_slot);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateRequestAsync_CreatesPendingRequest()
    {
        var service = new ShiftChangeRequestService(_db);

        var request = await service.CreateRequestAsync("user-1", _slot.Id, ShiftChangeType.Swap, "有課衝堂");

        Assert.Equal(ShiftChangeStatus.Pending, request.Status);
    }

    [Fact]
    public async Task ApproveAsync_OnPendingRequest_SetsApprovedAndReviewer()
    {
        var service = new ShiftChangeRequestService(_db);
        var request = await service.CreateRequestAsync("user-1", _slot.Id, ShiftChangeType.Leave, "生病");

        await service.ApproveAsync(request.Id, "admin-1");

        var reloaded = await _db.ShiftChangeRequests.FindAsync(request.Id);
        Assert.Equal(ShiftChangeStatus.Approved, reloaded!.Status);
        Assert.Equal("admin-1", reloaded.ReviewedByUserId);
        Assert.NotNull(reloaded.ReviewedAt);
    }

    [Fact]
    public async Task RejectAsync_OnPendingRequest_SetsRejected()
    {
        var service = new ShiftChangeRequestService(_db);
        var request = await service.CreateRequestAsync("user-1", _slot.Id, ShiftChangeType.Swap, "換班");

        await service.RejectAsync(request.Id, "admin-1");

        var reloaded = await _db.ShiftChangeRequests.FindAsync(request.Id);
        Assert.Equal(ShiftChangeStatus.Rejected, reloaded!.Status);
    }

    [Fact]
    public async Task ApproveAsync_OnAlreadyApprovedRequest_ThrowsInvalidOperationException()
    {
        var service = new ShiftChangeRequestService(_db);
        var request = await service.CreateRequestAsync("user-1", _slot.Id, ShiftChangeType.Swap, "換班");
        await service.ApproveAsync(request.Id, "admin-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(request.Id, "admin-2"));
    }

    [Fact]
    public async Task RejectAsync_OnAlreadyRejectedRequest_ThrowsInvalidOperationException()
    {
        var service = new ShiftChangeRequestService(_db);
        var request = await service.CreateRequestAsync("user-1", _slot.Id, ShiftChangeType.Swap, "換班");
        await service.RejectAsync(request.Id, "admin-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAsync(request.Id, "admin-2"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter ShiftChangeRequestServiceTests`
Expected: FAIL to compile — `ShiftChangeRequestService` does not exist.

- [ ] **Step 3: Implement `ShiftChangeRequestService`**

```csharp
// CenterHub/Features/Schedule/ShiftChangeRequestService.cs
using CenterHub.Data;
using CenterHub.Models;
using Microsoft.EntityFrameworkCore;

namespace CenterHub.Features.Schedule;

public class ShiftChangeRequestService
{
    private readonly AppDbContext _db;

    public ShiftChangeRequestService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ShiftChangeRequest> CreateRequestAsync(string userId, int shiftSlotId, ShiftChangeType type, string reason)
    {
        var request = new ShiftChangeRequest
        {
            RequestingUserId = userId,
            ShiftSlotId = shiftSlotId,
            Type = type,
            Reason = reason,
            Status = ShiftChangeStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.ShiftChangeRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task ApproveAsync(int requestId, string reviewerId)
    {
        var request = await GetPendingOrThrowAsync(requestId);
        request.Status = ShiftChangeStatus.Approved;
        request.ReviewedByUserId = reviewerId;
        request.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task RejectAsync(int requestId, string reviewerId)
    {
        var request = await GetPendingOrThrowAsync(requestId);
        request.Status = ShiftChangeStatus.Rejected;
        request.ReviewedByUserId = reviewerId;
        request.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task<ShiftChangeRequest> GetPendingOrThrowAsync(int requestId)
    {
        var request = await _db.ShiftChangeRequests.FindAsync(requestId)
            ?? throw new InvalidOperationException($"Shift change request {requestId} not found.");

        if (request.Status != ShiftChangeStatus.Pending)
            throw new InvalidOperationException(
                $"Request {requestId} is already {request.Status}; only Pending requests can be reviewed.");

        return request;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter ShiftChangeRequestServiceTests`
Expected: `Passed! - Failed: 0, Passed: 5`.

- [ ] **Step 5: Commit**

```bash
git add CenterHub CenterHub.Tests
git commit -m "feat: add ShiftChangeRequestService approval state machine"
```

---

### Task 15: Schedule Razor Pages

**Files:**
- Create: `CenterHub/Components/Pages/Schedule/ScheduleView.razor`
- Create: `CenterHub/Components/Pages/Schedule/ShiftChangeRequests.razor`
- Modify: `CenterHub/Program.cs`

**Interfaces:**
- Consumes: `ShiftChangeRequestService` (Task 14), `AppDbContext` for reading `ShiftSlot` rows directly (no separate schedule-read service — YAGNI, it's a simple query the page can issue itself).
- Produces: routed pages `/schedule` (view + request) and `/schedule/requests` (admin review queue).

- [ ] **Step 1: Register `ShiftChangeRequestService` for DI**

```csharp
// CenterHub/Program.cs
builder.Services.AddScoped<CenterHub.Features.Schedule.ShiftChangeRequestService>();
```

- [ ] **Step 2: Write the schedule view + request page**

```razor
@* CenterHub/Components/Pages/Schedule/ScheduleView.razor *@
@page "/schedule"
@rendermode InteractiveServer
@using CenterHub.Data
@using CenterHub.Features.Schedule
@using CenterHub.Models
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.EntityFrameworkCore
@inject AppDbContext Db
@inject ShiftChangeRequestService RequestService
@inject AuthenticationStateProvider AuthProvider
@attribute [Microsoft.AspNetCore.Authorization.Authorize]

<h3>班表</h3>

@if (_slots is null)
{
    <p>載入中...</p>
}
else
{
    <table>
        <thead><tr><th>日期</th><th>班別</th><th>值班人</th><th></th></tr></thead>
        <tbody>
            @foreach (var slot in _slots)
            {
                <tr>
                    <td>@slot.Date</td>
                    <td>@slot.Period</td>
                    <td>@slot.AssignedUserId</td>
                    <td><button @onclick="() => StartRequest(slot.Id)">申請換班/請假</button></td>
                </tr>
            }
        </tbody>
    </table>
}

@if (_requestingSlotId is not null)
{
    <h4>申請換班/請假</h4>
    <label>類型:
        <select @bind="_requestType">
            <option value="Swap">換班</option>
            <option value="Leave">請假</option>
        </select>
    </label>
    <br />
    <textarea @bind="_reason" placeholder="原因"></textarea>
    <br />
    <button @onclick="Submit">送出申請</button>
}

@code {
    private List<ShiftSlot>? _slots;
    private int? _requestingSlotId;
    private string _requestType = "Swap";
    private string _reason = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        _slots = await Db.ShiftSlots
            .Where(s => s.Date >= today && s.Date <= today.AddDays(14))
            .OrderBy(s => s.Date).ThenBy(s => s.Period)
            .ToListAsync();
    }

    private void StartRequest(int slotId) => _requestingSlotId = slotId;

    private async Task Submit()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("No authenticated user id found.");

        var type = _requestType == "Swap" ? ShiftChangeType.Swap : ShiftChangeType.Leave;
        await RequestService.CreateRequestAsync(userId, _requestingSlotId!.Value, type, _reason);

        _requestingSlotId = null;
        _reason = string.Empty;
    }
}
```

- [ ] **Step 3: Write the admin review queue page**

```razor
@* CenterHub/Components/Pages/Schedule/ShiftChangeRequests.razor *@
@page "/schedule/requests"
@rendermode InteractiveServer
@using CenterHub.Data
@using CenterHub.Features.Schedule
@using CenterHub.Models
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.EntityFrameworkCore
@inject AppDbContext Db
@inject ShiftChangeRequestService RequestService
@inject AuthenticationStateProvider AuthProvider
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = CenterHub.Models.Roles.Admin)]

<h3>換班/請假審核</h3>

@if (_pending is null)
{
    <p>載入中...</p>
}
else if (_pending.Count == 0)
{
    <p>目前沒有待審核的申請。</p>
}
else
{
    <table>
        <thead><tr><th>申請人</th><th>班次</th><th>類型</th><th>原因</th><th></th></tr></thead>
        <tbody>
            @foreach (var request in _pending)
            {
                <tr>
                    <td>@request.RequestingUserId</td>
                    <td>@request.ShiftSlot.Date @request.ShiftSlot.Period</td>
                    <td>@request.Type</td>
                    <td>@request.Reason</td>
                    <td>
                        <button @onclick="() => Approve(request.Id)">核准</button>
                        <button @onclick="() => Reject(request.Id)">拒絕</button>
                    </td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private List<ShiftChangeRequest>? _pending;

    protected override async Task OnInitializedAsync() => await Load();

    private async Task Load()
    {
        _pending = await Db.ShiftChangeRequests
            .Include(r => r.ShiftSlot)
            .Where(r => r.Status == ShiftChangeStatus.Pending)
            .ToListAsync();
    }

    private async Task Approve(int id)
    {
        var reviewerId = await GetCurrentUserIdAsync();
        await RequestService.ApproveAsync(id, reviewerId);
        await Load();
    }

    private async Task Reject(int id)
    {
        var reviewerId = await GetCurrentUserIdAsync();
        await RequestService.RejectAsync(id, reviewerId);
        await Load();
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        return authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("No authenticated user id found.");
    }
}
```

- [ ] **Step 4: Manual verification**

Seed one `ShiftSlot` row for testing:
```bash
sqlite3 CenterHub/centerhub.db "INSERT INTO ShiftSlots (Date, Period, AssignedUserId) VALUES ('2026-09-10', 'AM', 'user-1');"
```
Run the app, visit `/schedule` as a work-study user, submit a swap request; then visit `/schedule/requests` as an admin, approve it, and confirm it disappears from the pending list.

Expected: request moves from pending to approved and no longer shows in the queue.

- [ ] **Step 5: Commit**

```bash
git add CenterHub
git commit -m "feat: add schedule view and shift change request pages"
```

---

### Task 16: Email Notification Queue

**Files:**
- Create: `CenterHub/Features/Notifications/EmailMessage.cs`
- Create: `CenterHub/Features/Notifications/IEmailSender.cs`
- Create: `CenterHub/Features/Notifications/SmtpEmailSender.cs`
- Create: `CenterHub/Features/Notifications/EmailNotificationQueue.cs`
- Modify: `CenterHub/Program.cs`
- Modify: `CenterHub/appsettings.json`
- Test: `CenterHub.Tests/EmailNotificationQueueTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `IEmailSender.SendAsync(string to, string subject, string body, CancellationToken ct)`, `EmailNotificationQueue.Enqueue(EmailMessage message)` — Task 17 calls `Enqueue` from `ShiftChangeRequestService`. Per spec §錯誤處理: a failed send retries and logs, but never throws back into the caller of `Enqueue`.

- [ ] **Step 1: Write `EmailMessage` and `IEmailSender`**

```csharp
// CenterHub/Features/Notifications/EmailMessage.cs
namespace CenterHub.Features.Notifications;

public record EmailMessage(string To, string Subject, string Body);
```

```csharp
// CenterHub/Features/Notifications/IEmailSender.cs
namespace CenterHub.Features.Notifications;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write the failing tests for retry behavior**

```csharp
// CenterHub.Tests/EmailNotificationQueueTests.cs
using CenterHub.Features.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CenterHub.Tests;

public class FakeEmailSender : IEmailSender
{
    public int AttemptCount;
    public int FailUntilAttempt;
    public List<EmailMessage> Sent = new();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        AttemptCount++;
        if (AttemptCount <= FailUntilAttempt)
            throw new InvalidOperationException("simulated SMTP failure");

        Sent.Add(new EmailMessage(to, subject, body));
        return Task.CompletedTask;
    }
}

public class EmailNotificationQueueTests
{
    [Fact]
    public async Task ProcessMessageAsync_SucceedsOnFirstTry_SendsOnce()
    {
        var sender = new FakeEmailSender();
        var queue = new EmailNotificationQueue(sender, NullLogger<EmailNotificationQueue>.Instance,
            retryDelay: TimeSpan.Zero, maxAttempts: 3);

        await queue.ProcessMessageAsync(new EmailMessage("a@b.com", "subject", "body"), CancellationToken.None);

        Assert.Single(sender.Sent);
        Assert.Equal(1, sender.AttemptCount);
    }

    [Fact]
    public async Task ProcessMessageAsync_FailsTwiceThenSucceeds_RetriesAndSends()
    {
        var sender = new FakeEmailSender { FailUntilAttempt = 2 };
        var queue = new EmailNotificationQueue(sender, NullLogger<EmailNotificationQueue>.Instance,
            retryDelay: TimeSpan.Zero, maxAttempts: 3);

        await queue.ProcessMessageAsync(new EmailMessage("a@b.com", "subject", "body"), CancellationToken.None);

        Assert.Single(sender.Sent);
        Assert.Equal(3, sender.AttemptCount);
    }

    [Fact]
    public async Task ProcessMessageAsync_FailsMoreThanMaxAttempts_GivesUpWithoutThrowing()
    {
        var sender = new FakeEmailSender { FailUntilAttempt = 10 };
        var queue = new EmailNotificationQueue(sender, NullLogger<EmailNotificationQueue>.Instance,
            retryDelay: TimeSpan.Zero, maxAttempts: 3);

        await queue.ProcessMessageAsync(new EmailMessage("a@b.com", "subject", "body"), CancellationToken.None);

        Assert.Empty(sender.Sent);
        Assert.Equal(3, sender.AttemptCount);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter EmailNotificationQueueTests`
Expected: FAIL to compile — `EmailNotificationQueue` does not exist.

- [ ] **Step 4: Implement `EmailNotificationQueue`**

```csharp
// CenterHub/Features/Notifications/EmailNotificationQueue.cs
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CenterHub.Features.Notifications;

public class EmailNotificationQueue : BackgroundService
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>();
    private readonly IEmailSender _sender;
    private readonly ILogger<EmailNotificationQueue> _logger;
    private readonly TimeSpan _retryDelay;
    private readonly int _maxAttempts;

    public EmailNotificationQueue(
        IEmailSender sender,
        ILogger<EmailNotificationQueue> logger,
        TimeSpan? retryDelay = null,
        int maxAttempts = 3)
    {
        _sender = sender;
        _logger = logger;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(5);
        _maxAttempts = maxAttempts;
    }

    public void Enqueue(EmailMessage message)
    {
        _channel.Writer.TryWrite(message);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessMessageAsync(message, stoppingToken);
        }
    }

    // Internal for testability — exercised directly by EmailNotificationQueueTests
    // without needing to drive the BackgroundService's channel loop.
    internal async Task ProcessMessageAsync(EmailMessage message, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                await _sender.SendAsync(message.To, message.Subject, message.Body, ct);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email send attempt {Attempt}/{MaxAttempts} to {To} failed",
                    attempt, _maxAttempts, message.To);

                if (attempt == _maxAttempts)
                {
                    _logger.LogError("Giving up sending email to {To} after {MaxAttempts} attempts",
                        message.To, _maxAttempts);
                    return;
                }

                await Task.Delay(_retryDelay, ct);
            }
        }
    }

    // Internal for testability — lets tests drain a message from the channel
    // without waiting on the background ExecuteAsync loop's timing.
    internal bool TryReadForTest(out EmailMessage? message) => _channel.Reader.TryRead(out message);
}
```

- [ ] **Step 5: Write `SmtpEmailSender` and wire configuration**

```json
// CenterHub/appsettings.json — add alongside ConnectionStrings/Logging
"Smtp": {
  "Host": "smtp.example.edu",
  "Port": 587,
  "FromAddress": "centerhub@example.edu"
}
```

```csharp
// CenterHub/Features/Notifications/SmtpEmailSender.cs
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace CenterHub.Features.Notifications;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;

    public SmtpEmailSender(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        using var client = new SmtpClient(_config["Smtp:Host"], int.Parse(_config["Smtp:Port"] ?? "587"));
        using var message = new MailMessage(_config["Smtp:FromAddress"]!, to, subject, body);
        await client.SendMailAsync(message, ct);
    }
}
```

- [ ] **Step 6: Register everything in `Program.cs`**

```csharp
// CenterHub/Program.cs
builder.Services.AddSingleton<CenterHub.Features.Notifications.IEmailSender,
    CenterHub.Features.Notifications.SmtpEmailSender>();
builder.Services.AddSingleton<CenterHub.Features.Notifications.EmailNotificationQueue>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<CenterHub.Features.Notifications.EmailNotificationQueue>());
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test --filter EmailNotificationQueueTests`
Expected: `Passed! - Failed: 0, Passed: 3`.

- [ ] **Step 8: Verify the full build still succeeds**

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 9: Commit**

```bash
git add CenterHub CenterHub.Tests
git commit -m "feat: add email notification queue with retry"
```

---

### Task 17: Wire Notifications into Shift Change Requests

**Files:**
- Modify: `CenterHub/Features/Schedule/ShiftChangeRequestService.cs`
- Modify: `CenterHub/Components/Pages/Schedule/ScheduleView.razor`
- Modify: `CenterHub/Components/Pages/Schedule/ShiftChangeRequests.razor`
- Modify: `CenterHub.Tests/ShiftChangeRequestServiceTests.cs`

**Interfaces:**
- Consumes: `EmailNotificationQueue.Enqueue(EmailMessage)`, `EmailNotificationQueue.TryReadForTest(out EmailMessage?)` (Task 16).
- Produces: `ShiftChangeRequestService` now takes an `EmailNotificationQueue` constructor dependency, and its three public methods take an extra parameter carrying already-resolved email address(es): `CreateRequestAsync(..., IReadOnlyList<string> notifyEmails)`, `ApproveAsync(int requestId, string reviewerId, string requesterEmail)`, `RejectAsync(int requestId, string reviewerId, string requesterEmail)`.

  The service deliberately does **not** resolve user IDs to email addresses itself (that would require injecting `UserManager<ApplicationUser>` into a class whose tests should stay plain-string, no-Identity-plumbing unit tests). Resolving IDs to emails is the caller's job — the Schedule Razor pages already sit next to `UserManager` and do it before calling the service. This also fixes a bug an earlier draft of this task had: sending email `To: request.RequestingUserId` directly, which is a user ID, not an email address.

  Per Global Constraints, a queue failure must not block the state change — `Enqueue` itself never throws (it just writes to an unbounded channel), so no try/catch is needed here.

- [ ] **Step 1: Update the test file's constructor calls, fake sender, and method call sites**

```csharp
// CenterHub.Tests/ShiftChangeRequestServiceTests.cs — add this field to the class,
// change every `new ShiftChangeRequestService(_db)` call to `new ShiftChangeRequestService(_db, _emailQueue)`,
// update every CreateRequestAsync/ApproveAsync/RejectAsync call to pass the new email parameters,
// and add the NoOpEmailSender class outside the test class.

// Add as a field of ShiftChangeRequestServiceTests:
private readonly CenterHub.Features.Notifications.EmailNotificationQueue _emailQueue = new(
    new NoOpEmailSender(),
    Microsoft.Extensions.Logging.Abstractions.NullLogger<CenterHub.Features.Notifications.EmailNotificationQueue>.Instance,
    retryDelay: TimeSpan.Zero);

// Add outside the ShiftChangeRequestServiceTests class, in the same file:
public class NoOpEmailSender : CenterHub.Features.Notifications.IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default) => Task.CompletedTask;
}
```

Apply these changes to all five existing test methods in the file:
- `new ShiftChangeRequestService(_db)` → `new ShiftChangeRequestService(_db, _emailQueue)`
- `service.CreateRequestAsync("user-1", _slot.Id, ShiftChangeType.Swap, "有課衝堂")` → `service.CreateRequestAsync("user-1", _slot.Id, ShiftChangeType.Swap, "有課衝堂", new[] { "admin@example.edu" })` (same pattern for the other reason strings used in the other tests)
- `service.ApproveAsync(request.Id, "admin-1")` → `service.ApproveAsync(request.Id, "admin-1", "user-1@example.edu")`
- `service.RejectAsync(request.Id, "admin-1")` → `service.RejectAsync(request.Id, "admin-1", "user-1@example.edu")`
- the "already approved/rejected" negative tests' second call (e.g. `service.ApproveAsync(request.Id, "admin-2")`) also needs the new trailing argument (e.g. `service.ApproveAsync(request.Id, "admin-2", "user-1@example.edu")`) — the exact email value doesn't matter there since the call is expected to throw before it would be used.

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test --filter ShiftChangeRequestServiceTests`
Expected: FAIL to compile — `ShiftChangeRequestService` has no two-argument constructor, and its methods don't accept the new parameters yet.

- [ ] **Step 3: Update `ShiftChangeRequestService` to enqueue notifications using caller-supplied addresses**

```csharp
// CenterHub/Features/Schedule/ShiftChangeRequestService.cs — full replacement
using CenterHub.Data;
using CenterHub.Features.Notifications;
using CenterHub.Models;
using Microsoft.EntityFrameworkCore;

namespace CenterHub.Features.Schedule;

public class ShiftChangeRequestService
{
    private readonly AppDbContext _db;
    private readonly EmailNotificationQueue _emailQueue;

    public ShiftChangeRequestService(AppDbContext db, EmailNotificationQueue emailQueue)
    {
        _db = db;
        _emailQueue = emailQueue;
    }

    public async Task<ShiftChangeRequest> CreateRequestAsync(
        string userId, int shiftSlotId, ShiftChangeType type, string reason, IReadOnlyList<string> notifyEmails)
    {
        var request = new ShiftChangeRequest
        {
            RequestingUserId = userId,
            ShiftSlotId = shiftSlotId,
            Type = type,
            Reason = reason,
            Status = ShiftChangeStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.ShiftChangeRequests.Add(request);
        await _db.SaveChangesAsync();

        foreach (var email in notifyEmails)
        {
            _emailQueue.Enqueue(new EmailMessage(
                To: email,
                Subject: "有新的換班/請假申請待審核",
                Body: $"申請人 {userId} 針對班次 {shiftSlotId} 提出 {type} 申請，原因：{reason}"));
        }

        return request;
    }

    public async Task ApproveAsync(int requestId, string reviewerId, string requesterEmail)
    {
        var request = await GetPendingOrThrowAsync(requestId);
        request.Status = ShiftChangeStatus.Approved;
        request.ReviewedByUserId = reviewerId;
        request.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _emailQueue.Enqueue(new EmailMessage(
            To: requesterEmail,
            Subject: "換班/請假申請已核准",
            Body: $"你的申請（班次 {request.ShiftSlotId}）已被核准。"));
    }

    public async Task RejectAsync(int requestId, string reviewerId, string requesterEmail)
    {
        var request = await GetPendingOrThrowAsync(requestId);
        request.Status = ShiftChangeStatus.Rejected;
        request.ReviewedByUserId = reviewerId;
        request.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _emailQueue.Enqueue(new EmailMessage(
            To: requesterEmail,
            Subject: "換班/請假申請已被拒絕",
            Body: $"你的申請（班次 {request.ShiftSlotId}）已被拒絕。"));
    }

    private async Task<ShiftChangeRequest> GetPendingOrThrowAsync(int requestId)
    {
        var request = await _db.ShiftChangeRequests.FindAsync(requestId)
            ?? throw new InvalidOperationException($"Shift change request {requestId} not found.");

        if (request.Status != ShiftChangeStatus.Pending)
            throw new InvalidOperationException(
                $"Request {requestId} is already {request.Status}; only Pending requests can be reviewed.");

        return request;
    }
}
```

- [ ] **Step 4: Add a deterministic test that the approval email is enqueued to the right address**

```csharp
// Append inside ShiftChangeRequestServiceTests

[Fact]
public async Task ApproveAsync_EnqueuesApprovalEmailToRequesterEmail()
{
    var service = new ShiftChangeRequestService(_db, _emailQueue);
    var request = await service.CreateRequestAsync(
        "user-1", _slot.Id, ShiftChangeType.Swap, "換班", new[] { "admin@example.edu" });
    _emailQueue.TryReadForTest(out _); // drain the "new request" notification from CreateRequestAsync

    await service.ApproveAsync(request.Id, "admin-1", "user1@example.edu");

    var enqueued = _emailQueue.TryReadForTest(out var message);
    Assert.True(enqueued);
    Assert.Equal("user1@example.edu", message!.To);
    Assert.Contains("核准", message.Subject);
}
```

- [ ] **Step 5: Run all Schedule and Notification tests to verify they pass**

Run: `dotnet test --filter "ShiftChangeRequestServiceTests|EmailNotificationQueueTests"`
Expected: `Passed! - Failed: 0`.

- [ ] **Step 6: Update the Schedule pages to resolve real email addresses before calling the service**

```razor
@* CenterHub/Components/Pages/Schedule/ScheduleView.razor — add this @inject line near the top *@
@inject Microsoft.AspNetCore.Identity.UserManager<CenterHub.Models.ApplicationUser> UserManager
```

```csharp
// CenterHub/Components/Pages/Schedule/ScheduleView.razor — replace the Submit method
private async Task Submit()
{
    var authState = await AuthProvider.GetAuthenticationStateAsync();
    var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? throw new InvalidOperationException("No authenticated user id found.");

    var admins = await UserManager.GetUsersInRoleAsync(CenterHub.Models.Roles.Admin);
    var adminEmails = admins
        .Select(a => a.Email)
        .Where(e => !string.IsNullOrEmpty(e))
        .Select(e => e!)
        .ToList();

    var type = _requestType == "Swap" ? ShiftChangeType.Swap : ShiftChangeType.Leave;
    await RequestService.CreateRequestAsync(userId, _requestingSlotId!.Value, type, _reason, adminEmails);

    _requestingSlotId = null;
    _reason = string.Empty;
}
```

```razor
@* CenterHub/Components/Pages/Schedule/ShiftChangeRequests.razor — add this @inject line near the top *@
@inject Microsoft.AspNetCore.Identity.UserManager<CenterHub.Models.ApplicationUser> UserManager
```

```csharp
// CenterHub/Components/Pages/Schedule/ShiftChangeRequests.razor — replace Approve and Reject
private async Task Approve(int id)
{
    var reviewerId = await GetCurrentUserIdAsync();
    var requesterEmail = await GetRequesterEmailAsync(id);
    await RequestService.ApproveAsync(id, reviewerId, requesterEmail);
    await Load();
}

private async Task Reject(int id)
{
    var reviewerId = await GetCurrentUserIdAsync();
    var requesterEmail = await GetRequesterEmailAsync(id);
    await RequestService.RejectAsync(id, reviewerId, requesterEmail);
    await Load();
}

private async Task<string> GetRequesterEmailAsync(int requestId)
{
    var request = _pending!.Single(r => r.Id == requestId);
    var requester = await UserManager.FindByIdAsync(request.RequestingUserId);
    return requester?.Email ?? throw new InvalidOperationException(
        $"User {request.RequestingUserId} has no email on file; cannot notify them.");
}
```

- [ ] **Step 7: Verify the whole solution still builds**

Confirm `CenterHub/Program.cs` already registers `EmailNotificationQueue` as a singleton (Task 16, Step 6) — the DI container supplies it automatically to `ShiftChangeRequestService`'s constructor parameter, since `ShiftChangeRequestService` is registered with `AddScoped` (Task 15, Step 1) and `EmailNotificationQueue` with `AddSingleton`, a valid scope combination (a scoped service may depend on a singleton). `UserManager<ApplicationUser>` is already registered by `AddIdentity` (Task 2), so the two Razor pages' new `@inject` lines resolve without further DI setup.

Run: `dotnet build`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 8: Manual verification**

This depends on Task 19's login/accounts being in place, so if doing tasks in order, come back to this step after Task 19. Create a work-study account and an admin account via `/account/manage-users` (Task 19), **making sure to fill in a real email address for each** (Step required by Task 19's updated form — see that task). Submit a shift-change request as the work-study user, approve it as the admin, and confirm (via server console log, since no real SMTP server is configured yet) that `SmtpEmailSender.SendAsync` was attempted with the work-study user's actual email in the `to` field — not their user ID.

- [ ] **Step 9: Commit**

```bash
git add CenterHub CenterHub.Tests
git commit -m "feat: send email notifications on shift change request events"
```

---

### Task 18: Password Generator Tool

**Files:**
- Create: `CenterHub/Features/Tools/PasswordGeneratorService.cs`
- Create: `CenterHub/Components/Pages/Tools/PasswordGenerator.razor`
- Modify: `CenterHub/Program.cs`
- Test: `CenterHub.Tests/PasswordGeneratorServiceTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `PasswordGeneratorService.Generate(int length = 12) : string` — standalone tool page calls this directly, no DB involved.

- [ ] **Step 1: Write the failing tests**

```csharp
// CenterHub.Tests/PasswordGeneratorServiceTests.cs
using CenterHub.Features.Tools;
using Xunit;

namespace CenterHub.Tests;

public class PasswordGeneratorServiceTests
{
    [Fact]
    public void Generate_DefaultLength_Returns12Characters()
    {
        var service = new PasswordGeneratorService();

        var password = service.Generate();

        Assert.Equal(12, password.Length);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    public void Generate_CustomLength_ReturnsRequestedLength(int length)
    {
        var service = new PasswordGeneratorService();

        var password = service.Generate(length);

        Assert.Equal(length, password.Length);
    }

    [Fact]
    public void Generate_ContainsOnlyUnambiguousAllowedCharacters()
    {
        var service = new PasswordGeneratorService();
        const string allowed = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%^&*";

        var password = service.Generate(50);

        Assert.All(password, c => Assert.Contains(c, allowed));
    }

    [Fact]
    public void Generate_CalledTwice_ProducesDifferentValues()
    {
        var service = new PasswordGeneratorService();

        var first = service.Generate();
        var second = service.Generate();

        Assert.NotEqual(first, second);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter PasswordGeneratorServiceTests`
Expected: FAIL to compile — `PasswordGeneratorService` does not exist.

- [ ] **Step 3: Implement `PasswordGeneratorService`**

```csharp
// CenterHub/Features/Tools/PasswordGeneratorService.cs
using System.Security.Cryptography;

namespace CenterHub.Features.Tools;

public class PasswordGeneratorService
{
    // Excludes visually ambiguous characters (0/O, 1/l/I) since generated
    // passwords are read aloud or retyped by hand at the help desk.
    private const string AllowedCharacters = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%^&*";

    public string Generate(int length = 12)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            var index = RandomNumberGenerator.GetInt32(AllowedCharacters.Length);
            chars[i] = AllowedCharacters[index];
        }
        return new string(chars);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter PasswordGeneratorServiceTests`
Expected: `Passed! - Failed: 0, Passed: 5`.

- [ ] **Step 5: Register the service and write the tool page**

```csharp
// CenterHub/Program.cs
builder.Services.AddScoped<CenterHub.Features.Tools.PasswordGeneratorService>();
```

```razor
@* CenterHub/Components/Pages/Tools/PasswordGenerator.razor *@
@page "/tools/password-generator"
@rendermode InteractiveServer
@using CenterHub.Features.Tools
@inject PasswordGeneratorService Generator
@attribute [Microsoft.AspNetCore.Authorization.Authorize]

<h3>密碼產生器</h3>

<button @onclick="GenerateNew">產生新密碼</button>

@if (_password is not null)
{
    <p><code>@_password</code></p>
}

@code {
    private string? _password;

    private void GenerateNew()
    {
        _password = Generator.Generate();
    }
}
```

- [ ] **Step 6: Manual verification**

Run the app, visit `/tools/password-generator`, click the button several times, confirm each click produces a different 12-character string with no ambiguous characters.

- [ ] **Step 7: Commit**

```bash
git add CenterHub CenterHub.Tests
git commit -m "feat: add password generator tool"
```

---

### Task 19: Role-Based Navigation, Login, and Admin User Management

**Files:**
- Create: `CenterHub/Components/Pages/Account/Login.razor`
- Create: `CenterHub/Components/Pages/Account/Logout.razor`
- Create: `CenterHub/Components/Pages/Account/ManageUsers.razor`
- Modify: `CenterHub/Components/Layout/NavMenu.razor`
- Modify: `CenterHub/Program.cs`

**Interfaces:**
- Consumes: ASP.NET Core Identity's `SignInManager<ApplicationUser>` / `UserManager<ApplicationUser>` (Task 2).
- Produces: working login/logout, a role-filtered nav menu, and an admin-only page to create work-study accounts — this is the last piece needed before the spec's role-gated pages (already written with `[Authorize]`/`[Authorize(Roles = ...)]` attributes in Tasks 6, 9, 12, 15, 18) are reachable end-to-end.

- [ ] **Step 1: Confirm cookie authentication is already wired**

`AddIdentity` (Task 2) already registers cookie authentication under the hood. No changes needed here beyond what Task 2 set up; this step is a checkpoint, not new code.

- [ ] **Step 2: Write the login page**

```razor
@* CenterHub/Components/Pages/Account/Login.razor *@
@page "/account/login"
@rendermode InteractiveServer
@using CenterHub.Models
@using Microsoft.AspNetCore.Identity
@inject SignInManager<ApplicationUser> SignInManager
@inject NavigationManager Nav

<h3>登入</h3>

<EditForm Model="_model" OnValidSubmit="DoLogin">
    <DataAnnotationsValidator />
    <div><label>帳號: <InputText @bind-Value="_model.UserName" /></label></div>
    <div><label>密碼: <InputText type="password" @bind-Value="_model.Password" /></label></div>
    <button type="submit">登入</button>
</EditForm>

@if (_error is not null)
{
    <p style="color:red">@_error</p>
}

@code {
    private readonly LoginModel _model = new();
    private string? _error;

    public class LoginModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string UserName { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required]
        public string Password { get; set; } = string.Empty;
    }

    private async Task DoLogin()
    {
        var result = await SignInManager.PasswordSignInAsync(_model.UserName, _model.Password, isPersistent: true, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            Nav.NavigateTo("/tickets", forceLoad: true);
        }
        else
        {
            _error = "帳號或密碼錯誤。";
        }
    }
}
```

- [ ] **Step 3: Write the logout page**

```razor
@* CenterHub/Components/Pages/Account/Logout.razor *@
@page "/account/logout"
@rendermode InteractiveServer
@using CenterHub.Models
@using Microsoft.AspNetCore.Identity
@inject SignInManager<ApplicationUser> SignInManager
@inject NavigationManager Nav

@code {
    protected override async Task OnInitializedAsync()
    {
        await SignInManager.SignOutAsync();
        Nav.NavigateTo("/account/login", forceLoad: true);
    }
}
```

- [ ] **Step 4: Write the admin user management page**

```razor
@* CenterHub/Components/Pages/Account/ManageUsers.razor *@
@page "/account/manage-users"
@rendermode InteractiveServer
@using CenterHub.Models
@using Microsoft.AspNetCore.Identity
@inject UserManager<ApplicationUser> UserManager
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.Admin)]

<h3>帳號管理</h3>

<EditForm Model="_model" OnValidSubmit="CreateUser">
    <DataAnnotationsValidator />
    <ValidationSummary />
    <div><label>顯示名稱: <InputText @bind-Value="_model.DisplayName" /></label></div>
    <div><label>帳號: <InputText @bind-Value="_model.UserName" /></label></div>
    <div><label>Email(用於接收換班/請假通知): <InputText @bind-Value="_model.Email" /></label></div>
    <div><label>初始密碼: <InputText type="password" @bind-Value="_model.Password" /></label></div>
    <div>
        <label>角色:
            <InputSelect @bind-Value="_model.Role">
                <option value="@Roles.WorkStudy">工讀生</option>
                <option value="@Roles.Admin">管理員</option>
            </InputSelect>
        </label>
    </div>
    <button type="submit">建立帳號</button>
</EditForm>

@if (_message is not null)
{
    <p>@_message</p>
}

@code {
    private readonly NewUserModel _model = new();
    private string? _message;

    public class NewUserModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "請輸入顯示名稱")]
        public string DisplayName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "請輸入帳號")]
        public string UserName { get; set; } = string.Empty;

        // Required, not just [EmailAddress]-validated-if-present: every account needs a
        // real email so Task 17's shift-change notifications have somewhere to send to.
        // Identity's own UserManager.CreateAsync does not enforce Email presence by
        // default (only RequireUniqueEmail, which doesn't check for non-empty), so this
        // page must be the one place that guarantees it.
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "請輸入 Email，換班/請假通知需要寄送到這個信箱")]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "請輸入有效的 Email 格式")]
        public string Email { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "請輸入初始密碼")]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = Roles.WorkStudy;
    }

    private async Task CreateUser()
    {
        var user = new ApplicationUser { UserName = _model.UserName, DisplayName = _model.DisplayName, Email = _model.Email };
        var result = await UserManager.CreateAsync(user, _model.Password);
        if (result.Succeeded)
        {
            await UserManager.AddToRoleAsync(user, _model.Role);
            _message = $"已建立帳號 {_model.UserName}。";
        }
        else
        {
            _message = string.Join("; ", result.Errors.Select(e => e.Description));
        }
    }
}
```

- [ ] **Step 5: Update `NavMenu.razor` to be role-aware**

```razor
@* CenterHub/Components/Layout/NavMenu.razor — replace the nav contents *@
@using Microsoft.AspNetCore.Components.Authorization
@using CenterHub.Models

<AuthorizeView>
    <Authorized>
        <nav>
            <a href="/tickets">工單</a>
            <a href="/shift-summary">班別彙總</a>
            <a href="/sop">SOP 知識庫</a>
            <a href="/schedule">班表</a>
            <a href="/tools/password-generator">密碼產生器</a>
            <AuthorizeView Roles="@Roles.Admin" Context="adminContext">
                <a href="/schedule/requests">換班審核</a>
                <a href="/account/manage-users">帳號管理</a>
            </AuthorizeView>
            <a href="/account/logout">登出 (@context.User.Identity!.Name)</a>
        </nav>
    </Authorized>
    <NotAuthorized>
        <a href="/account/login">登入</a>
    </NotAuthorized>
</AuthorizeView>
```

- [ ] **Step 6: Ensure `App.razor` supports authentication state on first load**

Confirm `builder.Services.AddCascadingAuthenticationState();` from Task 2 is present in `Program.cs`, and that `CenterHub/Components/App.razor` wraps its router in a `<CascadingAuthenticationState>` — the `dotnet new blazor` template already does this; if not, wrap the existing `<Router>` markup:

```razor
<CascadingAuthenticationState>
    <Router AppAssembly="typeof(Program).Assembly">
        ...
    </Router>
</CascadingAuthenticationState>
```

- [ ] **Step 7: Seed one bootstrap admin account so the system is reachable on first run**

```csharp
// CenterHub/Program.cs — inside the existing `using (var scope = ...)` startup block, after role seeding
var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
if (await userManager.FindByNameAsync("admin") is null)
{
    var admin = new ApplicationUser
    {
        UserName = "admin",
        DisplayName = "系統管理員",
        Email = "admin@example.edu" // replace with a real school mailbox once Task 17's email notifications go live
    };
    var result = await userManager.CreateAsync(admin, "ChangeMe123!");
    if (result.Succeeded)
    {
        await userManager.AddToRoleAsync(admin, Roles.Admin);
    }
}
```

- [ ] **Step 8: Manual verification**

Run: `dotnet run --project CenterHub`.
1. Visit any page (e.g. `/tickets`) while logged out — confirm you're redirected to `/account/login`.
2. Log in as `admin` / `ChangeMe123!`.
3. Confirm the nav menu shows `換班審核` and `帳號管理` (admin-only links).
4. Use `/account/manage-users` to create a work-study account, log out, log back in as that account, and confirm `換班審核`/`帳號管理` are **not** in the nav menu.

Expected: role-based visibility matches the spec's §頁面結構與操作流程 table exactly.

- [ ] **Step 9: Commit**

```bash
git add CenterHub
git commit -m "feat: add login/logout, role-based nav, and admin user management"
```

---

### Task 20: Global Error Handling

**Files:**
- Create: `CenterHub/Components/Pages/Error.razor` (only if the template didn't already generate one — check first)
- Modify: `CenterHub/Components/App.razor`

**Interfaces:**
- Consumes: nothing.
- Produces: a global `ErrorBoundary` around the router so an unhandled exception in any page shows a friendly message instead of crashing the whole circuit, per spec §錯誤處理.

- [ ] **Step 1: Check whether `dotnet new blazor` already generated `Components/Pages/Error.razor`**

Run: `ls CenterHub/Components/Pages/Error.razor`
If it exists, skip Step 2 — the template's default error page is sufficient for this internal tool.

- [ ] **Step 2 (only if missing): Write a minimal error page**

```razor
@* CenterHub/Components/Pages/Error.razor *@
@page "/Error"

<h3>發生錯誤</h3>
<p>系統發生未預期的錯誤，請稍後再試或聯絡管理員。</p>
```

- [ ] **Step 3: Wrap the router body in an `ErrorBoundary`**

```razor
@* CenterHub/Components/App.razor — wrap whatever the Router currently renders *@
<CascadingAuthenticationState>
    <Router AppAssembly="typeof(Program).Assembly">
        <Found Context="routeData">
            <ErrorBoundary>
                <ChildContent>
                    <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" />
                </ChildContent>
                <ErrorContent>
                    <p style="color:red">系統發生未預期的錯誤，請重新整理頁面或聯絡管理員。</p>
                </ErrorContent>
            </ErrorBoundary>
        </Found>
        <NotFound>
            <p>找不到這個頁面。</p>
        </NotFound>
    </Router>
</CascadingAuthenticationState>
```

Adjust the exact `<Found>`/`<NotFound>` markup to match whatever the scaffolded `App.razor` already contains — the only required change is inserting the `<ErrorBoundary>` around the existing `<RouteView>`.

- [ ] **Step 4: Confirm server-side exception logging is on**

`Program.cs` already calls `app.UseExceptionHandler("/Error", ...)` in non-development environments (Task 2, Step 4) and ASP.NET Core's default logging provider writes unhandled exceptions to the console/log by default — no additional code needed, this step is a checkpoint.

- [ ] **Step 5: Manual verification**

Temporarily add `throw new Exception("test");` at the top of `OnInitializedAsync` in `TicketList.razor`, run the app, visit `/tickets`, confirm the friendly error message renders (not a raw stack trace or blank page), then remove the temporary throw.

Expected: friendly Chinese error message shown, not a stack trace.

- [ ] **Step 6: Commit**

```bash
git add CenterHub
git commit -m "feat: add global error boundary"
```

---

## Post-Plan Manual Smoke Test

After Task 20, run through the spec's success-criteria flow end-to-end once:

1. Log in as a work-study account.
2. Create 2-3 tickets across AM and PM for the same day, mixing `已完成`/`未完成` and different units/categories.
3. Open `/shift-summary` for that date/period and confirm T/S/F/defense rate/cross-tab match what you'd compute by hand.
4. Create a SOP article, edit it, confirm the revision history shows the prior content.
5. Submit a shift-change request, approve it as admin, confirm the requester would receive an email (check the console log for the `SmtpEmailSender` attempt if no real SMTP server is configured yet — wiring a real school SMTP server is a deployment-time configuration change to `appsettings.json`, not a code change).
6. Generate a password from the tool page.

If all six pass, Phase 1 is functionally complete per the spec.
