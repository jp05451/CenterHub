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
- **Blazor Server 每個 circuit 共用一個 `AppDbContext`,** 追蹤中的實體不會被重新讀取:從另一個分頁看到舊資料時,先想到這一點(必要時 `ChangeTracker.Clear()` 或用 `AsNoTracking()`)。
- **`Program.cs` 的 middleware 順序是刻意的:** `UseAuthentication` → `UseAuthorization` → `UseAntiforgery`。

## 測試慣例

- xUnit 搭配 `Assert.*` 與 EF Core **InMemory** provider(`UseInMemoryDatabase(Guid.NewGuid().ToString())`);不用 Moq 或 FluentAssertions。
- InMemory 不會強制唯一索引、外鍵或 cascade 規則。任何重要的規則都必須在 Service 程式碼裡自己擋,並在那裡測試。
- `[Authorize]` 的行為沒有自動化測試;頁面權限要手動驗證。
