# CenterHub

電算中心值班交接系統 — 取代 Evernote + HackMD + Excel + Word 拼裝流程的內部工具。

工讀生用它記錄服務工單（申請單位、申請人、服務性質與類別、處理結果、服務時間等），並以帳號與角色（`Admin` / `WorkStudy`）控管誰能編輯或作廢。設計背景與決策理由見 [`docs/superpowers/specs/2026-09-08-centerhub-design.md`](docs/superpowers/specs/2026-09-08-centerhub-design.md)（原始設計，包含後來移除的功能）；現況架構速覽見 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)。

## 技術棧

- **ASP.NET Core 10 + Blazor Server**（InteractiveServer render mode）
- **EF Core 10 + SQLite**
- **ASP.NET Core Identity**（`Admin` / `WorkStudy` 兩種角色）
- **xUnit**（單元/整合測試）

單一專案、依功能拆成 `Features/` 資料夾，前後端統一用 C#，不需要另外的前端建置流程。

## 需求環境

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- 任一編輯器：Visual Studio、JetBrains Rider、或 VS Code + C# Dev Kit
- 開發階段可全程在 macOS / Windows / Linux 上進行；正式部署目標是電算中心的 Windows 主機（見 [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md)）

## 本機建置與執行

```bash
git clone <repo-url>
cd ComputerCenter_System

# 還原套件、建置
dotnet restore
dotnet build

# 執行（會在啟動時自動套用 migration、建立角色與管理員帳號）
dotnet run --project CenterHub
```

啟動後開啟 `https://localhost:7274`（或 `http://localhost:5252`）。資料庫檔案 `centerhub.db` 會建立在 `CenterHub/` 目錄下（已被 `.gitignore` 排除，不會進版控）。

### 第一次登入

啟動時若資料庫裡還沒有 `admin` 帳號，系統會自動建立一個：

- 若透過 `appsettings.json`、環境變數 `Seed__AdminPassword`、或 user-secrets 設定了 `Seed:AdminPassword` → 以該處設定的密碼為準（本機開發可用 user-secrets 設定，不要寫進進版控的設定檔）。
- 完全沒有設定（預設情況，含 `dotnet run` 的本機開發環境）→ 系統會用內建密碼產生器隨機產生一組密碼，並**印在啟動時的 console log**（`Seeded initial admin password: ...`），登入後請盡快更改。
- **正式部署時務必自行設定一組密碼，不要依賴 log 產生的密碼**（見 [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md)）。

> **從舊版升級：** migration 已重置為單一 `InitialCreate`。舊版產生的 `centerhub.db`（含舊 migration 紀錄與已移除功能的資料表）不能沿用，啟動會因為資料表已存在而失敗。請先把舊的 `centerhub.db` 改名備份，讓新版建立全新的資料庫。

## 執行測試

```bash
dotnet test
```

測試專案 `CenterHub.Tests` 用 xUnit，涵蓋工單（編號、篩選、編輯/作廢的權限）與種子管理員密碼產生等核心邏輯（詳見 [`CONTRIBUTING.md`](CONTRIBUTING.md#測試) 的測試慣例）。

## 專案結構

```
CenterHub/
├── Data/               # AppDbContext + EF Core Migrations
├── Features/           # 依功能拆分的服務層（目前只有 Tickets）
├── Components/Pages/   # 對應各功能模組的 Blazor 頁面
├── Models/             # 資料模型與 Identity 使用者/角色
└── Program.cs          # DI 註冊、Identity、路由、啟動時的種子資料

CenterHub.Tests/         # xUnit 測試專案，逐一對應 Features/ 底下的服務
docs/
├── ARCHITECTURE.md      # 現況架構速覽
├── DEPLOYMENT.md        # 正式環境部署步驟
└── superpowers/         # 原始設計規格與實作計畫（歷史決策紀錄）
```

## 想參與開發？

新增功能、修 bug、送 PR 前請先讀 [`CONTRIBUTING.md`](CONTRIBUTING.md) — 裡面有分支/commit 慣例、如何新增一個 Feature 模組、以及 code review 期望。

## 授權

內部工具，僅供電算中心使用，未另行開源授權。
