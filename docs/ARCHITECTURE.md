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
