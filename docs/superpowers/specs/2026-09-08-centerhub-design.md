# CenterHub 設計規格

## 背景與目標

電算中心工讀生目前用 Evernote（服務工單記錄）+ HackMD（班別彙總統計）+ Excel + Word（報表）拼裝管理值班交接與 SOP 知識。核心痛點：

- 班別彙總統計（防衛率、單位×類別交叉表）目前是人工把 Evernote 工單內容手動結算、貼進 HackMD，非常花時間；現有一個「貼上 Evernote 內容產生彙總表」的輔助小工具，但仍需人工介入。
- SOP、交接資訊分散在多個工具，新接手的人不容易找到權威版本。
- 換班/請假、班表管理沒有系統化流程。

目標：開學前（1~2 週內）做出一套整合式的內部工具，取代上述拼裝方案，並且要讓「未來接手的資訊/電機系工讀生」容易維護與擴充——這是僅次於「先求可用」的第二優先目標。

## 使用者與規模

- 主要使用者：電算中心工讀生（10 人以下），角色分兩級：**工讀生**、**管理員**。
- 使用情境：以值班室內電腦操作為主，手機只需「能勉強看」，不特別做響應式優化。
- 未來維護者：資訊/電機相關科系工讀生，具備物件導向程式基礎，但未必有 Web 開發實戰經驗。

## 技術棧決策

**ASP.NET Core + Blazor Server + EF Core**，資料庫先用 SQLite，部署在電算中心 Windows 主機。

決策理由（詳細比較見討論記錄，此處摘要）：

- 部署環境是 Windows 採購機，.NET 是原生選項，文件與踩坑經驗都比在 Windows 上部署 Python/Django 生態更成熟。
- Blazor Server 讓前後端統一用 C#，開發者（目前使用者）不需要依賴自己還不熟的 JavaScript。
- 對「未來由資訊/電機系工讀生接手」這個目標而言：單一語言、Visual Studio/Rider 的圖形化專案總管、強型別在編譯期就能抓到低階錯誤，都能降低新人上手與誤改程式碼的風險。
- ASP.NET Core Identity 內建帳號/角色系統，EF Core 內建 ORM + Migration，符合兩週 MVP 的時程壓力。
- 開發環境：全程可留在 macOS 上開發（.NET SDK 官方支援 macOS，含 Apple Silicon；用 VS Code + C# Dev Kit 或 JetBrains Rider），只有正式上線前需要在 Windows 環境驗證一次部署流程（IIS 或 Windows Service 常駐）。

## 整體架構

單一 ASP.NET Core Blazor Server 專案，依功能拆成 Feature 資料夾，共用一個 EF Core + SQLite 資料庫：

```
CenterHub/
├── Data/AppDbContext.cs + Migrations/
├── Features/
│   ├── Tickets/        # 服務工單（核心，取代 Evernote）
│   ├── ShiftSummary/   # 班別彙總統計（自動算，取代人工填 HackMD）
│   ├── SopWiki/        # SOP 知識庫（含版本歷史）
│   ├── Schedule/       # 班表 + 換班/請假申請
│   ├── Reports/        # 統計圖表 + Word 匯出（第二期）
│   └── Tools/          # 密碼產生器等小工具
├── Components/Pages/   # 對應各模組的 .razor 頁面
└── Program.cs          # Identity、DI、路由設定
```

`ShiftSummary` 不是獨立資料表，而是從 `Ticket` 資料即時運算出來的檢視/服務——這是取代人工結算痛點的關鍵設計。

## 資料模型

### 帳號與角色

沿用 ASP.NET Core Identity 內建的 User 表，加一個角色欄位區分「工讀生」/「管理員」。未來若學校 SSO 談成，只需替換登入的驗證來源，User 表結構基本不需要改動（目前不確定學校是否有可介接的兩別/SSO 介面，先做站內帳號，管理員預先建帳號給工讀生）。

建帳號時必須填寫真實 Email（Identity 內建欄位），作為換班/請假通知信的收件地址——這是實作規劃時發現的相依性：通知信必須寄到真人信箱，不能用帳號登入用的內部使用者 ID 頂替。

### Ticket（服務工單）— 核心表

| 欄位 | 說明 |
|---|---|
| DailySeq | 當日編號，依日期自動流水 |
| ContactTime | 聯絡時間 |
| RequestingUnit | 申請單位（自由輸入文字，因單位多樣不易預列；輸入框做歷史紀錄自動完成，減少手打不一致） |
| RequesterName / RequesterContact | 申請人 / 分機或學號 |
| ServiceMode | 服務性質：到場處理 / 電話線上 |
| ServiceCategories | 服務類別（可複選，來自獨立表：硬體/軟體/網路/郵件/重灌/中毒/無線/諮詢/印表機/其他，管理員可新增） |
| ResolutionStatus | 處理結果：已完成 / 未完成 |
| SatisfactionRating | 服務滿意度 1~5，可空 |
| ServiceDurationMinutes | 服務時間（分） |
| OperatingSystem | 作業系統 |
| ProblemDescription / SolutionDescription | 問題簡述 / 解決方法（多行文字） |
| CreatedByUserId, ShiftDate, ShiftPeriod | 值班人、值班日期、班別（AM/PM），用於歸類到對應班次彙總 |

### ShiftSummary（班別彙總，計算視圖，非資料表）

依 `ShiftDate + ShiftPeriod + CreatedByUserId` 分組，從 Ticket 資料即時算出：

- 總服務案件數（T）
- 防衛率（S/T）：**S（自行解決）= 已完成筆數，F（轉介其他單位）= 未完成筆數**，兩者由 `ResolutionStatus` 直接推導，不需額外欄位或規則（已與使用者確認：現行工具本來就是用這個邏輯從 Evernote 內容推算）
- 總服務時間
- 單位 × 服務類別交叉表（時間/件數）

此範圍在第一期即可完整實作，不需等待 Word/Excel 範本。

### SopArticle（SOP 知識庫）

- `SopArticle`：Title、CategoryId、Content、CreatedByUserId、CreatedAt、UpdatedAt、RowVersion（樂觀鎖，防止並行編輯覆蓋）
- `SopCategory`：管理員可維護的分類清單
- `SopArticleRevision`：每次修改留下內容快照、修改人、修改時間，滿足「工讀生可編輯但留痕」的需求

### Schedule（班表）

- `ShiftSlot`：日期 + 班別 + 指定工讀生
- `ShiftChangeRequest`：換班或請假（Type 區分），狀態機：待審核 → 已核准 / 已拒絕（不可跳過或重複審核），管理員審核

### Tools

`PasswordGenerator`：無資料表，純無狀態工具頁，使用加密安全亂數（`RandomNumberGenerator`）產生，按鈕觸發、可複製，與工單流程無關聯。

## 頁面結構與操作流程

| 頁面 | 使用者 | 功能 |
|---|---|---|
| 工單列表 + 新增/編輯 | 工讀生、管理員 | 篩選（日期/單位/類別/完成狀態）、新增工單表單、當日編號自動帶出 |
| 班別彙總 | 工讀生、管理員 | 選日期+班別，自動顯示 T/S/防衛率、單位×類別表 |
| SOP 知識庫 | 工讀生、管理員 | 分類瀏覽、全文搜尋、文章檢視/編輯、修改歷史 |
| 班表 | 工讀生、管理員 | 檢視當週/當月班表；工讀生可發起換班/請假；管理員審核 |
| 密碼產生器 | 工讀生、管理員 | 獨立小工具頁 |
| （第二期）統計報表 | 管理員 | 期間篩選、圖表呈現、匯出 Word |

登入後依角色顯示選單：工讀生看不到「使用者管理」；管理員多一個帳號管理入口（建立/停用工讀生帳號）。

## 錯誤處理與資料驗證

- **表單驗證**：工單必填欄位（聯絡時間、服務性質、至少一個服務類別、處理結果）用 Blazor 內建 `DataAnnotations`（如 `[Required]`）在送出前擋掉，錯誤訊息即時顯示在對應欄位旁。
- **並行編輯衝突**：SOP 文章用 `RowVersion` 樂觀鎖，偵測到版本不一致時提示「這篇文章已被其他人修改，請重新整理後再編輯」。
- **換班/請假狀態機**：限制只能從「待審核」轉到「已核准/已拒絕」，防止重複審核或誤按。
- **未預期錯誤**：全域 `ErrorBoundary` 攔截，畫面顯示友善訊息，詳細例外寫入伺服器端 log，不暴露技術細節給使用者。
- **Email 通知失敗**：通知走背景佇列處理，失敗重試並記錄，但不擋住主流程（換班申請一定要先成功存檔，通知能否寄出是次要）。

## 通知需求

換班/請假待審核、上班前提醒等場景，用 Email 發送即可（不需要即時通訊軟體整合）。

## 測試策略

小型內部工具、單人兩週 MVP 時程，測試力氣優先放在「壞了會很痛」的邏輯，不追求形式上的覆蓋率數字：

- **單元測試（優先）**：ShiftSummary 彙總運算邏輯（T/S/防衛率、單位×類別交叉表）——這是取代人工結算的核心價值，算錯會直接影響對外報告正確性，必須有測試覆蓋。
- **整合測試**：工單新增/編輯流程（含當日編號自動遞增邏輯）、換班申請狀態轉換，使用 EF Core In-Memory 或 SQLite in-memory provider。
- **手動驗收**：SOP 編輯/版本歷史、密碼產生器、依角色顯示選單，用人工檢查即可，不特別寫自動化測試。

## 分期規劃

**第一期（MVP，目標 1~2 週）**：
- Ticket 工單 CRUD + 篩選
- ShiftSummary 自動彙總（含防衛率）
- SOP 知識庫（含版本歷史）
- 班表 + 換班/請假申請與審核
- Email 通知
- 密碼產生器工具
- 帳號系統（站內帳號，兩級角色）

**第二期**：
- 統計圖表視覺化
- 匯出 Word 報表
- 視使用者提供的 Word/Excel 範本，微調 ShiftSummary 欄位與報表格式

## 資料搬遷

不搬移 Evernote/HackMD/Excel 舊資料，新系統上線當天開始記錄，舊工具保留供查閱歷史存檔。

## 成功標準

- 工讀生真的改用新系統，不再回頭用 Evernote/HackMD/Excel（採用率是最直接的指標）
- 交接品質變好：下一班能看懂上一班交代的事項
- 即使推廣不順利、未能讓所有人都換，對開發者本人而言也能作為完整的作品/專案經驗

## 待確認事項（Open Items）

1. 學校是否有可介接的帳號/SSO 介面（LDAP/OAuth 等）——目前先做站內帳號，登入層設計會保留未來替換驗證來源的彈性。
2. Word/Excel 報表範本——使用者稍後提供，將用來微調第二期報表格式與可能的 ShiftSummary 欄位細節。
3. `RequestingUnit`（申請單位）目前設計為自由輸入 + 歷史自動完成，若日後單位命名混亂造成統計失真，可考慮補建議清單或後台正規化工具。
