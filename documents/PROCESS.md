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

1. `/Products/LowStock` 不帶參數 → 門檻 10 的結果；帶 `?threshold=3` → 結果隨之改變
2. `?threshold=0`、`?threshold=-1` → 頁面顯示驗證錯誤，不是 500
3. 售出數量欄位排除了 Cancelled 訂單（可用一筆已取消的訂單驗證）
4. 停售（已停售 badge）商品不出現在列表
5. 程式分層與命名跟既有的 Products 功能一致（請 agent 自我 review 一次，並自己確認）
6. 至少 3 個新測試，`dotnet test` 全綠

練習 4

1. 重構後 `dotnet test` 全綠
2. 我能說出這次重構「改善了什麼、沒有改變什麼」
3. 我有在 code review 的角度看過 diff（不是 agent 說好就好）

---

## 附錄：值得留下的對話片段

（貼 1–2 段最有代表性的 prompt 與回應**摘要**——不用貼全文，重點是「我怎麼問」和「它怎麼答」。）
