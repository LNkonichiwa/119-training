# OrderHub — 專案記憶

## 專案簡介

公司內部訂單管理系統：業務可建立/查詢訂單、管理商品與客戶。
內部使用、單一 SQL Server 資料庫，不需要考慮多租戶或高併發架構。

## 技術棧

- .NET 8 / ASP.NET Core MVC（Razor Views）
- EF Core 8 + SQL Server
- 測試：xUnit（EF Core InMemory，不需要 SQL Server）

## 分層與慣例

- 三層：`OrderHub.Web`（Controller/View/ViewModel）→ `OrderHub.Core`
  （Domain/Services/Interfaces）→ `OrderHub.Infrastructure`（Repositories/Migrations）
- Controller 保持薄，只轉接 service 結果；商業邏輯一律放 Core 的 service
- 只有 repository 碰 `DbContext`；Controller / Service 不可直接用 EF Core
- Service 回傳 `ServiceResult<T>`，用它表達預期內的失敗，不要丟例外
- View 綁 ViewModel，不要把 domain model 直接丟給 View
- 使用者輸入用 DataAnnotations + ModelState 驗證；輸入錯誤絕不能變成 500
- 金額一律用 `decimal`；折扣集中在 `OrderService`，不要在別處重算
- 參考檔：Controller 照 `ProductsController.cs`、Service 照 `ProductService.cs` 的寫法

## 常用指令

- `dotnet build`：建置
- `dotnet test`：跑全部測試
- `dotnet run --project src/OrderHub.Web`：啟動網站（http://localhost:5150）

## 重要 / 危險檔案

- `src/OrderHub.Infrastructure/Migrations/**`：EF migration 是歷史紀錄，不要手改
- `src/OrderHub.Web/appsettings.json`：連線字串等設定，改動前先問

## 不要做的事

- 不要未經同意就加新的 NuGet 套件
- 不要在 Controller / Service 直接使用 DbContext
- 不要為了「順手」重構與當前任務無關的程式碼
- 不要讀取或寫入任何機密檔（*.pfx、appsettings.Production.json、user-secrets）
