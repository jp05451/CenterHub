# 只留工單:移除其他功能 — 設計規格

日期:2026-09-29
分支:`refactor/tickets-only`(從本機 `main` 的 `a58aad6` 分出,在獨立 worktree `.claude/worktrees/tickets-only` 內進行)

## 背景與目標
專案累積了班表、換班審核、班別彙總、SOP 知識庫、密碼產生器、Email 通知等功能,其中班表最複雜,已經開始難以維護。決定**先整個砍掉,回到最小核心:工單記錄加登入/帳號**,之後再依需要重新設計其他功能(尤其是班表)。

## 已確認的決定(使用者於 2026-09-29 回答)
1. 「紀錄工作的功能」= **工單**。班別彙總、SOP、班表(含換班審核)、密碼產生器、Email 通知全部刪除。登入與帳號管理一定保留(工單需要知道承辦人)。
2. 資料庫:**重置 migration,從頭來**(刪掉全部舊 migration,重新產生單一 `InitialCreate`)。
3. 現有資料庫裡的資料都是測試資料,**不需要保留**,使用全新的資料庫。
4. 用**獨立 worktree + 新分支**進行,`feat/schedule-periods` 分支與使用者現有的工作資料夾完全不動,留作存檔。
5. 在新分支**原地刪除**(不是開全新專案再搬檔案),用 git 保留歷史,每一塊功能各一個 commit。

## 保留的範圍
- **工單:** 新增 / 編輯 / 作廢(軟刪除)、服務類別(`ServiceCategory`,含 10 筆種子資料)、`TicketCategory`。工單上的 `ShiftDate` / `ShiftPeriod` / `DailySeq` 欄位保留,它們是工單自己的當日編號規則,不是班表;因此 `ShiftPeriod` enum 保留。
- **登入 / 帳號:** `Login`、`Logout`、`ManageUsers`(僅 Admin)、`ApplicationUser`、角色 `Admin` / `WorkStudy`、啟動時建立角色與 `admin` 帳號。
- **骨架:** `App`、`Routes`、`MainLayout`、`NavMenu`、`ReconnectModal`、`Home`、`Error`、`NotFound`、`RedirectToLogin`、`wwwroot/app.css`。
- 導覽列只剩:「工單」;Admin 額外看到「帳號管理」。

## 要刪除的內容
| 區塊 | 刪除項目 |
|---|---|
| 班表 + 換班審核 | `Features/Schedule/*`、`Components/Pages/Schedule/*`、`Models/ShiftSlot.cs`、`Models/ShiftChangeRequest.cs`、`Enums.cs` 中的 `ShiftChangeType`、`ShiftChangeStatus`、`AppDbContext` 中的 `ShiftSlots` / `ShiftChangeRequests` 及其設定、`Program.cs` 的 `ShiftChangeRequestService` 註冊、測試 `ShiftChangeRequestServiceTests.cs` |
| 班別彙總 | `Features/ShiftSummary/*`、`Components/Pages/ShiftSummary/*`、`Program.cs` 註冊、測試 `ShiftSummaryServiceTests.cs`(沒有自己的資料表) |
| SOP 知識庫 | `Features/SopWiki/*`、`Components/Pages/SopWiki/*`、`Models/Sop*.cs`(`SopArticle`、`SopArticleRevision`、`SopCategory`)、`AppDbContext` 的三個 DbSet、`RowVersion` 設定與 6 筆 `SopCategory` 種子、`Program.cs` 註冊、測試 `SopArticleServiceTests.cs` |
| 密碼產生器 | `Components/Pages/Tools/PasswordGenerator.razor`、`Features/Tools/*`、`Program.cs` 註冊、測試 `PasswordGeneratorServiceTests.cs`(邏輯移轉見下) |
| Email 通知 | `Features/Notifications/*`、`Program.cs` 的 `IEmailSender` / `EmailNotificationQueue` / HostedService 註冊、`appsettings*.json` 的 `Smtp` 區塊、測試 `EmailNotificationQueueTests.cs`(只有換班申請在用) |
| 導覽列 / 首頁 | `NavMenu.razor` 移除班別彙總、SOP、班表、密碼產生器、換班審核;`Home.razor` 文字維持 |
| 舊規格 | `docs/superpowers/plans/2026-09-15-admin-schedule-editing.md`(針對已刪功能、從未實作) |

### 唯一的牽連:種子管理員密碼
`Program.cs` 啟動時若沒有設定 `Seed:AdminPassword`,會用 `PasswordGeneratorService.Generate()` 產生管理員密碼並記到 log。頁面與 Service 刪除後,需要保留這個能力:
- 把 `PasswordGeneratorService.Generate()` 的邏輯**原樣**移到一個小型靜態類別 `CenterHub/Data/AdminSeedPassword.cs`(`AdminSeedPassword.Generate()`),`Program.cs` 改呼叫它;重試迴圈與只在成功後才記 log 的行為維持不變。
- 把 `PasswordGeneratorServiceTests` 中仍適用的案例改成針對新類別(保留產生的密碼長度與字元類別的驗證),類別/檔名改為 `AdminSeedPasswordTests`。
- 既有的 `Generate(int length = 12)` 簽章與允許字元集(排除易混淆字元 `0/O`、`1/l/I`)原樣保留,只是從實例方法改成靜態方法。

### 只改註解、不改行為的兩處
`Features/Tickets/TicketService.cs` 第 65 行與 `Models/Ticket.cs` 第 35 行的註解提到 `ShiftSummary`(將被刪除的功能)。改寫成不依賴已刪功能的說法(例如說明這些欄位驅動的是工單自己的當日編號),程式碼本身不動。除此之外,`Features/Tickets/`、`Models/Ticket*.cs`、`Components/Pages/Tickets/`、`Components/Pages/Account/` 沒有任何被刪功能的引用(已用 grep 確認);`Smtp` 設定只出現在 `appsettings.json`;`TicketServiceTests` 完全不依賴被刪除的類別。

## 資料庫
- 刪除 `CenterHub/Migrations/` 內全部檔案(8 個 migration + snapshot),用 `dotnet ef migrations add InitialCreate --project CenterHub` 重新產生單一 migration。內容應只含:Identity 資料表、`Tickets`(含編輯/作廢欄位)、`ServiceCategories`(含 10 筆種子)、`TicketCategories`。不應出現任何 `Sop*`、`ShiftSlots`、`ShiftChangeRequests`。
- 不碰使用者現有的 `centerhub.db`。使用者需要自己把舊檔改名備份;新版啟動時會在該位置建立全新的空資料庫。驗證一律使用臨時資料庫。

## 文件
繁體中文、現在式,不寫修改紀錄式語句:
- `README.md`:功能描述、測試說明、專案結構改成只剩工單與帳號。
- `docs/ARCHITECTURE.md`:移除班表、班別彙總、SOP、Notifications、Tools 章節與相關的已知限制,保留請求管線、帳號與角色、Ticket;資料夾樹只列保留的部分。
- `docs/DEPLOYMENT.md`:移除 SMTP 設定與相關檢查項目;migration 一節改說明「初始 migration 只包含帳號與工單」。
- `CONTRIBUTING.md`:範例中提到 `SopWiki` 的地方改用 `Tickets`。
- `docs/superpowers/specs/2026-09-08-centerhub-design.md` 與 `plans/2026-09-08-centerhub-mvp.md`:**不修改內容**,視為歷史紀錄;在 `ARCHITECTURE.md` 開頭加一句說明它們描述的是包含已移除功能的原始設計。
- **`CLAUDE.md`(需使用者在審閱規格時確認):** `main` 上沒有 `CLAUDE.md`(它只存在於 `feat/schedule-periods`)。建議在新分支加入一份**精簡版**,只含仍適用的內容:指令(build / test / ef)、Production 執行陷阱(launchSettings、publish、相對路徑的 SQLite、機密設定)、工單相關的架構重點、測試慣例,以及使用者寫的規則「重要!!所有計畫、規格與輸出都必須使用繁體中文」。全部繁體中文。

## 不做的事
- 不改工單與帳號功能的任何行為,`TicketService` / `TicketServiceTests` 不受影響。
- 不動 `feat/schedule-periods` 分支、使用者的主工作資料夾、`centerhub.db`。
- 不推送、不合併;合併與否由使用者決定。
- 不順手重構、不加新功能。

## Commit 切分(每個 commit 都必須 build 零警告、測試全過)
1. 移除班表與換班審核(先刪它,因為它依賴 Notifications)
2. 移除班別彙總
3. 移除 SOP 知識庫
4. 移除密碼產生器頁面與 Service,種子密碼邏輯移到 `AdminSeedPassword`
5. 移除 Email 通知與 Smtp 設定
6. 重置 migration 為單一 `InitialCreate`
7. 更新文件、加入精簡版 `CLAUDE.md`

## 驗證
1. 基線:分支起點 `dotnet build` 0 警告 0 錯誤、`dotnet test` 42 個全過(已確認)。
2. 完成後:`dotnet build` 0 警告、0 錯誤;`dotnet test` 全過(只剩 `TicketServiceTests` 與 `AdminSeedPasswordTests`)。
3. 殘留檢查:在 `CenterHub/`、`CenterHub.Tests/`、`docs/`(不含歷史規格與計畫)、`README.md`、`CONTRIBUTING.md` 中搜尋 `Sop`、`Schedule`、`ShiftSlot`、`ShiftChange`、`ShiftSummary`、`Smtp`、`EmailNotification`、`PasswordGenerator`,除了被明確保留的以外(如 `ShiftPeriod` 屬於工單、歷史規格)不應有結果。
4. 瀏覽器實測(用 chrome-devtools MCP,臨時資料庫,不碰 `centerhub.db`):
   - 空資料庫啟動成功、自動套用 `InitialCreate`、建立 `admin`。
   - `admin` 登入,導覽列只有「工單」「帳號管理」;新增(多個服務類別)、編輯、作廢工單;帳號管理建立一個 `WorkStudy` 使用者。
   - `WorkStudy` 登入,看得到工單、看不到「帳號管理」且直接開 `/account/manage-users` 被擋。
   - 已刪除的網址(`/schedule`、`/sop`、`/shift-summary`、`/tools/password-generator`)回應「找不到」,不是錯誤頁或當機。
   - 主控台沒有錯誤。
5. 完成後 `git status` 乾淨,repo 內沒有臨時資料庫、截圖或 publish 輸出。

## 風險與取捨
- 重置 migration 讓舊資料庫檔案無法使用(已確認資料可丟棄)。
- 班表相關的所有工作都留在 `feat/schedule-periods`(22 個 commit),之後重新設計班表時可參考或挑選,不會遺失。
- 刪除的範圍很大,所以切成 7 個獨立 commit,任何一步有問題都可以單獨還原。
