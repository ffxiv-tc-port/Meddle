# Meddle

[PassiveModding/Meddle](https://github.com/PassiveModding/Meddle) 的台服移植版。
匯出遊戲內角色模型與場景為 glTF 的插件，搭配 [MeddleTools](https://github.com/PassiveModding/MeddleTools) Blender 附加元件使用。

> 若只是要匯出單一模型部件或製作模組，請改用 Penumbra。Meddle 適合一次匯出整組角色或整個場景。

## 主要功能

- **角色分頁**：匯出玩家或 NPC 的完整角色模型，含裝備、外觀與骨骼資料。
- **場景分頁**：搭配世界疊加層挑選場景物件，匯出環境／地形物件。
- **動作分頁**：匯出角色骨骼動畫。
- **材質參數分頁**：檢視與調整材質參數。
- **偵錯／地形偵錯分頁**：開發除錯用資訊。
- **設定分頁**：匯出格式與行為設定。

指令 `/meddle` 開啟模型匯出視窗。

## 台服版的修改

- 場景分頁的世界疊加層預設關閉（既有設定檔也會關掉一次，之後自己打開不會再被動到）
- 場景解析預設不包含光源（Light）、環境光（EnvLighting）與貼花（Decal）——這幾類要走未經台服驗證的記憶體偏移；需要時可自己勾選
- 場景分頁的「場景資料（進階）」預設收起；場景解析加上節流，連續失敗多次就停用（重新載入插件才恢復）
- 記憶體寫入分頁預設隱藏，偵錯分頁的部分區塊預設收起
- 角色選取改成每幀依 ID 重查，不再跨幀持有遊戲物件
- 介面繁體中文化（插件資訊、分頁名稱與匯出按鈕）

## 安裝

在 Dalamud 設定的「自訂插件庫」加入
`https://raw.githubusercontent.com/ffxiv-tc-port/DalamudPluginsTC/main/repo.json` 並啟用，
再從插件列表安裝。

## 出處與致謝

本專案大量參考或使用以下專案的成果：
- [Xande](https://github.com/xivdev/Xande)：PBD 檔案結構、MeshBuilder、RaceDeformer、Havok 骨骼
- [Penumbra](https://github.com/xivdev/Penumbra)：shader 編譯邏輯、頂點格式資訊、SpanBinaryReader
- [Lumina](https://github.com/NotAdam/Lumina/)：檔案結構
- [SaintCoinach](https://github.com/xivapi/SaintCoinach)：地形與關卡檔案結構
- [Alpha](https://github.com/NotNite/Alpha)：Meddle.UI 專案架構與繪製邏輯參考
- [PathFinder](https://github.com/chirpxiv/ffxiv-pathfinder)：世界疊加層邏輯參考
- [Ktisis](https://github.com/ktisis-tools/Ktisis)：燈光結構、hkQsTransformf 矩陣處理

主要貢獻者：
- [WorkingRobot](https://github.com/WorkingRobot)：GPU 貼圖匯出、角色樹狀匯出結構、Shape／屬性資料、Attach 結構、骨骼解析
- Penumbra Discord 社群成員：測試與回饋

原作者：[PassiveModding](https://github.com/PassiveModding)
