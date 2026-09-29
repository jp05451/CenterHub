# 部署文件

CenterHub 的正式部署目標是電算中心的 Windows 主機。目前尚未決定要用 IIS 反向代理還是獨立 Windows Service，這份文件先把兩種常見做法都寫清楚——正式上線前依實際主機環境挑一種即可。

## 事前準備

1. Windows 主機上安裝 **[.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)**（同時包含 ASP.NET Core Runtime 與 IIS 整合模組，即使最後選獨立 Windows Service 也建議裝這個版本而不是只裝 Runtime，避免之後想換方式要重裝）。
2. 確認主機防火牆開放要對外服務的埠（例如 443/80，或自訂埠）。
3. 準備一張正式的 TLS 憑證（`Program.cs` 在非 Development 環境會執行 `UseHsts()` + `UseHttpsRedirection()`，沒有有效憑證瀏覽器會被擋）。

## 設定機密資訊

`CenterHub/appsettings.json` 裡的機密欄位**不要直接改這個檔案送進版控**，改用下列任一方式在主機上覆寫：

| 設定鍵 | 用途 | 建議來源 |
|---|---|---|
| `ConnectionStrings:Default` | SQLite 檔案路徑 | 環境變數 `ConnectionStrings__Default`，或 `appsettings.Production.json`（不進版控） |
| `Seed:AdminPassword` | 首次啟動要用的管理員密碼；留空則自動產生並印在 log | 環境變數 `Seed__AdminPassword`，正式環境建議明確設定，避免密碼只留在 log 裡 |

環境變數命名規則：巢狀設定鍵用雙底線 `__` 取代冒號，例如 `Seed:AdminPassword` → 環境變數 `Seed__AdminPassword`。

`CenterHub/appsettings.Production.json` 這個檔案本身**有**送進版控，內容只放 Kestrel 監聽位址設定（預設 `http://0.0.0.0:5252`，讓正式環境的處理程序監聽所有網路介面，不只 localhost），**不是**用來放機密資訊——上表列的機密欄位仍然只能透過環境變數（或主機層級的密碼管理工具）覆寫，絕對不要把它們寫進這個檔案。若某台主機需要改用不同的埠或改成 HTTPS 繫結，可以直接編輯這個檔案，或用環境變數 `Kestrel__Endpoints__Http__Url` 覆寫。

### 管理員種子帳號

`Program.cs` 目前把種子管理員的 Email 寫成佔位符 `admin@example.edu`。系統目前沒有任何寄信功能，所以這個信箱不會收到通知，但它是帳號資料的一部分；正式部署前建議改成真實信箱。目前程式碼裡是寫死的常數，若要在不同環境用不同 Email，需要先把這行改成讀取設定值。

## 建置與套用 Migration

```bash
dotnet publish CenterHub -c Release -o ./publish
```

Migration 只有單一 `InitialCreate`（帳號與工單）。**舊版產生的 `centerhub.db` 不能沿用**（它含舊的 migration 紀錄與已移除功能的資料表，啟動會因為資料表已存在而失敗）。升級前請先把舊的 `centerhub.db` 改名備份，讓新版建立全新的資料庫。

Migration 不需要另外手動套用——`Program.cs` 啟動時會自動呼叫 `db.Database.Migrate()`。若想在部署前先確認會套用哪些 migration，可在本機執行：

```bash
dotnet ef migrations list --project CenterHub
```

## 選項一：IIS 反向代理

1. 在 IIS 管理員建立新的應用程式集區，**.NET CLR 版本設為「沒有受管理的程式碼」**（No Managed Code）——ASP.NET Core 不透過傳統 CLR 託管。
2. 將 `./publish` 資料夾內容複製到 IIS 網站的實體路徑（例如 `C:\inetpub\centerhub`）。`dotnet publish` 會自動產生 `web.config`，內含 ASP.NET Core Module (ANCM) 設定，通常不需要手動修改。
3. 在該應用程式集區底下建立網站或應用程式，繫結 HTTPS 憑證。
4. 啟動網站，確認 `C:\inetpub\centerhub\logs`（或 `web.config` 裡設定的 stdout log 路徑）沒有啟動錯誤。
5. 之後更新版本：重新 `dotnet publish` → 覆蓋檔案前先在 IIS 管理員「停止」該應用程式（避免檔案鎖定）→ 覆蓋 → 重新啟動。

## 選項二：獨立 Windows Service

不依賴 IIS，直接把 Kestrel 常駐成背景服務：

```powershell
sc.exe create CenterHub binPath= "C:\centerhub\CenterHub.exe" start= auto
sc.exe description CenterHub "CenterHub 值班交接系統"
sc.exe start CenterHub
```

- `binPath` 指向 `dotnet publish` 產出的可執行檔（自我裝載的發佈輸出裡會有 `CenterHub.exe`）。
- 環境變數（如 `Seed__AdminPassword`）要設在**系統層級**或用 `sc.exe` 搭配登錄機碼設定，服務啟動時才讀得到；不要只設在互動式使用者的 session 環境變數裡。
- 需要對外提供 HTTPS 時，`Program.cs` 目前沒有另外設定 Kestrel 憑證繫結，需在 `appsettings.Production.json` 或環境變數補上 `Kestrel:Endpoints:Https:Certificate` 相關設定，或在前面加一層反向代理（例如 IIS ARR、或另一台已有憑證的服務）處理 TLS 終止。
- 停止/更新版本：`sc.exe stop CenterHub` → 覆蓋發佈檔案 → `sc.exe start CenterHub`。
- 若偏好比 `sc.exe` 更好操作的服務管理工具，也可以用 [NSSM](https://nssm.cc/) 包裝同一個可執行檔，介面較友善（設定環境變數、log 導向都有 GUI）。

## 上線後檢查清單

- [ ] 用種子管理員帳號成功登入，並立刻更改密碼
- [ ] 確認 `Seed:AdminPassword` 或首次啟動 log 沒有殘留在任何會被存取的地方（例如公開的 log 收集系統）
- [ ] 建立一筆測試工單並確認可以編輯、作廢
- [ ] 確認 HTTPS 憑證有效、`http://` 會自動導向 `https://`
- [ ] 確認資料庫檔案（`centerhub.db`）所在磁碟有納入主機既有的備份機制
