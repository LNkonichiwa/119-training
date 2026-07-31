# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：

Claude Code（Sonnet 5），搭配 Claude in Chrome 做瀏覽器端重現與驗證。

---

## 通用四問

### 1. 我的任務拆解

先做練習 1（CLAUDE.md、settings.json、hooks、subagents、fix-bug skill）並單獨 commit；
接著 `dotnet run` 把網站跑起來，用瀏覽器逐一重現三張客訴單；
每個 bug 都是「重現 → 讀 code 定位根因 → 修 → 跑 `dotnet test` 全綠 → 回瀏覽器複查 → 獨立 commit」。
順序沒有變，但每個 bug 修完都先用「暫時還原成 buggy 版本跑新測試」的方式，
確認新加的回歸測試真的會在修復前失敗（不是恆真斷言），這是原計畫裡沒有的一步，臨時加上。

### 2. AI 幫上大忙的地方

- 直接讀 `OrderRepository.GetPagedAsync` 抓到 `Skip(page * pageSize)` 應該是 `Skip((page - 1) * pageSize)`，
  一行程式碼配合瀏覽器實際建單觀察（新訂單 #201 不在第一頁、第 11 頁空白）就能百分之百定位，不用猜。
- 讀 `OrderService.CreateOrderAsync` 時發現 Gold 會員的折扣被在建立訂單當下就套用到 `UnitPriceSnapshot`，
  之後 `CalculateTotal` 又套用一次——對照 Silver 訂單（只套一次、正常）馬上能鎖定「只有 Gold 路徑」的差異。

### 3. AI 誤導我的地方，與我如何發現

沒有明顯誤導；但如果只讀程式碼不對照瀏覽器實測，庫存 bug（`CancelOrderAsync` 裡
`order.Status = Cancelled` 先執行，導致後面 `if (order.Status == Pending || Confirmed)`
恆假）很容易被誤判成「單純沒寫還原邏輯」，而不是「還原邏輯寫了但永遠進不去」。
先用瀏覽器建單、取消、回商品頁確認庫存數字沒有變動，再回頭讀 code 確認是這個 if 順序問題，
比反過來（先看 code 猜測）更準。

### 4. 我會帶回日常工作的一招

修 bug 時，寫完回歸測試後，先用 `git stash push -- <改動檔案>` 把修復本身暫時擋掉，
單獨跑新加的測試，確認它會失敗（且失敗訊息符合預期的錯誤數字），
再 `git stash pop` 恢復修復、跑全部測試確認全綠。
這樣能避免「測試永遠是綠的、但其實沒測到問題」的假陽性回歸測試。

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1

1. [x] Web/Core/Infrastructure 三層職責：Web 只做 Controller/View/ViewModel 接線；Core 放 domain、service 介面與商業邏輯（折扣、庫存、狀態轉移）；Infrastructure 放 EF Core DbContext、repository、migration、種子資料。
2. [x] `.claude/settings.json` 的 hook 兩支腳本都手動跑過確認：`block-destructive-sql.ps1` 對 `TRUNCATE TABLE ...` 回傳 exit code 2（擋下）；`log-edits.ps1` 對模擬的 PostToolUse payload正確寫入 `edit-log.txt`。
3. 商業邏輯放 Core 的 service（如 `OrderService`），新增頁面要動 Controller / Service+Interface / Repository+Interface / ViewModel / View 五個地方（練習 3 會實際驗證這點）。

練習 2

1. [x] 三個 bug 都先在瀏覽器（Claude in Chrome）重現：建單看第一頁/第11頁、Gold vs Silver 各建一張同商品訂單比對金額、建單再取消看庫存數字。
2. [x] 定位時用的是具體數字：訂單 #201（2026-07-24 10:27）不在第一頁、第11頁顯示「沒有符合條件的訂單」；SKU-1002（NT$2,320）Gold 訂單應付 NT$1,879.20（應為 2,088）而 Silver 訂單正確為 NT$2,204；SKU-1002 庫存 99→建單後98→取消後仍是98（應回 99）。
3. [x] 三個修復都回瀏覽器複查：#201 出現在新頁1頂端、頁11顯示1筆；新 Gold 訂單 #204 顯示 NT$2,088；建單#206 取消後庫存回到建單前的98。
4. [x] 每個 bug 都補了回歸測試，且用「先跑修復前版本確認測試會紅」的方式驗證測試本身有效；`dotnet test` 最終 33 個全綠。
5. [x] 三個獨立 commit（分頁、Gold折扣、庫存還原），message 都寫「症狀→重現數字→根因→修法」。
6. （思考題）為什麼原本的測試沒抓到這三個 bug？
   - 分頁測試只斷言 `TotalCount`/`TotalPages`，從沒檢查「頁1裡實際是哪幾筆」，所以 Skip 位移的 bug 完全不影響斷言。
   - `CreateOrder_SnapshotsCurrentUnitPrice` 只用預設（Standard）會員層級建單，從沒測過 Gold 專屬的折扣路徑。
   - 沒有任何測試斷言「取消訂單後庫存應該加回來」，只測了狀態變成 Cancelled，Cancel 對商業結果（庫存）的副作用完全沒被覆蓋。

練習 3

1. [x] `/Products/LowStock` 不帶參數 → 門檻 10 的結果（實測回傳 5 筆，庫存 2~4）；帶 `?threshold=20` → 結果變 7 筆，含庫存 17、18 的商品。
2. [x] `?threshold=0` 頁面顯示「門檻必須是大於 0 的整數」、`?threshold=-1` 同樣顯示驗證訊息；兩者 HTTP 狀態碼皆為 200，不是 500（用 `Invoke-WebRequest` 確認過）。
3. [x] 售出數量排除 Cancelled：service 測試 `GetLowStock_Sold30Days_ExcludesCancelledOrders` 用一筆 Confirmed（數量3）+一筆 Cancelled（數量100）驗證結果是 3 不是 103。
4. 停售商品不出現：repository 查詢帶 `p.IsActive` 條件，並有 `GetLowStock_ExcludesInactiveProducts` 測試覆蓋。
5. [x] 分層跟既有 Products 功能一致：Controller 只做 ViewModel↔Core 結果映射；EF 查詢在 `ProductRepository`；30 天門檻計算放在 `ProductService`（讓 repository 保持純查詢、好測）；驗證用 DataAnnotations `[Range]`，跟 `CreateOrderViewModel` 同一套。
6. [x] 3 個新測試（門檻過濾+排序、排除停售、售出數量排除 Cancelled），`dotnet test` 36 個全綠。

練習 4

1. [x] 重構後 `dotnet test` 36 個全綠，行為完全沒變。
2. 改善：把 `CreateOrderAsync` 裡「請求層級驗證」（明細非空、數量、重複商品）與「單行驗證」（商品存在/停售/庫存）拆成兩個獨立、不碰 DB 的私有靜態方法，方法本體變短、驗證規則各自可讀。沒改變：仍是同一個 `OrderService`、沒有新增 interface 或 DI 註冊、錯誤訊息文字逐字不變、呼叫端完全無感。
3. diff 只多兩個 private static method、原本的 if 判斷搬過去但文字不變——確認過沒有夾帶練習 3 以外的改動。

---

## 附錄：值得留下的對話片段

**片段 1（練習 2，客訴 1）**
問法：「開 http://localhost:5150/Orders，建一筆新訂單記下編號，回列表第一頁找找看；再點分頁的最後一頁。」
（實際操作：建立訂單 #201，回到 `/Orders` 第一頁——最上面是 07-15 的舊訂單，#201 完全不在頁1；點到頁11顯示「沒有符合條件的訂單」。）
回應摘要：agent 直接讀 `OrderRepository.GetPagedAsync`，指出 `Skip(page * pageSize)` 在 `page` 從 1 起算時會多跳過一頁，應改成 `Skip((page - 1) * pageSize)`；並解釋這同時解釋了「新單不在頁1」和「頁11空白」兩個症狀（同一根因），不是兩個獨立 bug。

**片段 2（練習 2，客訴 2）**
問法：「到 /Products 記下 SKU-1002 原價 NT$2,320 → 用 Gold 客戶建一筆該商品 x1 的訂單 → 明細頁應付 NT$1,879.20，手算應該是 2,320*0.9=2,088 → 再用 Silver 客戶做對照組，Silver 顯示 NT$2,204（2,320*0.95，正確）。」
回應摘要：agent 讀 `CreateOrderAsync` 發現只有 `customer.Tier == Gold` 時會把折扣先套進 `UnitPriceSnapshot`，`CalculateTotal` 之後又套一次；建議修法是「快照永遠存原價，折扣只在 `CalculateTotal` 算一次」，並主動指出既有測試 `CreateOrder_SnapshotsCurrentUnitPrice` 只測了 Standard 會員、從沒測到這條路徑。

---

## 第二階段 — 自建 MCP Server（活動 2）

### 練習 0 — 接 Playwright MCP

`claude mcp add playwright -- npx @playwright/mcp@latest`（在 `training-repo` 目錄下、local scope）執行成功，`claude mcp list` 確認 `playwright: ✔ Connected`。

對比活動 1 練習 2：現在同一組操作 agent 自己用瀏覽器工具做完，我只需要口頭描述「建一筆訂單，截圖結果頁」。差異不是「agent 比較聰明」，而是**多了一種工具**：沒有瀏覽器工具時，agent 對「頁面上發生了什麼」是瞎的，只能靠我口述症狀；有了以後，agent 能自己去看、自己去重現。

### 練習 1 — 建立 OrderHub MCP Server

`src/OrderHub.Mcp` 新增 3 個唯讀工具（`get_order`、`low_stock`、`customer_orders`），走 `IOrderService`/`IProductRepository`，折扣規則重用 `OrderService`，不重算。

踩到地雷區原文警告的雷：用 `dotnet run --project ... < 輸入檔` 直接測試時，stdout 完全是空的——`dotnet run` 自己的 stdio 轉發似乎會在 stdin EOF 後就把還沒 flush 完的回應吞掉。改成直接呼叫編譯後的 `.exe`、並用 `{ cat 輸入檔; sleep 3; }` 讓 stdin 晚一點關閉，兩個請求（`initialize`、`tools/list`）才正常收到回應。

### 練習 2 — 用 MCP Inspector 除錯

`npx @modelcontextprotocol/inspector dotnet run --project src/OrderHub.Mcp` 啟動後開瀏覽器連線：

1. [x] List Tools 顯示 `customer_orders`、`get_order`、`low_stock` 三個工具，description 與程式裡寫的逐字一致
2. [x] `low_stock`（threshold=10）回傳 SKU-1048（庫存2）、SKU-1005（庫存3）排第一二名，和 `/Products/LowStock` 頁面（練習 3 活動1）的順序完全一致
3. [x] `get_order`（id=999999）回傳 `"找不到訂單 999999"`，Tool Result 顯示 Success（工具本身正常執行，只是查無資料），不是 exception stack trace

### 練習 3 — 註冊給 agent，before/after 對照

`training-repo/.mcp.json` 建立並進 git（`{"mcpServers":{"orderhub":{"command":"dotnet","args":["run","--project","src/OrderHub.Mcp"]}}}`）。

`claude mcp list` 在 `training-repo` 目錄下執行，確認 CLI 有偵測到這個專案層級的 server：`orderhub: dotnet run --project src/OrderHub.Mcp - ⏸ Pending approval (run 'claude' to approve)`——這個「待核准」狀態本身就是重點：專案共用的 `.mcp.json` 不會自動被信任執行，要有人在真的開一個 session 時手動核准，才會真正連上。

**沒有 MCP 工具（用 sqlcmd 直接查 DB）：**
```
sqlcmd -S localhost -d OrderHubTraining -E -Q "SELECT Sku, Name, StockQuantity FROM Products WHERE StockQuantity < 5 AND IsActive = 1 ORDER BY StockQuantity ASC"
```
要自己想清楚要查哪張表、欄位名稱、拼 SQL、加 `IsActive` 條件——這些都是**只有讀過 code 或 schema 的人才知道的細節**。

**有 MCP 工具（透過 Inspector 呼叫 `low_stock`，threshold=5）：**
一次工具呼叫、參數只有一個 `threshold`，結果直接是結構化 JSON（`Sku`/`Name`/`StockQuantity`），兩邊資料一模一樣（SKU-1048=2、SKU-1005=3、SKU-1023=3、SKU-1032=4、SKU-1014=4）。

差異：沒有工具時，agent 要嘛去讀 `ProductRepository`/DbContext 猜表結構、嘛請人代勞跑 SQL；有了工具後，「庫存規則」（`IsActive`、排序方向）已經封裝在 server 裡，agent 只需要知道「有一個叫 low_stock 的工具，給它門檻就好」——**這正是 MCP 的核心價值：把『怎麼查』的知識從 agent 的推理搬到 server 的實作**，也是 Resource/Prompt 想解決的同一類問題（練習 5 會再碰到）。

### 練習 4 — 會改資料的工具：cancel_order

`cancel_order` 新增（`Destructive = true, Idempotent = false`），三個唯讀工具補上 `ReadOnly = true`。透過 Inspector 的 `tools/list` 展開 `annotations` 欄位逐一核對：

1. [x] `get_order` → `{ readOnlyHint: true }`；`cancel_order` → `{ destructiveHint: true, idempotentHint: false }`，和程式碼標註一致
2. [x] 先建立訂單 #209（蔡承翰，SKU-1001 x1，庫存 26→25），呼叫 `cancel_order(209)` → `"訂單 209 已取消，庫存已回補"`，回 `/Products` 確認 SKU-1001 庫存回到 26
3. [x] 對同一筆訂單（209，已是 Cancelled）**在完全獨立的第二次呼叫**再叫一次 `cancel_order` → `"取消失敗：狀態為 Cancelled 的訂單不可取消"`，清楚訊息，不是 exception dump；庫存仍是 26，沒有被錯誤地再加一次

### 練習 5 — Resource 與 Prompt

新增 `OrderHubResources.cs`（`orderhub://discount-rules`，text/markdown，靜態內容：三個會員等級的折扣率與「折扣只在總額上算一次、UnitPriceSnapshot 是下單當下原價」這句話）與 `OrderHubPrompts.cs`（`low_stock_report`，帶一個 `threshold`（預設10）參數，展開成一段要 agent 先呼叫 `low_stock` 再彙整成採購建議表的提示詞）。`Program.cs` 接上 `.WithResources<OrderHubResources>().WithPrompts<OrderHubPrompts>()`。

MCP Inspector 的瀏覽器操作這階段一直不穩定（連線常斷、截圖偶爾卡住），改用原始 stdio 直接送 JSON-RPC 驗證（跟練習1踩雷後的作法一樣，把輸入用 `{ cat 輸入檔; sleep 3; }` 餵給編譯後的 `.exe`）：

1. [x] `resources/list` 回傳 `orderhub://discount-rules`，name/description/mimeType 都對；`resources/read` 讀出來的文字跟 `DiscountRules()` 裡寫的逐字一致
2. [x] `prompts/list` 顯示 `low_stock_report`，帶一個非必填的 `threshold` 參數（description 寫著「庫存門檻，預設 10」）
3. [x] `prompts/get`（threshold=8）展開後的訊息正確把 8 代入模板：「請用 low_stock 工具（threshold=8）查出低庫存商品…」

思考題（5c 第 3 點）：
規則只有一份，改版時只要改這裡；讓 agent 自己讀 code，等於每次都要重新爬一次，而且沒人保證它讀對版本（例如漏看 Gold 的雙重折扣 bug 就是活動1練習2的教訓）。Prompt 放 server vs 每個人自己打字：`low_stock_report` 這段提示詞進了 git，全隊問法一致、之後要調整「輸出表格要不要加理由欄」只要改一個地方；每個人自己打，問法會慢慢分歧，而且新人不知道該怎麼問。兩者都是同一堂課：**把『怎麼做』的知識從『每次重新推理/重新打字』搬到『寫一次、大家共用、進版控』**。
