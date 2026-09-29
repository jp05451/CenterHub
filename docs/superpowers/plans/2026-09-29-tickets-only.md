# 只留工單:移除其他功能 實作計畫

> **給執行計畫的 agent:** 必須使用的子技能:用 superpowers:subagent-driven-development(建議)或 superpowers:executing-plans 逐項任務執行這份計畫。步驟使用核取方塊(`- [ ]`)語法追蹤進度。

**目標:** 把 CenterHub 縮減成「工單記錄 + 登入/帳號」:刪除班表(含換班審核)、班別彙總、SOP 知識庫、密碼產生器、Email 通知,重置 migration 為單一 `InitialCreate`,並更新文件。

**架構:** 在獨立 worktree 的新分支上原地刪除,依「依賴由外而內」的順序切成 7 個 commit,每個 commit 都能 build(零警告)並通過剩下的測試。唯一需要保留的牽連是啟動時建立種子管理員的密碼產生邏輯,搬成 `AdminSeedPassword` 小類別。最後重置 migration,並在瀏覽器實測。

**技術棧:** ASP.NET Core 10 Blazor Server(InteractiveServer)、EF Core 10 + SQLite、ASP.NET Core Identity、xUnit。

**規格:** `docs/superpowers/specs/2026-09-29-tickets-only-design.md`(已由使用者核准,含:加入精簡版 `CLAUDE.md`、歷史規格不修改、刪除 `2026-09-15-admin-schedule-editing.md`)。

## 全域限制

- 工作目錄一律是 `/Users/ethan/Documents/CenterHub/.claude/worktrees/tickets-only`(分支 `refactor/tickets-only`,起點 `55a3f82`)。**絕對不要**切換分支、不要動 `/Users/ethan/Documents/CenterHub` 主資料夾與 `feat/schedule-periods` 分支。
- **git 指令請一律寫成 `/usr/bin/git ...`**:這個環境的 hook 會攔下單純的 `git` 指令(訊息是 "this command runs rtk with a git command among its operands"),用完整路徑就能正常執行。
- 使用者主資料夾有一個他自己開著的 `dotnet run`(占用 `http://localhost:5252`)。**不要動它、不要 kill 它、不要用 5252**。本計畫需要啟動應用程式時一律用環境變數 `Kestrel__Endpoints__Http__Url=http://127.0.0.1:5299` 改用 5299,並用 `ConnectionStrings__Default="Data Source=<scratchpad 內的路徑>/xxx.db"` 指向暫存資料庫,絕不使用或建立 repo 內的 `centerhub.db`。scratchpad 目錄:`/private/tmp/claude-501/-Users-ethan-Documents-CenterHub/8ff53f2e-906f-4b26-9199-a350edc73056/scratchpad`。
- 每個 commit 都必須:`dotnet build --nologo -v quiet` 0 個警告、0 個錯誤;`dotnet test --nologo -v quiet` 全過(各任務會寫出預期的總數)。
- **中間 commit(任務 1–5)不需要能啟動應用程式**:EF 在啟動時會因為「模型與 migration snapshot 不一致」而拒絕 `Migrate()`,這是預期的,任務 6 重置 migration 後才恢復。所以任務 1–5 只驗證 build 與測試,不要跑 `dotnet run`。
- 一個一個檔案 stage(不要 `git add -A`);刪除檔案用 `/usr/bin/git rm`;commit 訊息格式 `type: description`,結尾空一行加 `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`。
- 不修改工單與帳號功能的行為;不順手重構;不加新功能。所有新增的文件、UI 文字、規格與輸出一律使用**繁體中文**(專案規則:「所有計畫、規格與輸出都必須使用繁體中文」)。
- 不推送、不合併。

## 審查重點

規格暗示、但沒有單元測試能鎖定的失敗情境(由任務 6、8 的實測或文件鎖定):

1. 全新空資料庫啟動:必須成功套用 `InitialCreate`、建立 `admin` 與兩個角色,且資料庫裡**沒有** `Sop*`、`ShiftSlots`、`ShiftChangeRequests` 資料表。→ 任務 6
2. 沿用**舊的** `centerhub.db`(含舊 migration 紀錄與舊資料表)啟動會失敗(資料表已存在),這是重置 migration 的已知後果,不是 bug;必須在 README / DEPLOYMENT / `CLAUDE.md` 明確寫出「舊資料庫要改名備份」。→ 任務 7
3. 沒設定 `Seed:AdminPassword` 時,啟動 log 會印出隨機產生的管理員密碼,且用該密碼真的能登入(Identity 密碼規則:長度 ≥ 8、含大小寫與數字)。→ 任務 4(單元)、任務 8(實測)
4. `WorkStudy` 使用者看不到「帳號管理」,且直接開 `/account/manage-users` 會被擋。→ 任務 8
5. 已刪除功能的舊網址(`/schedule`、`/sop`、`/shift-summary`、`/tools/password-generator`)回應「找不到」頁面,不是 500 或當機。→ 任務 8

---

## 檔案結構(完成後)

```
CenterHub/
├── Data/AppDbContext.cs、AdminSeedPassword.cs(新)
├── Features/Tickets/(不變)
├── Components/Pages/{Account,Tickets}/、Home、Error、NotFound(不變)
├── Components/Layout/NavMenu.razor(只剩「工單」「帳號管理」)
├── Migrations/(單一 InitialCreate)
├── Models/ApplicationUser、Enums(只剩 ServiceMode、ResolutionStatus、ShiftPeriod)、Roles、Ticket、TicketCategory、ServiceCategory
└── Program.cs
CenterHub.Tests/TicketServiceTests.cs、AdminSeedPasswordTests.cs(新)
```

預期測試總數:任務 1 後 35、任務 2 後 28、任務 3 後 23、任務 4 後 23、任務 5 後 20;最終 20 = 工單 15 + 種子密碼 5。

---

### 任務 1:移除班表與換班審核

**檔案:**
- 刪除:`CenterHub/Features/Schedule/ShiftChangeRequestService.cs`、`CenterHub/Components/Pages/Schedule/ScheduleView.razor`、`CenterHub/Components/Pages/Schedule/ShiftChangeRequests.razor`、`CenterHub/Models/ShiftSlot.cs`、`CenterHub/Models/ShiftChangeRequest.cs`、`CenterHub.Tests/ShiftChangeRequestServiceTests.cs`
- 修改:`CenterHub/Models/Enums.cs`、`CenterHub/Data/AppDbContext.cs`、`CenterHub/Program.cs`、`CenterHub/Components/Layout/NavMenu.razor`

**介面:**
- 提供:之後每個任務都以「班表已不存在」為前提。`ShiftPeriod` enum 保留(工單使用)。

- [ ] **步驟 1:刪除檔案**

```bash
cd /Users/ethan/Documents/CenterHub/.claude/worktrees/tickets-only
/usr/bin/git rm -q CenterHub/Features/Schedule/ShiftChangeRequestService.cs \
  CenterHub/Components/Pages/Schedule/ScheduleView.razor \
  CenterHub/Components/Pages/Schedule/ShiftChangeRequests.razor \
  CenterHub/Models/ShiftSlot.cs \
  CenterHub/Models/ShiftChangeRequest.cs \
  CenterHub.Tests/ShiftChangeRequestServiceTests.cs
```

- [ ] **步驟 2:改 `CenterHub/Models/Enums.cs`**

把整個檔案內容換成:
```csharp
namespace CenterHub.Models;

public enum ServiceMode { OnSite, Phone }
public enum ResolutionStatus { Completed, Incomplete }
public enum ShiftPeriod { AM, PM }
```

- [ ] **步驟 3:改 `CenterHub/Data/AppDbContext.cs`**

刪除這兩行 DbSet:
```csharp
    public DbSet<ShiftSlot> ShiftSlots => Set<ShiftSlot>();
    public DbSet<ShiftChangeRequest> ShiftChangeRequests => Set<ShiftChangeRequest>();
```
並刪除 `OnModelCreating` 裡這整段(連同它前後多餘的空白行,讓 `Ticket` 的 `ShiftPeriod` 轉換設定與 `SopArticle` 的 `RowVersion` 設定之間只留一個空白行):
```csharp
        builder.Entity<ShiftSlot>()
            .Property(s => s.Period)
            .HasConversion<string>();

        builder.Entity<ShiftChangeRequest>()
            .Property(r => r.Type)
            .HasConversion<string>();

        builder.Entity<ShiftChangeRequest>()
            .Property(r => r.Status)
            .HasConversion<string>();

        builder.Entity<ShiftChangeRequest>()
            .HasOne(r => r.ShiftSlot)
            .WithMany()
            .HasForeignKey(r => r.ShiftSlotId)
            .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **步驟 4:改 `CenterHub/Program.cs`**

刪除這一行:
```csharp
builder.Services.AddScoped<CenterHub.Features.Schedule.ShiftChangeRequestService>();
```

- [ ] **步驟 5:改 `CenterHub/Components/Layout/NavMenu.razor`**

刪除這一行(班表連結):
```razor
                        <li class="nav-item"><a class="nav-link" href="/schedule">班表</a></li>
```
以及這一行(換班審核,在 Admin 區塊內):
```razor
                            <li class="nav-item"><a class="nav-link" href="/schedule/requests">換班審核</a></li>
```

- [ ] **步驟 6:確認資料夾清空並建置、測試**

```bash
ls CenterHub/Features/Schedule CenterHub/Components/Pages/Schedule 2>&1   # 預期:No such file or directory(git 已移除空資料夾)
dotnet build --nologo -v quiet
dotnet test --nologo -v quiet
```
預期:build 0 警告 0 錯誤;測試 **35** 個全過。若 build 因為某處還引用 `ShiftSlot` / `ShiftChange*` 而失敗,用 `grep -rn "ShiftSlot\|ShiftChange" CenterHub CenterHub.Tests --include='*.cs' --include='*.razor' | grep -v Migrations/` 找出來,只刪除該引用。

- [ ] **步驟 7:Commit**

```bash
/usr/bin/git add CenterHub/Models/Enums.cs CenterHub/Data/AppDbContext.cs CenterHub/Program.cs CenterHub/Components/Layout/NavMenu.razor
/usr/bin/git commit -m "refactor: remove schedule and shift change requests"
```
(訊息結尾記得加上署名 trailer;刪除的檔案在步驟 1 已由 `git rm` stage。)

---

### 任務 2:移除班別彙總

**檔案:**
- 刪除:`CenterHub/Features/ShiftSummary/`(`ShiftSummaryResult.cs`、`ShiftSummaryService.cs`、`UnitCategoryBreakdown.cs`)、`CenterHub/Components/Pages/ShiftSummary/ShiftSummaryPage.razor`、`CenterHub.Tests/ShiftSummaryServiceTests.cs`
- 修改:`CenterHub/Program.cs`、`CenterHub/Components/Layout/NavMenu.razor`

- [ ] **步驟 1:刪除檔案**

```bash
/usr/bin/git rm -q -r CenterHub/Features/ShiftSummary CenterHub/Components/Pages/ShiftSummary CenterHub.Tests/ShiftSummaryServiceTests.cs
```

- [ ] **步驟 2:改 `Program.cs`**,刪除:
```csharp
builder.Services.AddScoped<CenterHub.Features.ShiftSummary.ShiftSummaryService>();
```

- [ ] **步驟 3:改 `NavMenu.razor`**,刪除:
```razor
                        <li class="nav-item"><a class="nav-link" href="/shift-summary">班別彙總</a></li>
```

- [ ] **步驟 4:建置、測試**

```bash
dotnet build --nologo -v quiet
dotnet test --nologo -v quiet
```
預期:0 警告 0 錯誤;測試 **28** 個全過。

- [ ] **步驟 5:Commit**

```bash
/usr/bin/git add CenterHub/Program.cs CenterHub/Components/Layout/NavMenu.razor
/usr/bin/git commit -m "refactor: remove shift summary"
```

---

### 任務 3:移除 SOP 知識庫

**檔案:**
- 刪除:`CenterHub/Features/SopWiki/`(`SopArticleService.cs`、`SopEditConflictException.cs`)、`CenterHub/Components/Pages/SopWiki/`(4 個 razor)、`CenterHub/Models/SopArticle.cs`、`SopArticleRevision.cs`、`SopCategory.cs`、`CenterHub.Tests/SopArticleServiceTests.cs`
- 修改:`CenterHub/Data/AppDbContext.cs`、`CenterHub/Program.cs`、`CenterHub/Components/Layout/NavMenu.razor`

- [ ] **步驟 1:刪除檔案**

```bash
/usr/bin/git rm -q -r CenterHub/Features/SopWiki CenterHub/Components/Pages/SopWiki \
  CenterHub/Models/SopArticle.cs CenterHub/Models/SopArticleRevision.cs CenterHub/Models/SopCategory.cs \
  CenterHub.Tests/SopArticleServiceTests.cs
```

- [ ] **步驟 2:改 `AppDbContext.cs`**

刪除三行 DbSet:
```csharp
    public DbSet<SopCategory> SopCategories => Set<SopCategory>();
    public DbSet<SopArticle> SopArticles => Set<SopArticle>();
    public DbSet<SopArticleRevision> SopArticleRevisions => Set<SopArticleRevision>();
```
刪除 `RowVersion` 設定:
```csharp
        builder.Entity<SopArticle>()
            .Property(a => a.RowVersion)
            .IsConcurrencyToken();
```
刪除 `SopCategory` 種子資料(整段 `builder.Entity<SopCategory>().HasData(...)`,含 6 筆):
```csharp
        builder.Entity<SopCategory>().HasData(
            new SopCategory { Id = 1, Name = "硬體維修" },
            new SopCategory { Id = 2, Name = "軟體安裝" },
            new SopCategory { Id = 3, Name = "網路設定" },
            new SopCategory { Id = 4, Name = "帳號管理" },
            new SopCategory { Id = 5, Name = "借用流程" },
            new SopCategory { Id = 6, Name = "常見問題" }
        );
```
完成後 `AppDbContext.cs` 整個檔案應該是:
```csharp
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

- [ ] **步驟 3:改 `Program.cs`**,刪除:
```csharp
builder.Services.AddScoped<CenterHub.Features.SopWiki.SopArticleService>();
```

- [ ] **步驟 4:改 `NavMenu.razor`**,刪除:
```razor
                        <li class="nav-item"><a class="nav-link" href="/sop">SOP 知識庫</a></li>
```

- [ ] **步驟 5:建置、測試**

```bash
dotnet build --nologo -v quiet
dotnet test --nologo -v quiet
```
預期:0 警告 0 錯誤;測試 **23** 個全過。

- [ ] **步驟 6:Commit**

```bash
/usr/bin/git add CenterHub/Data/AppDbContext.cs CenterHub/Program.cs CenterHub/Components/Layout/NavMenu.razor
/usr/bin/git commit -m "refactor: remove SOP wiki"
```

---

### 任務 4:移除密碼產生器,種子密碼邏輯改用 `AdminSeedPassword`

**檔案:**
- 建立:`CenterHub/Data/AdminSeedPassword.cs`、`CenterHub.Tests/AdminSeedPasswordTests.cs`
- 刪除:`CenterHub/Features/Tools/PasswordGeneratorService.cs`、`CenterHub/Components/Pages/Tools/PasswordGenerator.razor`、`CenterHub.Tests/PasswordGeneratorServiceTests.cs`
- 修改:`CenterHub/Program.cs`、`CenterHub/Components/Layout/NavMenu.razor`

**介面:**
- 提供:`public static class AdminSeedPassword`(命名空間 `CenterHub.Data`),`public static string Generate(int length = 12)`。字元集與舊 `PasswordGeneratorService` 完全相同(排除易混淆字元),只是從實例方法改成靜態方法。

- [ ] **步驟 1:先寫測試(此時舊測試還在,所以先不刪)**

`CenterHub.Tests/AdminSeedPasswordTests.cs`:
```csharp
using CenterHub.Data;
using Xunit;

namespace CenterHub.Tests;

public class AdminSeedPasswordTests
{
    private const string Allowed = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%^&*";

    [Fact]
    public void Generate_DefaultLength_Returns12Characters()
    {
        var password = AdminSeedPassword.Generate();

        Assert.Equal(12, password.Length);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    public void Generate_CustomLength_ReturnsRequestedLength(int length)
    {
        var password = AdminSeedPassword.Generate(length);

        Assert.Equal(length, password.Length);
    }

    [Fact]
    public void Generate_ContainsOnlyUnambiguousAllowedCharacters()
    {
        var password = AdminSeedPassword.Generate(50);

        Assert.All(password, c => Assert.Contains(c, Allowed));
    }

    [Fact]
    public void Generate_CalledTwice_ProducesDifferentValues()
    {
        var first = AdminSeedPassword.Generate();
        var second = AdminSeedPassword.Generate();

        Assert.NotEqual(first, second);
    }
}
```

- [ ] **步驟 2:執行,確認失敗**

執行:`dotnet test --nologo -v quiet --filter AdminSeedPasswordTests`
預期:建置失敗 —— `AdminSeedPassword` 不存在。

- [ ] **步驟 3:實作**

`CenterHub/Data/AdminSeedPassword.cs`:
```csharp
using System.Security.Cryptography;

namespace CenterHub.Data;

/// <summary>Generates the one-time password for the seeded admin account.</summary>
public static class AdminSeedPassword
{
    // Excludes visually ambiguous characters (0/O, 1/l/I) since the generated password is
    // read from the startup log and retyped by hand.
    private const string AllowedCharacters = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%^&*";

    public static string Generate(int length = 12)
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

- [ ] **步驟 4:執行,確認通過**

執行:`dotnet test --nologo -v quiet --filter AdminSeedPasswordTests`
預期:通過(5 個測試案例:3 個 Fact + Theory 的 2 個案例)。

- [ ] **步驟 5:改 `Program.cs`,讓種子邏輯改用新類別**

(a) 刪除 Service 註冊:
```csharp
builder.Services.AddScoped<CenterHub.Features.Tools.PasswordGeneratorService>();
```
(b) 在種子區塊裡,把這段:
```csharp
            var passwordGenerator = scope.ServiceProvider
                .GetRequiredService<CenterHub.Features.Tools.PasswordGeneratorService>();
            const int maxAttempts = 5;
```
改成:
```csharp
            const int maxAttempts = 5;
```
並把:
```csharp
                generatedPassword = passwordGenerator.Generate();
```
改成:
```csharp
                generatedPassword = AdminSeedPassword.Generate();
```
(c) 更新兩則已經不正確的註解:
- 把 `// Prefer an operator-supplied password ... and generate one with the project's existing password generator` 這段裡的 `generate one with the project's existing password generator` 改成 `generate one with AdminSeedPassword`(保持該註解其餘文字與換行風格不變)。
- 把 `Email = "admin@example.edu" // replace with a real school mailbox once Task 17's email notifications go live` 改成 `Email = "admin@example.edu" // placeholder; replace with a real mailbox before deployment`。

(`Program.cs` 第 1 行已有 `using CenterHub.Data;`,不用再加。)

- [ ] **步驟 6:刪除舊檔案、改 `NavMenu.razor`**

```bash
/usr/bin/git rm -q -r CenterHub/Features/Tools CenterHub/Components/Pages/Tools CenterHub.Tests/PasswordGeneratorServiceTests.cs
```
`NavMenu.razor` 刪除:
```razor
                        <li class="nav-item"><a class="nav-link" href="/tools/password-generator">密碼產生器</a></li>
```
完成後 `NavMenu.razor` 的導覽清單應該是:
```razor
                    <ul class="navbar-nav me-auto mb-2 mb-lg-0">
                        <li class="nav-item"><a class="nav-link" href="/tickets">工單</a></li>
                        <AuthorizeView Roles="@Roles.Admin" Context="adminContext">
                            <li class="nav-item"><a class="nav-link" href="/account/manage-users">帳號管理</a></li>
                        </AuthorizeView>
                    </ul>
```

- [ ] **步驟 7:建置、測試**

```bash
dotnet build --nologo -v quiet
dotnet test --nologo -v quiet
```
預期:0 警告 0 錯誤;測試 **23** 個全過(工單 15 + 種子密碼 5 + Email 3)。

- [ ] **步驟 8:Commit**

```bash
/usr/bin/git add CenterHub/Data/AdminSeedPassword.cs CenterHub.Tests/AdminSeedPasswordTests.cs CenterHub/Program.cs CenterHub/Components/Layout/NavMenu.razor
/usr/bin/git commit -m "refactor: replace password generator tool with AdminSeedPassword"
```

---

### 任務 5:移除 Email 通知與 Smtp 設定

**檔案:**
- 刪除:`CenterHub/Features/Notifications/`(`EmailMessage.cs`、`EmailNotificationQueue.cs`、`IEmailSender.cs`、`SmtpEmailSender.cs`)、`CenterHub.Tests/EmailNotificationQueueTests.cs`
- 修改:`CenterHub/Program.cs`、`CenterHub/appsettings.json`

- [ ] **步驟 1:刪除檔案**

```bash
/usr/bin/git rm -q -r CenterHub/Features/Notifications CenterHub.Tests/EmailNotificationQueueTests.cs
```

- [ ] **步驟 2:改 `Program.cs`**,刪除這一整段(含前後空白行,保持相鄰區塊之間只有一個空白行):
```csharp
builder.Services.AddSingleton<CenterHub.Features.Notifications.IEmailSender,
    CenterHub.Features.Notifications.SmtpEmailSender>();
builder.Services.AddSingleton<CenterHub.Features.Notifications.EmailNotificationQueue>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<CenterHub.Features.Notifications.EmailNotificationQueue>());
```
完成後 `Program.cs` 的服務註冊區應該是:
```csharp
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<CenterHub.Features.Tickets.TicketService>();

var app = builder.Build();
```

- [ ] **步驟 3:改 `CenterHub/appsettings.json`**,刪除整個 `Smtp` 區塊,完成後檔案應該是:
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
  },
  "Seed": {
    "AdminPassword": ""
  }
}
```

- [ ] **步驟 4:建置、測試,並確認殘留**

```bash
dotnet build --nologo -v quiet
dotnet test --nologo -v quiet
grep -rn "Smtp\|EmailNotification\|IEmailSender\|Notifications" CenterHub CenterHub.Tests --include='*.cs' --include='*.razor' --include='*.json' | grep -v "Migrations/\|/bin/\|/obj/"
```
預期:0 警告 0 錯誤;測試 **20** 個全過(工單 15 + 種子密碼 5);grep 沒有輸出。

- [ ] **步驟 5:Commit**

```bash
/usr/bin/git add CenterHub/Program.cs CenterHub/appsettings.json
/usr/bin/git commit -m "refactor: remove email notifications and smtp settings"
```

---

### 任務 6:重置 migration 為單一 `InitialCreate`

**檔案:**
- 刪除:`CenterHub/Migrations/` 內全部檔案(8 個 migration + Designer + `AppDbContextModelSnapshot.cs`)
- 建立:`CenterHub/Migrations/<時間戳>_InitialCreate.cs`、`.Designer.cs`、`AppDbContextModelSnapshot.cs`(由 `dotnet ef` 產生)

- [ ] **步驟 1:刪除舊 migration 並重新產生**

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
/usr/bin/git rm -q -r CenterHub/Migrations
dotnet ef migrations add InitialCreate --project CenterHub
```
(找不到 `dotnet ef` 時:`dotnet tool install --global dotnet-ef`。)

- [ ] **步驟 2:檢查產生的內容**

```bash
ls CenterHub/Migrations
grep -n "CreateTable" -A1 CenterHub/Migrations/*_InitialCreate.cs | grep "name:"
grep -c "InsertData" CenterHub/Migrations/*_InitialCreate.cs
grep -rn "Sop\|ShiftSlot\|ShiftChange" CenterHub/Migrations
```
預期:只有 `InitialCreate` 一組檔案加 snapshot;資料表只有 Identity 的(`AspNetRoles`、`AspNetUsers`、`AspNetRoleClaims`、`AspNetUserClaims`、`AspNetUserLogins`、`AspNetUserRoles`、`AspNetUserTokens`)以及 `ServiceCategories`、`Tickets`、`TicketCategories`;`ServiceCategories` 有 10 筆種子(一個 `InsertData` 區塊,欄位含 10 列);最後一個 grep **沒有輸出**。

- [ ] **步驟 3:建置、測試**

```bash
dotnet build --nologo -v quiet
dotnet test --nologo -v quiet
```
預期:0 警告 0 錯誤;測試 **20** 個全過。

- [ ] **步驟 4:全新資料庫啟動實測(審查重點 1、3)**

**不要**使用 repo 內的資料庫、**不要**用 5252。設定 `SP` 為 scratchpad 目錄(見全域限制):
```bash
SP=/private/tmp/claude-501/-Users-ethan-Documents-CenterHub/8ff53f2e-906f-4b26-9199-a350edc73056/scratchpad
rm -f $SP/tickets-only-check.db
ConnectionStrings__Default="Data Source=$SP/tickets-only-check.db" \
Kestrel__Endpoints__Http__Url=http://127.0.0.1:5299 \
  dotnet run --project CenterHub --no-launch-profile > $SP/tickets-only-run.log 2>&1 &
sleep 15
grep -n "Seeded initial admin password\|Failed to seed\|Exception" $SP/tickets-only-run.log
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5299/
sqlite3 $SP/tickets-only-check.db ".tables"
sqlite3 $SP/tickets-only-check.db "select count(*) from ServiceCategories; select UserName from AspNetUsers; select Name from AspNetRoles;"
```
預期:log 裡有 `Seeded initial admin password: <密碼>`,沒有 `Failed to seed` 與 `Exception`;`curl` 回 `200` 或 `302`(不是 `500`);`.tables` **沒有** `Sop*`、`ShiftSlots`、`ShiftChangeRequests`;`ServiceCategories` 筆數為 `10`;使用者只有 `admin`;角色為 `Admin`、`WorkStudy`。

**做完必須結束你啟動的程序**(找出 5299 那個 `CenterHub` 程序的 PID 後 kill;**不要碰使用者在 5252 的程序**),並用 `lsof -iTCP:5299 -sTCP:LISTEN` 確認已無殘留。

- [ ] **步驟 5:Commit**

```bash
/usr/bin/git add CenterHub/Migrations
/usr/bin/git status --short   # 預期:只有 Migrations 相關的異動,沒有 .db、log
/usr/bin/git commit -m "refactor: reset migrations to a single InitialCreate"
```

---

### 任務 7:更新文件、加入精簡版 `CLAUDE.md`、刪除舊計畫

**檔案:**
- 修改:`README.md`、`docs/ARCHITECTURE.md`、`docs/DEPLOYMENT.md`、`CONTRIBUTING.md`、`CenterHub/Features/Tickets/TicketService.cs`(僅註解)、`CenterHub/Models/Ticket.cs`(僅註解)
- 建立:`CLAUDE.md`
- 刪除:`docs/superpowers/plans/2026-09-15-admin-schedule-editing.md`
- **不要修改**:`docs/superpowers/specs/2026-09-08-centerhub-design.md`、`docs/superpowers/plans/2026-09-08-centerhub-mvp.md`(歷史紀錄)

所有文字繁體中文、現在式,不要寫「新增 / 改成 / 已移除」這類修改紀錄式語句(下面明確指定要寫的說明句除外)。

- [ ] **步驟 1:改兩處註解(只改註解,不改程式碼)**

`CenterHub/Features/Tickets/TicketService.cs`,把:
```csharp
        // ShiftDate/ShiftPeriod/DailySeq are intentionally left untouched here — they drive
        // daily numbering and ShiftSummary's grouping, so "moving" a ticket to a different
        // shift after the fact is out of scope; use void + a new ticket for that instead.
```
改成:
```csharp
        // ShiftDate/ShiftPeriod/DailySeq are intentionally left untouched here — they drive
        // the per-day ticket numbering, so "moving" a ticket to a different shift after the
        // fact is out of scope; use void + a new ticket for that instead.
```
`CenterHub/Models/Ticket.cs`,把:
```csharp
    // Soft delete: voided tickets stay in the database (for audit/history) but are
    // excluded from the default ticket list and from ShiftSummary's aggregates —
    // a hard delete would silently change historical shift stats that were already
    // reported. See TicketService.VoidTicketAsync.
```
改成:
```csharp
    // Soft delete: voided tickets stay in the database (for audit/history) but are
    // excluded from the default ticket list — a hard delete would silently change
    // numbering and history that was already reported. See TicketService.VoidTicketAsync.
```

- [ ] **步驟 2:改 `README.md`**

- 第 5 行整段換成:
```markdown
工讀生用它記錄服務工單(申請單位、申請人、服務性質與類別、處理結果、服務時間等),並以帳號與角色(`Admin` / `WorkStudy`)控管誰能編輯或作廢。設計背景與決策理由見 [`docs/superpowers/specs/2026-09-08-centerhub-design.md`](docs/superpowers/specs/2026-09-08-centerhub-design.md)(原始設計,包含後來移除的功能);現況架構速覽見 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)。
```
- 在「### 第一次登入」那一節的最後一個項目之後(「**正式部署時務必自行設定一組密碼…**」那行之後)加一段:
```markdown

> **從舊版升級:** migration 已重置為單一 `InitialCreate`。舊版產生的 `centerhub.db`(含舊 migration 紀錄與已移除功能的資料表)不能沿用,啟動會因為資料表已存在而失敗。請先把舊的 `centerhub.db` 改名備份,讓新版建立全新的資料庫。
```
- 第 52 行「測試專案 …」整行換成:
```markdown
測試專案 `CenterHub.Tests` 用 xUnit,涵蓋工單(編號、篩選、編輯/作廢的權限)與種子管理員密碼產生等核心邏輯(詳見 [`CONTRIBUTING.md`](CONTRIBUTING.md#測試) 的測試慣例)。
```
- 專案結構的 `Features/` 那行換成:
```
├── Features/           # 依功能拆分的服務層(目前只有 Tickets)
```

- [ ] **步驟 3:整份換掉 `docs/ARCHITECTURE.md`**

````markdown
# 架構總覽(現況速覽)

這份文件描述 CenterHub **目前程式碼的實際樣子**,給要接手維護或新增功能的人快速定位。`docs/superpowers/` 底下的原始設計規格與實作計畫是歷史決策紀錄,描述的是**包含後來移除的功能**(班表、班別彙總、SOP 知識庫、密碼產生器、Email 通知)的原始設計,不代表現況。

## 專案結構

單一 ASP.NET Core Blazor Server 專案,依功能拆資料夾,共用一個 EF Core + SQLite 資料庫:

```
CenterHub/
├── Data/                # AppDbContext、AdminSeedPassword(種子管理員密碼)、Migrations
├── Features/Tickets/    # 服務工單
├── Components/          # 版面、導覽列、頁面(Account、Tickets)
├── Models/              # 資料模型、Identity 使用者/角色
└── Program.cs           # Identity、DI、路由、啟動時的種子資料
```

## 請求管線(`Program.cs`)

中介軟體順序是刻意排的,改動前請注意:

```
UseHttpsRedirection → UseAuthentication → UseAuthorization → UseAntiforgery
```

`UseAuthentication` 必須在 `UseAuthorization` 之前;兩者都要在 `UseAntiforgery` 之前,否則已登入使用者重新送出表單會被防偽驗證擋下。

啟動時(`Program.cs` 最後一段)會依序:套用所有 pending migration(`db.Database.Migrate()`)→ 確保 `Admin`/`WorkStudy` 角色存在 → 若沒有 `admin` 帳號則建立一個(密碼來源見 [README](../README.md#第一次登入))。這對單機內部工具來說夠用,沒有另外做獨立的 migration pipeline。

Migration 只有一個 `InitialCreate`(Identity、`Tickets`、`ServiceCategories`、`TicketCategories`)。舊版產生的資料庫不能沿用,見 README 的「從舊版升級」。

## 資料模型

### 帳號與角色

沿用 ASP.NET Core Identity 的 `ApplicationUser`(繼承 `IdentityUser`,多加 `DisplayName`),角色只有兩種,定義在 `Models/Roles.cs`:`Admin`、`WorkStudy`。頁面用 `[Authorize(Roles = Roles.Admin)]` 這類屬性做角色限制。

種子管理員的密碼由 `AdminSeedPassword.Generate()` 產生(排除 `0/O`、`1/l/I` 等易混淆字元),只在沒有設定 `Seed:AdminPassword` 時使用。

### Ticket(服務工單)

核心資料表,`TicketService` 提供 CRUD。`ServiceCategories` 可複選、來自獨立可維護的 `ServiceCategory` 表。`CreatedByUserId` 記錄承辦人,`TicketList.razor` 會把它解析成 `DisplayName` 顯示。

- **編輯**:只開放內容類欄位(申請單位、申請人、聯絡方式、服務性質、服務類別、處理結果、滿意度、服務時間、作業系統、問題/解決方法)。`ShiftDate`/`ShiftPeriod`/`DailySeq` 不可編輯——這幾個欄位驅動當日編號,事後改會讓工單「跳到別的班次」;`LastEditedByUserId`/`LastEditedAt` 只記最後一次修改的人與時間,不留完整修訂快照。
- **刪除是軟刪除(作廢)**:`IsVoided`/`VoidedByUserId`/`VoidedAt`,不是真的從資料庫移除——硬刪除會讓已經報告過的編號與歷史悄悄改變、查無可查。`TicketService.GetTicketsAsync` 預設排除已作廢工單。
- **權限**:`UpdateTicketAsync`/`VoidTicketAsync` 在 Service 層驗證呼叫者是 `CreatedByUserId` 本人或 Admin,不只是畫面隱藏按鈕。

## 已知限制(給接手者的提醒)

- 沒有針對 `[Authorize]` 權限控管本身的自動化測試——改動 `Components/Routes.razor` 或角色設定時,目前只能靠手動驗證有沒有重新打開權限漏洞。
- 種子管理員帳號的 Email(`Program.cs` 裡的 `admin@example.edu`)是佔位符,目前沒有任何功能會寄信給它;之後若加入通知功能,需要先換成真實信箱。
````

- [ ] **步驟 4:改 `docs/DEPLOYMENT.md`**

- 表格中刪除這兩列:
```markdown
| `Smtp:Host` / `Smtp:Port` / `Smtp:EnableSsl` | 學校 SMTP 伺服器連線資訊 | 同上 |
| `Smtp:Username` / `Smtp:Password` | SMTP 帳密（兩者皆非空才會套用驗證） | 環境變數，或主機層級的密碼管理工具 |
```
(此文件沿用原本的全形標點,只刪除整列。)
- 「環境變數命名規則」那行的範例 `Smtp:Password` → 環境變數 `Smtp__Password` 改成 `Seed:AdminPassword` → 環境變數 `Seed__AdminPassword`。
- 刪除整個 `### SMTP 設定` 小節(標題與該段內文)。
- 「### 管理員種子帳號」那段內文改成:
```markdown
`Program.cs` 目前把種子管理員的 Email 寫成佔位符 `admin@example.edu`。系統目前沒有任何寄信功能,所以這個信箱不會收到通知,但它是帳號資料的一部分;正式部署前建議改成真實信箱。目前程式碼裡是寫死的常數,若要在不同環境用不同 Email,需要先把這行改成讀取設定值。
```
- 「## 建置與套用 Migration」小節中,`Migration 不需要另外手動套用` 那段之前加一段:
```markdown
Migration 只有單一 `InitialCreate`(帳號與工單)。**舊版產生的 `centerhub.db` 不能沿用**(它含舊的 migration 紀錄與已移除功能的資料表,啟動會因為資料表已存在而失敗)。升級前請先把舊的 `centerhub.db` 改名備份,讓新版建立全新的資料庫。
```
- 「選項二:獨立 Windows Service」裡 `環境變數(如 Seed__AdminPassword、Smtp__Password)` 那句,把 `Smtp__Password` 這部分連同前面的頓號刪掉,只留 `Seed__AdminPassword`。
- 「上線後檢查清單」中,把 `- [ ] 送出一筆測試換班/請假申請,確認 Email 通知有寄達` 換成 `- [ ] 建立一筆測試工單並確認可以編輯、作廢`。

- [ ] **步驟 5:改 `CONTRIBUTING.md`**

- 第 29 行範例 `fix: 換班申請未檢查是否為本人排班` 改成 `fix: 工單編輯未檢查是否為承辦人`。
- 第 34 行 `(例如 `Tickets`、`SopWiki`)` 改成 `(例如 `Tickets`)`。
- 第 61 行 `(班別彙總計算、並行編輯衝突偵測、狀態機轉換)` 改成 `(工單編號、編輯/作廢的權限檢查)`。
- 第 69 行 `(例如未指派的班表時段、空白輸入)` 改成 `(例如空白輸入、沒有承辦人的工單)`。
- 第 71 行 `改到 SOP 編輯、班表狀態機這類共用狀態的地方` 改成 `改到工單編輯、作廢這類共用狀態的地方`。

- [ ] **步驟 6:建立精簡版 `CLAUDE.md`**

````markdown
# CLAUDE.md

本文件為 Claude Code (claude.ai/code) 在此程式庫中工作時的指引。

重要!!所有計畫、規格與輸出都必須使用繁體中文。使用者偏好簡單、白話的繁體中文說明。

CenterHub 是電算中心的值班交接內部工具,目前縮減為「工單記錄 + 登入/帳號」:ASP.NET Core 10 Blazor Server(InteractiveServer)+ EF Core 10 + SQLite + ASP.NET Core Identity(`Admin` / `WorkStudy` 兩種角色)。一個依功能拆分的 Web 專案(`CenterHub/`),加上一個 xUnit 測試專案(`CenterHub.Tests/`)。班表、班別彙總、SOP 知識庫、密碼產生器、Email 通知已在 2026-09-29 移除(相關工作保留在 `feat/schedule-periods` 分支,僅供參考)。

修改工單的資料模型之前,先讀 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。[CONTRIBUTING.md](CONTRIBUTING.md) 有分支/commit 慣例,以及如何新增一個功能模組。

## 指令

從 repo 根目錄(`CenterHub.sln`)執行。沒有獨立的 lint 步驟;`dotnet build` 必須沒有警告。

```bash
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~TicketServiceTests"           # 跑單一測試類別
dotnet run --project CenterHub        # http://localhost:5252(Development,經由 launchSettings)

dotnet ef migrations add <Name> --project CenterHub
dotnet ef database update --project CenterHub
```

啟動時也會自動套用 migration(`Program.cs` 裡的 `db.Database.Migrate()`),接著建立角色和 `admin` 帳號。若沒有設定 `Seed:AdminPassword`,自動產生的密碼(`AdminSeedPassword`)只會在啟動 log 印出一次。

migration 只有單一 `InitialCreate`。**舊版產生的 `centerhub.db` 不能沿用**(啟動會因為資料表已存在而失敗),要先改名備份。

## 在 Production 執行(很容易搞錯)

- `dotnet run` 會套用 `Properties/launchSettings.json`,它會強制 `ASPNETCORE_ENVIRONMENT=Development` 和 `applicationUrl=http://localhost:5252`。這會蓋掉環境變數,所以 `appsettings.Production.json`(Kestrel `0.0.0.0:5252`)永遠不會被載入。加上 `--no-launch-profile` 才能跳過。
- Production 也無法從原始碼解析 Blazor 靜態網頁資產(會出現 `blazor.web.js`、`*.styles.css` 的 `FileNotFoundException`)。改用發佈輸出:
  ```bash
  dotnet publish CenterHub -c Release -o ~/centerhub-publish
  cd ~/centerhub-publish && ASPNETCORE_ENVIRONMENT=Production dotnet CenterHub.dll
  ```
- SQLite 路徑是相對路徑(`Data Source=centerhub.db`),所以新的發佈資料夾一開始是空資料庫,除非把 `centerhub.db` 複製進去。`*.db` 已被 gitignore。可用環境變數 `ConnectionStrings__Default` 指定路徑。
- 機密資訊絕不放進進版控的檔案。用 `appsettings.Local.json`(已 gitignore,在 `Program.cs` 最後載入),或用雙底線表示巢狀的環境變數(例如 `Seed__AdminPassword`)。詳見 [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)。

## 架構重點

- **功能資料夾的分層:** `Features/<Name>/*Service.cs`(業務邏輯,建構子注入 `AppDbContext`,在 `Program.cs` 註冊為 `Scoped`)→ `Components/Pages/<Name>/*.razor` → `CenterHub.Tests/<Name>ServiceTests.cs`。頁面直接呼叫 Service 或 `AppDbContext`,沒有 API/controller 層。
- **權限在兩個地方都要檢查:** 頁面上的 `[Authorize(Roles = Roles.Admin)]`(常數在 `Models/Roles.cs`,不要寫死字串),**以及** Service 內部的擁有權/角色檢查(例如 `TicketService`)。只把按鈕藏起來是不夠的。
- **工單是軟刪除(作廢):** 不要改成硬刪除;`ShiftDate`/`ShiftPeriod`/`DailySeq` 驅動當日編號,不可編輯。
- **Blazor Server 每個 circuit 共用一個 `AppDbContext`,** 追蹤中的實體不會被重新讀取:發生 `DbUpdateConcurrencyException` 之後必須把該 entity detach;從另一個分頁看到舊資料時,先想到這一點(必要時 `ChangeTracker.Clear()` 或用 `AsNoTracking()`)。
- **`Program.cs` 的 middleware 順序是刻意的:** `UseAuthentication` → `UseAuthorization` → `UseAntiforgery`。

## 測試慣例

- xUnit 搭配 `Assert.*` 與 EF Core **InMemory** provider(`UseInMemoryDatabase(Guid.NewGuid().ToString())`);不用 Moq 或 FluentAssertions。
- InMemory 不會強制唯一索引、外鍵或 cascade 規則。任何重要的規則都必須在 Service 程式碼裡自己擋,並在那裡測試。
- `[Authorize]` 的行為沒有自動化測試;頁面權限要手動驗證。
````

- [ ] **步驟 7:刪除舊計畫**

```bash
/usr/bin/git rm -q docs/superpowers/plans/2026-09-15-admin-schedule-editing.md
```

- [ ] **步驟 8:建置、測試、殘留檢查**

```bash
dotnet build --nologo -v quiet
dotnet test --nologo -v quiet
grep -rniE "sop|schedule|ShiftSlot|ShiftChange|ShiftSummary|Smtp|EmailNotification|PasswordGenerator" \
  CenterHub CenterHub.Tests README.md CONTRIBUTING.md CLAUDE.md docs/ARCHITECTURE.md docs/DEPLOYMENT.md \
  --include='*.cs' --include='*.razor' --include='*.json' --include='*.md' 2>/dev/null | grep -v "Migrations/\|/bin/\|/obj/"
```
預期:build 0 警告 0 錯誤;測試 **20** 個全過。grep 只應出現以下**明確允許**的結果,其餘都要處理:`README.md` 與 `docs/ARCHITECTURE.md` 中「原始設計…包含後來移除的功能」的說明句、`CLAUDE.md` 中說明「已在 2026-09-29 移除」與 `feat/schedule-periods` 分支的那一句、`DEPLOYMENT.md`/`README.md`/`CLAUDE.md` 中「舊版…已移除功能的資料表」的升級提示。`ShiftPeriod`(工單欄位)不會被 `ShiftSlot|ShiftChange|ShiftSummary` 這些樣式命中,屬於保留。

- [ ] **步驟 9:Commit**

```bash
/usr/bin/git add README.md CONTRIBUTING.md CLAUDE.md docs/ARCHITECTURE.md docs/DEPLOYMENT.md CenterHub/Features/Tickets/TicketService.cs CenterHub/Models/Ticket.cs
/usr/bin/git commit -m "docs: update docs for the tickets-only scope and add a slim CLAUDE.md"
```
(刪除的舊計畫在步驟 7 已 stage。)

---

### 任務 8:最終驗證(不產生 commit,結果寫進報告)

**檔案:** 無變更。全程用暫存資料庫與 5299 埠(見全域限制),做完必須清掉所有你啟動的程序、確認 `git status` 乾淨。

- [ ] **步驟 1:完整建置與測試**

```bash
dotnet build --nologo -v quiet --no-incremental
dotnet test --nologo -v quiet
/usr/bin/git status --short
/usr/bin/git log --oneline a58aad6..HEAD
```
預期:0 警告 0 錯誤;20 個測試全過;`git status` 沒有輸出;log 從 `a58aad6` 之後依序是:規格、班表、班別彙總、SOP、密碼產生器、Email、migration、文件,共 8 個 commit(含規格 commit,若任務實作時把計畫檔也 commit 了,則多 1 個)。

- [ ] **步驟 2:用瀏覽器實測(chrome-devtools MCP)**

用 `ToolSearch` 載入 `mcp__plugin_ecc_chrome-devtools__*`(例如 `select:mcp__plugin_ecc_chrome-devtools__navigate_page,mcp__plugin_ecc_chrome-devtools__take_snapshot,mcp__plugin_ecc_chrome-devtools__take_screenshot,mcp__plugin_ecc_chrome-devtools__click,mcp__plugin_ecc_chrome-devtools__fill,mcp__plugin_ecc_chrome-devtools__list_console_messages,mcp__plugin_ecc_chrome-devtools__new_page,mcp__plugin_ecc_chrome-devtools__wait_for,mcp__plugin_ecc_chrome-devtools__evaluate_script,mcp__plugin_ecc_chrome-devtools__press_key`)。載不到就回報 BLOCKED,不要跳過。

啟動(全新暫存資料庫、**不要**設定 `Seed__AdminPassword`,好用 log 產生的密碼測試審查重點 3):
```bash
SP=/private/tmp/claude-501/-Users-ethan-Documents-CenterHub/8ff53f2e-906f-4b26-9199-a350edc73056/scratchpad
rm -f $SP/tickets-only-final.db
ConnectionStrings__Default="Data Source=$SP/tickets-only-final.db" \
Kestrel__Endpoints__Http__Url=http://127.0.0.1:5299 \
  dotnet run --project CenterHub --no-launch-profile > $SP/tickets-only-final.log 2>&1 &
sleep 15
grep "Seeded initial admin password" $SP/tickets-only-final.log
```
從那行取得 `admin` 的密碼,然後在瀏覽器逐項驗證並記錄結果:
1. 用 `admin` 與該密碼登入成功(審查重點 3);導覽列**只有**「工單」「帳號管理」與登出。
2. 工單:新增一張(選**兩個以上**服務類別)→ 列表看得到、編號正確;編輯內容並儲存;作廢後預設列表不再顯示。
3. 帳號管理:建立一個 `WorkStudy` 使用者(密碼至少 8 碼含大小寫與數字)。
4. 登出後用該 `WorkStudy` 使用者登入:看得到「工單」、**看不到**「帳號管理」;直接開 `http://127.0.0.1:5299/account/manage-users` 被擋(導向登入或拒絕頁,不是頁面內容)(審查重點 4)。
5. 已刪除的舊網址 `/schedule`、`/sop`、`/shift-summary`、`/tools/password-generator`:每個都回應「找不到」頁面(NotFound),不是 500 或當機(審查重點 5)。可用 `evaluate_script` 或 `navigate_page` 後看頁面文字與 `list_console_messages`。
6. 主控台(console)沒有錯誤。
7. 至少截 2 張圖(登入後的導覽列與工單列表、找不到頁面)存到 scratchpad,並用 Read 看過確認可讀,把觀察寫進報告。

- [ ] **步驟 3:清理**

結束你啟動的程序(只殺 5299 那個,**不要動 5252**),`lsof -iTCP:5299 -sTCP:LISTEN` 確認沒有殘留;確認 repo 內沒有 `.db`、log、截圖(`/usr/bin/git status --short` 為空,且 `find . -name "*.db" -not -path "./.git/*"` 在 worktree 內沒有結果);暫存資料庫與 log 留在 scratchpad 即可。
