# 貢獻指南

CenterHub 是電算中心的內部工具，目前由少數工讀生/未來接手者維護。這份文件是給任何要新增功能、修 bug、或接手維護這個專案的人看的協作規範。

## 分支與 PR 流程

- **不要直接在 `main` 上開發**，即使是很小的改動。從 `main` 開新分支：
  ```bash
  git checkout main
  git pull
  git checkout -b feature/<簡短描述>   # 或 fix/<簡短描述>
  ```
- 分支命名用小寫、連字號分隔，字首依性質選：`feature/`、`fix/`、`docs/`、`refactor/`、`chore/`。
- 改完、測試通過後推上遠端，開 Pull Request 到 `main`，等至少一位其他維護者 review 過再合併。
- PR 說明請包含：改了什麼、為什麼改、怎麼測試過（見下方「測試」）。
- 合併方式偏好 **squash merge** 或一般 merge 皆可，但避免把一堆「fix typo」「fix again」的中間 commit 直接攤平進 `main` 的歷史。

## Commit 訊息格式

```
<type>: <一句話描述>

<可選的補充說明>
```

`type` 用：`feat`、`fix`、`refactor`、`docs`、`test`、`chore`、`perf`、`ci`。例如：

```
fix: 工單編輯未檢查是否為承辦人
```

## 新增一個 Feature 模組的慣例

CenterHub 依功能（不是依技術層）拆資料夾。新增一個功能時，照現有模組（例如 `Tickets`）的樣子建立對應結構：

```
CenterHub/Features/<功能名>/
├── <功能名>Service.cs        # 業務邏輯，注入 AppDbContext
├── <相關 Model/DTO>.cs        # 若有專屬於這個功能的資料傳輸物件
CenterHub/Components/Pages/<功能名>/
└── *.razor                    # 對應的 Blazor 頁面
CenterHub.Tests/
└── <功能名>ServiceTests.cs    # 針對 Service 層的 xUnit 測試
```

- Service 類別用建構子注入 `AppDbContext`，註冊為 Scoped（參考 `Program.cs` 既有的 `AddScoped<...>()` 那幾行）。
- 新的資料表要透過 EF Core Migration 新增，不要手動改資料庫檔案：
  ```bash
  dotnet ef migrations add <描述性名稱> --project CenterHub
  dotnet ef database update --project CenterHub
  ```
  （沒裝 `dotnet-ef` 工具的話先 `dotnet tool install --global dotnet-ef`。）
- 需要角色限制的頁面用 `[Authorize(Roles = Roles.Admin)]` 或 `[Authorize(Roles = Roles.WorkStudy)]`（角色常數定義在 `CenterHub/Models/Roles.cs`），不要把角色字串直接寫死在頁面裡。

## 測試

```bash
dotnet test
```

- 新邏輯，尤其是「壞了會直接影響對外數據或造成資料損毀」的部分（工單編號、編輯/作廢的權限檢查），一定要有對應的 xUnit 測試，跟隨 `CenterHub.Tests` 現有測試的 Arrange-Act-Assert 寫法。
- 單純畫面呈現、樣式調整可以用手動驗收，不用為每個頁面都寫測試。
- PR 前確認 `dotnet build` 沒有警告/錯誤、`dotnet test` 全過。

## Code Review 期望

Review 別人的 PR、或請別人 review 自己的 PR 時，優先看：

1. **正確性**：邏輯是否符合預期？有沒有漏掉的邊界情況（例如空白輸入、沒有承辦人的工單）？
2. **權限控管**：涉及帳號、角色、資料存取的改動有沒有正確加上 `[Authorize]`？
3. **並行/資料完整性**：改到工單編輯、作廢這類共用狀態的地方，有沒有確認過並行編輯的行為？
4. **測試覆蓋**：新邏輯有沒有測試？既有測試有沒有因為這次改動而需要更新？

不需要為了風格吹毛求疵——這是小團隊的內部工具，可讀性與正確性優先於形式規範。
