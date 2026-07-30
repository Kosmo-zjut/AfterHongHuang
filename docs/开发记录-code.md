# 开发记录 - 洪荒之后-code

> 工作空间：洪荒之后-code（git worktree）
> 主文档：洪荒之后-main/docs/
> 对应需求：MVP开发需求-第一轮.md v2.0

---

## 2026-07-17 | UI-FOCUS-SETTINGS-20260717-C3 CardReward 可恢复全局入口

### 根因与修复

- TopBar 已按 C1R 保持可交互，但 `OverlayCoordinator` 为打开地图/套牌/设置而直接 `QueueFree()` CardReward，绕过 `CardRewardHelper.Show()` 的 `onCancelled` 回调，导致胜利页奖励行继续处于禁用状态。
- `CardRewardHelper` 现在为每个已打开 overlay 登记唯一的可恢复取消回调。跳过按钮与 TopBar 的全局入口复用同一 `CancelWithoutSettlement()` 路径：只关闭/注销 CardReward、恢复源奖励行，不消费候选、不写永久牌组、不提交 NodeResult、不离场。
- 协调器要求该回调完成注销；回调缺失或未注销会明确拒绝全局 overlay 请求并记录错误，避免静默丢失奖励入口。节点识别改用 Godot `InstanceId`，不再依赖不可靠的 managed-wrapper `ReferenceEquals`。
- UI 层级收敛为 `OverlayCoordinator` 常量：Victory `400..449`、Map/Utility `450..469`、CardReward `470..489`。胜利、地图、套牌和奖励控件均引用对应常量，不再分散使用旧魔法数。
- `OverlayCoordinatorSelfCheck` 依次验证 CardReward 打开后经 TopBar 地图、套牌、设置入口取消：源奖励行恢复，候选标记和已选进度保持不变。

### 修改文件

- `scripts/UI/CardRewardController.cs`
- `scripts/UI/OverlayCoordinator.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/MapOverlayController.cs`
- `scripts/UI/DeckViewer.cs`
- `scripts/Core/OverlayCoordinatorSelfCheck.cs`
- `docs/开发记录-code.md`

### 验证结果

- 隔离 `APPDATA/LOCALAPPDATA` 的 Debug/Release `dotnet build -p:NuGetAudit=false` 均通过，0 警告、0 错误。
- Godot 4.7 Mono headless 项目启动退出码 0；`OverlayCoordinatorSelfCheck`、奖励状态机、StateContract 和 500-seed MapGraph 自检均 PASS，`failures=0`、`crossings=0`。
- Battle、Map、Lingmai、Shop、Event editor-headless 场景解析均通过。
- BUG-20260717-001 回归：有效保存配置 `1280x720` 的 non-headless 启动退出码 0，D3D12 初始化并记录实际应用；非法字符串配置的 headless 启动退出码 0，明确保留工程默认值。
- 静态扫描确认协调器不再直接释放 `_cardRewardOverlay`，相关层级仅使用集中 `OverlayCoordinator` 常量。

### 残留风险

- 自动自检验证的是取消回调和奖励状态不变；仍需窗口复测真实交互顺序：打开 CardReward -> 点 TopBar 地图/套牌/设置 -> 关闭该全局 overlay -> 同一奖励行可重新打开，已选卡和未选候选保持正确。

---

## 2026-07-17 | UI-FOCUS-SETTINGS-20260717-C1R TopBar 全局入口口径纠正

> 本节覆盖本文件中较早“胜利期间禁用 TopBar/普通工具 overlay”的描述；当前生产口径是 TopBar 始终可交互。

### 变更概述

- 胜利 `VictoryModalLayer` 从全屏改为仅覆盖 `y=44..1080` 的内容区，保留 `ZIndex=200`、`MouseFilter.Stop` 和 `0.72` 遮罩。TopBar 不再被遮罩或输入层覆盖；手牌、日志、结束回合、F9 和战斗区仍在内容区焦点门禁下不可交互。
- 移除 `BattleController` 对胜利状态下 TopBar 地图回调的拒绝。TopBar 地图和胜利“继续”都只打开同一共享地图 overlay，不改变奖励、节点结算、场景或胜利页。
- `OverlayCoordinator` 改为允许胜利/奖励期间的全局地图、套牌和设置入口；新的全局入口仅关闭可选的 CardReward 子 overlay，保留胜利页面和未领取奖励计划。套牌 overlay 提升至 `ZIndex=220`，保证在胜利内容层之上显示。
- `OverlayCoordinatorSelfCheck` 改为验证胜利与 CardReward 存在时均可由全局入口准备地图/工具 overlay。

### 修改文件

- `scripts/UI/BattleController.cs`
- `scripts/UI/OverlayCoordinator.cs`
- `scripts/UI/DeckViewer.cs`
- `scripts/Core/OverlayCoordinatorSelfCheck.cs`
- `docs/开发记录-code.md`

### 验证结果

- 隔离 `APPDATA/LOCALAPPDATA` 后，Debug/Release `dotnet build -p:NuGetAudit=false` 均通过，0 警告、0 错误。
- Godot 4.7 Mono headless 项目启动退出码 0；`OverlayCoordinatorSelfCheck`、StateContract、500-seed MapGraph 与既有战斗/奖励/事件自检均 PASS。

### 残留风险

- headless 不能验证真实点击命中，需窗口复测：胜利/卡牌奖励期间 TopBar 的地图、套牌、设置均可打开；TopBar 以下战斗区仍不能 hover、拖拽或触发 F9；关闭全局 overlay 后胜利页与未领取奖励仍存在。

---

## 2026-07-17 | UI-FOCUS-SETTINGS-20260717-C1 胜利焦点门禁与分辨率回滚收口

### 变更概述

- `BattleController` 将胜利根层固定为全屏 `ZIndex=200`、`MouseFilter.Stop`，遮罩 alpha 调整为 `0.72`。胜利期间 `_Process`、`_Input`、`_UnhandledInput` 都在统一焦点门禁处提前返回，故 held 手牌（最高 `ZIndex=130`）、右键卡牌取消、F9 手牌调试、日志、结束回合和 TopBar 地图入口均不能穿透。
- 胜利面板的“继续”只绑定 `OpenVictoryMapOverlay()`；该方法只在胜利模态仍有效时打开共享地图，不包含 `GoToScene`、`TryFinalizeBattleVictory`、奖励领取/跳过、节点提交或胜利页隐藏。地图 `ZIndex=220`，卡牌奖励 overlay `ZIndex=240`，均高于胜利页与 held 手牌。
- `MapOverlayController` 的完整展开终态冻结为 `Rect2(64, 104, 1792, 872)`。透明输入层从 TopBar 下缘 `y=44` 覆盖至屏底，在左至右展开和收起全过程阻断底层节点页；既有横向滚轮、左键拖拽、8px 阈值与 clamp 保留。
- `LingmaiController` 的“返回地图”改为只切换共享地图 overlay，不再隐式创建 `Exited` 结果；新增单独的“放弃灵脉并继续前行”明确提交 `Exited/Skipped`。
- 设置分辨率目录增加当前尺寸不在支持列表时的禁用状态项；窗口应用或配置持久化失败时，`ResolutionSettings` 恢复本次操作前的完整窗口尺寸，设置 UI 按实际恢复尺寸重新选中并显示错误。

### 修改文件

- `scripts/UI/BattleController.cs`：胜利焦点门禁、继续专用地图入口及分层。
- `scripts/UI/MapOverlayController.cs`：冻结展开矩形与全动画输入隔断。
- `scripts/UI/CardRewardController.cs`：奖励 overlay 置于胜利模态/地图之上。
- `scripts/UI/LingmaiController.cs`：导航与灵脉放弃结算语义拆分。
- `scripts/UI/SettingsHelper.cs`：不在目录的当前分辨率可见状态及失败后 UI 恢复。
- `scripts/UI/ResolutionSettings.cs`：窗口/持久化失败时恢复最近一次成功窗口尺寸。
- `docs/开发记录-code.md`。

### 验证结果

- 隔离 `APPDATA/LOCALAPPDATA` 后执行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj` 与 `dotnet build -c Release -p:NuGetAudit=false .\AfterHongHuang.csproj`，均通过，0 警告、0 错误。
- 使用 Godot 4.7 Mono console 的项目 headless 启动：退出码 0；`StateContractSelfCheck`、`MapGraphRngSelfCheck`（500 seeds，failures=0、crossings=0）、`RewardStateMachineSelfCheck`、`OverlayCoordinatorSelfCheck` 等既有自检均 PASS。
- 使用有效的隔离保存分辨率 `1280x720` 与非法字符串配置分别启动：均退出码 0；非法配置记录“已保存的分辨率配置类型无效，保留工程默认值”。headless 显示服务对有效配置明确报告无法应用窗口尺寸，不假报成功。
- 使用同一有效配置非 headless 启动项目：退出码 0，D3D12 初始化成功并记录“分辨率已应用并保存：1280×720”。随后已将隔离测试配置改回非法值，避免测试目录伪装为有效用户设置。
- Battle、Map、Lingmai、Shop、Event 的 editor-headless 场景解析均退出码 0。
- 静态复核：继续回调仅为 `OpenVictoryMapOverlay`；`NodeResultType.Exited` 仅位于灵脉“放弃”按钮动作；未在上述 UI 文件中引入 `OpenMapOnEnter + CallDeferred` 或 `GoToScene(Map.tscn)` 成功路径。

### 残留风险

- headless 不能验证实际鼠标命中和视觉遮罩，需窗口手测：胜利页下层手牌/F9/TopBar 均无响应，继续后地图关闭可回到完全相同的胜利页，CardReward 在胜利页之上，灵脉“返回地图”不改变节点结果。
- 当前真实窗口管理器若拒绝某尺寸，服务会恢复窗口并显示错误；不同 DPI/多显示器环境下仍需实机确认恢复后的 OptionButton 文案与可读性。

---

## 2026-07-17 | BUG-20260717-001 Godot 启动访问冲突排查与分辨率恢复修复

### 根因

- `ResolutionSettings.ApplyPersistedAtStartup()` 读取 `user://afterhonghuang-settings.cfg` 后，将 Godot `ConfigFile.GetValue()` 返回的 `Variant` 直接传给 `System.Convert.ToInt32()`。只要用户已经保存过分辨率，该路径就会抛出 `InvalidCastException`。
- 该调用原先位于 `GameManager._EnterTree()`，即 Autoload 的最早生命周期。异常会中断启动链和自检输出；同一位置还会在 native 窗口初始化完成前调用 `DisplayServer.WindowSetSize()`，不适合作为持久化窗口设置的应用点。
- 在隔离 `APPDATA/LOCALAPPDATA` 中写入 `1280×720` 保存配置后，旧 DLL 可稳定复现完整 C# 栈：`ResolutionSettings.ApplyPersistedAtStartup -> GameManager._EnterTree`。这不是对 D3D12、显卡或编辑器的推测。

### 修复

- `ResolutionSettings` 改为检查 `Variant.Type.Int` 后使用 `Variant.AsInt32()`；损坏或旧格式的保存配置记录明确错误并保留工程默认分辨率，不再抛异常或假成功。
- 移除 `GameManager._EnterTree()` 的窗口设置调用；主场景 `TitleController._Ready()` 在可见场景已经入树后应用保存分辨率，避免 Autoload/native 窗口初始化竞争。
- 未改动 OverlayCoordinator、MapOverlay、奖励、节点流程、玩法数据或渲染驱动配置。

### 修改文件

- `scripts/UI/ResolutionSettings.cs`
- `scripts/Core/GameManager.cs`
- `scripts/UI/TitleController.cs`
- `docs/开发记录-code.md`

### 验证结果

- Godot 4.7 Mono executable：`D:\OtherApp\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe`。
- 使用隔离 `APPDATA/LOCALAPPDATA`、有效保存分辨率（1280×720）运行项目 headless：退出码 0；全部既有自检和 `OverlayCoordinatorSelfCheck` PASS。headless 正确记录“不支持窗口尺寸应用”，未崩溃。
- 使用同一有效配置运行非 headless 项目：退出码 0；日志确认 `D3D12 12_0 / NVIDIA GeForce RTX 3070 Laptop GPU` 和 `分辨率已应用并保存：1280×720`，无访问冲突。
- 使用同一环境运行非 headless 编辑器：退出码 0，D3D12 初始化成功。
- 将保存配置改为非法字符串后重新运行项目 headless：退出码 0；日志为“已保存的分辨率配置类型无效，保留工程默认值”，所有自检继续 PASS。
- Debug/Release：隔离用户目录下执行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj` 及 `dotnet build -c Release -p:NuGetAudit=false .\AfterHongHuang.csproj`，均为 0 警告、0 错误。
- Battle、Map、Lingmai、Shop、Event editor-headless 场景解析均退出码 0。隔离日志目录未发现 `.dmp`/`.mdmp`/crash dump。

### 残留风险

- 本次定位并修复的是可复现的 C# 配置读取/生命周期错误；尚未复现用户 Windows 弹窗中的 native 地址 `0x58`。若用户使用修复后的代码仍出现访问冲突，应保留发生时的 `user://logs/godot*.log`、Windows 事件查看器应用错误记录和复现步骤，再区分驱动或引擎问题。
- 沙箱不允许读取用户真实 `AppData` 或删除验证产生的隔离用户目录；本结论基于可控隔离环境和实际 D3D12 启动，不覆盖用户原有缓存或驱动状态。

---

## 2026-07-17 | UI-FOCUS-SETTINGS-20260717 胜利模态、地图卷轴与分辨率应用

### 变更概述

- 新增 `OverlayCoordinator`，集中管理共享地图、套牌、设置、胜利总面板和 CardReward 的层级关系：地图/套牌/设置互斥；胜利期间普通工具 overlay 被明确拒绝；CardReward 只能在有效胜利总面板上方创建。该协调不写 NodeResult、奖励或导航状态。
- `BattleController` 的胜利页改为 TopBar 下方的全屏 `VictoryModalLayer`：背景整体降暗，`MouseFilter.Stop` 阻断手牌、日志、结束回合及下层测试控件。总面板仍保留，CardReward 位于更高 ZIndex；关闭或跳过奖励只关闭自身 overlay。继续按钮移动到总面板右下侧，仍只调用共享地图 overlay。
- `MapOverlayController` 新增透明输入阻断层，从 TopBar 下缘覆盖到屏底，在卷轴开合动画全过程阻断底层页面输入；卷轴仍固定左起向右展开。首次布局后以整张地图内容居中横向视口，保留 160px 横向滚轮、左键拖拽、8px 阈值和 clamp。
- 节点结果页到下一节点改用 `TryTransitionAndRouteFromCompletedNode`：在保留原完成节点快照的事务内完成目标激活和场景路由；路由失败会恢复原完成节点与原结果页，overlay 保留可重试。MapScene 的首次节点进入仍使用原有“激活失败则清理未开始节点”路径。
- 新增 `ResolutionSettings` 作为唯一分辨率目录、实际应用和持久化服务。设置页不再散落分辨率值；选择后校验 `DisplayServer.WindowGetSize()`，失败会恢复选择并显示错误，成功写入 `user://afterhonghuang-settings.cfg`，下次启动由 `GameManager` 应用。headless 显示服务会明确拒绝而不假成功。
- 标题页设置入口接入同一设置面板；角色随机入口通过 `CharacterSelectionResolver` 从所有已解锁 `CharacterDefinition` 中抽取，不再固定选择首项。
- 新增 `OverlayCoordinatorSelfCheck`，覆盖胜利模态拒绝普通工具 overlay、CardReward 期间拒绝地图，以及分辨率目录反查；既有 `CharacterSelectionBindingSelfCheck` 增加双 Definition 随机选择反证。

### 修改文件

- `scripts/UI/OverlayCoordinator.cs`（新增）
- `scripts/UI/ResolutionSettings.cs`（新增）
- `scripts/UI/MapOverlayController.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/CardRewardController.cs`
- `scripts/UI/DeckViewer.cs`
- `scripts/UI/SettingsHelper.cs`
- `scripts/UI/NodeSceneRouter.cs`
- `scripts/UI/CharacterSelectController.cs`
- `scripts/UI/CharacterSelectionResolver.cs`（新增）
- `scripts/UI/TitleController.cs`
- `scripts/UI/LingmaiController.cs`
- `scripts/UI/ShopController.cs`
- `scripts/UI/EventController.cs`
- `scripts/Core/GameManager.cs`
- `scripts/Core/OverlayCoordinatorSelfCheck.cs`（新增）
- `scripts/Core/CharacterSelectionBindingSelfCheck.cs`
- `docs/开发记录-code.md`

### 验证结果

- 直接 `dotnet build .\AfterHongHuang.csproj` 在受限环境中被用户级 `NuGet.Config` 读取权限阻断；隔离 `APPDATA/LOCALAPPDATA` 后，默认构建仅因无网络查询 NuGet audit 输出 `NU1900`。
- 使用隔离用户目录和 `-p:NuGetAudit=false`：`dotnet build .\AfterHongHuang.csproj`、`dotnet build -c Release .\AfterHongHuang.csproj` 均通过，0 警告、0 错误。
- Godot 4.7 Mono console headless：退出码 0。StateContract、MapGraphRng（500 seeds，failures=0、crossings=0）、B2、战斗/意图、奖励、角色、节点/事件自检全部 PASS，新增 `OverlayCoordinatorSelfCheck` PASS。
- Godot editor headless 分别加载 Battle、Map、Lingmai、Shop、Event 场景，全部退出码 0。
- 静态扫描：正式节点结果页仅调用 `TryTransitionAndRouteFromCompletedNode`；未发现 `OpenMapOnEnter + CallDeferred`、`GoToScene("res://scenes/Map/Map.tscn")`、`TrySkipBattleCardReward`、F10/DebugService/DebugForceVictory 或 MouseFilter 数字强转。`System.Random` 仍仅命中既有道痕/Map UI 的 R-039 遗留，本轮未扩大处理。

### 残留风险

- 未进行可见窗口输入实测；需用户确认胜利页背景降暗和卡牌/日志 hover 隔离、继续 -> 地图 -> 收卷回原页面、动画初段的底层输入隔断、地图居中留白以及 1280×720/1600×900 下的实际可读性。
- `ResolutionSettings` 持久化使用 `user://`；受限/headless 环境会明确拒绝窗口应用。真实桌面窗口管理器若强制最小尺寸，服务会显示实际尺寸不符而不保存成功。
- `OpenMapOnEnter` 仍作为迁移期兼容状态保留；本任务未新增它的 deferred 路径。R-039 的奖励消费持久化与既有 UI `System.Random` 仍为 P1。

---

## 2026-07-17 | UI-FLOW-C2-20260717 入口、overlay 与角色定义绑定收口

### 变更概述

- `MapOverlayController` 保持根节点 `MouseFilter=Ignore`、TopBar 下方的 Panel/Viewport 使用 `Stop`；地图构建完成后按 `RunState.CurrentMapNodeId` 延迟一次定位横向视口，定位值经过 ScrollContainer 当前 min/max clamp。该延迟只用于内容测量，不参与 MapScene 成功入口，不恢复 `OpenMapOnEnter + deferred`。
- 新增 `GameManager.TryEnterNode`，MapScene 不再复制 Battle/Lingmai/Shop/Event 的 `TryEnter* + GoToScene` 分支；`NodeSceneRouter` 统一使用显式 `ChangeSceneToFile`。路由失败时调用 `AbortActiveNodeEntry` 清理未提交 ActiveNode/BattleState/NodeState，避免场景失败后残留活动节点。
- `SettingsHelper` 增加跨宿主的单例引用和 toggle：重复点击 TopBar 设置按钮关闭当前弹窗，关闭按钮/返回标题/退出游戏均清理引用。`DeckViewer` 既有单例 toggle 与 CardReward 的重复 overlay 防护保留。
- `CharacterSelectController` 改为从 `DataDefs.Characters` 定义目录读取头像、选中角色和初始牌组；不再以字面量下标选择默认角色，不存在可用定义或初始牌组时明确禁用/报错。新增 `CharacterSelectionBindingSelfCheck`，用两个不同 fixture 通过同一 `CharacterDeckFactory` 反证定义绑定。

### 修改文件

- `scripts/Core/GameManager.cs`
- `scripts/Core/CharacterSelectionBindingSelfCheck.cs`（新增）
- `scripts/UI/NodeSceneRouter.cs`
- `scripts/UI/MapController.cs`
- `scripts/UI/MapOverlayController.cs`
- `scripts/UI/CharacterSelectController.cs`
- `scripts/UI/SettingsHelper.cs`
- `scripts/UI/TitleController.cs`
- `docs/开发记录-code.md`

### 验证结果

- `dotnet build .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- `dotnet build -c Release .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- Godot 4.7 Mono console：项目 headless、editor headless、Battle/Map/Lingmai/Shop/Event 场景解析均退出码 0。启动自检输出 `StateContractSelfCheck`、`MapGraphRngSelfCheck`（500 seeds，failures=0，crossings=0）、B2、战斗/意图、奖励、节点/事件及新增 `CharacterSelectionBindingSelfCheck` PASS。
- 静态扫描：生产 UI 未发现 MouseFilter 数字强转；未发现 `GoToScene("res://scenes/Map/Map.tscn")`、`OpenMapOnEnter + CallDeferred`、F10/DebugService/DebugForceVictory 或 `TrySkipBattleCardReward`。`GoToScene` 仅保留 GameManager 兼容包装；正式 Map/Battle/Lingmai/Shop/Event 路由均走 `ChangeSceneToFile`。`System.Random` 仍有 DaoMark/旧地图 UI 命中，属于 R-039/既有随机遗留，本轮未宣称迁移。

### 残留风险

- 未进行可见窗口操作；仍需实机确认 MapOverlay 当前位置居中、TopBar 不被覆盖、Map/Deck/Settings 重复点击 toggle、灵脉/商店/事件结果页关闭并重开地图 overlay、CardReward 3 选 2 连续领取和重新打开。
- `OpenMapOnEnter`、`PendingMapEntry` 仍是迁移期兼容导航状态；本轮正式节点页和新 MapScene 节点进入路径不依赖其 deferred 成功时序，后续可单独清理兼容债务。
- 动态脚本 UI 与既有 `System.Random` 奖励/道韵路径仍属于技术债；R-039 继续保持 P1，未扩大到随机流或奖励持久化幂等。

---

## 2026-07-17 | UI-FLOW-C1-20260717 交互边界与节点迁移修复

### 根因与修复

- `CardRewardController` 原先用 `(Control.MouseFilterEnum)2/1` 强转并写反注释。Godot 的语义枚举实际导致 overlay 可能不拦截输入、遮罩也可能穿透；现改为 `MouseFilterEnum.Stop`（overlay）和 `MouseFilterEnum.Ignore`（mask），overlay 位于 TopBar 下方。
- 共享 `MapOverlayController` 根节点曾全屏 Stop，可能覆盖 TopBar；现根节点 Ignore，实际 y=44 以下的 Panel/Viewport Stop，TopBar 可继续操作。横向滚轮步长改为 160px，8px 拖拽阈值和显式 clamp 保留。
- `GameManager.TryValidateNodeEntry` 现在检查当前节点下一层、合法出边、未访问状态；战斗目标还必须通过 EncounterPool 解析、EnemyDefinitionValidator 和 RewardProfileCatalog 校验。缺少任何内容定义都会拒绝进入并记录错误。
- 新增 `TryTransitionFromCompletedNode`，统一 Battle/Lingmai/Shop/Event 结算页到目标节点的事务迁移。它在清理 ActiveNode 前预检目标，成功后才激活目标；激活失败会恢复 ActiveNode、BattleState、奖励暂存、节点生命周期和导航字段。新增 `NodeSceneRouter` 只负责成功后的底层场景路由，不修改 RunState。
- 四个节点页不再各自执行 `TryExit... -> TryEnter...`；地图 overlay 只有合法节点点击才触发迁移，关闭 overlay 保留原页面。

### 修改文件

- `scripts/UI/CardRewardController.cs`
- `scripts/UI/MapOverlayController.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/LingmaiController.cs`
- `scripts/UI/ShopController.cs`
- `scripts/UI/EventController.cs`
- `scripts/Core/GameManager.cs`
- `scripts/UI/NodeSceneRouter.cs`（新增）
- `docs/开发记录-code.md`

### 验证结果

- `dotnet build .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- `dotnet build -c Release .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- Godot 4.7 Mono console：项目 headless、editor headless 及 Battle/Map/Lingmai/Shop/Event 场景解析均退出码 0；既有 StateContract、MapGraphRng（500 seeds）、B2、BattleContent、Enemy/Intent、Reward、NodeContent、EventEffect 自检均 PASS。
- 静态扫描：未发现 `GoToScene("res://scenes/Map/Map.tscn")`、`TrySkipBattleCardReward`、F10/DebugService/DebugForceVictory；CardReward MouseFilter 已使用语义枚举；旧 `260f` 滚轮步长已清除。

### 残留风险

- 尚未进行可见窗口操作，仍需实测 CardReward overlay 对 TopBar/底层按钮的拦截边界、地图滚轮 160px、拖拽 clamp，以及目标激活失败时结果页是否可重试。
- `OpenMapOnEnter` 仍是迁移期兼容字段；本任务的共享 overlay 和节点事务迁移不依赖它，也未新增 `deferred` 或 `GoToScene(Map.tscn)` 路径。
- MapOverlay/CardReward 仍属于现有动态脚本 UI 技术债，未扩大为新的正式动态 UI 系统。

---

## 2026-07-17 | UI-FLOW-P0-20260716 交互语义统一整改

### 变更概述

- 新增 `scripts/UI/MapOverlayController.cs` 作为 Battle、Map、Lingmai、Shop、Event 共用的地图 overlay：固定左边界向右揭示，视口宽度按当前 MapGraph 层数、节点宽度和层间距计算；支持横向滚轮、左键拖拽、8px 拖拽阈值和显式滚动边界 clamp。
- `CardRewardController` 与 `BattleController.AddCardRewardRow` 已将“跳过/关闭”与奖励结算分离。跳过只关闭 CardReward overlay，奖励行和已领取进度保留；再次打开读取当前冻结 RewardPlan 的候选顺序及领取集合。ChoiceCount 达标后才移除奖励行并完成该槽位。
- Lingmai、Shop、Event 页面结算后保留原页面和结果摘要，重复操作灰显；TopBar 地图按钮统一呼出共享 overlay，关闭后返回原页面。只有在 overlay 中点击经过入口预检的合法下一节点，才执行当前节点离场和目标场景进入。
- MapScene 目标节点点击新增统一 `TryValidateNodeEntry` 预检，预检失败时保留当前地图 overlay；MapRenderer 对已结算服务节点提供与节点页一致的可探索提示和可达判定。Battle 战斗中地图仍只读，胜利页地图才可推进。

### 修改文件

- `scripts/UI/MapOverlayController.cs`（新增）
- `scripts/UI/CardRewardController.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/MapController.cs`
- `scripts/UI/MapRenderer.cs`
- `scripts/UI/LingmaiController.cs`
- `scripts/UI/ShopController.cs`
- `scripts/UI/EventController.cs`
- `scripts/Core/GameManager.cs`
- `scripts/Core/EventEffectSelfCheck.cs`
- `scenes/Lingmai/Lingmai.tscn`
- `scenes/Shop/Shop.tscn`
- `scenes/Event/Event.tscn`
- `docs/开发记录-code.md`

### 验证结果

- `dotnet build .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- `dotnet build -c Release .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- Godot 4.7 Mono console headless：项目启动、编辑器解析及 Battle/Map/Lingmai/Shop/Event 场景解析均退出码 0；StateContract、MapGraphRng（500 seeds）、B2、敌人/意图、奖励、事件自检均输出 PASS。
- 静态按钮审查：胜利页“继续”仅打开地图 overlay；服务页地图按钮仅 toggle overlay；卡牌奖励跳过不调用 NodeResult/奖励跳过接口；`scripts/UI/` 未发现 `GoToScene("res://scenes/Map/Map.tscn")`。

### 已知问题与限制

- 尚未完成可见窗口鼠标操作，滚轮、拖拽阈值、overlay 关闭后页面像素级保持、CardReward 3 选 2 的真实连续点击仍需用户实机复测。
- `OpenMapOnEnter` 仍是迁移期兼容导航字段，当前 UI-FLOW 正式共享 overlay 路径不使用它；没有新增 `OpenMapOnEnter + deferred` 成功路径。
- 共享 overlay 和 CardReward 仍沿用现有动态脚本创建 UI，属于当前 MVP/P0 范围内的技术债，不涉及玩法数据或 C7-C11 Intent Resolution 回归。

---

## 2026-07-16 | BUG-20260716-002 C11 胜利页地图 Overlay 纠偏

### 根因与修复

- 上一轮把“继续”实现成战斗离场，仍然违背用户要求。现改为胜利页和奖励页保持不动，`continueBtn.Pressed` 只调用 `ShowMapOverlay()`。
- 敌人死亡由 `TryRegisterEnemyDefeated()` 先锁定战斗胜利并提交唯一 `Completed` NodeResult；奖励计划只负责可选奖励，不再决定节点完成或路线推进。
- Battle 内地图 overlay 在普通战斗状态下保持只读；在 `战斗胜利结算` 状态下允许点击合法下一节点。关闭 overlay 只释放 overlay，原胜利/奖励页面节点和奖励状态不重建、不清理。
- 只有点击合法下一节点才调用 `TryExitBattleToMapAfterVictory()`，销毁已结束战斗并进入目标 Battle/Lingmai/Shop/Event 场景；继续本身不调用 `GoToScene`、Dispose、奖励领取/跳过或 NodeResult。
- 未领取卡牌候选及暂存灵韵在实际离开胜利页时清理；已领取卡牌已独立写入永久牌组并保留。

### 修改文件

- `scripts/Core/GameManager.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/MapRenderer.cs`
- `scripts/Core/RewardStateMachineSelfCheck.cs`
- `docs/开发记录-code.md`

### 验证结果

- `dotnet build .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- `dotnet build -c Release .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- Godot 4.7 Mono headless：退出码 0；StateContract、MapGraphRng（500 seeds）、B2、敌人/意图、RewardBinding、RewardStateMachine、NodeContent、EventEffect 等自检均 PASS。
- 新增自检覆盖：敌人死亡立即 Completed；奖励生成失败后继续离场；已领取一张卡后继续，永久牌组保留且不重复 NodeResult；离场失败注入不改变胜利页状态。
- 代码核对：胜利页继续按钮及奖励错误页继续按钮均只绑定 `ShowMapOverlay()`；实际场景跳转只存在于合法地图节点回调。

### 残留风险

- 尚未进行窗口鼠标操作；需复测“继续 -> 关闭地图 -> 原胜利/奖励页面完全保留 -> 再继续”，以及胜利地图点击不同节点类型后的实际场景进入。
- `OpenMapOnEnter` 仍作为灵脉/事件旧导航兼容字段保留，但战斗胜利 overlay 路径不使用它，也不使用 `CallDeferred`。

---

## 2026-07-16 | BUG-20260716-001 C10 敌人死亡与显式地图入口修复

### 根因与修复

1. `BattleController.CheckBattleResult()` 原先在敌人 HP 归零后先解析/提交奖励计划，奖励来源或卡池错误会让 `BattleOver/PlayerWon` 仍为 false。新增 `GameManager.TryRegisterEnemyDefeated()`：敌人生命归零时先同步锁定 `BattleState.BattleOver/PlayerWon`、关闭玩家回合；奖励计划失败只进入可追踪的奖励结算错误页，不产生奖励或 `NodeResult`。
2. `TryBuildBattleVictoryPlan()` 与 `TryCommitBattleVictory()` 现在要求战斗已由死亡端口标记为胜利，奖励接口不再负责决定战斗胜负。自检覆盖无效奖励源：战斗已结束、输入被锁定、牌组/灵韵/节点结果未改变。
3. 战斗成功离场通过 `DisposeActiveBattleAfterResult(PlayerState.空闲, MapEntryMode.OpenInteractiveMap, ...)` 直接写入显式地图入口；`TryExitBattleToMapAfterRewards()` 不再经过 `OpenMapOnEnter=true` 兼容 setter。上一轮“继续纯导航”、未处理奖励阻止和 MapScene 同步开图逻辑保持不变。

### 修改文件

- `scripts/Core/GameManager.cs`
- `scripts/Core/RewardStateMachineSelfCheck.cs`
- `scripts/UI/BattleController.cs`
- `docs/开发记录-code.md`

### 验证结果

- `dotnet build .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- `dotnet build -c Release .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- Godot headless：`D:\OtherApp\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe --path . --headless --quit-after 3`，退出码 0；`RewardStateMachineSelfCheck PASS`，同时现有 StateContract、MapGraphRng、B2、敌人内容、意图、奖励、事件等自检通过。
- Godot editor headless 场景/脚本解析：同 console exe 执行 `--path . --headless --editor --quit`，退出码 0。
- 静态核对：`BattleController` 的继续回调仅调用 `TryExitBattleToMapAfterRewards()`；该方法直接使用 `MapEntryMode.OpenInteractiveMap`；`MapController` 不再引用 `OpenMapOnEnter`、`CallDeferred` 或 `OpenMapAfterEnter`。

### 残留风险

- `OpenMapOnEnter` 仍作为非生产旧调用者的迁移兼容属性保留，灵脉/事件等旧离场路径仍可能使用它；战斗胜利成功离场已不经过该属性。
- 未执行窗口实机操作；仍需复测奖励计划异常时的错误页、正常领取/跳过后继续、Debug 一键胜利真实链和战后地图交互。

---

## 2026-07-16 | ACT1-FEEDBACK-C9 三项 P0 封闭修复

### 根因与修复
1. 胜利奖励候选的 `CardInfo` 仍可能被 UI/计划持有者修改，且领取入口曾直接把该引用写入永久牌组。现在胜利提交后保存冻结奖励计划；领取前会逐字段验证活动计划与冻结副本，候选被修改时拒绝领取；实际入牌由冻结候选再复制一份 `CardInfo`。普通、Boss、额外来源和真实 3 选 2 两次领取/第三次拒绝均加入自检。
2. 胜利 NodeResult 原先由“继续”路径调用 `TryFinalizeBattleVictory`，会隐式领取/跳过剩余奖励并与战斗离场绑在一起。现行生产路径中 `TryCommitBattleVictory` 只暂存战斗结束与奖励计划；灵韵、卡牌领取和明确跳过分别更新奖励状态，最后一个奖励动作调用 `TryCommitBattleNodeAfterRewards` 提交 NodeResult，`BattleController` 的“继续”只调用 `TryExitBattleToMapAfterRewards` 销毁战斗实例和设置地图进入目标。`TryFinalizeBattleVictory` 保留为迁移期旧自检桥，不再由正式胜利 UI 调用。
3. `ResolvedEnemyIntent` 增加 EnemyHp、EnemyPhase、EnemySpecialTriggered 身份快照，执行前与敌方定义、护体、机制、攻击标记、阶段/游标一起校验。自检直接改 HP 跨阶段、阶段编号和特殊触发状态且不清缓存/加版本时，旧 Resolution 均被拒绝。

### 修改文件
- `scripts/Core/GameManager.cs`
- `scripts/Core/BattleState.cs`
- `scripts/Core/RewardPlan.cs`
- `scripts/Core/RewardContext.cs`
- `scripts/Core/PreparedIntentSelfCheck.cs`
- `scripts/Core/RewardStateMachineSelfCheck.cs`
- `scripts/Data/DataDefs.cs`
- `scripts/UI/BattleController.cs`
- `docs/开发记录-code.md`

### 验证结果
- `dotnet build .\AfterHongHuang.csproj`：0 错误、0 警告。
- `dotnet build -c Release .\AfterHongHuang.csproj`：0 错误、0 警告。
- Godot 4.7 Mono headless：退出码 0；StateContract、MapGraphRng（500 seeds，failures=0、crossings=0、fingerprints=500）、B2EncounterProduction、BattleContentBinding、EnemyDefinitionExecution、PreparedIntent、EnemyTurnTransaction、RewardBinding、RewardStateMachine、NodeContentBinding、EventEffect 均 PASS。
- Godot editor headless 场景/脚本解析：退出码 0。
- 硬编码扫描：生产 UI/Core 未新增固定奖励池、具体敌人/道痕分支、F10/DebugService/DebugForceVictory 或 unknown 奖励 ID；`System.Random` 仍仅为既有 R-039 遗留 UI 路径，集中定义和自检中的卡池/内容命中已分类保留。

### 已知问题
- R-039 仍为 P1：RewardId 消费集合尚未持久化，`DaoMarkSelectController.cs` 与 `MapController.cs` 的既有 `System.Random` 尚未迁移。
- 未执行窗口实机操作；需 QA Sol/max 严格只读回归，并复测奖励领取、跳过、3 选 2、离场失败重试和不同敌方阶段意图。

## 2026-07-16 | ACT1-FEEDBACK-C8 最小 P0 修复

### 根因与修复
1. 胜利计划提交原先主要依赖 ProfileId、卡牌 ID 和卡池成员关系，外部计划可以尝试替换候选或把普通奖励指向 Boss 卡池。现在 `GameManager` 在解析成功后保存计划原件和深拷贝快照；提交要求原件身份匹配，并逐字段比较 Profile/Source、Slot、CardPool、候选数、ChoiceCount、MergeRule、显示字段以及候选卡完整定义。`RewardPlanValidator` 增加完整候选定义比较和卡牌定义复制，Catalog 仍是卡池唯一入口。
2. 胜利事务快照补齐实际 BattleState 字段、敌人定义/最大生命、永久牌组运行实例、奖励计划持有状态和已领取候选集合。NodeResult 或离场提交失败时恢复 HP、灵韵、暂存奖励、牌组、节点生命周期、路线、战斗牌堆和奖励状态；不再用同一个 BattleState 引用伪造快照。
3. `ResolvedEnemyIntent` 执行前校验护体、敌方护体、破盾、机制激活/计数、当前回合攻击标记、阶段/游标、敌人身份等完整状态快照；相关 `BattleState` 属性收紧为程序集内写入。结束回合事务先消费已展示 Resolution，成功后才更新攻击统计，避免把已展示事实静默改写成另一份意图。
4. `RewardSourceMergeRule` 增加显式 `Combined` 配置路径，来源字段从 DaoMark/RewardContext 传入 Resolver，Resolver 不再固定 `Independent`。当前 MVP 仍按来源生成独立奖励槽，Combined 作为已验证的集中配置路径，后续再实现合并展示语义。
5. 新增/扩展确定性自检：普通计划伪造 Boss 卡池、替换同卡池候选均拒绝；终态 NodeResult 注入后 HP、灵韵、永久牌组、战斗牌堆和奖励状态不变；直接改护体、攻击标记、机制、阶段游标、敌人身份且不手工清缓存/加版本时，旧 Resolution 均拒绝。保留 R-039 为后续 P1。

### 修改文件
- `scripts/Core/GameManager.cs`
- `scripts/Core/BattleState.cs`
- `scripts/Core/RewardContext.cs`
- `scripts/Core/RewardPlan.cs`
- `scripts/Core/RewardResolver.cs`
- `scripts/Core/PreparedIntentSelfCheck.cs`
- `scripts/Core/RewardStateMachineSelfCheck.cs`
- `scripts/Core/RewardBindingSelfCheck.cs`
- `scripts/Data/DataDefs.cs`
- `scripts/Data/RewardProfileCatalog.cs`
- `docs/开发记录-code.md`

### 验证结果
- `dotnet build .\AfterHongHuang.csproj`：0 错误、0 警告。
- `dotnet build -c Release .\AfterHongHuang.csproj`：0 错误、0 警告。
- Godot 4.7 Mono headless：`Godot_v4.7-stable_mono_win64_console.exe --headless --path . --quit-after 25`，退出码 0。StateContract、MapGraphRng（500 seeds，failures=0、paths=4..9、shops=2..3、attempts=1/12.41/69、fingerprints=500、crossings=0）、B2EncounterProduction、BattleContentBinding、EnemyDefinitionExecution、PreparedIntent、EnemyTurnTransaction、RewardBinding、RewardStateMachine、NodeContentBinding、EventEffect 均输出 PASS。
- Godot 4.7 Mono editor headless 场景/脚本解析：`--headless --editor --path . --quit`，退出码 0。
- 硬编码扫描：`BattleController`/`CardRewardController` 未命中固定普通/Boss 卡池、奖励标题或 Debug 入口；`RewardResolver` 未固定 `Independent`；`System.Random` 仅保留在既有道痕/地图 UI 随机路径，归入 R-039。具体内容名称/ID仅命中集中 Definition、Catalog 或自检 fixture。

### 已知问题
- R-039 仍为 P1：RewardId 消费集合尚未持久化，`DaoMarkSelectController.cs` 与 `MapController.cs` 的既有 `System.Random` 尚未迁移。本轮不宣称跨存档奖励幂等或全局随机完成。
- 未执行窗口实机操作；需 QA Sol/max 严格只读回归，并由用户复测普通/Boss 奖励、额外来源合并配置、胜利失败回滚和不同敌人意图预告/结束回合日志。

## 2026-07-13 | ACT1-FEEDBACK-C7 QA7 奖励状态机与意图同源修复

### 根因与修复
1. `RewardPlan` 现在携带 `NodeId`、`EncounterId`、稳定 `RewardSlotId`、`CandidateCount`、`ChoiceCount`、`MergeRule`、卡池和显示字段。主奖励槽由 `RewardProfileDefinition.SlotId` 声明，额外来源由道痕定义的 `RewardSlotId` 声明，控制器不再构造 `"base"` 或解释合并规则。`RewardPlanValidator`、`RewardProfileCatalog`、`RewardContext` 会拒绝缺池、空池、候选不足、重复候选和非法选择规则。
2. 新增 `BattleVictoryPlan` 和 `GameManager.TryBuildBattleVictoryPlan` / `TryCommitBattleVictory`。完整灵韵、基础卡牌、所有额外来源、槽位和上下文在胜利状态变化前一次性解析；提交失败恢复 HP、灵韵、暂存奖励、牌组、节点生命周期、路线和战斗状态。`CardRewardHelper` 只接受 GameManager 持有的计划，`TryClaimBattleCard` 按每个槽位的 `ChoiceCount` 记录候选，重复/越界/外来计划被拒绝。
3. 敌方意图增加 `EnemyId`、`PhaseId`、阶段游标、Opening 游标、`IntentId` 和 `ResolutionId` 身份。执行前校验对象引用、版本、敌人/阶段/游标，结束玩家回合不再静默丢弃已展示且条件等价的缓存；`TryCommitEnemyTurn` 返回并执行同一解析对象，战斗日志记录 ResolutionId、最终摘要和实际结算。
4. 新增 `RewardStateMachineSelfCheck`，扩展 `PreparedIntentSelfCheck` 覆盖胜利计划失败状态不变、合法领取/重复领取/外来计划、3 选 2 计划校验，以及玩家已出斗击时预告、结束回合和执行共享同一 Resolution。

### 修改文件
- `scripts/Core/RewardPlan.cs`
- `scripts/Core/RewardContext.cs`
- `scripts/Core/RewardResolver.cs`
- `scripts/Core/BattleVictoryPlan.cs`（新增）
- `scripts/Core/RewardStateMachineSelfCheck.cs`（新增）
- `scripts/Core/GameManager.cs`
- `scripts/Core/BattleState.cs`
- `scripts/Core/PreparedIntentSelfCheck.cs`
- `scripts/Data/DataDefs.cs`
- `scripts/Data/RewardProfileCatalog.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/CardRewardController.cs`
- `scripts/Core/RewardBindingSelfCheck.cs`
- `docs/开发记录-code.md`

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj --no-restore`：0 错误、0 警告。
- `dotnet build -c Release .\\AfterHongHuang.csproj --no-restore`：0 错误、0 警告。
- Godot 4.7 Mono headless：命令 `Godot_v4.7-stable_mono_win64_console.exe --headless --path . --quit-after 25`，退出码 0。StateContract、MapGraphRng（500 seeds，failures=0、paths=4..9、shops=2..3、attempts=1/12.41/69、fingerprints=500、crossings=0）、B2EncounterProduction、BattleContentBinding、EnemyDefinitionExecution、PreparedIntent、EnemyTurnTransaction、RewardBinding、RewardStateMachine、NodeContentBinding、EventEffect 均输出 PASS；非法定义/重复奖励/外来计划均有明确拒绝日志。
- 未执行窗口实机操作；需用户复测胜利弹窗多奖励行、ChoiceCount>1 视觉流程、灵韵领取、不同敌人意图预告与结束回合日志。

### 已知问题
- R-039 仍为后续 P1：RewardId 消费集合尚未持久化，旧道痕选择/地图 UI 随机路径仍使用 `System.Random`；本轮只增加当前胜利计划内存生命周期校验，不宣称跨存档幂等完成。
- 当前生产 Profile 仍为 MVP 的 ChoiceCount=1；3 选 2 已由通用计划校验和自检覆盖，尚未配置为正式奖励来源。

---

## 2026-07-13 | ACT1-FEEDBACK-C6 QA6 奖励端口、最终意图与事件原子事务

### 根因与修复
1. 新增 `RewardProfileDefinition`、`RewardPlan` 和集中 `RewardResolver`。普通/首领战斗奖励由 `EnemyDefinition.RewardProfileId` 绑定，候选卡牌统一经 `CardPoolCatalog` 解析；道痕额外奖励在 `RewardContext` 中显式声明 `CardPoolId`、候选数和选择数。`BattleController` 与 `CardRewardHelper` 只消费计划，不按遭遇层级选择卡池，也不再写死奖励标题或使用本地 `System.Random`。
2. 新增 `ResolvedEnemyIntent`。`TryPrepareEnemyIntent` 按 `BattleState` 状态版本冻结最终伤害、条件是否触发、条件 ID/显示名和定义效果；`IntentPresentation`、战斗绑定自检和 `TryExecutePreparedEnemyIntent` 使用同一个对象。出牌、回合切换、敌方执行和条件状态变化会使缓存失效，过期对象拒绝执行。
3. 新增 `TryCommitEnemyTurn`，把弃牌、永炎、最终意图执行和敌方回合结束包进同一事务；扩展快照覆盖玩家资源、全部战斗牌堆、临时状态、敌方阶段/机制/游标和意图缓存，失败时完整回滚。`BattleController` 只在整个提交成功后刷新和记录行动。
4. 事件改为 `OptionId -> ActiveNode.ContentId -> EventDefinition -> EventResolutionPlan` 单一路径。`EventController` 不再直接写永久状态或提交节点；`GameManager.TryCommitEventOption` 原子应用 HP、灵韵、牌组、NodeResult、生命周期和导航，事件定义验证逐个实际成功/失败分支要求 `Exit`，事务失败恢复完整 RunState。
5. 新增 `PreparedIntentSelfCheck`、`EnemyTurnTransactionSelfCheck`，扩展 `RewardBindingSelfCheck`、`EventEffectSelfCheck`，覆盖双奖励档案/双额外来源、最终意图同对象展示与执行、条件护体/破盾解析、未知阶段回滚、外来事件选项拒绝和重复提交不变更状态。

### 修改文件
- `scripts/Core/RewardPlan.cs`（新增）
- `scripts/Data/RewardProfileCatalog.cs`（新增）
- `scripts/Core/RewardResolver.cs`
- `scripts/Core/RewardContext.cs`
- `scripts/Core/RewardBindingSelfCheck.cs`
- `scripts/Core/BattleState.cs`
- `scripts/Core/GameManager.cs`
- `scripts/Core/IntentPresentation.cs`
- `scripts/Core/EventEffectExecutor.cs`
- `scripts/Core/EventEffectSelfCheck.cs`
- `scripts/Core/PreparedIntentSelfCheck.cs`（新增）
- `scripts/Core/EnemyTurnTransactionSelfCheck.cs`（新增）
- `scripts/Core/BattleContentBindingSelfCheck.cs`
- `scripts/Data/DataDefs.cs`
- `scripts/Data/EventDefinition.cs`
- `scripts/Encounter/EncounterPool.cs`
- `scripts/Encounter/EnemyDefinitionValidator.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/CardRewardController.cs`
- `scripts/UI/EventController.cs`
- `docs/开发记录-code.md`

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj`：0 错误、0 警告。
- `dotnet build -c Release .\\AfterHongHuang.csproj`：0 错误、0 警告。
- Godot 4.7 Mono headless：退出码 0；StateContract、MapGraphRng（500 seeds，failures=0、paths=4..9、shops=2..3、attempts=1/12.41/69、fingerprints=500、crossings=0）、B2EncounterProduction、BattleContentBinding、EnemyDefinitionExecution、PreparedIntent、EnemyTurnTransaction、RewardBinding、NodeContentBinding、EventEffect 均输出 PASS；失败路径日志可追踪且无泄漏警告。
- Godot editor headless 场景解析：退出码 0，完成文件扫描、全局类注册和编辑器布局加载。
- 硬编码门禁：`BattleController`/`CardRewardController` 不再命中 `RewardCardPool`、`BossRewardCardPool`、首领专属旧文案或 `System.Random`；`EventController` 不再直接提交节点/修改永久状态；条件二次计算已从正式执行入口移除。`System.Random` 仍仅命中 `DaoMarkSelectController.cs`、`MapController.cs` 两处既有 UI 随机遗留；集中数据与自检中对卡池/内容名称的命中属于定义或 fixture。

### 已知问题
- R-039 仍保留为后续 P1：RewardId 消费集合尚未持久化；旧道痕选择/地图 UI 随机路径仍使用 `System.Random`。本轮未宣称奖励幂等或全局随机迁移完成。
- 尚未完成窗口实机操作验证；需复测普通/首领奖励标题与候选、额外来源奖励、条件意图显示与实际扣血、结束回合失败恢复、事件成功/失败分支和重复点击。

---

## 2026-07-13 | ACT1-FEEDBACK-C5 QA5 内容端口与意图事务修复

### 根因与修复
1. 新增 `TryPrepareEnemyIntent` 与 `BattleState.PreparedEnemyIntent`。战斗 UI 预告、敌方回合执行和日志消费同一解析意图实例；出牌、结束玩家回合和新回合会按条件清理缓存。`TryExecuteEnemyTurn` 先验证定义和解析结果，再一次性应用伤害、护体、机制护体、机制动作、易损、条件伤害并推进阶段/Opening 游标，失败恢复完整战斗快照。
2. `EnemyDefinitionValidator` 补全机制种类、触发类型、动作、条件伤害、意图和序列组合校验。增加基础阶段 0、相柳多阶段、山魈王单阶段、猴子 7 回合、多 Opening fixture、攻击+护体和非法定义状态不变自检。
3. 事件系统改为 `EventOptionDefinition + EventEffectCommand` 数据模型，`EventController` 不再按事件 Kind、ID 或专名分支；`EventEffectExecutor` 统一执行 LoseHp、GainLingyun、AddCardFromPool、RemoveCardChoice、Exit，并校验活动 Event NodeContext。两个生产事件通过同一执行器验证状态差值、结果重复锁定和离场。
4. 新增 `CardPoolCatalog` 与 `ShopInventoryService`。`ShopController` 不再直接读取 `DataDefs.RewardCardPool`、不固定三槽或返回 `int.MaxValue`；库存数量、卡池、洗牌和价格均来自 ShopDefinition/共享服务。双 Shop fixture 使用不同 CardPoolId 和槽位验证绑定差异。
5. `ActDefinition` 新增页面标题绑定入口；Map/Battle 地图标题从当前 Act 上下文读取，缺失时显式报错；`DaoMarkSelect.tscn` 移除固定 ACT 标题。

### 修改文件
- `scripts/Core/BattleState.cs`
- `scripts/Core/GameManager.cs`
- `scripts/Core/IntentPresentation.cs`
- `scripts/Core/EventEffectExecutor.cs`（新增）
- `scripts/Core/EventEffectSelfCheck.cs`（新增）
- `scripts/Core/EnemyDefinitionExecutionSelfCheck.cs`
- `scripts/Core/BattleContentBindingSelfCheck.cs`
- `scripts/Core/NodeContentBindingSelfCheck.cs`
- `scripts/Data/DataDefs.cs`
- `scripts/Data/EventDefinition.cs`
- `scripts/Data/CardPoolCatalog.cs`（新增）
- `scripts/Data/ShopInventoryService.cs`（新增）
- `scripts/Data/ShopDefinition.cs`
- `scripts/Encounter/EnemyDefinitionValidator.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/EventController.cs`
- `scripts/UI/ShopController.cs`
- `scripts/UI/MapController.cs`
- `scripts/Map/ActDefinition.cs`
- `scenes/DaoMarkSelect/DaoMarkSelect.tscn`
- `docs/开发记录-code.md`

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj`：0 错误、0 警告。
- `dotnet build -c Release .\\AfterHongHuang.csproj`：0 错误、0 警告。
- Godot 4.7 Mono headless：退出码 0；StateContract、MapGraphRng（500 seeds，failures=0、paths=4..9、shops=2..3、attempts=1/12.41/69、fingerprints=500、crossings=0）、B2EncounterProduction、BattleContentBinding（attack_guard=6）、EnemyDefinitionExecution、RewardBinding、NodeContentBinding、EventEffect 均 PASS。非法敌人 fixture 均显式拒绝，事件结果重复提交显式拒绝。
- Godot editor headless 场景解析：退出码 0，完成资源扫描和全局类注册；Map/Battle/Lingmai/Shop/Event/DaoMarkSelect 场景均存在。
- 硬编码门禁：敌人/商店/ACT 文案仅命中 `EncounterPool`、`MapNodeContentCatalog`、`ShopDefinition`、`ActDefinition` 等集中定义及自检 fixture；UI/Core 没有具体内容分支。未发现 unknown 奖励 ID、旧静态地图旁路、固定起点、NodeId 文案兜底、F10/DebugService/DebugForceVictory 或全局 EnemyTurnIndex 阶段索引。Release 构建不编译 Debug 一键胜利入口。
- `System.Random` 仍只命中三个既有稳定随机遗留：`scripts/UI/BattleController.cs` 的奖励抽取属于 R-039，`scripts/UI/MapController.cs` 与 `scripts/UI/DaoMarkSelectController.cs` 为旧 UI 随机路径；本轮未处理奖励稳定流和 RewardId 持久化幂等。

### 已知问题
- R-039 仍为后续 P1：生产奖励随机未迁移到稳定 Reward 流，RewardId 消费集合未持久化。
- 尚未完成窗口实机操作验证；需复测攻击+护体意图、阶段切换、猴子 Opening/Loop、事件两个生产入口、不同库存 Shop 以及页面标题上下文。

---

## 2026-07-13 | ACT1-FEEDBACK-C4 QA4 阶段游标、意图摘要与 Shop 数据绑定

### 根因与修复
1. `BattleState` 新增阶段 ID、阶段内回合游标和 Opening 完成状态。`EnemyInfo.TryResolveIntent()` 只使用阶段内游标选择序列，`EnemyTurnIndex` 仅保留为全局统计；阶段切换时统一重置游标，缺少阶段定义、空序列、未知意图类型或非法攻击值时显式拒绝，不回退到基础意图。
2. `EnemyIntentSequencePolicy` 支持一次性 Opening/Intro 与循环 Loop。山野妖猴的嬉斗由集中遭遇定义声明，执行后才激活机制，后续意图按循环序列解析；核心流程不识别具体敌人。
3. 新增 `IntentPresentation` 统一格式化解析意图，攻击、护体、机制护体、易损、机制激活、不攻击、条件效果和定义说明由同一 `EnemyIntent` 输出，UI 与实际结算共用解析结果。
4. 新增 `EnemyDefinitionValidator`，在遭遇解析、战斗进入和自检入口校验阶段、序列、机制和意图数据，避免未知类型或缺定义静默成功。
5. 新增 `ShopDefinition`/`ShopDefinitionCatalog` 作为商店标题、说明和价格的定义入口。`ShopController` 从当前 `MapNodeDefinition.ContentId` 绑定定义，场景标题保持中性占位；离场摘要也动态读取当前商店定义。新增 `NodeContentBindingSelfCheck` 覆盖两个商店 fixture 和两个事件定义。

### 修改文件
- `scripts/Core/BattleState.cs`
- `scripts/Core/GameManager.cs`
- `scripts/Encounter/EnemyDefinitionValidator.cs`（新增）
- `scripts/Core/IntentPresentation.cs`（新增）
- `scripts/Core/EnemyDefinitionExecutionSelfCheck.cs`
- `scripts/Core/RewardBindingSelfCheck.cs`
- `scripts/Core/NodeContentBindingSelfCheck.cs`（新增）
- `scripts/Data/DataDefs.cs`
- `scripts/Data/ShopDefinition.cs`（新增）
- `scripts/Data/EventDefinition.cs`
- `scripts/Encounter/EncounterPool.cs`
- `scripts/Map/B2EncounterProductionSelfCheck.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/ShopController.cs`
- `scenes/Shop/Shop.tscn`
- `docs/开发记录-code.md`

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj`：0 错误、0 警告。
- `dotnet build -c Release .\\AfterHongHuang.csproj`：0 错误、0 警告。
- Godot 4.7 .NET headless：退出码 0；`StateContractSelfCheck`、`MapGraphRngSelfCheck`、`B2EncounterProductionSelfCheck`、`BattleContentBindingSelfCheck`、`EnemyDefinitionExecutionSelfCheck`、`RewardBindingSelfCheck`、`NodeContentBindingSelfCheck` 均输出 PASS。500 seeds 统计：失败 0、路径 4..9、商店 2..3、尝试次数 1/12.41/69、fingerprint 500、交叉 0、混合层指标通过。
- 门禁扫描未发现 unknown 奖励 ID、旧静态地图旁路、`DisplayName ?? NodeId`、空起点、F10/DebugService/DebugForceVictory 或 `EnemyTurnIndex` 作为阶段序列索引。现存 `System.Random` 命中仅为既有 R-039 奖励稳定流遗留：`MapController.cs`、`DaoMarkSelectController.cs`、`BattleController.cs`，本轮未扩大修复。
- 具体敌人名称/ID扫描仅命中 `EncounterPool`、`MapNodeContentCatalog` 等集中定义和 B2/执行自检 fixture；`scripts/UI`、`scripts/Core` 没有按敌人名或 ID 分支。`ActDefinition` 的层窗口属于集中规则定义，不是生成器按层号投机分配；`ShopDefinition` 的“行脚宝商”属于生产商店定义，Shop 场景和控制器不再拥有该固定文案。

### 已知问题
- R-039 的生产奖励随机仍未迁移到稳定 Reward 流，RewardId 消费集合持久化也未在本轮处理，不能据此宣称奖励幂等已完成。
- 尚未完成窗口实机验证；需重点复测阶段切换连续意图、猴子 7 回合 Opening/Loop、散修摘要同时显示伤害与护体、石精护体破除后碎岩，以及不同 Shop/事件定义的页面绑定。

---

## 2026-07-13 | ACT1-FEEDBACK-C3 QA3 战斗事实与奖励上下文修复

### 根因与修复
1. `EnemyInfo` 现在通过 `EnemyPhaseDefinition`、`EnemyMechanicDefinition` 和 `EnemyIntent` 统一解析当前意图、阶段阈值、机制触发、机制显示和结算参数。`GameManager` 不再按敌人/机制补写石精伤害、散修护体或相柳阶段阈值；意图索引、空意图、缺机制定义、缺触发意图和缺显示字段都会记录错误并阻止行动。
2. 碎石护体破除、掠灵生命阈值、阶段型敌人均走同一 `TryResolveIntent` -> `ExecuteEnemyTurn` 链路；UI 的意图摘要也消费同一解析结果。机制日志读取定义中的显示名和说明，不再写死具体机制名。
3. 奖励入口改为要求有效 `ActiveNode`、`RewardContext`、奖励槽和三张卡牌选项。`GameManager.TryCreateRewardId` 由真实节点构造 `card/lingyun` 奖励 ID，`GrantReward`、灵韵暂存/领取和 CardReward overlay 均校验节点归属；移除 `unknown` 伪 ID。奖励随机稳定流与 RewardId 持久化消费集合仍属于 R-039 后续范围。
4. `RunState.CurrentMapNodeId` 改为空初始化，仅由 `StartNewRun` 写入生成图起点；`MapRenderer` 和 `MapGraphValidator` 在缺显示名时拒绝绘制/验证，不把内部 NodeId 当玩家文案。
5. 扩展 `BattleContentBindingSelfCheck` 与 `RewardBindingSelfCheck`，新增 `EnemyDefinitionExecutionSelfCheck`：通过双遭遇、无活动节点奖励阻断、双奖励来源、空牌组/缺角色、石精/散修/相柳定义执行和非法空意图反证上述契约。

### 修改文件
- `scripts/Data/DataDefs.cs`
- `scripts/Encounter/EncounterPool.cs`
- `scripts/Core/BattleState.cs`
- `scripts/Core/GameManager.cs`
- `scripts/Core/RewardContext.cs`
- `scripts/Core/RunState.cs`
- `scripts/Core/BattleContentBindingSelfCheck.cs`
- `scripts/Core/EnemyDefinitionExecutionSelfCheck.cs`（新增）
- `scripts/Core/RewardBindingSelfCheck.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/CardRewardController.cs`
- `scripts/UI/MapRenderer.cs`
- `scripts/Map/MapGraphValidator.cs`
- `docs/开发记录-code.md`

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj`：0 错误、0 警告。
- Godot 4.7 Mono headless：退出码 0；`StateContractSelfCheck`、`MapGraphRngSelfCheck`、`B2EncounterProductionSelfCheck`、`BattleContentBindingSelfCheck`、`EnemyDefinitionExecutionSelfCheck`、`RewardBindingSelfCheck` 均输出 PASS，无泄漏警告。
- C1 指标保持：500 seeds 生成失败 0、交叉边 0、完整路径 4..9、可达商店 2..3、fingerprint 500。
- 硬编码扫描：具体敌人/机制名称与数值仅命中 `scripts/Encounter/EncounterPool.cs` 集中定义及 `MapNodeContentCatalog.cs` 内容目录；`scripts/UI`、`scripts/Core`、`scripts/Map` 未命中 `unknown`、旧静态地图入口、`MountainMonkey`、旧机制字段或具体内容流程分支。`System.Random` 既有命中仍属 R-039/旧 UI 路径，本轮未扩大处理。

### 残留风险
- 尚未进行窗口实机操作；需复测不同敌人意图摘要、石精/散修/相柳机制时序、奖励缺上下文阻断和正常卡牌/灵韵领取。
- R-039 的奖励稳定随机流与 RewardId 持久化去重仍待后续任务；本轮只完成真实上下文传递和入口校验。

---

## 2026-07-13 | ACT1-FEEDBACK-C2 QA P0 硬编码门禁修复

### 根因与修复
1. `GameManager.StartNewRun` 原先用 `characterId == "wuzhu"` 绑定 `DataDefs.WuZhuStarterDeck`。新增 `CharacterInfo.StarterDeck` 字段、角色定义查询和 `CharacterDeckFactory`；新局只消费当前 CharacterDefinition，缺失角色或牌组会显式失败，不再默认首项或具体角色分支。
2. `BattleController` 和 `CardRewardController` 原先直接显示具体道痕名称。新增 `RewardContext`、`RewardSourceContext` 和 `DaoMarkInfo.RewardDisplayDescription`；战斗根据当前道痕定义构建上下文，奖励 UI 只消费来源 ID、名称、说明和次数。多个来源按上下文逐项生成独立奖励行。
3. 灵韵奖励解析原先非法区间记录后返回 0，继续伪装成功。新增 `RewardResolver.TryResolveLingYunAmount`；敌人定义、随机流、区间任一无效都会阻止胜利奖励结算，并写入战斗日志和错误日志。
4. 全仓无生产调用的 `MapNodeInfo`、`MapLayerData`、`MapConnection` 和对应 GameManager 旧入口已移除；生产地图继续只使用 `RunState.MapGraph` / `MapNodeDefinition`。
5. 新增 `RewardBindingSelfCheck`：同一 `CharacterDeckFactory` 入口验证生产角色与第二个测试 CharacterDefinition fixture 得到不同牌组；同一 `RewardContext` 入口验证两个不同奖励来源；异常来源和非法灵韵区间均被拒绝。

### 修改文件
- `scripts/Data/DataDefs.cs`
- `scripts/Core/CharacterDeckFactory.cs`（新增）
- `scripts/Core/RewardContext.cs`（新增）
- `scripts/Core/RewardResolver.cs`（新增）
- `scripts/Core/RewardBindingSelfCheck.cs`（新增）
- `scripts/Core/GameManager.cs`
- `scripts/Core/RunState.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/CardRewardController.cs`
- `docs/开发记录-code.md`

### 验证结果
- `dotnet build .\AfterHongHuang.csproj`：退出码 0，0 错误、0 警告。
- `dotnet build -c Release .\AfterHongHuang.csproj`：退出码 0，0 错误、0 警告。
- Godot 4.7 Mono headless：
  `D:\OtherApp\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit-after 15`
  退出码 0；`StateContractSelfCheck PASS`、`MapGraphRngSelfCheck PASS`、`B2EncounterProductionSelfCheck PASS`、`BattleContentBindingSelfCheck PASS`、`RewardBindingSelfCheck PASS`。
- C1 500 seeds 结果保持：生成失败 0、交叉边 0、路径 4..9、商店 2..3、fingerprint 500。

### 硬编码扫描分类
- 集中定义允许命中：`DataDefs.cs` 的角色/道痕定义、`EncounterPool.cs` 的敌人定义、`EventDefinition.cs`/`MapNodeContentCatalog.cs` 的事件和地图内容定义；这些位置不承担控制器流程分支。
- 测试 fixture 允许命中：`BattleContentBindingSelfCheck.cs`、`RewardBindingSelfCheck.cs`、`B2EncounterProductionSelfCheck.cs`、`StateContractSelfCheck.cs`。
- `CharacterSelectController.cs` 的巫祝默认选择属于现有角色选择 UI 口径；`ActDefinition.cs` 的层窗口属于集中规则数据，不是生成器按 layer 的内容分支。
- `scripts/UI`、`scripts/Core`、`scripts/Map`、`scripts/Encounter` 未发现 `MapNodeInfo`、`DataDefs.MapLayers`、`TryGetEnemyById`、`legacy_compat` 或具体敌人/道痕名称参与生产流程；Battle/GameManager 未直接引用具体敌人默认事实。
- 仍命中的 `System.Random` 位于 `BattleController.cs`、`DaoMarkSelectController.cs`、`MapController.cs` 的既有随机路径，属于 R-039/旧 UI 范围，不在本轮重构；不能宣称奖励稳定流或 RewardId 幂等已完成。

### 残留风险
- 未进行窗口鼠标实机操作；需要 QA/用户复测角色牌组解析、不同道痕来源奖励行、非法奖励定义阻断和正常胜利结算。
- R-039 的奖励稳定随机流与 RewardId 持久化去重仍待后续任务。

---

## 2026-07-13 | ACT1-FEEDBACK-C0 战斗内容绑定与取消修复

### 变更概述
1. 修复战斗敌人机制的通用哨兵问题：`BattleState` 改用 `EnemyMechanicActive + EnemyDodgeCounter`，开战默认未激活；只有当前 `EnemyInfo.Mechanic` 匹配且意图数据声明 `MechanicAction.ActivateDodge` 并实际执行后才激活。非对应敌人不会显示、累加或消费斗击闪避计数。
2. `BattleController` 的敌方名称改为读取 `ActiveEncounter.EnemyInfo.Name`；敌人意图改为从名称、伤害、护体、易损、条件伤害和描述字段生成“名称 + 效果摘要”，移除控制器内的具体敌名绑定。
3. 右键取消提升到 Battle `_Input`，在 GUI 子控件处理前统一清理选中、按压、拖拽、箭头和目标提示；保留 `CardButton` 的局部取消回调作为控件边界保护。
4. 新增 Debug 构建自检 `BattleContentBindingSelfCheck`，通过同一个生产 `TryEnterBattle` 入口抽取两个不同遭遇，验证名称/生命/意图来自当前 `EnemyInfo`，并验证机制激活前后及非对应敌人路径。

### 修改文件
- `scripts/Data/DataDefs.cs`
- `scripts/Encounter/EncounterPool.cs`
- `scripts/Core/BattleState.cs`
- `scripts/Core/GameManager.cs`
- `scripts/UI/BattleController.cs`
- `scripts/Core/BattleContentBindingSelfCheck.cs`（新增）
- `docs/开发记录-code.md`

### 验证结果
- `dotnet build .\AfterHongHuang.csproj`：成功，0 错误，0 警告。
- `dotnet build -c Release .\AfterHongHuang.csproj`：成功，0 错误，0 警告。
- Godot 4.7 Mono headless：
  `D:\OtherApp\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit-after 15`
  退出码 `0`；输出包含 `StateContractSelfCheck PASS`、`MapGraphRngSelfCheck PASS`、`B2EncounterProductionSelfCheck PASS` 和 `BattleContentBindingSelfCheck PASS`，无泄漏警告。
- 硬编码扫描：`scripts/UI`、`scripts/Core` 未发现具体 ACT1 敌名/敌 ID、`MountainMonkey`、`TryGetEnemyById` 或旧 `MonkeyDodgeCounter`；`scripts/Encounter/EncounterPool.cs` 的敌名/ID仅为集中定义，既有 `scripts/Map/B2EncounterProductionSelfCheck.cs` 的 ID 为测试 fixture。

### 残留风险与范围边界
- `scripts/Map/MapGraphGenerator.cs` 仍有既有固定层号节点元数据和 Boss 显示名绑定；本任务明确禁止修改地图生成文件，已作为地图算法/硬编码门禁的独立后续项记录，不能以本轮战斗修复宣称全仓门禁清零。
- `scripts/UI/BattleController.cs`、`DaoMarkSelectController.cs`、`MapController.cs` 仍存在既有 `System.Random`，不属于本轮新增战斗内容绑定入口；稳定随机迁移和奖励随机迁移仍需后续任务处理。
- 未执行窗口鼠标实机操作；需要用户复测不同敌人进入战斗、意图摘要、机制在对应强化动作后出现，以及选中/拖拽/箭头状态下右键取消。

---

## 2026-07-13 | ACT1-PHASE-B2 遭遇池、7 敌人与生产 MapGraph 原子切换

### 变更概述
1. `RunState` 现在保存新局生成的唯一生产 `MapGraph` 和当前稳定节点 ID；`MapController`、`MapRenderer`、Battle 只读地图 overlay、`LingmaiController` 不再读取旧静态地图表。旧静态路线数据已移除；旧 `MapNodeInfo` 编译兼容入口已在 C2 清理。
2. 新增 `EncounterPool` / `EnemyDefinition` 数据层，弱怪、强怪和 Boss 分池，遭遇通过 `NodeContext.PoolId + Encounter` 稳定流解析为本场 `EnemyInfo` 副本。已接入山野妖猴、碎石山精、掠灵散修、不周残兵、符阵散修、山魈王和相柳之骸；战斗逻辑按机制枚举处理，不按敌人名称分支。
3. 战斗意图支持护体、易损、阶段/狂势、护体破除和无攻击回合加成等 ACT1 MVP 机制；灵韵奖励区间由遭遇定义提供。新增 `wx_a1_boss_001~003` Boss 天品固定池，Boss 战使用固定三选一入口。
4. 新增 `Shop.tscn` / `ShopController` 和 `Event.tscn` / `EventController`。商店提供稳定三卡库存、购买、已售状态和离开结算；事件接入天河倒灌、散修遗篆的可用选项、条件禁用、节点事件流和明确 `NodeResult`。
5. `MapGraphValidator` 显式拒绝重复 NodeId 和重复边；新增 B2 Debug 自检，覆盖遭遇确定性、7 个定义、Shop/Event 资源和重复结构防御。

### 修改文件
- `scripts/Core/RunState.cs`、`scripts/Core/NodeContext.cs`、`scripts/Core/BattleState.cs`、`scripts/Core/GameManager.cs`
- `scripts/Data/DataDefs.cs`
- `scripts/Encounter/EncounterPool.cs`
- `scripts/Map/MapNodeDefinition.cs`、`MapGraph.cs`、`MapGraphGenerator.cs`、`MapGraphValidator.cs`、`B2EncounterProductionSelfCheck.cs`
- `scripts/UI/MapController.cs`、`MapRenderer.cs`、`BattleController.cs`、`LingmaiController.cs`
- `scripts/UI/ShopController.cs`、`scripts/UI/EventController.cs`
- `scenes/Shop/Shop.tscn`、`scenes/Event/Event.tscn`
- `docs/开发记录-code.md`

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj`：成功，0 错误，0 警告。
- `dotnet build -c Release .\\AfterHongHuang.csproj`：成功，0 错误，0 警告。
- Godot 4.7 mono headless：`D:\OtherApp\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit-after 15`，退出码 0。
- headless 输出：`[StateContractSelfCheck] PASS`、`[MapGraphRngSelfCheck] PASS seeds=500 failures=0 paths=4..9 shops=3..3 attempts=1/1.00/1 fingerprints=500`、`[B2EncounterProductionSelfCheck] PASS seeds=500 encounterNodes=8000 fingerprints=500 pools=weak/strong/boss pages=Shop/Event`；无 ObjectDB 泄漏警告。
- `Map.tscn`、`Battle.tscn`、`Shop.tscn`、`Event.tscn` 均完成 headless 场景加载检查；直接无上下文启动 Shop/Event/Battle 时会记录明确无效入口并阻止假成功。
- 静态扫描：生产 Map/Battle/GameManager/Lingmai 路径不再引用旧静态路线或具体妖猴定义；无 F10/DebugService/DebugForceVictory。Reward 卡牌抽取仍保留既有 `System.Random`，因此没有宣称 Reward 流迁移或 RewardId 幂等完成。

### 节点覆盖矩阵
| MapGraph 节点 | 生产入口 | 结算 |
|---|---|---|
| Weak / Strong / Boss | `EncounterPool` → `Battle.tscn` | Battle `Completed/Defeated` → 统一 NodeResult |
| Lingmai | `Lingmai.tscn` | 既有成功 Completed / 返回 Exited |
| Shop | `Shop.tscn` | 购买后离开时 Completed |
| Event | `Event.tscn` | 选项或离开时 Completed |

### 残留风险
- `RewardGrant` 尚无完整持久化 RewardId 消费表；普通 CardReward 抽卡仍使用旧随机实现，符修额外奖励和重复领取的全流程幂等留待奖励阶段。
- 商店本轮为可结算最小页面，尚未接入法宝货架、重列货架和牌组移除服务；法宝选项在事件中按策划口径明确禁用，不伪造商品。
- 现有节点页面的 TopBar/地图只读 overlay 复用仍需 UI/UX 做窗口实机检查；本轮未进行鼠标操作和完整 9 层通关实机验收。

## 2026-07-13 | ACT1-PHASE-B1 稳定随机流与 9 层 MapGraph 生成器

### 前置加固
1. 修复 R-038：LingmaiScene 缺少 ActiveNode 或 ActiveNode 类型错误时，不创建正常 Exited 结果；通过 `RecoverFromInvalidNodeEntry()` 清理错误上下文、恢复地图入口，不写 Completed/Skipped/Abandoned，不推进路线。
2. 收敛 R-040：BattleController 不再直接写 `CurrentState`；战斗结果提交和统一离场 API 由 GameManager 设置胜利结算/失败/地图状态。
3. 增加最小 `RewardGrant/RewardId` 接口，CardReward 和灵韵领取改走 GameManager 奖励写入端口；本阶段尚未实现完整 RewardId 消费表幂等。

### 稳定随机流
1. 新增 `StableHash`，使用标准 SHA-256 的前 64 位派生 seed，不使用 `string.GetHashCode()`、系统时间或 UI 顺序作为局部随机事实。
2. 新增 `StableRandom`：SplitMix64 初始化 xoshiro256** 四状态，使用 Fisher-Yates 洗牌；算法状态只由 seed 决定，避免 `System.Random` 跨版本行为作为路线事实。
3. `StableRandomStreams` 从 `RunSeed + ActId + RuleVersion + StreamKey + Scope + Index` 派生 Map、Encounter、Event、Shop、Reward、Combat 六条隔离流；每次创建独立 RNG 实例。
4. BattleState 的初始洗牌/重洗使用 `Combat(NodeId, ShuffleIndex)`，永久套牌初始化不再洗牌，AddCardToDeck 不触碰活动战斗抽牌堆。Reward 流已提供稳定 API，但 CardReward 卡池抽取仍保留既有生产逻辑，未宣称完成奖励随机流迁移。

### MapGraph 模型与生成
1. 新增纯数据 `ActDefinition`、`MapGraph`、`MapNodeDefinition`、`MapNodeRuntime`、`MapGraphEdge`，不依赖 UI Control，也不复制 DataDefs.MapLayers 作为生产地图。
2. ACT1 固定 9 层节点数 `1/2/3/3/4/4/4/3/1`；L0 Start，L1~3 Weak，L4/L7 Strong，L5 Lingmai，L6 含 3 个 Shop、1 个 Event，L8 Boss。
3. 生成器先构造 4 条候选路线并集，再按 L3 代表路线布置商店，保证每个 L3 至少有一条经过 Shop 到 Boss 的路径，同时保留绕开全部 Shop 的路径。
4. `MapGraphValidator` 验证相邻层连边、节点入/出边、全节点有效路径、路径数 4~12、Strong/Lingmai 必经、事件上限、Shop 数量、非连续特殊节点、L3 经 Shop 到 Boss 等硬约束。
5. 生成失败达到 64 次上限时返回包含 RunSeed/规则版本/最后错误的明确失败，不回退静态图或添加隐藏边。
6. B1 只通过 Debug 自检使用生成器，MapController/MapRenderer 仍使用当前静态地图，未留下生产启用开关。

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj` → 成功，0 错误，0 警告。
- `dotnet build -c Release .\\AfterHongHuang.csproj` → 成功，0 错误，0 警告。
- Godot 4.7 mono headless 命令：`D:\OtherApp\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit-after 8`。
- headless 输出：`[StateContractSelfCheck] PASS`、`[MapGraphRngSelfCheck] PASS seeds=500 failures=0 paths=4..9 shops=3..3 attempts=1/1.00/1 fingerprints=500`，退出码 0，无自检失败、无泄漏警告。
- 静态扫描：`scripts/Map` 和 Stable RNG 实现无 `System.Random`/`string.GetHashCode()`；BattleController 无直接 `CurrentState =`；无 F10/DebugService/DebugForceVictory；Battle/GameManager 无直接 `DataDefs.MountainMonkey`。

### 未接生产范围与残留风险
- MapController/MapRenderer 尚未消费 MapGraph；B2 需与 EncounterPool、6 敌人和相柳内容一起原子切换，避免生成无内容节点。
- Reward 流只有稳定 API，CardReward 抽卡和完整 RewardId 幂等留待后续奖励阶段。
- 当前生成规则固定 L6 为 3 个商店和 1 个事件，后续可在不破坏 Validator 硬约束的前提下扩展权重。
- 尚未做窗口实机验证；本阶段交付为数据生成器、批量自检和 headless 验证。

---

## 2026-07-13 | ACT1-PHASE-A-REV1 QA 阻断修复

### 首轮 QA 失败与根因
1. 首轮 `StateContractSelfCheck` 在第一场战斗退出后没有恢复 `CurrentState`，第二场进入被“战斗中”拒绝；异常只打印，headless 仍返回 0。
2. 旧 `EncounterResult` 只有成功/放弃布尔值，失败提交返回 false，且只阻止 Completed 重复，Skipped/Abandoned 可以被覆盖。
3. 进入节点时提前写入 `CurrentMapLayer/Index`，失败/放弃存在错误推进路线的风险；灵脉场景也从坐标反推入口。
4. `ShuffleDrawPile` 同时承担永久套牌和战斗抽牌堆；奖励入牌会错误触碰活动战斗抽牌堆。

### 修复内容
1. 新增 `NodeResultType`、唯一 `ResultId`、`ConsumeNode`、`AdvanceRoute`；`CreateNodeResult()` 按结果类型生成真值，`SubmitNodeResult()` 校验真值并统一提交。
2. `Completed`、`Exited/NodeSkipped`、`Defeated/Abandoned` 分别映射到 Completed、Skipped、Abandoned；任何终态第二次提交或覆盖均拒绝，合法失败结果本身返回 true。
3. `TryActivateNode()` 不再修改地图坐标；只有幂等 `ApplyNodeResult()` 且 `AdvanceRoute=true` 时推进到 ActiveNode 坐标。失败/放弃保持原路线位置。
4. 新增 `ExitBattleToMap()`、`ExitBattleToTitleAfterDefeat()`、`ExitLingmaiToMap()`，统一负责结果后清理临时状态、清理上下文和恢复流程状态。
5. `LingmaiController` 改为消费 `ActiveNode.NodeId/NodeType`，成功使用 Completed，未行动使用 Exited/Skipped，不再从 CurrentMapLayer/Index 猜入口。
6. `ShuffleBattleDrawPile()` 只处理 BattleState；StartNewRun 不洗永久套牌，AddCardToDeck 只追加永久套牌，不触碰活动战斗抽牌堆。
7. 强化 Debug 状态契约自检：两场战斗、CardRuntime 引用隔离、5 张起手、临时牌堆清空、HP 保留、奖励入永久套牌、结果终态不可覆盖、灵脉 Exited 推进但不完成。
8. Debug 自检失败通过 `SceneTree.Quit(1)` 终止；通过时只输出唯一 `[StateContractSelfCheck] PASS` 标记。Release 不执行自检。

### 结果真值表
| `NodeResultType` | 生命周期终态 | `ConsumeNode` | `AdvanceRoute` |
|---|---|---:|---:|
| `Completed` | `Completed` | true | true |
| `Exited` | `Skipped` | false | true |
| `NodeSkipped` | `Skipped` | false | true |
| `Defeated` | `Abandoned` | false | false |
| `Abandoned` | `Abandoned` | false | false |

### 修改文件
| 文件 | 变更 |
|---|---|
| `scripts/Core/NodeContext.cs` | NodeResultType、NodeResult 和旧接口兼容层 |
| `scripts/Core/RunState.cs` | ResultId 幂等集合 |
| `scripts/Core/GameManager.cs` | 结果提交/路线应用/统一离场/战斗洗牌职责 |
| `scripts/Core/StateContractSelfCheck.cs` | 两战、结果终态、HP/套牌和灵脉 Exited 自检 |
| `scripts/UI/BattleController.cs` | 使用明确战斗结果和统一离场 API |
| `scripts/UI/LingmaiController.cs` | 使用 ActiveNode 和明确灵脉结果 |
| `docs/开发记录-code.md` | 记录首轮 QA 失败与 REV1 修复 |

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj` → 成功，0 错误，0 警告。
- `dotnet build -c Release .\\AfterHongHuang.csproj` → 成功，0 错误，0 警告。
- Godot headless 命令：`D:\OtherApp\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit-after 3`。
- headless 结果：Godot `4.7.stable.mono`，输出唯一 `[StateContractSelfCheck] PASS`，`HEADLESS_EXIT_CODE=0`，无“状态契约自检失败”，无 ObjectDB 泄漏警告。
- 静态扫描确认无 F10/DebugService/DebugForceVictory；`GameManager.cs`、`BattleController.cs` 无直接 `DataDefs.MountainMonkey`。

### 残留风险
- 尚未做窗口实机操作，仍需 QA 验证战斗胜利/失败、灵脉成功/返回、地图只读和奖励领取后的场景表现。
- `SubmitEncounterResult(EncounterResult)` 仅保留编译兼容层，新流程未使用；后续可在无外部调用后删除。
- NavigationState 的 `OpenMapOnEnter/LastLingmaiResult` 仍是迁移期兼容债务，Phase B 再收口。

---

## 2026-07-13 | ACT1-PHASE-A 第一大关架构端口与战斗生命周期

### 变更概述
1. 接入 `RunState` / `BattleState` 边界：生命上限、当前生命、基础最大灵力、永久套牌、灵韵、道痕和地图进度留在 RunState；当前灵力、护体、斗劲、易损/永炎、抽牌堆、手牌、弃牌堆、消弭堆和敌人运行态只存在 BattleState。
2. 新增显式 `NodeContext`、`EncounterRequest`、`EncounterResult`。Map 节点入口创建遭遇请求，Battle 只消费 ActiveEncounter；缺少敌人定义时记录错误并阻止进入，不静默替换为妖猴。
3. 地图节点生命周期区分 `Active`、`Completed`、`Skipped`、`Abandoned`。进入战斗/灵脉只激活节点；战斗胜利或灵脉成功结算提交结果后才完成，重复提交会被拒绝；灵脉无行动返回记录为 Skipped。
4. 连续战斗从永久套牌复制新的 `CardRuntime` 实例建立本场牌堆，战斗离场销毁临时状态，当前生命和永久套牌不重置。奖励卡和灵脉升级只写永久套牌，套牌页面改读永久套牌事实来源。
5. `PlayerState` 扩展为灵脉中、战斗胜利结算、失败等流程态，MapRenderer 仅在空闲探索态允许推进；初始道韵固化后不可再次点击。
6. 导航字段 `OpenMapOnEnter` / `LastLingmaiResult` 移到独立 `NavigationState`，仅作为现有场景切换兼容债务保留，新的节点契约不依赖其扩展流程。
7. Debug 构建新增不改当前运行态的状态契约自检，覆盖 10 张永久套牌、5 张起手、奖励入永久套牌、战斗临时状态销毁、HP 保留、第二场重新建牌堆和重复结果拒绝。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/Core/GameManager.cs` | RunState/BattleState 兼容属性、显式节点/遭遇入口、结果提交、战斗实例创建与销毁 |
| `scripts/Core/RunState.cs` | 永久运行状态和 Active/Completed/Skipped/Abandoned 生命周期 |
| `scripts/Core/BattleState.cs` | 单场战斗临时状态容器（接入 GameManager） |
| `scripts/Core/NodeContext.cs` | 节点、遭遇请求、结果契约和 EncounterTier 枚举 |
| `scripts/Core/NavigationState.cs` | 迁移期场景导航兼容状态 |
| `scripts/Core/StateContractSelfCheck.cs` | Debug 构建状态契约自检 |
| `scripts/Data/DataDefs.cs` | 敌人 ID 注册查找和流程状态枚举扩展 |
| `scripts/UI/MapController.cs` | 通过 GameManager 显式进入战斗/灵脉，不提前完成节点 |
| `scripts/UI/MapRenderer.cs` | 非探索态只读、道韵固化后禁用 |
| `scripts/UI/BattleController.cs` | 消费 ActiveEncounter、提交战斗结果、清理战斗临时状态 |
| `scripts/UI/LingmaiController.cs` | 成功提交灵脉结果，未行动走 Skipped 并清理上下文 |
| `scripts/UI/CharacterSelectController.cs` | 新局入口改用 StartNewRun |
| `scripts/UI/DeckViewer.cs`、`scripts/UI/DeckDisplayController.cs` | 改读永久套牌 |
| `docs/开发记录-code.md` | 记录本阶段迁移边界、验证和后续风险 |

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj` → 成功，0 个错误，0 个警告。
- `dotnet build -c Release .\\AfterHongHuang.csproj` → 成功，0 个错误，0 个警告。
- 静态扫描确认 `GameManager.cs`、`BattleController.cs` 战斗路径不再直接引用 `DataDefs.MountainMonkey`；敌人只通过 `DataDefs.TryGetEnemyById()` 进入 ActiveEncounter。
- 静态扫描确认 `VisitedNodeIds.Add()`、`RestoreExhaustedCards()`、`InitBattle()` 旧战斗生命周期入口已不再存在于 UI/核心调用路径。
- Debug 构建自检代码已加入 GameManager `_EnterTree()`，但本机未找到 Godot 4.7 .NET headless 可执行文件，未伪造运行时自检或窗口验证。

### 已知问题与 Phase B 待办
- `OpenMapOnEnter` / `LastLingmaiResult` 仍是 NavigationState 兼容债务，后续应由统一场景路由/NodeResult 替代。
- 当前仅注册 `enemy_monkey`，随机地图、多敌人、Boss 池留待 Phase B；缺失敌人 ID 会阻止进入，不会静默兜底。
- 当前 `CardRuntime` 已按实例复制，但 `CardInfo` 定义仍是静态引用；后续若卡牌运行态扩展临时字段，需要建立显式 CardInstance 数据结构。
- 未进行 Godot 窗口实机和 headless 场景链路验证，需要用户/QA 复测连续两场战斗、战败返回、奖励入牌、灵脉成功/跳过和地图只读状态。

---

## 2026-07-13 | DBG-ROLLBACK-20260713 回退开发期 F10 调试接口

### 回退原因
按用户要求移除最近新增的开发期 Debug/F10 快捷面板，不开展第一大关实现；保留正式地图/灵脉场景流、`OpenMapOnEnter`、`LastLingmaiResult` 和既有 F9 手牌布局调试工具。

### 移除范围
1. 删除 `project.godot` 中的 `DebugService` Autoload，保留 `GameManager`。
2. 删除 `scripts/Debug/DebugService.cs`、`DebugService.cs.uid`、`DebugPanelController.cs`、`DebugPanelController.cs.uid` 和 `scenes/Debug/DebugPanel.tscn`，并清理空目录。
3. 删除 `GameManager.DebugForceVictory` 字段及 `InitCharacter()` 中对应清理。
4. 删除 `BattleController` 中仅用于消费 `DebugForceVictory` 的 Debug 条件代码；保留 F9 手牌布局 Debug overlay。

### 验证结果
- 源码、工程配置和场景文件扫描确认已无 `DebugService`、`DebugPanel`、`DebugForceVictory`、F10 快捷面板实现引用；本记录中保留历史任务记录文字作为回退审计记录。
- `dotnet build .\\AfterHongHuang.csproj` → 成功，0 个错误 0 个警告。
- `dotnet build -c Release .\\AfterHongHuang.csproj` → 成功，0 个错误 0 个警告。
- F9 手牌布局 Debug overlay 仍保留，`BattleController` 的 F9 入口和 `HandLayoutDebugOverlay` 引用扫描正常。
- Godot headless → 本轮未运行；当前任务未要求重新做窗口启动，且此前环境 PATH 中无 Godot 4.7 .NET 控制台程序。

### 已知问题
- 回退后不再提供 P1-011 的 F10 快捷入口；后续如需调试工具应重新立项，不在本次回退中恢复。

---

## 2026-07-13 | BUG-20260713-001 Debug 面板跨场景 F10 重复使用修复

### 变更概述
1. 根因确认：DebugService 只使用 `_UnhandledInput`，且服务/面板未显式设置 `Always` 处理模式，场景切换后输入链存在被其他节点吞掉或暂停处理的风险。
2. DebugService 改用 `_Input` 接收 F10，移除 `SetInputAsHandled()`，并在 `_EnterTree()` 设置 `ProcessModeEnum.Always`。
3. DebugPanelController 和 `DebugPanel.tscn` 同样设置 Always 处理模式；面板仍挂在根窗口下，跨 Title/Map/Battle/Lingmai 保持有效。
4. 调试入口触发场景切换前自动隐藏面板但不释放实例，F10 或关闭按钮后仍可再次打开；失效引用会在 Toggle 时清空并重建。
5. 所有地图/战斗/灵脉/模拟胜利/重置按钮仍每次重新准备测试状态，可重复使用；一次性限制只作用于 `DebugForceVictory` 和 `OpenMapOnEnter`。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/Debug/DebugService.cs` | Always 生命周期、`_Input` F10、场景切换隐藏和面板引用恢复 |
| `scripts/Debug/DebugPanelController.cs` | 面板设置 Always 处理模式 |
| `scenes/Debug/DebugPanel.tscn` | 根节点设置 `process_mode = 3`（Always） |
| `docs/开发记录-code.md` | 记录 BUG-20260713-001 根因、修复和验证 |

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj` → Debug 成功，0 个错误 0 个警告。
- `dotnet build -c Release .\\AfterHongHuang.csproj` → Release 成功，0 个错误 0 个警告。
- 静态确认 F10 使用 `_Input`，服务/面板均为 Always，场景按钮隐藏面板而不释放，失效引用可重建。
- 静态确认 Release 仍由 `#if DEBUG` 与 `OS.IsDebugBuild()` 双重隔离。
- Godot headless 未运行：PATH 中未找到 `Godot_v4.7-stable_mono_win64_console.exe`，未伪造窗口验证。

### 已知问题
- 尚未进行实际窗口操作，需用户/QA 重复验证：F10 开关、按钮跳转后 F10 重开、关闭按钮后 F10 重开，以及连续多次点击各测试入口。

---

## 2026-07-13 | P1-011 开发期调试接口与快捷面板

### 变更概述
1. 新增 `DebugService` Autoload，注册顺序位于 `GameManager` 之后；面板挂在根窗口下，场景切换时不会被 Map/Battle/Lingmai 销毁。
2. 新增 `DebugPanel.tscn` 与 `DebugPanelController.cs`，Debug 构建按 F10 打开/关闭，面板显示当前场景、地图层/索引、生命/灵力和调试开关状态。
3. 调试按钮统一调用 `DebugService` 公共方法：重置测试状态、直接打开地图、进入战斗、进入灵脉、模拟真实胜利奖励、补满生命/灵力和关闭面板；按钮不直接写 GameManager 状态。
4. 新增 `GameManager.DebugForceVictory` 一次性标记。BattleController 仅在 `#if DEBUG` 且 `OS.IsDebugBuild()` 时消费该标记，设置敌人生命为 0 后调用真实 `CheckBattleResult()`，生成真实胜利弹窗、灵韵和卡牌奖励入口。
5. DebugService 的所有入口在 Release 构建中均被隔离；重置和测试状态准备会清理奖励暂存、访问集合、地图返回意图、战斗结果和 DebugForceVictory，避免污染正常流程。

### 使用方式
- Debug 构建启动后按 `F10` 打开/关闭调试面板。
- `重置测试状态`：回到 MapScene 初始道韵选择。
- `直接打开地图`：巫祝 + 九乌坠日测试道痕，直接打开地图卷轴。
- `直接进入战斗`：进入第一层战斗节点。
- `直接进入灵脉`：进入 layer 2 / index 0 的真实 LingmaiScene，未预先消耗节点。
- `模拟战斗胜利并打开奖励`：进入真实 BattleScene，自动走真实胜利和奖励流程。
- `补满生命 / 灵力`：只修改当前 GameManager 数值；不支持实时刷新的场景会输出日志说明。

### 修改文件
| 文件 | 变更 |
|------|------|
| `project.godot` | 新增 `DebugService` Autoload，位于 `GameManager` 之后 |
| `scripts/Core/GameManager.cs` | 新增 `DebugForceVictory` 状态 |
| `scripts/Debug/DebugService.cs` | 新增 Debug 构建限定的全局调试服务和状态准备接口 |
| `scripts/Debug/DebugPanelController.cs` | 新增面板按钮转发和状态显示 |
| `scenes/Debug/DebugPanel.tscn` | 新增调试面板场景 |
| `scripts/UI/BattleController.cs` | Debug 构建中一次性消费 DebugForceVictory 并走真实胜利判断 |
| `docs/开发记录-code.md` | 记录 P1-011 实现、使用方式和验证结果 |

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj` → Debug 成功，0 个错误 0 个警告。
- `dotnet build -c Release .\\AfterHongHuang.csproj` → Release 成功，0 个错误 0 个警告。
- 静态确认 Autoload 顺序为 `GameManager` → `DebugService`；DebugPanel、DebugService、Lingmai 场景文件均存在。
- 静态确认 F10、DebugService 入口和 DebugForceVictory 消费均受 `#if DEBUG` 与 `OS.IsDebugBuild()` 限制；Release 不创建面板、不响应快捷键、不消费模拟胜利标记。
- Godot headless 未运行：PATH 中未找到 `Godot_v4.7-stable_mono_win64_console.exe`，未伪造窗口或场景验证。

### 已知问题
- 当前无法在本机做窗口实测，需要用户/QA 使用 Debug 构建确认 F10 面板显示、场景切换后面板持续存在、真实奖励弹窗和灵脉选项流程。
- “补满生命/灵力”对 BattleScene/LingmaiScene 仅修改 GameManager 并记录日志，面板不强行调用未公开的正常 UI 刷新接口。

---

## 2026-07-12 | P1-010 灵脉独立场景化与 Map 底页移除

### 变更概述
1. 新增 `Lingmai.tscn` 与 `LingmaiController.cs`，将灵脉选项、升级选卡、返回地图和成功结算从 `MapController` 的动态 overlay 迁移到独立场景。
2. MapScene 已选道痕后的主体不再渲染“当前节点摘要”页；初始未固化道痕时的道韵选择页保留。
3. 点击可达灵脉后，MapController 更新当前位置但不访问消耗，切换到 `res://scenes/Lingmai/Lingmai.tscn`。
4. LingmaiScene 保留休养生息、精进道行、疗愈道友禁用和返回地图；返回/取消不加入 `VisitedNodeIds`，成功休养或精进才加入。
5. `GameManager.OpenMapOnEnter` 统一战斗胜利返回和灵脉返回后的自动开图意图，MapController 进入场景后通过 deferred 一次性消费，直接展开地图卷轴。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/MapController.cs` | 移除灵脉 overlay/结算逻辑；已选道痕后主体留空；灵脉改为场景切换；统一消费自动开图标记 |
| `scripts/UI/LingmaiController.cs` | 新增独立灵脉场景控制器和 MVP 互动流程 |
| `scenes/Lingmai/Lingmai.tscn` | 新增独立灵脉场景及基础 UI 节点 |
| `scripts/Core/GameManager.cs` | 新增 `OpenMapOnEnter`、`LastLingmaiResult` 状态 |
| `scripts/UI/BattleController.cs` | 战斗胜利返回地图改为设置 `OpenMapOnEnter` |
| `docs/开发记录-code.md` | 记录 P1-010 实现、验证和限制 |

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj` → 成功，0 个错误 0 个警告。
- 静态确认 `MapController -> res://scenes/Lingmai/Lingmai.tscn -> LingmaiController -> res://scenes/Map/Map.tscn` 路径存在。
- 静态确认 `VisitedNodeIds.Add()` 只在 `LingmaiController.CompleteLingmai()` 成功结算路径中执行；返回地图和精进取消不消耗灵脉。
- 静态确认旧 `ShowLingmaiOptions`、`ShowLingmaiUpgradeChoices`、`OnLingmaiHealPressed`、`OnLingmaiUpgradeCardPressed`、`CompleteLingmaiNode` overlay 实现已从 MapController 移除。
- Godot headless 未运行：PATH 中未找到 `Godot_v4.7-stable_mono_win64_console.exe`，未伪造场景加载验证。

### 已知问题
- 需要用户窗口实机复测战后自动开图、地图进入灵脉场景、休养/精进/取消/返回路径和成功结算后的下一层节点点击。
- LingmaiScene 的基础布局已场景化，按钮列表仍由独立控制器动态生成；后续可在 UI 资源稳定后继续抽取可复用按钮主题。

---

## 2026-07-12 | BUG-20260712-002 / BUG-20260712-003 战后地图直达与灵脉跳过流程

### 变更概述
1. `MapController._Ready()` 在战斗胜利返回 `Map.tscn` 后通过 `CallDeferred` 自动打开地图卷轴，战后主流程不再停留在当前节点摘要页。
2. 自动开卷只消费一次 `BattleOver && PlayerWon` 标记，并保留初次进入地图/道韵选择流程不自动开卷。
3. `EnterLingmaiNode()` 进入灵脉时立即更新 `CurrentMapLayer` / `CurrentMapIndex` 并保持 `CurrentState = 空闲`，但不加入 `VisitedNodeIds`。
4. 灵脉说明明确允许返回地图继续前行，未选择行动不会消耗灵脉；返回后下一层可达性按灵脉节点当前位置计算。
5. 成功休养或精进仍由 `CompleteLingmaiNode()` 标记节点已访问，精进选卡取消和返回仍不消耗灵脉。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/MapController.cs` | 战后自动开卷、灵脉进入位置更新、允许跳过行动的界面说明 |
| `docs/开发记录-code.md` | 记录 BUG-20260712-002 / BUG-20260712-003 修复和验证 |

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj` → 成功，0 个错误 0 个警告。
- Godot headless 未运行：PATH 中未找到 `Godot_v4.7-stable_mono_win64_console.exe`，因此未伪造启动验证结果。

### 已知问题
- 需要用户实机复测战斗胜利继续是否直接显示地图卷轴，以及灵脉返回地图后 Boss 节点是否可继续点击。

---

## 2026-07-12 | BUG-20260712-001 胜利继续进入独立地图并修复灵脉入口流程

### 变更概述
1. 修复胜利弹窗“继续”错误绑定 `ShowMapOverlay()` 的问题，改为调用已有 `GoToMap()`，从战斗场景切换到独立 `Map.tscn`。
2. 保持战斗中 TopBar 地图按钮的只读语义：`ShowMapOverlay()` 仍用于战斗中查看/关闭地图；`OnBattleMapNodePressed()` 增加只读边界日志，不再修改节点访问状态、位置或切换场景。
3. 切换到地图前清理战斗地图 overlay、胜利弹窗和卡牌奖励 overlay，避免旧 UI 节点残留并阻挡新 MapScene 的灵脉交互。
4. `CardRewardHelper` 为奖励 overlay 增加统一名称和父节点清理方法，保留奖励选择/跳过逻辑，不扩大为奖励系统重构。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/BattleController.cs` | 胜利继续改为 `GoToMap()`；清理战斗 overlay；战斗地图点击保持只读 |
| `scripts/UI/CardRewardController.cs` | 增加奖励 overlay 生命周期清理接口和稳定节点名称 |
| `docs/开发记录-code.md` | 记录 BUG-20260712-001 根因、修复和验证结果 |

### 验证结果
- `dotnet build .\\AfterHongHuang.csproj` → 成功，0 个错误 0 个警告。
- 代码检查确认：胜利继续 → `GoToMap()` → 清理战斗层 → `GameManager.GoToScene("res://scenes/Map/Map.tscn")`。
- 代码检查确认：MapScene 中灵脉仍由 `MapController.OnMapNodePressed()` → `EnterLingmaiNode()` → `ShowLingmaiOptions()` 处理，进入时不提前消耗节点。
- Godot headless / 窗口实机验证仍需环境可用后执行；本轮未伪造窗口验证结果。

### 已知问题
- 需要用户实机复测胜利结算、卡牌奖励选择/跳过、继续进入地图，以及从地图点击灵脉后选项 overlay 是否正常出现。

---

## 2026-07-12 | BUG-20260711-001 灵脉节点地图按钮点击区域修复

### 变更概述
1. 修复 `MapController.BuildInteractiveMap()` 中地图节点按钮只有 `CustomMinimumSize`、没有实际 `Size` 的问题。
2. 为直接挂在普通 `Control`（`_mapContent`）下的道韵、战斗、灵脉和 Boss 按钮显式设置 `Size = new Vector2(NodeWidth, NodeHeight)`，确保节点拥有稳定的非零点击区域。
3. 保持 `MapRenderer.IsNodeAccessible()` 的灵脉可达判定和 `OnMapNodePressed() -> EnterLingmaiNode()` 回调链不变；灵脉仍然进入时不消耗，成功结算后才标记已访问。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/MapController.cs` | 补齐所有交互地图节点按钮的实际尺寸 |
| `docs/开发记录-code.md` | 记录 BUG-20260711-001 修复、验证和限制 |

### 验证结果
- 代码检查确认 `MapRenderer.IsNodeAccessible()` 对灵脉沿用“已固化道痕、当前层下一层、未访问、非战斗中、存在地图连接”的通用判定。
- 代码检查确认可达灵脉节点的按钮回调为 `OnMapNodePressed()`，分支进入 `EnterLingmaiNode()`。
- `dotnet build .\\AfterHongHuang.csproj` → 成功，0 个错误 0 个警告。
- Godot headless 未运行：PATH 中未找到 `Godot_v4.7-stable_mono_win64_console.exe`，因此未伪造启动验证结果。

### 已知问题
- 仍需用户窗口实测地图卷轴完全展开后点击灵脉，确认 overlay 能正常出现；本修复不改变地图布局或灵脉玩法。

---

## 2026-07-11 | P1-008 + P1-009B 角色选择微调与灵脉节点 MVP 交互

### 变更概述
1. 角色选择界面按 UI/UX 口径微调：底部角色栏下移，开始游戏按钮下移；右侧统计收敛为 `心：当前/上限`，不再显示初始牌组、灵力/回合等冗余文字。
2. 灵脉节点从占位弹窗改为可交互 overlay：提供 `休养生息`、`精进道行`、`疗愈道友` 三项；满血时休养禁用，无可升级卡时精进禁用，单人 MVP 下疗愈道友禁用且不消耗节点。
3. `休养生息` 按策划口径使用 `floor(最大生命 × 30%)`，至少 1 点且不超过最大生命；成功结算后标记灵脉节点已访问并回到地图探索。
4. `精进道行` 接入最小卡牌升级链路：初始卡 `拳袭/格挡/烈拳/燃血` 通过 `UpgradeToId` 指向 `_p` 升级版，选择后永久替换当前牌组中的对应运行时卡牌；奖励卡池暂不扩展升级版。
5. 灵脉进入时不立即写入 `VisitedNodeIds`；只有休养或成功升级后才消耗该节点，返回/禁用/失败不会标记访问。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scenes/CharacterSelect/CharacterSelect.tscn` | 下移角色栏和开始按钮，调整生命统计标签位置 |
| `scripts/UI/CharacterSelectController.cs` | 隐藏初始牌组预览，统计文本改为 `心：当前/上限` |
| `scripts/UI/MapController.cs` | 新增灵脉 overlay、休养结算、升级选卡、禁用项提示和成功后节点访问标记 |
| `scripts/Core/GameManager.cs` | 新增完整牌组扫描、可升级卡查询、升级版查找和运行时卡牌替换接口；补充 `格挡+` 护体结算 |
| `scripts/Data/DataDefs.cs` | 为巫祝初始卡接入 `UpgradeToId`，新增 4 张 `_p` 升级版与按 ID 查卡接口 |

### 验证结果
- `dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告。
- Godot headless 未运行：本机 PATH、工作区、`C:\Users\ASUS`、`C:\Program Files` / `C:\Program Files (x86)` 下未找到 `Godot_v4.7-stable_mono_win64_console.exe`。
- 代码侧自查：灵脉禁用项不会调用结算；休养和升级成功才写入 `VisitedNodeIds` / 更新当前位置；新改 UI 文本未引入“火堆/遗物/力量”等禁用术语。

### 已知问题
- 本轮没有完整法宝系统和队友系统；对应灵脉选项仅保留禁用/扩展入口。
- headless 和窗口手感未能在本机验证，需要用户或 QA 实机复测角色选择布局、灵脉 overlay、休养回血上限、精进道行选卡和节点不可重复领取。

---

## 2026-07-10 | BUG-20260710-002 手牌默认遮挡层级修复

### 变更概述
1. 修复默认静止态手牌层级：`HandLayoutCalculator` 不再按距离中心提高 ZIndex，改为按手牌索引从左到右递增。
2. `HandLayoutProfile` 增加 `RestZIndexStep`，用于配置静止态相邻卡牌的层级步进；默认 `BaseZIndex=5`、`RestZIndexStep=2`。
3. 保持 hover / selected / held 层级不回退：`HoverZIndex=100`、`SelectedZIndex=120`、`HeldZIndex=130`，仍高于所有默认静止卡。
4. Debug 构建启动自查增加 ZIndex 校验：1/2/3/5/7/10 张静止布局必须 ZIndex 单调递增，active 层级必须高于最高静止层级。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/HandLayoutProfile.cs` | 新增 `RestZIndexStep`，移除中心优先层级参数 |
| `scripts/UI/HandLayoutCalculator.cs` | 默认静止态 ZIndex 改为 `BaseZIndex + index * RestZIndexStep` |
| `scripts/UI/BattleController.cs` | 增强 Debug 启动自查，覆盖静止态层级递增和 active 层级上浮 |

### 验证结果
- `dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告。
- `Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit` → 启动成功，`GameManager` Autoload 初始化。
- Debug 构建启动自查已覆盖 1/2/3/5/7/10 张静止布局 ZIndex 单调递增，未触发异常。

### 已知问题
- headless 无法直接观察默认遮挡观感；需要用户或 QA 实机确认右侧卡轻微盖住左侧卡，且 hover / selected / held 不被静止卡遮挡。

---

## 2026-07-10 | P1-007B 手牌布局参数化与 Debug 可视化实现

### 变更概述
1. 采用 UI/UX 文档 `01-MVP界面交互.md` v2.2.0 的 P1-007 参数表，新增 `HandLayoutProfile : Resource`，集中配置 `CardSize`、`HandAreaRect`、`HandAnchor`、`VirtualFanCenter`、1/2/3/5/7/10 角度曲线、hover/selected、箭头与 Debug 参数；Debug 默认关闭。
2. 新增 `HandLayoutCalculator`，将手牌位置、旋转、缩放、层级、箭头起点和箭头控制点计算从 `BattleController` 中拆出；计算器不依赖玩法状态，支持 1/2/3/5/7/10 张布局校验。
3. 新增 `HandLayoutDebugOverlay`，Debug 开启时绘制虚拟圆心、手牌区域、中心线、每张牌目标点/目标框、hover/held 点、手牌区外确认线、箭头起点/控制点/目标点。
4. 改造 `BattleController.RefreshHandUI()` 和 `CardButton`，手牌节点应用计算器输出；出牌成功后仍统一走 `UpdateAllUI()` / `RefreshHandUI()`，按当前 `GameManager.Hand` 重建和重排。
5. 保持 P0 已通过交互边界：hover 摆正放大；敌方目标牌本体只在手牌区小幅抬升并显示上凸箭头；Self/None 点击后跟随鼠标，越过 `HandExitThresholdY` 后点击/释放自动打出。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/HandLayoutProfile.cs` | 新增手牌布局调参 Resource，默认值来自 UI/UX 2.2.0 参数表 |
| `scripts/UI/HandLayoutCalculator.cs` | 新增独立布局计算器和 `HandCardLayout` 输出结构 |
| `scripts/UI/HandLayoutDebugOverlay.cs` | 新增 Debug 点位绘制层，默认不显示 |
| `scripts/UI/BattleController.cs` | 接入 profile/calculator/debug overlay；保留既有出牌与目标策略 |

### Debug 开关
- 默认关闭：`HandLayoutProfile.DebugOverlayEnabled = false`。
- 可在 `BattleController` 的导出字段中替换/调整 `HandLayoutProfile`。
- 运行时可按 `F9` 临时开关 Debug overlay；开启/关闭会写入战斗日志。

### 验证结果
- `dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告。
- `Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit` → 启动成功，`GameManager` Autoload 初始化。
- Debug 构建启动时执行 `ValidateHandLayoutProfile()`，已覆盖 1/2/3/5/7/10 张布局计算，未触发异常。

### 已知问题
- 本次未做完整 Godot EditorPlugin，也未新增运行时保存参数功能；调参仍通过 `HandLayoutProfile` / Inspector 或代码默认值完成。
- headless 无法验证真实窗口里的卡牌遮挡、hover 可读性和 Debug 点位观感；需要用户或 QA 实机调参复测 UI-019 / UI-020 / UI-021。

---

## 2026-07-10 | P1-007B 手牌布局参数化预研

### 结论
1. 已读取 `AGENTS.md`、任务看板、项目上下文快照、UI 文档和 `BattleController.cs`，确认 P1-007 已进入 Doing。
2. 当前文档中尚未发现 UI/UX部 P1-007A 的参数表、默认值、Debug 点位和验收口径，因此本轮只做现状阅读与方案设计，未改动手牌实现。
3. 按任务要求，正式实现应等待 UI/UX 口径返回后再落地，避免把开发部个人审美固化为最终参数。

### 当前硬编码点
| 位置 | 硬编码内容 |
|------|------------|
| `BattleController.BuildHandArea()` | 手牌区域位置 `60,780`，尺寸 `1800x280`，背景色，结束回合按钮和回合标签位置 |
| `BattleController.RefreshHandUI()` | 卡牌尺寸 `140x200`、虚拟圆心 `760,1000`、半径 `840`、旋转系数 `0.32`、ZIndex 公式 |
| `BattleController.GetHandFanAngleSpan()` | 1/2/3/4+ 张牌的总角度规则：`0 / 9.5 / 18 / min(76,(n-1)*9.5)` |
| `CardButton.ReturnToRest()` | hover 缩放 `1.18`、hover ZIndex `100`、hover 强制摆正 |
| `CardButton.ApplySelectedVisual()` | 敌方目标牌抬升 `-24`、自身/无目标牌抬升 `-42`、缩放 `1.08/1.12`、ZIndex `120` |
| `CardButton.ApplyDragVisual()` / `_GuiInput()` / `FollowMouse()` | drag 阈值 `12`、拖动/持牌缩放 `1.06/1.08`、ZIndex `130`、敌方目标牌留在手牌区抬升 `-24` |
| `TargetArrowOverlay.DrawCurvedArrow()` | 箭头曲线段数 `28`、弧高 `dist*0.45` 且夹在 `120~320`、线宽和颜色 |

### 建议最小实现方案
1. 新增 `HandLayoutProfile : Resource`：集中导出手牌区域、卡牌尺寸、虚拟圆心、半径、最大扇形角、每张牌角度步进、低张数角度、旋转系数、hover/selected/drag 缩放和抬升、ZIndex、Debug 开关等参数，默认关闭 Debug。
2. 新增 `HandLayoutCalculator`：输入手牌数量、索引、卡牌状态和 `HandLayoutProfile`，输出 `HandCardLayout`，包含 `Position`、`RotationDegrees`、`Scale`、`ZIndex`、`CardCenter`、`ArrowOrigin`。计算器不依赖 `GameManager` 或卡牌效果，便于做 1/2/3/5/7/10 张自查。
3. `BattleController.RefreshHandUI()` 改为只负责重建节点和应用计算结果，避免继续直接写几何公式。
4. `CardButton` 增加“接收布局快照/视觉状态”的方法，保留现有交互事件；hover、selected、drag 不改目标策略和出牌规则。
5. 新增轻量 Debug overlay：关闭时不显示；开启时绘制虚拟圆心、手牌区域边界、每张牌目标点/序号、可选卡牌外框。Debug 开关可先用 `HandLayoutProfile.DebugVisible`，必要时再接临时键位。

### 验证结果
- `dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告
- `Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit` → 启动成功，`GameManager` Autoload 初始化。

### 已知问题
- 本轮未实现 `HandLayoutProfile` / `HandLayoutCalculator` / Debug overlay，等待 UI/UX部 P1-007A 的参数口径。
- 后续正式实现需要重点回归：hover 摆正放大、攻击牌本体不飞出手牌栏、斗击箭头从卡牌指出并上凸、Self/None 点击持牌、手牌区外确认、出牌后按当前手牌数量重排。

---

## 2026-07-10 | BUG-20260710-001 Windows 导出包双击即退工程侧修复

### 变更概述
1. 新增 `AfterHongHuang.sln` 并加入 `AfterHongHuang.csproj`，补齐 Godot .NET 项目导出所需的 solution 文件；同时修正 solution 中 `Release|Any CPU` 到项目 `Release|Any CPU` 的映射。
2. 调整 `export_presets.cfg`，为 Windows Desktop preset 增加 `build/**` 排除规则，避免导出到工程内时把旧构建产物再次打入 PCK。
3. 重新导出到验证目录后，产物不再只有 exe/pck，新增 `data_AfterHongHuang_windows_x86_64/`，包含 Godot .NET 运行所需程序集与运行配置。
4. 重新打包正式 zip，并将旧坏包移入 `build/failed/v0.0.1-alpha/`，避免其继续被当作可试玩正式包。

### 修改文件
| 文件 | 变更 |
|------|------|
| `AfterHongHuang.sln` | 新增 solution，加入 `AfterHongHuang.csproj`，配置 Debug/Release |
| `export_presets.cfg` | 新增 `exclude_filter="build/**"`，避免发布包污染 |

### 编译验证
`dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告

### 导出验证
`Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --export-release "Windows Desktop" D:\GameCode\洪荒之后\洪荒之后-code\build\bug-20260710-001-clean\AfterHongHuang.exe` → 导出成功，日志显示 `dotnet_publish_project` 完成。

导出目录 `build/bug-20260710-001-clean/` 包含：
- `AfterHongHuang.exe`
- `AfterHongHuang.pck`
- `data_AfterHongHuang_windows_x86_64/`

`data_AfterHongHuang_windows_x86_64/` 现有 187 个文件，约 79.9 MB，关键文件包括：
- `AfterHongHuang.dll`
- `AfterHongHuang.deps.json`
- `AfterHongHuang.runtimeconfig.json`
- `GodotSharp.dll`
- `coreclr.dll`
- `hostfxr.dll`
- `hostpolicy.dll`

### 运行验证
- `AfterHongHuang.exe --headless --quit` → 能初始化 `GameManager`。
- `Start-Process AfterHongHuang.exe --quit-after 120 -Wait` → 非 headless 短时启动返回 `EXIT_CODE=0`，运行约 5.24 秒后正常退出。
- 可见窗口烟测：`AfterHongHuang.exe` 启动后保持可见窗口，主窗口标题为 `AfterHongHuang`，并生成截图 `build/v0.0.1-alpha/AfterHongHuang-smoke.png`。

### 已知问题
- 工程侧修复已收口；正式 zip 已由构建发布部重新打包，并确认包含 `data_AfterHongHuang_windows_x86_64/`。
- 旧的坏包已归档到 `build/failed/v0.0.1-alpha/AfterHongHuang-v0.0.1-alpha-windows-broken.zip`。

---

## 2026-07-10 | P0-005 Windows 导出 preset 配合配置

### 变更概述
1. 新增最小 Windows Desktop 导出配置 `export_presets.cfg`，preset 名称为 `Windows Desktop`。
2. 默认导出目标设置为 `build/windows/AfterHongHuang.exe`，导出全部资源，不启用加密、签名、自定义模板或嵌入 PCK。
3. 本次只做开发部工程侧配置支持，不处理便携 zip 包、发布说明或版本发布记录。

### 修改文件
| 文件 | 变更 |
|------|------|
| `export_presets.cfg` | 新增 Windows Desktop x86_64 最小导出 preset |

### 编译验证
`dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告

### Godot 验证
`Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit` → 启动成功，`GameManager` Autoload 初始化。

### 导出命令验证
`Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --export-release "Windows Desktop" D:\GameCode\洪荒之后\洪荒之后-code\build\windows\AfterHongHuang.exe` → preset 已被识别，但导出失败。

失败原因：本机缺少 Godot `4.7.stable.mono` Windows 导出模板，Godot 报告以下文件不存在：
- `C:\Users\ASUS\AppData\Roaming\Godot\export_templates\4.7.stable.mono\windows_debug_x86_64.exe`
- `C:\Users\ASUS\AppData\Roaming\Godot\export_templates\4.7.stable.mono\windows_release_x86_64.exe`

### 已知问题
- 需要构建发布部安装与当前编辑器版本完全匹配的 Godot `4.7.stable.mono` 导出模板后再执行真实导出。
- 本轮未生成可玩包，不应标记为导出成功。

---

## 2026-07-10 | P0-005 Windows 可玩包导出完成

### 变更概述
1. 安装并整理 Godot 4.7 stable mono 导出模板，最终使模板目录满足 Godot 读取要求：`C:\Users\ASUS\AppData\Roaming\Godot\export_templates\4.7.stable.mono\`。
2. 使用 Godot `4.7.stable.mono.official.5b4e0cb0f` 成功导出 `v0.0.1-alpha` Windows release 包。
  3. 打包生成便携 zip，并做最小烟测，确认产物可解压且导出 exe 可 `--headless --quit` 启动退出。
  4. 重新打包正式 zip，并将旧坏包移入 `build/failed/v0.0.1-alpha/`，避免其继续被当作可试玩正式包。

  ### 产物
  | 文件 | 结果 |
  |------|------|
  | `build/v0.0.1-alpha/AfterHongHuang.exe` | 成功导出 |
  | `build/v0.0.1-alpha/AfterHongHuang.pck` | 成功导出 |
  | `build/v0.0.1-alpha/data_AfterHongHuang_windows_x86_64/` | 成功导出 |
  | `build/v0.0.1-alpha/AfterHongHuang-v0.0.1-alpha-windows.zip` | 成功打包 |

### 验证
- `dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告
- `Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit` → 启动成功，`GameManager` Autoload 初始化
- `Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --export-release "Windows Desktop" "build/v0.0.1-alpha/AfterHongHuang.exe"` → 导出完成，带 C# `.sln` 缺失警告但未阻断
  - `AfterHongHuang.exe --headless --quit` → 返回码 0
  - 可见窗口烟测：`AfterHongHuang.exe` 启动后保持可见窗口，主窗口标题为 `AfterHongHuang`，并生成截图 `build/v0.0.1-alpha/AfterHongHuang-smoke.png`
  - `tar -tf AfterHongHuang-v0.0.1-alpha-windows.zip` → 现包含 `AfterHongHuang.exe`、`AfterHongHuang.pck`、`data_AfterHongHuang_windows_x86_64/`

### 已知问题
  - 旧坏包已归档到 `build/failed/v0.0.1-alpha/AfterHongHuang-v0.0.1-alpha-windows-broken.zip`；当前正式 zip 已包含 Godot .NET 运行目录，不再是仅 exe/pck 的不可玩包。

---

## 2026-07-10 | P0-005 Windows 可玩包导出预检查

### 检查结论
1. 当前工程具备基本启动条件：`project.godot` 已配置主场景 `res://scenes/Title/Title.tscn`，Autoload `GameManager` 指向 `res://scripts/Core/GameManager.cs`，C# 工程为 `Godot.NET.Sdk/4.7.0` / `net8.0`。
2. 本机 Godot 可执行文件可用：`Godot_v4.7-stable_mono_win64_console.exe --version` 返回 `4.7.stable.mono.official.5b4e0cb0f`。
3. 当前缺少导出必要条件：工程根目录没有 `export_presets.cfg`；`C:\Users\ASUS\AppData\Roaming\Godot\export_templates` 存在但为空，未发现 Godot 4.7 .NET/Mono Windows 导出模板或 `.tpz` 模板包。
4. 本轮仅做预检查，没有创建或修改导出 preset，没有执行真实导出。

### 检查文件
| 文件 | 结果 |
|------|------|
| `project.godot` | 主场景、Autoload、显示分辨率、C# assembly 已配置 |
| `AfterHongHuang.csproj` | `Godot.NET.Sdk/4.7.0`，目标框架 `net8.0` |
| `export_presets.cfg` | 不存在 |

### 验证结果
`dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告

`Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit` → 启动成功，`GameManager` Autoload 初始化。

### 建议下一步
- 先安装与当前编辑器完全匹配的 Godot `4.7.stable.mono` 导出模板。
- 由总项目经理确认是否允许开发部/构建发布部新增最小 Windows Desktop `export_presets.cfg`，再执行 `--export-release` 生成 Windows 可运行包并记录产物路径。

---

## 2026-07-09 | BUG-20260709-004 残留修复：低张数手牌并拢

### 变更概述
1. 修复手牌从 3 张变 2 张后仍像保留中间空位的问题：牌扇刷新时按当前手牌数量计算总角度，2 张牌不再沿用旧的 18 度最小跨度。
2. 保持既有出牌刷新路径：攻击牌、默认自身牌、无目标牌出牌成功后仍统一走 `UpdateAllUI()` / `RefreshHandUI()`，以 `GameManager.Hand` 为准重建当前手牌节点。
3. 未改动已通过的 hover 摆正、攻击箭头、攻击牌不飞出手牌栏、Self/None 点击持牌和 CardReward overlay 关闭逻辑。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/BattleController.cs` | 新增低张数牌扇角度计算，2 张手牌按更小跨度重新居中并拢 |

### 编译验证
`dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告

### Godot 验证
`Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit` → 启动成功，`GameManager` Autoload 初始化。

### 已知问题
- 本次仍是在既有动态 UI 上做 P0 小范围修补，未迁移为 `.tscn`/主题/可复用卡牌控件；按工程质量红线继续记录为技术债。
- headless 只能确认项目加载，不能证明真实窗口中 3->2、2->1、5->4 的手感和视觉间距；需用户或 QA 继续窗口实机复测。

---

## 2026-07-09 | P0-012 三次返工 / BUG-20260709-002/003/004

### 变更概述
1. 修复斗击箭头曲线方向：`TargetArrowOverlay` 的贝塞尔控制点改为固定在起点/终点上方，形成从卡牌出发的上凸弧线。
2. 修复默认自身/无目标牌点击选中后的确认方式：点击选中后卡牌进入跟随鼠标的持牌状态，在手牌区外再次点击或释放自动打出；手牌区内点击/释放取消并回位。
3. 修复出牌后手牌视觉空位：出牌成功后立即隐藏旧卡节点，再按当前 `Hand` 重新刷新牌扇，避免旧节点残留造成空洞观感。
4. 保持既有修复：hover 摆正放大不回退；攻击牌本体仍留在手牌栏；CardReward 选牌后立即关闭 overlay 的逻辑不回退。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/BattleController.cs` | 调整箭头上凸曲线算法；Self/None 选中后跟随鼠标；出牌后立即隐藏旧手牌节点并刷新重排 |

### 编译验证
`dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告

### Godot 验证
`Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit` → 启动成功，`GameManager` Autoload 初始化。

### 已知问题
- 本次仍是在既有动态 UI 上做 P0 小范围修补，未迁移为 `.tscn`/主题/可复用卡牌控件；按工程质量红线记录为技术债。
- headless 无法验证真实鼠标手感、箭头视觉观感和窗口点击边界；需用户或 QA 继续窗口实机复测。

---

## 2026-07-09 | P0-012 二次返工 / BUG-20260709-001

### 变更概述
1. 为 `CardInfo` 增加 `CardTargetMode` 目标策略，并为现有巫祝初始卡和奖赏卡显式标注 `Enemy` / `Self`；旧 `RequiresEnemyTarget` 仅保留兼容，不再作为 UI 交互主判断。
2. 战斗交互改为按 `TargetMode` 判断：敌方目标牌显示箭头并要求敌方目标；默认自身/无目标牌不显示箭头，拖出手牌区松手自动打出。
3. 攻击箭头改为独立高层 `TargetArrowOverlay` 绘制，避免被根节点、手牌或背景层级遮住；箭头从当前卡牌顶部指向鼠标/目标。
4. hover 状态下卡牌原位摆正放大，`RotationDegrees = 0`，不再保留牌扇倾斜角。
5. 卡牌奖励选择后立即加入牌组、关闭 CardReward overlay 并回到胜利弹窗；选择后不能继续点跳过或重复操作原 3 张奖励卡。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/Data/DataDefs.cs` | 新增 `CardTargetMode`；现有卡牌显式标注目标策略；`TargetsEnemy` 改为读取目标策略 |
| `scripts/UI/BattleController.cs` | 接入目标策略；新增高层攻击箭头绘制层；调整 hover/选中/拖拽释放逻辑 |
| `scripts/UI/CardRewardController.cs` | 选牌后立即加牌、关闭 overlay 并回调完成；禁用跳过防止二次交互 |

### 编译验证
`dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告

### Godot 验证
`Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit` → 启动成功，`GameManager` Autoload 初始化。

### 已知问题
- 本次按 P0 继续小范围修补现有动态 UI，未迁移到 `.tscn`、主题或可复用卡牌控件；后续正式 UI 应按新工程质量红线重构。
- headless 只能确认项目加载，不能证明真实窗口 hover、箭头视觉清晰度和拖拽释放手感；仍需用户或 QA 窗口实机复测。

---

## 2026-07-08 | P0-012 返工：战斗手牌牌扇、hover 与攻击箭头

### 变更概述
1. 手牌排布改为基于屏幕下方虚拟圆心的牌扇：中心卡最高最正，左右卡沿圆弧逐步降低并轻微旋转。
2. hover 改为原位放大卡面并提升层级，不改变整组牌扇布局，不把卡牌移动到战场中央。
3. 斗击/敌方目标牌的箭头起点改为当前选中卡牌顶部，跟随鼠标/目标，不再从玩家立绘出发。
4. 攻击牌点击或拖拽时本体留在手牌区小幅悬浮，主要反馈为箭头；自身术法卡仍可拖出手牌区到自身目标释放。
5. 非法释放、右键取消统一回到牌扇原位。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/BattleController.cs` | 调整手牌牌扇几何布局、箭头起点、CardButton hover/selected/drag 状态和取消回弹 |

### 编译验证
`dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告

### Godot 验证
`Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit` → 启动成功，`GameManager` Autoload 初始化。

### 已知问题
- 本次仍未完成可见窗口人工手感验收；headless 只能确认脚本加载，不能证明 hover、拖拽释放命中和箭头视觉最终观感。
- 当前卡牌仍使用代码生成占位卡面，美术表现未调整。

---

## 2026-07-07 | BUG-20260707-004 / P0-011 / P0-012 战斗 UI 近期目标

### 变更概述
1. 修复战斗日志遮挡风险：新增“日志”开关按钮；默认仅显示固定高度精简日志，不拦截手牌区域；展开后为固定高度滚动面板，底边不进入手牌操作区。
2. 移除胜利弹窗中的“道器碎片 ×1（暂未开放）”占位奖励；胜利奖励仅保留灵韵、卡牌奖励、继续，以及符修遗箓额外卡牌奖励。
3. 手牌改为底部轻弧形排布，hover/点击有隆起反馈；点击出牌保留目标确认，斗击/目标型术法显示攻击箭头，自身术法高亮玩家区域。
4. 增加长按拖拽出牌：拖到合法目标释放后打出，拖到非法区域或右键取消时回到手牌。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/BattleController.cs` | 日志开关与固定高度面板；胜利奖励移除道器占位；手牌弧形布局；CardButton 增加 hover、点击选中、拖拽释放、非法回弹 |

### 编译验证
`dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告

### Godot 验证
`Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit` → 启动成功，`GameManager` Autoload 初始化。

### 已知问题
- 本次未执行窗口实机拖拽验收；headless 只能确认项目启动与脚本加载，不能证明鼠标拖拽手感。
- 日志为 MVP 调试面板，暂未做完整战报分类或持久化。

---

## 2026-07-07 | BUG-20260707-003 初始道痕节点出门后仍可点击

### 变更概述
修复玩家离开初始道韵节点后，地图中的初始道痕/道韵节点仍显示可交互的问题。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/MapRenderer.cs` | 道韵起点只在玩家仍位于初始层时可访问；离开起点后 MapScene 与 Battle overlay 均禁用该节点；不可访问节点在共用地图渲染中显示灰态 |

### 编译验证
`dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告

### 已知问题
- 未运行 Godot 实机点击验证，本次以 C# 编译和共用地图判定代码审查为准。

---

## 2026-07-07 | BUG-20260707-001/002 奖励复领与套牌叠开修复

### 变更概述
1. 修复卡牌奖励可重复领取：胜利弹窗中的卡牌奖励入口改为按奖励次数生成独立行，选完一张后该行移除，不能再次把卡加入牌组。
2. 胜利弹窗奖励行改为 `VBoxContainer` 排列：灵韵领取后行移除，下方奖励自动上移，不保留明显空白。
3. 符修遗箓额外奖励作为独立卡牌奖励行显示，每行各自预抽 3 张奖励卡，互不复领。
4. 修复套牌页面重复叠开：`DeckViewer` 改为单例 toggle，重复点击套牌按钮关闭当前页面，任意时刻最多一个套牌 overlay。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/BattleController.cs` | 胜利奖励列表改为领取后移除；卡牌奖励入口按次数独立生成并选后移除 |
| `scripts/UI/CardRewardController.cs` | 增加取消回调，跳过奖励时恢复对应入口，不误标为已领取 |
| `scripts/UI/DeckViewer.cs` | 增加当前 overlay 引用和 toggle 关闭逻辑；关闭按钮/遮罩共用关闭路径 |

### 编译验证
`dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告

### 已知问题
- 未运行 Godot 实机点击验证，本次以 C# 编译和代码路径审查为准。
- CardReward 的“跳过”交互仍沿用现状：跳过只关闭 overlay，不领取该奖励行。

---

## 2026-07-07 | P0-006/P0-007/P0-008 修复与接入

### 变更概述
1. 修复体修金刚重复加生命：道痕固化时应用最大生命 +10，进入战斗不再重复应用。
2. 修复战斗中地图只读状态：从 Map 进入战斗和 Battle 初始化时均设置 `CurrentState = 战斗中`；胜利弹窗出现后恢复 `空闲`，继续按钮呼出的地图可继续探索。
3. 接入巫祝 MVP 正式奖赏卡池：`RewardCardPool` 替换为 `wx_r_001` ~ `wx_r_010`，不再使用拳袭/格挡/烈拳/燃血。
4. 最小实现“消弭”：带 `Exhausts` 的卡牌打出后进入 `ExhaustPile`，本场战斗不再参与循环；战斗结束时归还到弃牌堆；`祝融残焰` 按目标型术法处理。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/Data/DataDefs.cs` | 奖赏池替换为 10 张巫祝正式卡；新增 `SecondaryEffect.永炎`、`RequiresEnemyTarget`、`Exhausts` 字段 |
| `scripts/Core/GameManager.cs` | 出牌流程支持消弭和战后归还；术法支持护体/斗劲/永炎效果；移除 `InitBattle()` 中体修金刚重复加生命 |
| `scripts/UI/MapController.cs` | 从地图节点进入战斗前设置 `CurrentState = 战斗中` |
| `scripts/UI/BattleController.cs` | 战斗场景初始化兜底设置 `CurrentState = 战斗中`；胜利后保持恢复为空闲 |
| `scripts/UI/DeckViewer.cs` | 套牌查看统计纳入 `ExhaustPile`，避免消弭牌战斗中被误判为永久丢失 |

### 编译验证
`dotnet build .\AfterHongHuang.csproj` → 0 错误 0 警告

### 已知问题
- 本次未处理 P1-005：CardReward 跳过方式、选牌后加入抽牌堆还是弃牌堆仍沿用现状。
- 奖赏卡池暂按简单随机 3 选 1，未接入 rareOffset、稀有度权重和升级概率。

---

## 2026-07-05 | MVP 第一轮 v2.0 - UI交互系统完整重构

### 变更概述
根据文档 v2.0 重构全部场景，新增标题界面和牌组展示，角色选择和道痕选择大改。

### 项目目录结构

```
洪荒之后-code/
├── project.godot                      # 主场景: Title, Autoload: GameManager
├── 洪荒之后-code.csproj               # .NET 项目（Godot.NET.Sdk/4.7.0）
├── scripts/
│   ├── Core/
│   │   └── GameManager.cs             # 全局状态管理（v2.0 重构）
│   ├── Data/
│   │   └── DataDefs.cs                # 静态数据（5角色/3道痕/地图节点）
│   └── UI/
│       ├── TitleController.cs         # 场景0：标题界面（NEW）
│       ├── CharacterSelectController.cs # 场景A：角色选择（v2.0 重制）
│       ├── DaoMarkSelectController.cs   # 场景B：道痕+地图双面板（v2.0 重制）
│       ├── BattleController.cs          # 场景C：战斗系统（微调）
│       ├── CardRewardController.cs      # 场景D：卡牌奖励（微调）
│       └── DeckDisplayController.cs     # 场景E：牌组展示（NEW）
└── scenes/
    ├── Title/Title.tscn
    ├── CharacterSelect/CharacterSelect.tscn
    ├── DaoMarkSelect/DaoMarkSelect.tscn
    ├── Battle/Battle.tscn
    ├── CardReward/CardReward.tscn
    └── DeckDisplay/DeckDisplay.tscn
```

### 场景清单（6个场景）

| 场景 | 文件 | 功能 |
|------|------|------|
| 0 标题 | Title.tscn | 开始游戏/时间线/设置/退出 + 版本号 |
| A 角色选择 | CharacterSelect.tscn | 6头像栏 + 锁定态 + 信息面板 + 背景色切换 |
| B 道痕+地图 | DaoMarkSelect.tscn | 双面板切换 + 道痕3选1固化 + 地图节点 |
| C 战斗 | Battle.tscn | 顶部功能栏（套牌/地图/设置）+ 战斗UI |
| D 卡牌奖励 | CardReward.tscn | 战后3选1 + 符修遗箓额外奖励跳转 |
| E 牌组展示 | DeckDisplay.tscn | 牌组统计 + 道痕列表 + 验证信息 |

### MVP 验收标准 v2.0 覆盖

| # | 验收项 | 状态 |
|---|------|:--:|
| 1 | 标题界面4按钮全部正常（开始/时间线"开发中"/设置"开发中"/退出） | ✅ |
| 2 | 右下角显示版本号 v0.0.1-alpha | ✅ |
| 3 | 角色选择：6个头像，巫祝默认选中（✓），后4个灰+🔒不可点 | ✅ |
| 4 | 锁定角色hover显示"此角色将在后续版本中开放" | ✅ |
| 5 | 随机按钮始终可点，MVP随机=选巫祝 | ✅ |
| 6 | 道痕3选1，固化后立即生效（体修金刚+10HP） | ✅ |
| 7 | 地图切换，选道痕前节点🔒锁定，选后解锁 | ✅ |
| 8 | 战斗UI：顶部栏+套牌/地图/设置按钮+斗击/术法+护体伤害公式 | ✅ |
| 9 | "结束回合"后敌人行动，灵力花光无法出牌 | ✅ |
| 10 | 打死→奖励3选1→牌组11张，不选可以跳过 | ✅ |
| 11 | 拿完奖励→牌组展示，显示11张+道痕列表 | ✅ |
| 12 | HP=0→道心破碎→返回标题 | ✅ |

### 技术要点

- **GameManager._EnterTree**：替代 `_Ready`，确保 Autoload 在其他场景初始化前完成 Instance 赋值
- **双面板切换**：DaoMarkSelect 场景使用 DaoMarkPanel/MapPanel 两个 Panel 互斥显示
- **角色锁定**：DataDefs.Characters[].Unlocked 控制按钮状态和悬停提示
- **编译验证**：`dotnet build 洪荒之后-code.csproj` → 0 错误 0 警告
- **入口场景**：`res://scenes/Title/Title.tscn`

### 已知问题

- [ ] 战斗中点地图按钮应呼出只读 overlay 而非跳转场景（已修复）
- [ ] 选了道韵后选之前点道韵节点无反应（已修复）

---
## 2026-07-05（续6）| 道韵底页化 + 地图统一呼出卷轴 + 战斗交互优化

### 变更概述
1. 道韵选择从 overlay 迁移到底页直接渲染
2. 战斗胜利弹窗简化：去"返回地图/不领视为放弃"，改"继续"按钮
3. 战斗 TopBar 地图按钮 → 呼出只读地图 + 卷轴动画 toggle
4. MapController 全面重写：底页=当前节点信息/道韵选择，地图=呼出卷轴

### 新增文件
| 文件 | 说明 |
|------|------|
| `scripts/UI/MapRenderer.cs` | 地图只读绘制工具类 |

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/MapController.cs` | 全面重写（~630行），道韵底页化 + 呼出地图 + 卷轴动画 |
| `scripts/UI/BattleController.cs` | 地图呼出 overlay + 卷轴动画 toggle + 胜利弹窗简化 |
| `scripts/UI/CardRewardController.cs` | 改为静态工具类 overlay |

### 地图交互流程（新）
- TopBar 地图按钮 → toggle 呼出/关闭
- 卷轴动画：x=1920 w=0 → x=320 w=1600（350ms ease-in-out）
- MapScene 可交互，战斗中只读

### 道韵交互流程（新）
- 道韵选择直接渲染在底页（非 overlay）
- 点「固化」→ 原地切换：选中高亮 + ✓，其余变灰 35%

### 编译验证
`dotnet build` → 0 错误 0 警告

---
## 2026-07-13 | ACT1-FEEDBACK-C1：无交叉混合层 MapGraph 与 Debug 一键胜利

### 变更概述
1. 将生产 MapGraph 生成改为由 `ActDefinition` 的层窗口、节点权重、配额和上限驱动；先生成稳定 lane 的相邻层拓扑，再分配节点类型并按实际路径约束受控修复。生成器不再按固定层号或具体内容 ID 分支，也不会在失败时静默退回旧静态地图。
2. `MapGraph` 正式保存 `ActId`、`RuleVersion`、`LayerOrder`、`StableOrder`；`MapGraphValidator` 独立校验稳定顺序、边反转/交叉、层混合度、战斗节点、非战斗节点连续性、可达性、完整路径和商店路径约束。
3. 将地图节点内容和事件内容集中到 `MapNodeContentCatalog`、`EventDefinitionCatalog`；`EventController` 消费事件定义，战斗角色名称从当前角色定义读取，不再由控制器固定显示巫祝。
4. 新增仅 Debug 构建可用的战斗内“一键胜利”工具场景。它只允许活动 Battle/Boss 节点，设置当前真实敌人生命为 0 后调用既有胜利检测、节点结果、奖励和地图流程；不重置 RunState，不恢复 F10 或全局调试面板。
5. 修复现有状态契约自检与 B2 自检对混合生产地图的固定索引假设，并补充非法图校验 fixture。

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/Map/ActDefinition.cs` | 增加 ACT1 层窗口、节点类型窗口、拓扑/路径/配额规则和 RuleVersion 数据 |
| `scripts/Map/ActLayerDefinition.cs` | 新增层级规则定义 |
| `scripts/Map/NodeTypeWindowDefinition.cs` | 新增节点类型窗口定义 |
| `scripts/Map/NodeAdjacencyRule.cs` | 新增相邻层规则定义 |
| `scripts/Map/MapNodeDefinition.cs` | 增加 LayerOrder、StableOrder |
| `scripts/Map/MapGraph.cs` | 保存 ActId/RuleVersion，fingerprint 纳入稳定顺序字段 |
| `scripts/Map/MapNodeContentCatalog.cs` | 新增集中式地图节点内容定义与解析 |
| `scripts/Map/MapGraphGenerator.cs` | 重写为稳定 lane 拓扑、数据驱动类型分配和受控重试 |
| `scripts/Map/MapGraphValidator.cs` | 重写为独立结构、内容、路径和交叉边校验 |
| `scripts/Map/MapGraphValidationReport.cs` | 增加 CrossingCount |
| `scripts/Map/MapGraphSelfCheck.cs` | 500 seed 统计、稳定性、混合层和非法图 fixture 自检 |
| `scripts/Map/B2EncounterProductionSelfCheck.cs` | 保留图元数据、校验事件定义和 Debug 战斗工具场景 |
| `scripts/Data/EventDefinition.cs` | 新增 ACT1 事件定义目录 |
| `scripts/UI/EventController.cs` | 改为按 EventDefinition/Kind 驱动，不按事件 ID 写分支 |
| `scripts/UI/MapRenderer.cs` | 消费 MapGraph 的稳定顺序和节点坐标；连线先于节点创建 |
| `scripts/UI/BattleController.cs` | 角色名称绑定当前角色；接入 Debug 一键胜利工具并保留既有真实胜利链 |
| `scripts/Debug/BattleDebugToolsController.cs` | 新增 Debug 构建战斗工具控件 |
| `scenes/Debug/BattleDebugTools.tscn` | 新增战斗内 Debug 一键胜利按钮场景 |
| `scripts/Core/StateContractSelfCheck.cs` | 灵脉 fixture 改为按节点类型查找，不依赖固定层索引 |
| `docs/开发记录-code.md` | 记录本轮实现和验证边界 |

### 验证结果
- `dotnet build .\AfterHongHuang.csproj`：通过，0 错误、0 警告。
- `dotnet build -c Release .\AfterHongHuang.csproj`：通过，0 错误、0 警告。
- Godot 4.7 Mono headless：
  `D:\OtherApp\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe --headless --path D:\GameCode\洪荒之后\洪荒之后-code --quit-after 15`
  退出码 0；`StateContractSelfCheck PASS`、`MapGraphRngSelfCheck PASS`、`B2EncounterProductionSelfCheck PASS`、`BattleContentBindingSelfCheck PASS`，无 ObjectDB 泄漏告警。
- MapGraph 500 seed：`failures=0`、`crossings=0`、完整路径 `4..9`、商店数 `2..3`、fingerprint 数 `500`、生成尝试 `min/avg/max=1/12.41/69`、混合层最小类型数 `2`、混合层最小战斗数 `1`。
- B2 遭遇生产自检：`encounterNodes=7638`、`fingerprints=500`、覆盖 `weak/strong/boss`，Shop/Event 页面定义均可解析。
- 非法图 fixture 已验证 Validator 拒绝：交叉边、错误 StableOrder、混合层全灵脉、相邻同类非战斗节点。
- 已执行 Godot editor headless 场景/工程解析检查，退出码 0；未伪造窗口交互通过。

### 硬编码扫描分类
- 允许的集中定义：`ActDefinition`、`MapNodeContentCatalog`、`EventDefinitionCatalog`、`EncounterPool` 中的内容 ID/名称属于数据定义，不由控制器或生成器决定规则。
- 允许的测试 fixture：`B2EncounterProductionSelfCheck`、`StateContractSelfCheck` 中的测试角色/内容名仅用于自检。
- 角色选择页仍有巫祝默认选择，这是现有角色选择 UI/默认数据，不是 Battle 内容绑定；BattleController 已改为读取当前角色定义。
- 未发现生成器/Validator 的 `switch(layer)`、固定层号内容分支、具体敌人名回退、`ContentId ==` 生产分支；Battle/GameManager 相关战斗路径未直接读取 `DataDefs.MountainMonkey`。
- 旧 `System.Random` 仍命中 `BattleController.cs`、`DaoMarkSelectController.cs`、`MapController.cs` 的既有 UI/奖励/道痕随机路径；这是 R-039 稳定奖励流及旧 UI 随机的残留，不在 C1 范围内，不能据此宣称奖励随机和 RewardId 幂等已完成。
- 全局 F10、`DebugService`、`DebugForceVictory` 均未恢复。C1 Debug 工具仅在 `#if DEBUG` 且 `OS.IsDebugBuild()` 下实例化/响应，Release 不生成可调用胜利入口。

### 已知问题与后续
- 一键胜利、地图交叉和混合层规则尚未完成窗口鼠标实机验收，需要 QA/用户在 Debug 包中从真实 Battle 节点点击复测。
- R-039 的 Reward 随机稳定流与 RewardId 持久化幂等仍未处理；本轮仅保留后续接口边界，没有将其标记为已完成。
- Shop/Event 页面已有最小真实入口和明确结果链，但仍需后续按 UI/UX 口径补充完整体验。

---
## 2026-07-06（续8）| 战斗奖励逻辑重做

### 变更概述
1. 战后节点标记已过关 + PlayerState 状态机
2. 灵韵从自动结算改为点击领取（按钮消失）
3. CardReward 不再销毁胜利弹窗（叠在上方，跳过回弹窗）
4. "继续"改为呼出地图 overlay（不跳转）

### 新增/修改文件
| 文件 | 变更 |
|------|------|
| `scripts/Data/DataDefs.cs` | 新增 PlayerState 枚举（空闲/战斗中） |
| `scripts/Core/GameManager.cs` | 新增 CurrentState 字段 |
| `scripts/UI/BattleController.cs` | ShowVictoryPopup 重写：灵韵按钮/继续→ShowMapOverlay/MarkCurrentNodeVisited；ClaimCardReward 移除弹窗销毁+跳转 |
| `scripts/UI/CardRewardController.cs` | 跳过按钮不调 onComplete，只关 overlay |

### 编译验证
`dotnet build` → 0 错误 0 警告

---
## 待续...

---
## 2026-07-07 | 前端 UI 部 MVP 验收清单复核

### 变更概述
按总项目经理 P0-001 / P0-002 / P0-003 协同任务，仅执行 UI/UX 审查与文档补齐，不修改 Godot/C# UI 代码。

### 修改文件
| 文件 | 变更 |
|------|------|
| `洪荒之后-main/docs/验收清单.md` | 补齐玩家视角最小 UI 验收路径、发布前最小回归路径、UI 风险归属、Title/CharacterSelect/Map/Battle/胜利弹窗/CardReward overlay 验收项 |
| `洪荒之后-main/docs/06-UI交互/00-UI交互总览.md` | 将 MVP 出牌操作同步为“点击手牌选中，再点目标确认”，保留拖拽为后续版本方向 |
| `洪荒之后-main/docs/06-UI交互/01-MVP界面交互.md` | 同步 CardReward 当前代码行为：跳过按钮仍存在；选卡当前加入抽牌堆并洗牌；继续按钮呼出地图 overlay |

### 审查结论
- P0 主流程验收需覆盖：Title → CharacterSelect → Map 道韵固化 → 地图节点解锁 → Battle 点击出牌 → 胜利弹窗 → CardReward overlay → 回胜利弹窗 → 继续呼出地图。
- 开发部需重点复核：战斗中地图是否只读、胜利后继续是否能自然回到地图探索、灵韵/卡牌奖励是否可重复领取。
- 美术侧需重点补齐：标题主视觉、巫祝角色图、山野妖猴图、战斗背景；MVP 可继续使用占位，但不得影响玩家识别。
- 文档侧已标记不一致：CardReward 跳过按钮、奖励加入位置、拖拽/点击出牌描述。

### 验证结果
未运行 `dotnet build`。本次只修改 Markdown 文档，未改 C#、Godot 场景或工程配置。

### 已知问题
- [ ] 当前 `CardRewardController.cs` 仍有“跳过”按钮，但此前开发记录写已移除，需要开发部确认最终交互。
- [ ] 当前选卡奖励加入抽牌堆并洗牌，和部分设计文档“加入弃牌堆”描述不一致。
- [ ] 胜利弹窗“继续”后的完整回地图/继续探索体验尚未手动执行验证。

---
## 2026-07-06（续7）| CardReward 跳过移除 + overlay 遮罩修正

### 变更概述
1. 移除 CardReward 跳过按钮（对标尖塔2：不选即跳过）
2. CardReward overlay 遮罩定位修正：从 TopBar 下方开始，不影响顶部栏交互

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/CardRewardController.cs` | 移除 skipBtn 及相关回调；overlay 从 FullRect 改为 SetPosition(0,44)+Size(1920,1036)，ZIndex=50，MouseFilter=Stop |

### 编译验证
`dotnet build` → 0 错误 0 警告

---
## 待续...

---
## 2026-07-05（续5）| 地图统一呼出式 + 卷轴动画

### 变更概述
1. 地图改为统一呼出式 overlay，底层显示当前节点内容
2. 卷轴从右向左展开动画（ease-in-out, 350ms）
3. MapRenderer 工具类提取共享绘制逻辑

### 新增文件
| 文件 | 说明 |
|------|------|
| `scripts/UI/MapRenderer.cs` | 地图只读绘制工具类 |

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/UI/MapController.cs` | 全面重写：底层节点内容 + 地图呼出 overlay + 卷轴动画 + toggle |
| `scripts/UI/BattleController.cs` | 战斗地图必读 overlay 使用 MapRenderer + 卷轴动画 + toggle |

### 地图交互逻辑（新）
- 进 MapScene → 底层 = 道韵选择（未选时自动弹出） / 当前节点信息（已选后）
- TopBar 地图按钮 → toggle 呼出/关闭
- 战斗中地图按钮 → 呼出只读 overlay + 卷轴动画，再点关闭
- 动画：初始 x=1920 width=0 → x=320 width=1600

### 编译验证
`dotnet build` → 0 错误 0 警告

---
## 2026-07-05（续3）| 道韵 overlay 改进 + UI 等比缩放

### 变更概述
1. 道韵选择 overlay 新增返回地图按钮；已选后可重新打开查看结果
2. 项目使用 `viewport` 拉伸模式，UI 随窗口分辨率等比缩放

### 修改文件
| 文件 | 变更 |
|------|------|
| `project.godot` | stretch/mode=viewport + window_width/height_override=1920/1080 |
| `scripts/UI/MapController.cs` | 道韵 overlay 重构：双模式（选择/查看）+ CloseDaoMarkOverlay + 返回按钮 |

### UI 缩放原理
- `viewport` 模式：Godot 内部始终以 1920×1080 渲染，再等比缩放到物理窗口
- 代码中所有 `SetPosition`、`CustomMinimumSize`、`font_size` 等硬编码值无需修改
- 窗口 resize → Godot 自动缩放整个画面，无黑边无拉伸变形

### 编译验证
`dotnet build` → 0 错误 0 警告

---
## 2026-07-05（续2）| 分辨率调整 + 设置菜单 + 战斗UI重构

### 变更概述
1. 默认分辨率调整为 1920×1080
2. 设置菜单补全（分辨率切换、返回标题、退出游戏）
3. 战斗UI全面重做（左右立绘+拖拽卡牌）

### 新增文件
| 文件 | 说明 |
|------|------|
| `scripts/UI/SettingsHelper.cs` | 设置弹窗静态工具类（Map/Battle 共用） |

### 修改文件
| 文件 | 变更 |
|------|------|
| `project.godot` | viewport_width/height = 1920×1080 |
| `scripts/UI/BattleController.cs` | 全面重写：左右立绘区 + 拖拽卡牌 + CardDragButton 子类 |
| `scripts/UI/MapController.cs` | 复用 SettingsHelper；尺寸适配 1920；移除旧 ShowSettingsDialog |
| `scripts/UI/TopBar.cs` | CustomMinimumSize 适配 1920 |

### 卡牌拖拽机制
- `CardDragButton`：继承 Control，重写 `_GuiInput`
- 左键按下 → 开始拖拽，ZIndex 提升到最上层
- 拖拽中 → 跟随鼠标移动
- 释放到敌人区 (x:1560~1840, y:160~580) 或玩家区 (x:80~360, y:160~580) → 打出
- 释放到无效区域 → 回弹到原位
- 右键 → 取消拖拽

### 编译验证
`dotnet build` → 0 错误 0 警告

---
## 2026-07-05（续）| UI 重构 — 横向分岔地图 + 持久化TopBar + 返回按钮

### 变更概述
根据用户需求全面重构UI交互层：新增 MapScene 横向分岔地图、持久化 TopBar 状态栏、角色选择返回按钮。

### 新增文件
| 文件 | 说明 |
|------|------|
| `scripts/UI/TopBar.cs` | 通用持久化顶部状态栏（代码生成，无 .tscn 依赖） |
| `scripts/UI/MapController.cs` | 横向分岔地图场景控制器（含道痕选择 overlay） |
| `scenes/Map/Map.tscn` | 地图场景入口（仅 root Control + 脚本引用） |

### 修改文件
| 文件 | 变更 |
|------|------|
| `scripts/Data/DataDefs.cs` | 地图改为分层结构（MapLayerData/MapConnection）；新增难度字段；MapNodeType 枚举重构 |
| `scripts/Core/GameManager.cs` | 新增 Difficulty、VisitedNodeIds、CurrentMapLayer/Index；场景跳转改为 Map |
| `scripts/UI/CharacterSelectController.cs` | 新增 BackBtn 返回按钮 + 跳转目标改为 MapScene |
| `scripts/UI/BattleController.cs` | 全面重构：所有 UI 改为代码生成，集成 TopBar，新增套牌查看弹窗 |
| `scripts/UI/CardRewardController.cs` | 奖励后跳转到 MapScene（非 DeckDisplay） |
| `scripts/UI/DaoMarkSelectController.cs` | 精简：移除地图面板，仅保留道痕选择逻辑 |
| `scenes/CharacterSelect/CharacterSelect.tscn` | 新增 BackBtn 节点 |
| `scenes/Battle/Battle.tscn` | 精简到仅 root Control + 脚本引用 |

### 场景流转（新）

```
Title → CharacterSelect → Map → (道痕 overlay) → Map → Battle → CardReward → Map → ...
```

### 编译验证
`dotnet build` → 0 错误 0 警告
# 2026-07-16 | 开发部 BUG-20260716-001 战后 MapScene 空白页与继续语义修复

## 变更概述

- 修正胜利流程边界：敌人死亡后立即结束战斗并暂存已验证奖励计划；灵韵领取、卡牌领取和明确跳过分别改变奖励状态。
- 新增奖励完成后的节点提交端口。最后一个奖励动作完成后才提交战斗节点 `Completed`；“继续”不再调用 `TryFinalizeBattleVictory`，也不领取、跳过奖励或提交 `NodeResult`，只校验状态、销毁战斗实例并请求进入地图。
- 新增 `MapEntryMode.OpenInteractiveMap` 显式场景进入目标。MapScene 在 `_EnterTree()` 先隐藏根节点，战后入口同步构建完整地图卷轴后才显示，移除 `OpenMapOnEnter + CallDeferred` 成功路径依赖。
- 地图构建失败时显示明确错误页并保留导航请求，不暴露只有 TopBar 的空白底页。

## 修改文件

- `scripts/Core/NavigationState.cs`
- `scripts/Core/GameManager.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/MapController.cs`

## 验证与限制

- Debug `dotnet build .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- Release `dotnet build -c Release .\\AfterHongHuang.csproj`：通过，0 警告、0 错误。
- 代码自检入口已补覆盖：未处理奖励时继续被拒绝；最后奖励动作提交节点后，继续只离场并产生 `OpenInteractiveMap` 目标。
- 当前环境找不到 Godot 可执行文件（不在 PATH，已检索常见本机目录），未伪造 headless/窗口通过。
- 尚未进行窗口实机操作；需重点复测胜利后领取/跳过全部奖励、未处理奖励点击继续、进入地图即展开、关闭后再次打开。

## 已知风险

- `OpenMapOnEnter` 仍作为迁移期兼容字段保留，并由其 setter 同步显式 `MapEntryMode`；MapController 正式入口不再读取该布尔字段。后续可在导航统一迁移后删除兼容字段。

---

# 2026-07-17 | 开发部 NODE-MAP-ENTRY-C1 节点页共享地图入口

## 变更概述

- 新增 `NodePageMapEntryButton`，将战斗胜利、灵脉和商店的外置“继续”统一绑定到各页面已有的 `ToggleMapOverlay`。入口本身不提交 `NodeResult`、不消费奖励或服务节点收益、不切场景。
- 战斗胜利页移除内容面板内的继续按钮，改为胜利模态右侧偏下的共享地图入口；胜利与 `CardReward` UI 保持在原页，地图关闭后不重建或清理它们。
- 灵脉和商店进入时立即把当前地图位置推进到服务节点，地图 overlay 因而立即展示并允许下一层合法节点；灵脉不再提供“返回地图/放弃灵脉”动作，商店不再提供“完成商店结算”动作。
- 灵脉的休养生息和精进道行分别只灰置自身，收益只在按钮点击时写入；不选任何收益不阻塞后续地图节点。商店购买状态仅由库存和灵韵决定，开关地图不改变库存。
- 新增 `GameManager.TryTransitionAndRouteFromActiveServiceNode`：只有点击 overlay 中的合法下一节点才会离开活动灵脉/商店页；场景路由失败会回滚到原服务页和原活动节点上下文。
- `MapRenderer` 识别活动灵脉/商店为可探索路线状态，不再要求伪造完成结果。TopBar 背景和右侧容器改为完整拉伸，修复右侧背景缺口。

## 修改文件

- `scripts/UI/NodePageMapEntryButton.cs`（新增）
- `scripts/Core/GameManager.cs`
- `scripts/UI/MapRenderer.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/LingmaiController.cs`
- `scripts/UI/ShopController.cs`
- `scripts/UI/TopBar.cs`

## 入口扫描结论

- 战斗胜利页：外置共享地图入口；实际节点迁移仅在 overlay 合法节点回调中调用 `TryTransitionAndRouteFromCompletedNode`。
- 灵脉/商店：TopBar 与外置共享入口都调用同一 `ToggleMapOverlay`；实际节点迁移仅在合法节点回调中调用 `TryTransitionAndRouteFromActiveServiceNode`。
- Event：仍使用已完成事件的合法节点迁移端口；“返回地图”仅保留无效入口恢复页，不属于正常流程。
- MapScene 道痕底页的“继续”仍是初始引导，等效 TopBar 地图开关；本轮未将其作为节点页离场入口处理。

## 验证与限制

- Debug：`dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj` 通过，0 警告、0 错误。
- Release：`dotnet build -c Release -p:NuGetAudit=false .\AfterHongHuang.csproj` 通过，0 警告、0 错误。
- 使用隔离 `APPDATA/LOCALAPPDATA` 的 Godot 4.7 Mono Editor headless 场景解析：`Map/Battle/Lingmai/Shop/Event` 均退出码 0。
- 完整项目 `--headless --quit` 在 120 秒后发生 Godot 原生 signal 11，未得到自检 PASS；因此本轮未宣称 500-seed 自检或窗口交互已通过。

## 已知风险

- 本轮复用了现有脚本动态 UI；`NodePageMapEntryButton` 是集中复用的过渡控件，后续正式 UI 资源化时应迁移为 `.tscn` 组件。
- 需要窗口复测：胜利页继续 -> 关地图 -> 原奖励页不变；灵脉/商店无需结算即可点击下一层；非法目标和路由失败仍保留原页面；TopBar 右侧背景连续且始终可交互。

---

# 2026-07-17 | 开发部 NODE-MAP-ENTRY-C1R 资源化统一地图入口

## 变更概述

- 移除 C1 临时的 `NodePageMapEntryButton` 脚本布局实现，改为复用 `scenes/UI/NodeMapEntry.tscn + NodeMapEntry.cs`。
- 入口场景使用右下锚点和偏移承载冻结布局：`right=64px`、`bottom=72px`、`200×64px`；文本为 `🗺 地图`。不再在 C# 写入位置、尺寸、颜色或字号。
- `NodeMapEntry` 只接收当前页面已有的地图 overlay toggle 和状态查询。它按真实开关状态显示“打开地图卷轴”或“收起地图卷轴” tooltip，不具备结算、奖励、NodeResult 或场景路由能力。
- Battle、Lingmai、Shop 三个入口均改为同一 `NodeMapEntry.Add(..., Toggle, IsMapOverlayOpen)` 绑定；C1 的服务节点进入即推进、合法下一节点点击才离场、失败回滚逻辑保持不变。

## 修改文件

- 删除 `scripts/UI/NodePageMapEntryButton.cs` 及其 UID。
- 新增 `scripts/UI/NodeMapEntry.cs`。
- 新增 `scenes/UI/NodeMapEntry.tscn`。
- 修改 `scripts/UI/BattleController.cs`、`scripts/UI/LingmaiController.cs`、`scripts/UI/ShopController.cs`。

## 扫描与验证

- Battle/Lingmai/Shop 仅各有一个 `NodeMapEntry.Add` 调用；没有遗留 `NodePageMapEntryButton`、`完成商店结算`、`放弃灵脉并继续前行`、`LeaveShop` 或直接 `Map.tscn` 路由。
- 节点页的路线迁移仍仅出现在地图合法节点回调：Battle 使用已完成节点端口，Lingmai/Shop 使用活动服务节点端口。
- Debug：`dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj` 通过，0 警告、0 错误。
- Release：`dotnet build -c Release -p:NuGetAudit=false .\AfterHongHuang.csproj` 通过，0 警告、0 错误。
- 隔离 `APPDATA/LOCALAPPDATA` 的 Godot 4.7 Mono Editor headless 场景解析：`NodeMapEntry/Battle/Lingmai/Shop` 均退出码 0。

## 已知限制

- 完整项目 `--headless --quit` 仍会在 120 秒后发生 Godot 原生 signal 11；本轮没有据此宣称完整自检、500-seed 或窗口交互通过，等待 QA 与窗口复测。

---

# 2026-07-17 | 开发部 NODE-MAP-ENTRY-C2 CardReward 全局地图入口层级修复

## 根因与修复

- QA 发现 `NodeMapEntry` 固定在 z=401，而 `CardRewardOverlay` 位于 z=470 且以 `MouseFilter.Stop` 覆盖 TopBar 下方内容区，导致奖励打开时外置地图入口不可点击。
- 在 `OverlayCoordinator` 集中新增 `GlobalOperationZIndex`，占用 CardReward 平面的顶端 z=489，并声明 Transition/Error 为 z=490~499。`NodeMapEntry.Configure()` 从该常量设置绝对层级；资源场景不再保存独立的 z 值。
- CardReward 保持全内容区 `MouseFilter.Stop`，不向手牌、日志、结束回合或战斗区域穿透。全局 NodeMapEntry 以 z=489 位于其上方；点击继续调用宿主已有的地图 toggle，进而由 `MapOverlayController.Open()` 调用 `OverlayCoordinator.TryPrepareMap()`。
- `TryPrepareMap()` 已通过 `CardRewardHelper.TryCancelForGlobalOverlay()` 调用与跳过相同的取消回调，关闭奖励层并恢复原奖励行；不消费奖励、不改变候选或领取进度。地图关闭后底层胜利页仍保留。
- 更新 `OverlayCoordinatorSelfCheck`：断言全局操作层高于 CardReward 根层且低于 Transition/Error 层；原有全局地图/套牌/设置取消奖励、恢复奖励行且不改变候选进度的反证保持执行。

## 修改文件

- `scripts/UI/OverlayCoordinator.cs`
- `scripts/UI/NodeMapEntry.cs`
- `scenes/UI/NodeMapEntry.tscn`
- `scripts/UI/CardRewardController.cs`
- `scripts/Core/OverlayCoordinatorSelfCheck.cs`
- `scripts/UI/LingmaiController.cs`
- `scripts/UI/BattleController.cs`

## 验证

- Debug：`dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj` 通过，0 警告、0 错误。
- Release：`dotnet build -c Release -p:NuGetAudit=false .\AfterHongHuang.csproj` 通过，0 警告、0 错误。
- 隔离 `APPDATA/LOCALAPPDATA` 的 Godot 4.7 Mono Editor headless 场景解析：`NodeMapEntry/Battle/Lingmai/Shop/Event` 均退出码 0。
- 完整项目 `--headless --quit` 的 Godot 原生 signal 11 仍存在，未宣称完整自检、500-seed 或窗口交互通过，等待 QA 严格回归和窗口复测。

---

# 2026-07-20 | 开发部 UI-REWARD-NODE-COHERENCE C1 本地继续与灵脉一致性

## 变更概述

- 废止 C2 的 `GlobalOperationZIndex=489`：`NodeMapEntry` 改为页面本地继续控件，统一显示“继续”、tooltip 为“打开地图”，层级集中为 `NodePageMapEntryZIndex=449`。Map overlay（450）与 CardReward（470）均在它之上，地图展开或选卡时会完整遮挡和阻断继续；TopBar 仍在内容区外保持全局可用。
- CardReward 不再需要向本地继续穿透。TopBar 打开地图仍通过 `OverlayCoordinator.TryPrepareMap()` 的可恢复取消链关闭奖励 overlay，恢复同一奖励行、候选和多选进度，不消费奖励。
- 新增 `VictoryRewardList.tscn` 与 `VictoryRewardList` 工厂，将胜利奖励列改为 960px 胜利面板内居中的 600px `VBoxContainer`；奖励行采用横向 `ExpandFill`，不再在 BattleController 堆叠奖励列位置和宽度。
- 新增最小 `PartyRoster/PartyMember` RunState 契约及 `GameManager.GetEligibleLingmaiHealingTargets()` 查询。单人局没有可疗愈的非自身友方，因此不渲染“疗愈道友”；未来招募友方后可由同一查询提供入口，不实现联机或完整队友结算。
- 灵脉“休养生息”满血时保持可点击。点击会进入一次性已使用状态，生命保持不变并显示明确反馈；受伤时继续按 30% 上限规则回复。
- 新增 `LingmaiActionRules` 和 `LingmaiInteractionSelfCheck`，覆盖满血无数值变化、受伤回复、单人隐藏和真实友方目标查询。Overlay 自检补充本地继续必须被 Map/CardReward 覆盖的层级断言，并保留 TopBar 可恢复关闭 CardReward 的候选进度反证。

## 修改文件

- `scripts/UI/OverlayCoordinator.cs`
- `scripts/UI/NodeMapEntry.cs`
- `scenes/UI/NodeMapEntry.tscn`
- `scripts/UI/VictoryRewardList.cs`（新增）
- `scenes/UI/VictoryRewardList.tscn`（新增）
- `scripts/UI/BattleController.cs`
- `scripts/UI/CardRewardController.cs`
- `scripts/UI/LingmaiController.cs`
- `scripts/UI/ShopController.cs`
- `scripts/Core/RunState.cs`
- `scripts/Core/GameManager.cs`
- `scripts/Core/PartyRoster.cs`（新增）
- `scripts/Core/LingmaiActionRules.cs`（新增）
- `scripts/Core/LingmaiInteractionSelfCheck.cs`（新增）
- `scripts/Core/OverlayCoordinatorSelfCheck.cs`

## 验证与限制

- Debug：`dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj` 通过，0 警告、0 错误。
- Release：`dotnet build -c Release -p:NuGetAudit=false .\AfterHongHuang.csproj` 通过，0 警告、0 错误。
- 隔离 `APPDATA/LOCALAPPDATA` 的 Godot 4.7 Mono Editor headless 场景解析：`NodeMapEntry/VictoryRewardList/Battle/Lingmai/Shop/Event` 均退出码 0。
- 完整项目 `--headless --quit` 仍会发生 Godot 原生 signal 11，因此新增 Debug 自检尚未在完整运行态得到 PASS；未宣称窗口或 500-seed 通过。

## 扫描结论与风险

- Battle/Lingmai/Shop/Event 扫描未发现旧的全局入口、完成商店结算、放弃灵脉或直接 `Map.tscn` 路由；只有地图合法节点回调调用迁移端口。
- 内容硬编码扫描命中均为 Encounter/Map Catalog 集中定义或自检 fixture。`MapController.cs` 与 `DaoMarkSelectController.cs` 的 `System.Random` 是已登记 R-039 奖励稳定流遗留，本轮未扩大处理。
- 需要窗口复测：胜利奖励列居中；CardReward 打开时继续被遮挡、TopBar 地图可恢复奖励行；地图关闭后继续和同一奖励候选恢复；满血点击休养后按钮灰置且生命不变；单人灵脉不显示疗愈道友。

---

# 2026-07-20 | 开发部 CARD-DATA-AUTHORING-C2 + CARD-BALANCE-B1-C2B

## 变更概述

- 新增 Schema v1 的 `CardDefinitionResource`、`CardCatalogResource`、强类型费用/目标策略/有序效果投影及纯 `CardDefinitionValidator`。运行期由 `CardCatalogService` 加载显式 Catalog 和单卡 Resource；缺资源、未知池、升级断裂、无效目标/效果和预览/可用性元数据会显式拒绝，不回退 `DataDefs`。
- 新增 `resources/cards/CardCatalog.tres` 与 21 张单卡 `.tres`：4 初始、4 升级、10 常规奖励、3 Boss 奖励。初始重复量改为 `CharacterInfo.StarterDeckEntries` 的 `CardId + Count` 配置，单卡定义不再复制十次。
- `CharacterDeckFactory`、`CardPoolCatalog`、升级查询、奖励/商店/事件卡池入口改由 Catalog 投影供给；`DataDefs` 的旧 `CardInfo[]` 只保留给 `CardCatalogSelfCheck` 的迁移 fixture。战斗临时堆和永久套牌均复制 `CardInfo` 投影，避免临时实例反向污染永久套牌。
- 新增 `CardCatalogSelfCheck`：验证 21 卡/4 池、迁移双读、两张显著不同卡经同一 Catalog 入口不串用、结构化目标/消弭投影，以及 B1 受控差异。旧 `RequiresEnemyTarget` 仅作为迁移兼容字段；Schema 唯一目标事实为 `TargetPolicy`/`TargetMode`。
- 应用冻结 `B1-20260720`，仅写新 Resource：格挡+护体 7；血挡护体 10；裂肤引火与祭血凝劲按“自损 -> 斗劲 -> 消弭”；焚脉重拳易损 1；烬骨守势护体 16；九首回潮护体 16。旧 `DataDefs.cs` 基线未改。
- 新增仅编辑器可见的 `addons/card_authoring`：`EditorPlugin + CardAuthoringDock.tscn` 支持草稿创建/编辑、目标策略预览、效果排序、字段校验、预览/Diff、取消丢弃和确认写入单卡 Resource/Catalog 索引。插件未注册到 `project.godot`，玩家 TopBar/战斗/地图/设置没有入口。

## 修改文件

- `scripts/Data/CardDefinitionResource.cs`、`CardCatalogResource.cs`、`CardDefinitionValidator.cs`、`CardCatalogService.cs`、`CardCatalogSelfCheck.cs`（新增）
- `resources/cards/CardCatalog.tres` 与 21 张单卡 Resource（新增）
- `addons/card_authoring/plugin.cfg`、`card_authoring_plugin.gd`、`CardAuthoringDock.tscn`、`card_authoring_dock.gd`（新增）
- `scripts/Data/DataDefs.cs`、`CardPoolCatalog.cs`
- `scripts/Core/CharacterDeckFactory.cs`、`GameManager.cs`、`StateContractSelfCheck.cs`
- `scripts/Map/B2EncounterProductionSelfCheck.cs`

## 验证

- Debug：`dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj` 通过，0 警告、0 错误。
- Release：`dotnet build -c Release -p:NuGetAudit=false .\AfterHongHuang.csproj` 通过，0 警告、0 错误。
- Godot 4.7 Mono Console `--headless --quit` 退出码 0；`CardCatalogSelfCheck`、`StateContractSelfCheck`、奖励状态机、灵脉自检均 PASS。MapGraph 验证仍为 500 seeds、failures=0、crossings=0。
- Editor headless 场景解析通过：`addons/card_authoring/CardAuthoringDock.tscn`、`Battle/Map/Lingmai/Shop/Event`。
- 硬编码扫描：生产 `scripts/Core`/`scripts/UI` 未发现具体卡牌 ID/名称、旧 DataDefs 卡池或按职业/费用/数值分支；旧数组只由迁移自检引用。`RequiresEnemyTarget` 只保留在兼容投影/fixture，运行时交互仍读 `TargetMode`。

## 已知限制

- 编辑器插件已完成最小可加载/写入链，但未在可见 Godot Editor 内手工点按保存；需内容团队打开插件后补一次草稿校验、预览和确认写入窗口验收。
- `CardInfo` 仍是现有 Battle/UI 的兼容投影；后续可逐步让卡面与执行器直接消费 `CardDefinitionResource` 的强类型效果列表，避免长期保留旧字段。
- R-039（奖励消费 RewardId 跨重载持久化与部分奖励稳定随机）仍是 P1，本轮未宣称完成。

---

# 2026-07-20 | 开发部 UI-REWARD-NODE-C1R / C1R2 多人灵脉疗愈入口

## 变更与验证

- `PartyRoster` 成为唯一队伍查询端口：成员具备稳定 ID、显示名、当前/最大生命、存活/可疗愈资格；查询仅返回非自身、存活且可疗愈友方。
- `LingmaiController` 按每个合规成员创建独立可点击“疗愈道友：显示名”入口，并传递实际成员 ID；单人时不渲染占位。操作后由集中规则结果灰置全部同伴疗愈入口，不影响地图 overlay 或继续语义。
- `LingmaiActionRules.TryResolveCompanionHealing` 统一 PartyRoster 实际治疗、满血无数值变化、动作消耗和“气血已满”反馈。`LingmaiInteractionSelfCheck` 覆盖双队友分别治疗、目标隔离、单人隐藏、满血目标仍可选/不变/动作消耗/反馈与灰置契约。
- Debug/Release build 通过；完整 Godot headless 中 `LingmaiInteractionSelfCheck` PASS；`Lingmai.tscn` editor-headless 解析通过。

## 已知限制

- 未实现完整队友招募、战斗队友或联机；当前仅提供可扩展 RunState roster/节点行动入口。仍需窗口复测多名真实队友的入口顺序、点击反馈和灰置视觉。

---

# 2026-07-20 | 开发部 CARD-DATA-EXECUTION-EDITOR-C3

## 变更概述

- 新增不可变 `CardExecutionPlan`，由通过 Schema 校验的 `CardDefinitionResource` 与兼容 `CardInfo` 投影同时生成。`GameManager.PlayCard()` 现在按 `Effects.Order` 逐项执行自损、伤害、护体、状态和消弭；旧 `CardInfo` 聚合字段仅用于既有卡面/控件兼容，不再决定战斗结算顺序。
- 永久套牌、战斗复制、奖励入牌和升级均通过 Catalog 创建或复制带 `ExecutionPlan` 的 `CardRuntime`。缺少执行计划或 Catalog 定义会明确拒绝出牌/入牌，不回退旧 `DataDefs`。
- `CardDefinitionReader` 增加严格读取路径。TargetPolicy、费用、效果、升级字段缺失、类型不符或未知枚举会携带字段路径进入 Validator，不能再静默降级为 `None` 或零值。
- 编辑器 dock 增加描述、角色归属、卡池、升级家族、标签、可用版本、平衡定位和效果类型/数值的草稿入口；保存改为 Resource 与 Catalog 双 pending 写入、字节备份与回滚。任一预写/提交失败不更新内存 Catalog，并清理临时文件。
- `CardCatalogSelfCheck` 增加执行计划存在性和未知/缺失 TargetPolicy 被拒绝的反证。

## 修改文件

- `scripts/Data/CardExecutionPlan.cs`（新增）
- `scripts/Data/CardDefinitionResource.cs`
- `scripts/Data/CardDefinitionValidator.cs`
- `scripts/Data/CardCatalogService.cs`
- `scripts/Data/CardCatalogSelfCheck.cs`
- `scripts/Core/GameManager.cs`
- `scripts/Core/CharacterDeckFactory.cs`
- `scripts/Core/EnemyDefinitionExecutionSelfCheck.cs`
- `addons/card_authoring/CardAuthoringDock.tscn`
- `addons/card_authoring/card_authoring_dock.gd`

## 验证与限制

- 使用隔离 `APPDATA/LOCALAPPDATA/DOTNET_CLI_HOME`、本机只读 `C:\Users\ASUS\.nuget\packages` 的 Godot SDK 缓存：Debug `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj` 通过，0 警告、0 错误；Release 同命令加 `-c Release` 通过，0 警告、0 错误。
- Godot 4.7 Mono Console `--path . --headless --quit` 退出码 0：`CardCatalogSelfCheck`、奖励状态机、灵脉自检及既有 `MapGraphRngSelfCheck`（500 seeds、failures=0、crossings=0）均 PASS。
- `--editor --headless --quit` 退出码 0，编辑器成功加载 `CardDefinitionResource` 和 `addons/card_authoring` dock；未发现 GDScript/tscn 解析错误。
- 仍需在可见 Editor 中手工覆盖草稿写入失败注入、真实 Catalog 文件替换与两效果顺序互换后的卡面日志体验；本轮自动化覆盖严格解析与执行计划存在性，尚未替代该窗口验收。
- R-039（RewardId 跨重载持久化与部分奖励稳定随机）仍为 P1，未在本轮处理。

---

# 2026-07-20 | 开发部 CARD-DATA-EXECUTION-EDITOR-C4

## 变更概述

- 新增 `ResolvedCardExecution`：出牌前冻结执行计划、战斗状态指纹、状态版本和有序摘要；执行前再次核对身份/版本/指纹。卡面、战斗日志和实际结算统一读取同一个 `CardExecutionPlan` 的有序 Effects。
- `PlayCard` 使用 `EnemyTurnSnapshot` 包裹能量扣除、效果、移区与临时状态。任何计划验证或执行期失败都会回滚生命、护体、敌方状态、能量和战斗牌堆；不再留下半张已结算卡。
- `CardRuntime.ExecutionPlan` 改为私有 setter，只能由构造、Catalog 创建或受控升级替换写入，避免外部任意改写计划。
- `CardInfo` 新增兼容的 `ExecutionSummary`，由 Catalog 投影以 `CardExecutionPlanFormatter` 生成；Battle 卡面优先显示该有序摘要，打出日志记录已消费的 `ResolvedCardExecution.Summary`。
- 编辑器新草稿改为显式未分配状态，不再预填角色、卡池、家族或名称占位；保存前拒绝 Catalog 中相同 ID 的不同路径和目标路径覆盖，并保留 C3 的 pending/备份回滚机制。

## 验证

- Debug：隔离 `APPDATA/LOCALAPPDATA/DOTNET_CLI_HOME`，使用只读 `C:\Users\ASUS\.nuget\packages`，执行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`，0 警告、0 错误。
- Release：同环境执行 `dotnet build -c Release -p:NuGetAudit=false .\AfterHongHuang.csproj`，0 警告、0 错误。
- Godot 4.7 Mono Console：`--path . --headless --quit` 退出码 0；CardCatalog、奖励、灵脉和 500-seed MapGraph 自检均 PASS。

## 残留风险

- 仍需可见 Editor 复测完整草稿的字段编辑、磁盘故障注入与 pending 文件回滚；本轮已完成代码/Editor headless 解析和运行态主链验证，但不把该人工操作替代为自动化通过。
- UI/UX C4 冻结后已调整：新草稿仅保留 SchemaVersion；费用、目标、效果、所有者、卡池、升级、可用性、预览和 Balance 都从空的“未分配”状态开始，字段校验前不允许写入。

---

# 2026-07-20 | 开发部 CARD-DATA-C5

- `CardDefinitionValidator.TryValidateExecutionPlan()` 成为运行时计划门禁，覆盖 SelectionMode、Scope、数量、重定向策略、连续 Order、效果类型、参数和目标匹配；`TryPrepareCardExecution()` 在任何战斗状态写入前复用该校验。
- `PlayCard()` 以 `EnemyTurnSnapshot` 事务包裹扣费、效果和移区，并保留旧 trace/resolution 的失败回滚边界；预解析结果的状态指纹或版本变化会被明确拒绝。
- 编辑器新草稿仅保留 SchemaVersion，新增效果添加/删除入口；保存前额外拒绝不同路径的重复 ID 和对既有资源的误覆盖，继续使用 pending/备份回滚。
- 验证：Debug/Release build 均 0 警告、0 错误；Godot 4.7 Mono player headless 退出码 0，500-seed MapGraph、CardCatalog、奖励、灵脉和既有状态契约自检 PASS。仍需 QA 严格只读以及可见 Editor 的完整草稿/故障写入窗口复测。

---

# 2026-07-20 | 开发部 CARD-DATA-C6

- 将 Battle 选中/拖拽开始接入 `TryPrepareCardExecution`：UI 保存目标确定时的 `ResolvedCardExecution`，确认、日志和 `GameManager.PlayCard(card, resolved)` 消费同一实例；取消和成功出牌都会清除该实例。
- 运行时不再以局部 `IsValidCardEffect` 进行简化判断，改为复用 `CardDefinitionValidator.TryValidateExecutionPlan()` 的 TargetPolicy/Order/效果类型与匹配校验。状态版本或指纹不一致会明确拒绝旧解析对象。
- EditorPlugin 新草稿仅保留 SchemaVersion；补充新增/删除效果入口，保持未分配字段在校验通过前不可写入。
- 验证：Debug/Release build 均为 0 警告、0 错误；隔离 Godot player headless 退出码 0，CardCatalog、奖励、灵脉和 500-seed MapGraph 自检 PASS。等待 QA 严格只读回归。

---

# 2026-07-20 | 开发部 CARD-DATA-C7

- `CardDefinitionValidator.TryValidateExecutionPlan` 补齐 DurationScope 枚举和效果目标组合校验；Resource/Catalog 验证同时调用此 canonical 语义门禁。
- 新增并接入 `CardExecutionC7SelfCheck`。首次实际 headless 暴露 `DurationScope=99` 漏检且以退出码 1 失败；补齐后重跑退出码 0，并输出唯一 PASS 标记。
- Debug build 0 警告/0 错误；隔离 Godot player headless 退出码 0，既有 500-seed、Catalog 和状态契约自检无回归。
- C7 尚未完成 editor-only 通用持久化事务服务和其 Resource/Catalog/manifest 故障注入自动化；现有 GDScript pending/回滚链仍需 QA 继续作为 P0 审查，不将此条宣称通过。

---

# 2026-07-20 | 开发部 C7R（进行中）

- 新增 `addons/card_authoring/card_authoring_transaction.gd` 并让 dock 的保存入口调用它。事务层独立拒绝新草稿的既有 CardId、既有目标路径和文件存在；编辑草稿必须属于 Catalog；Resource/Catalog 临时写与失败恢复均返回可见结果。
- Editor headless 退出码 0，插件与 `CardAuthoringTransaction` 解析成功。
- 尚未完成 C7R 要求的 manifest 资产、通用事务故障注入自检、完整字段表单和 Resolved 最终值封闭验证；本条仅记录进行中的最小事务抽取，禁止送 QA。

## C7R-B1 续作

- `CardAuthoringTransaction` 已升级为 Resource + `CardCatalog.tres` + `resources/cards/CardCatalog.manifest.json` 三件套 pending/提交/回滚。manifest 写入和恢复结果与前两件套一样被检查并包含在错误文本中。
- Godot Editor headless 退出码 0，事务脚本解析成功。
- 完整未分配枚举表单、候选 Catalog 全量验证与故障注入自检仍在实现，C7R-B1 未收口、不得送 QA。

## C7R-B1 Schema/Form 续作

- `CardDefinitionResource.EditorDraft` 提供新草稿的显式、可序列化未配置态；既有 `.tres` 缺字段默认为 false，不改变既有 enum 序列化值。canonical validator 拒绝 `EditorDraft=true`。
- dock 新建草稿设为 EditorDraft；保存前对候选解除标识执行同一 validator，失败恢复草稿标识，成功才转换为生产 Definition。
- Debug build 0 警告/0 错误。完整字段表单和专用自检尚未完成，本子项仍不得送 QA。

---

# 2026-07-20 | 开发部 C7R-B1-FORM-EFFECT-VERIFY-R1

## 变更

- `CardAuthoringDock` 的 TargetPolicy 读写补齐 `selectionMode`、`scope`、`minimumTargets`、`maximumTargets`、`allowDeadTargets`、`retargetOnInvalid` 六字段；未填写的枚举使用可见“待配置”首项，数值/布尔字段以独立“已配置”标记禁用并提示，避免把 `0` 或 `false` 静默当作有效输入。
- Effect 编辑补齐 `effectType`、`amount`、`targetSelector`、`statusKind`、`durationScope`、`destinationZone` 的双向绑定。切换当前 Effect 前会写回已配置字段；新 Effect 只包含 `order`，不再创建具有效果语义的默认项。
- 修复 `OptionButton` 的 pending 项与枚举索引混淆：Dock 用集中映射将首项解释为语义值 `-1`，正式枚举从后续项映射，避免“待配置”被当作枚举 `0` 写入草稿。
- 移动/删除 Effect 后按当前数组重新写入连续 `order=1..n`，保留其余参数并重设有效选中项。
- 新增 editor-only `addons/card_authoring/card_authoring_form_selfcheck.gd`。它实际实例化 `CardAuthoringDock.tscn`，检查待配置态、完整 TargetPolicy/Effect 写回，以及双 Effect 重排后的顺序和参数保持；失败调用 `quit(1)`。

## 修改文件

- `addons/card_authoring/CardAuthoringDock.tscn`
- `addons/card_authoring/card_authoring_dock.gd`
- `addons/card_authoring/card_authoring_form_selfcheck.gd`

## 验证

- Debug：隔离 `APPDATA/LOCALAPPDATA/DOTNET_CLI_HOME` 后执行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`，通过，0 警告、0 错误。
- Release：同环境执行 `dotnet build -c Release -p:NuGetAudit=false .\AfterHongHuang.csproj`，通过，0 警告、0 错误。
- Editor 解析：Godot 4.7 Mono Console 使用隔离 `APPDATA/LOCALAPPDATA` 执行 `--path . --editor --headless --quit`，退出码 0，插件和 Dock 场景均可加载。
- FORM 自检：`--path . --headless --script res://addons/card_authoring/card_authoring_form_selfcheck.gd --quit-after 300`，退出码 0，输出 `[CardAuthoringFormSelfCheck] PASS Dock instance preserves pending fields, bindings, and effect ordering`。

## 残留范围

- 本条只收口 FORM 子项，不代表 C7R-B1 完成。Resource/Catalog/manifest 候选全量验证、故障注入与恢复失败自检仍由后续子项处理。
- Godot 的 `OptionButton` 原生 `item_id=-1` 被引擎作为自动 ID 保留值；Dock 因此把第一个可见“待配置”项集中映射为语义 `-1`，不将其写入 Resource。

---

# 2026-07-20 | 开发部 UI-REWARD-NODE-IMPLEMENT-R1

## 变更

- 移除 `NodeMapEntry.Configure()` 的绝对 `ZIndex` / `ZAsRelative=false` 设置。本地“继续”现在继承 Battle、Lingmai、Shop 等父页面的内容层级，地图或 CardReward 覆盖时不会再浮到全局最上层。
- 在 `OverlayCoordinator` 中分离共享地图覆盖平面（`MapCoverZIndex`）。打开地图不再取消或销毁 CardReward；地图在视觉与输入上覆盖当前节点页、胜利弹窗、CardReward 和本地继续，关闭后保留同一个奖励 overlay、RewardPlan、候选顺序与多选进度。
- 继续保留 TopBar 的 y=0..44 常驻输入区；地图输入阻断从 TopBar 下缘开始。Deck/Settings 仍采用原有可恢复取消 CardReward 逻辑，避免把“地图覆盖”与其他互斥 utility overlay 混为一谈。
- 扩展 `OverlayCoordinatorSelfCheck`：验证本地入口继承父级层级、文本为“继续”、按下只调用地图开关；验证地图覆盖不取消 CardReward，Deck/Settings 仍通过取消回调恢复奖励行；同时维持 CardReward 高于胜利内容层的输入门禁。
- 已复核现有 `VictoryRewardList.tscn` 的居中奖励列、灵脉满血休养反馈/消耗、PartyRoster 单人隐藏疗愈入口和页面地图 toggle。它们保持场景/集中规则实现，本轮未引入页面专属结算或 MapScene 路由。

## 修改文件

- `scripts/UI/OverlayCoordinator.cs`
- `scripts/UI/MapOverlayController.cs`
- `scripts/UI/NodeMapEntry.cs`
- `scripts/UI/CardRewardController.cs`
- `scripts/Core/OverlayCoordinatorSelfCheck.cs`
- `docs/开发记录-code.md`

## 验证

- Debug：隔离 `APPDATA/LOCALAPPDATA/DOTNET_CLI_HOME` 后运行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- Release：同环境运行 `dotnet build -c Release -p:NuGetAudit=false .\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console：隔离 `APPDATA/LOCALAPPDATA` 后运行 `--path . --headless --quit`，退出码 0；`OverlayCoordinatorSelfCheck`、`LingmaiInteractionSelfCheck`、奖励状态机、CardCatalog 与 MapGraph 500-seed 自检均 PASS。
- Godot Editor headless：`--path . --editor --headless --quit`，退出码 0，项目和 UI 脚本可解析。
- 入口扫描：未发现 `NodePageMapEntryZIndex`、`ZAsRelative = false`、本地入口文字“地图”、`GoToScene(Map.tscn)`、`OpenMapOnEnter` 或 `CallDeferred(OpenMap...)` 成功路径。命中的“返回地图”仅存在于 Lingmai/Shop/Event 的无效上下文恢复分支，明确调用错误恢复后再开地图，不是正常节点流程。

## 残留风险

- 自动化验证覆盖了协调器契约和真实 headless 自检，仍需用户窗口复测：CardReward 打开时本地继续不可见/不可点而 TopBar 可点；TopBar 打开地图再收起后同一候选和已选进度恢复；胜利、灵脉、商店的本地继续均只开关地图。
- `MapController` 的道痕固化页仍有旧的动态“继续”按钮，但它只调用 `ToggleMap()`，不涉及节点结算或场景跳转；本轮不扩大到道痕页 UI 重构。

---

# 2026-07-20 | 开发部 C7R-B1-SEMANTIC-FIELDS-R2

## 变更

- CardAuthoring Dock 将 `RewardPoolIds` 改为可见的完整列表编辑：支持逗号或换行输入，写回时去除空白、保留首次出现顺序并去重；新草稿保持空列表和“待配置”状态，不再将空字符串当作已填卡池。
- 增加并接通 Upgrade 的显式配置开关、`canUpgrade`、`familyId`、`nextCardId` 字段。可升级卡保存完整三元组；不可升级卡也保存显式的 `canUpgrade=false`、空后继和 family，交由同一 validator 校验，不再以隐式 `false/\"\"` 伪默认通过。
- Preview 增加显式配置开关和 `frameKey` 双向绑定；预览面板显示“待配置”或当前 frameKey，不再从卡名、ID 或资源路径派生预览框。
- `CardDefinitionValidator` 拒绝重复 RewardPoolIds。`CardDefinitionResource` 提供只读的编辑器校验结果属性，使 Dock 和 editor-only 自检复用唯一 C# validator，而不在 GDScript 复制 Resource 语义校验。
- 扩展真实 Dock 自检：实例化 `CardAuthoringDock.tscn`，验证 Pool/Upgrade/Preview 初始待配置、两个卡池保序写回和去重、可升级/不可升级两种 Upgrade 结构、缺 Preview/Upgrade/Pool 被 validator 拒绝。

## 修改文件

- `addons/card_authoring/CardAuthoringDock.tscn`
- `addons/card_authoring/card_authoring_dock.gd`
- `addons/card_authoring/card_authoring_form_selfcheck.gd`
- `scripts/Data/CardDefinitionResource.cs`
- `scripts/Data/CardDefinitionValidator.cs`
- `docs/开发记录-code.md`

## 验证

- Debug：隔离 `APPDATA/LOCALAPPDATA`，复用本机 `C:\\Users\\ASUS\\.nuget\\packages` 后运行 `dotnet build -p:NuGetAudit=false .\\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- Release：同环境运行 `dotnet build -c Release -p:NuGetAudit=false .\\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- Dock 自检：Godot 4.7 Mono Console 运行 `--path . --headless --script res://addons/card_authoring/card_authoring_form_selfcheck.gd --quit-after 300`，退出码 0，输出 `[CardAuthoringFormSelfCheck] PASS Dock preserves pending, form, Pool, Upgrade, Preview, and effect ordering`；同时运行现有 500-seed、Catalog 与状态自检。
- Editor 解析：`--path . --editor --headless --quit`，退出码 0，Dock 与插件可加载。`git diff --check` 退出码 0。
- 硬编码扫描：Dock 仅引用通用 Catalog/卡牌目录和事务服务；未发现具体卡牌 ID、角色 ID、固定卡池或预览资源路径。`CardCatalog.manifest.json` 是 transaction 服务的通用 manifest 路径，且当前缺失，留给下一事务子项处理。

## 残留范围

- 本条仅完成 C7R-B1 的语义字段子项，不代表 C7R-B1 总体完成。下一子项仍需完成 Resource/Catalog/manifest 候选全量验证、临时写/恢复故障注入和无残留事务自检。
- 尚未做用户窗口实测；需在 Godot 编辑器内复测完整卡牌的字段编辑、取消、保存与 Catalog 刷新。

---

# 2026-07-20 | 开发部 UI-REWARD-NODE-R2

## 根因与修复

- 根因：`MapOverlayController` 的 TopBar 下方输入屏障使用透明 `ColorRect`；它虽然阻断输入，但地图面板只覆盖 `y=104..976`，使 CardReward 顶部和本地“继续”底部在地图开关期间仍可见。
- 将该屏障改为集中定义的非透明 `ContentCover`。它从 TopBar 下缘 `y=44` 延伸至屏幕底部，同时承担视觉承载和输入阻断；地图卷轴面板仍保持居中的 `Rect2(64, 104, 1792, 872)` 与左到右揭示，不影响 TopBar 常驻输入。
- 新增 `MapOverlayController.OpenForSelfCheck()`，仅供启动期自检构造真实地图视觉/输入层级，跳过尚未存在的 RunState MapGraph。`OverlayCoordinatorSelfCheck` 现验证：TopBar 区域未被覆盖、CardReward 与本地继续完整位于实色遮罩内、遮罩阻断输入、关闭地图后仍是同一 CardReward 实例且候选/选择进度不变。

## 修改文件

- `scripts/UI/MapOverlayController.cs`
- `scripts/Core/OverlayCoordinatorSelfCheck.cs`
- `docs/开发记录-code.md`

## 验证

- Debug：隔离 `APPDATA/LOCALAPPDATA` 并复用本机 NuGet 包缓存，运行 `dotnet build -p:NuGetAudit=false .\\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- Release：同环境运行 `dotnet build -c Release -p:NuGetAudit=false .\\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- 隔离 player headless：Godot 4.7 Mono Console 运行 `--path . --headless --quit`，退出码 0；所有现有自检、500-seed MapGraph 与新的 `OverlayCoordinatorSelfCheck PASS` 均通过。
- 隔离 editor headless：`--path . --editor --headless --quit`，退出码 0，插件和工程场景解析完成。
- 默认用户环境复现：不覆盖 `APPDATA/LOCALAPPDATA` 时，相同 Godot console 命令在未输出项目日志前卡住，30 秒和 120 秒两次超时后均由 Godot CrashHandler 报 `signal 11`；未复现 QA 所述带 C# `GodotObject.Finalize()` 的 AccessViolation，也没有生成可读项目 crash log。因隔离环境完整通过，当前证据不足以归因项目对象生命周期，不能将默认用户环境的 headless 命令记为 PASS。

## 残留风险

- 用户/QA 仍需窗口实测：打开地图时 CardReward、胜利内容和本地继续均不可见不可点，TopBar 仍可用；关闭地图后同一候选与多选进度恢复。
- 默认用户环境的 Godot mono headless `signal 11` 仍是外部环境阻断。建议保留上述最小复现命令，并以隔离 `APPDATA/LOCALAPPDATA` 作为当前自动化执行环境；若要彻底归因，需要在用户环境中采集 Godot/Windows crash dump。

---

# 2026-07-22 | ARCH-GOV-001 商店购买原子命令边界

## 变更

- 新增短生命周期、非 Autoload 的 `ShopPurchaseCommand`：由 `ShopController` 在成功生成当前商店库存后创建，冻结每个槽位的 Catalog `DefinitionId` 与价格，并独占已售状态。
- `TryPurchase(slot)` 会在任何 RunState 写入前校验槽位、售出状态、余额，并用 `CardCatalogService` 创建唯一的运行时卡牌实例。成功时才一次性扣除灵韵、写入永久牌组并标记售出；提交委托异常时恢复灵韵、永久牌组快照和售出标记。
- `ShopController` 不再直接扣灵韵、调用 `AddCardToDeck` 或维护售出数组；它只提交槽位、展示明确结果并按命令状态刷新商品按钮。
- 新增 Debug 启动自检，覆盖成功、余额不足、缺失 Catalog 卡和“写牌后抛错”的提交失败回滚路径。

## 修改文件

- `scripts/Core/ShopPurchaseCommand.cs`
- `scripts/Core/ShopPurchaseCommandSelfCheck.cs`
- `scripts/Core/GameManager.cs`
- `scripts/UI/ShopController.cs`
- `docs/开发记录-code.md`

## 验证

- 初始 `dotnet build .\AfterHongHuang.csproj` 被用户 NuGet 配置读取权限阻断；使用隔离 `APPDATA/LOCALAPPDATA`、空有效 `NuGet.Config` 和现有 `C:\Users\ASUS\.nuget\packages` 缓存后，`dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj` 退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console 隔离环境运行 `--headless --path . --quit` 退出码 0；输出 `[ShopPurchaseCommandSelfCheck] PASS success, insufficient balance, catalog failure and commit rollback`。
- 购买链静态核对：`ShopController` 仅保留槽位 `BuyCard(index)` 调用；不再存在直接 `LingYun` 写入、`AddCardToDeck` 或 `_sold` 写入。`git diff --check` 退出码 0。

## 残留风险

- 自检以注入的牌组提交委托验证回滚；真实永久牌组 `List.Add` 通常不会抛出，窗口实测仍应确认购买成功、余额不足和 Catalog 资源损坏时页面售出状态与灵韵显示保持一致。

---

# 2026-07-22 | BUG-20260722-001 巫祝初始牌组绑定启动阻断

## 根因与修复

- 根因：生产角色定义已使用 `StarterDeckEntries` 表达 Catalog 卡牌 ID 和重复量，但 `CharacterSelectController` 仍检查迁移期 `StarterDeck` 投影字段，导致巫祝在开始新局前被错误拒绝。
- `CharacterDeckFactory.TryValidate` 现成为角色选择与新局创建共用的无状态校验入口：生产定义验证每个 `StarterDeckEntries` 条目的 ID、数量、Catalog 投影和执行计划；迁移 fixture 的旧 `StarterDeck` 仍仅限自检使用。
- 角色选择改为调用该入口，不识别具体角色 ID；`GameManager.StartNewRun` 会在角色和初始牌组均验证成功后才更新当前角色状态，缺失定义或 Catalog 卡时保留明确错误并不启动半成品新局。
- 扩展现有角色选择绑定自检，验证默认生产角色的 Catalog 配置为拳袭 x4、格挡 x4、烈拳 x1、燃血 x1。

## 修改文件

- `scripts/Core/CharacterDeckFactory.cs`
- `scripts/Core/CharacterSelectionBindingSelfCheck.cs`
- `scripts/Core/GameManager.cs`
- `scripts/UI/CharacterSelectController.cs`
- `docs/开发记录-code.md`

## 验证

- 隔离 `APPDATA/LOCALAPPDATA`、复用本机 NuGet 包缓存后运行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。默认环境仍可能因 NuGet 配置读取权限无法还原 SDK。
- Godot 4.7 Mono Console 隔离环境运行 `--headless --path . --quit`，退出码 0；`[CharacterSelectionBindingSelfCheck] PASS definition-driven selection`，并通过启动期角色/状态/Catalog 自检。
- 静态核对：角色选择不再调用 `TryResolveStarterDeck`，而是使用与 `StartNewRun` 共用的 `CharacterDeckFactory.TryValidate`；`git diff --check` 退出码 0。

## 残留风险

- 自动验证覆盖定义绑定和新局初始化；仍需用户窗口从标题选择默认巫祝并确认进入地图，以验证完整场景路由与实际牌组展示。

---

# 2026-07-22 | ARCH-GOV-002 节点页导航边界治理

## 变更

- 新增非 Autoload、随当前节点页销毁的 `NodePageNavigationCoordinator`。Battle、Lingmai、Shop 页面只请求其地图开关；协调器负责持有 Map overlay、在关闭时保留底层页面，并只在地图节点回调后请求核心路由。
- `GameManager.TryGetNodePageMapInteractivity` 集中判定 Battle 的只读/胜利可推进状态、Lingmai/Shop 的可推进状态和无 ActiveNode 的恢复只读状态。`TryRouteFromNodePage` 是节点页唯一离场命令，关闭 overlay 不会触发它。
- Battle、Lingmai、Shop 移除了各自的 `MapOverlayController.Open`、目标节点迁移和页面清理回调；Battle 成功路由后的奖励/胜利表现层清理由协调器的成功回调执行。缺失或非法路由会保留当前页面与 overlay，并回传明确错误。
- 新增聚焦自检，覆盖 Battle 只读地图、Lingmai/Shop 交互地图、空目标路由拒绝和拒绝后 ActiveNode 保持不变。

## 修改文件

- `scripts/Core/NodePageNavigationCoordinator.cs`
- `scripts/Core/NodePageNavigationSelfCheck.cs`
- `scripts/Core/GameManager.cs`
- `scripts/UI/BattleController.cs`
- `scripts/UI/LingmaiController.cs`
- `scripts/UI/ShopController.cs`
- `docs/开发记录-code.md`

## 验证

- 隔离 `APPDATA/LOCALAPPDATA`、复用本机 NuGet 包缓存后运行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console 隔离环境运行 `--headless --path . --quit`，退出码 0；输出 `[NodePageNavigationSelfCheck] PASS Battle/Lingmai/Shop map access and rejected routes`。
- 静态核对 Battle、Lingmai、Shop 控制器：不再直接调用 `MapOverlayController.Open`、`TryTransitionAndRoute*`、`ChangeSceneToFile`、`GoToScene` 或 `NodeSceneRouter`。`git diff --check` 退出码 0。

## 残留风险

- 自检验证核心访问与拒绝路由边界；仍需窗口实测 Battle 胜利、Lingmai、Shop 三页各自执行“打开地图 -> 收卷 -> 原页面与奖励/选项/库存状态保持”，以及点击真实合法下一节点后的场景离场。

---

# 2026-07-22 | ARCH-GOV-003 战斗胜利结算命令边界

## 变更

- 新增非 Autoload、短生命周期的 `BattleVictorySettlementCommand`。它只编排现有 `GameManager` 的敌人死亡登记、胜利计划构建与原子暂存，并返回绑定当前节点、遭遇与唯一 `BattleVictoryPlan` 的明确结果对象。
- `BattleController` 改为只请求命令并呈现结果：成功时绑定已提交计划显示原有胜利页；计划提交失败时显示原有错误页；不再直接写 `BattleOver`、`PlayerWon`、节点结果或奖励状态。
- 失败登记同样移入 `GameManager.TryRegisterPlayerDefeated`，防止表现层自行提交节点结果。
- 增加仅 Debug 自检用的一次性胜利计划提交失败注入，验证奖励暂存失败不会写入永久牌组、灵韵或未领取灵韵；敌人死亡时独立完成的节点结果维持既有规则。
- 新增聚焦自检，覆盖正常胜利、重复请求复用同一已提交计划、缺失活动战斗上下文拒绝，以及提交失败无永久奖励污染。

## 修改文件

- `scripts/Core/BattleVictorySettlementCommand.cs`
- `scripts/Core/BattleVictorySettlementCommandSelfCheck.cs`
- `scripts/Core/GameManager.cs`
- `scripts/UI/BattleController.cs`
- `docs/开发记录-code.md`

## 验证

- 默认 `dotnet build` 仍可能被用户 NuGet 配置读取权限阻断；隔离 `APPDATA/LOCALAPPDATA`、复用本机 NuGet 包缓存后运行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console 在同一隔离环境运行 `--headless --path . --quit`，退出码 0；输出 `[BattleVictorySettlementCommandSelfCheck] PASS normal, duplicate, stale context and commit failure`。
- 静态核对 `BattleController`：不再命中 `TryRegisterEnemyDefeated`、`TryBuildBattleVictoryPlan`、`TryCommitBattleVictory`、`CreateNodeResult`、`SubmitNodeResult` 或对 `BattleOver` / `PlayerWon` 的直接赋值；`git diff --check` 退出码 0。

## 残留风险

- 自动化覆盖核心命令和状态边界；仍需窗口实测敌人死亡后胜利页、CardReward overlay 与本地“继续”地图入口保持原有呈现和交互。奖励计划异常时应显示错误页且不产生可领取奖励。

---

# 2026-07-22 | ARCH-GOV-004 灵脉与地图操作命令边界

## 变更

- 核对后保留 `MapRenderer`：它只读取 `GameManager` 状态决定按钮可达性，不写 RunState、节点生命周期或路线位置；灵脉路线推进继续由 `GameManager.TryEnterLingmai` 与节点导航端口负责。
- 新增页面生命周期内的普通核心命令 `LingmaiActionCommand`。休养生息、精进道行和疗愈道友均先验证活动灵脉上下文，再通过 `GameManager` / `PartyRoster` 提交状态并返回不可变呈现结果；满血休养与满血队友疗愈仍是有效的一次性无数值变化行动。
- `LingmaiController` 改为只请求命令、显示结果并刷新选项；不再直接写玩家生命、灵脉动作消费标记、永久卡牌升级或 `LastLingmaiResult`。
- 扩展灵脉聚焦自检，覆盖有效灵脉入口的路线推进、满血休养、单人队友目标隐藏、实际队友疗愈、升级、重复行动拒绝，以及无活动/无效地图节点时 RunState 保持不变。

## 修改文件

- `scripts/Core/LingmaiActionCommand.cs`
- `scripts/Core/LingmaiInteractionSelfCheck.cs`
- `scripts/UI/LingmaiController.cs`
- `docs/开发记录-code.md`

## 验证

- 隔离 `APPDATA/LOCALAPPDATA`、复用本机 NuGet 包缓存后运行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console 在同一隔离环境运行 `--headless --path . --quit`，退出码 0；输出 `[LingmaiInteractionSelfCheck] PASS full-health rest and roster eligibility`，以及既有节点导航自检 PASS。
- 静态核对 `LingmaiController` / `MapRenderer`：无玩家生命、地图位置、节点结果、节点推进或 `LastLingmaiResult` 的直接赋值；地图渲染器仅保留当前节点的只读比较。`git diff --check` 退出码 0。

## 残留风险

- 自检覆盖规则端口和失败不变边界；仍需用户窗口确认灵脉页的满血反馈、各行动灰置、多人队友列表及地图关闭后页面状态保持符合预期。

---

# 2026-07-22 | ARCH-GOV-005 卡牌编辑器提交语义校验

## 变更

- 新增纯 C# `CardCatalogSemanticValidator`，统一校验候选 Catalog 路径、Definition/Catalog Schema、运行时执行计划投影及可选 manifest 镜像；运行时 `CardCatalogService` 与编辑器提交共用该入口。
- 新增仅随 `CardAuthoringDock` 生命周期存在的 `[Tool] CardAuthoringValidationBridge`。GDScript 保存事务只提交 staging 路径、manifest 文本和目标路径；Bridge 在 C# 侧加载强类型 Resource，避免跨语言将 `CardDefinitionResource` 降级为基类 `Resource`。
- 保存事务改为先写入 `user://card_authoring/staging/<transaction-id>/`，通过桥接校验后才替换正式 Resource、Catalog 与固定路径 `res://resources/cards/CardCatalog.manifest.json`。语义失败不触碰正式三件套；替换失败时逐项回滚并显式返回回滚失败状态。
- 新增 editor-only 聚焦自检，覆盖合法提交、缺失 TargetPolicy 的语义拒绝，以及 Resource/Catalog/manifest 三个替换阶段的故障回滚。

## 修改文件

- `scripts/Data/CardCatalogSemanticValidator.cs`
- `scripts/Data/CardCatalogService.cs`
- `scripts/Data/CardCatalogResource.cs`
- `scripts/Data/CardDefinitionResource.cs`
- `scripts/Editor/CardAuthoringValidationBridge.cs`
- `addons/card_authoring/CardAuthoringDock.tscn`
- `addons/card_authoring/card_authoring_dock.gd`
- `addons/card_authoring/card_authoring_transaction.gd`
- `addons/card_authoring/card_authoring_transaction_selfcheck.gd`
- `docs/开发记录-code.md`

## 验证

- 隔离 `APPDATA/LOCALAPPDATA`、复用本机 NuGet 缓存后运行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console 在同一隔离环境运行 `--headless --editor --path . --script res://addons/card_authoring/card_authoring_transaction_selfcheck.gd`，退出码 0，输出 `[CardAuthoringTransactionSelfCheck] PASS shared semantic validation and three-file rollback`。
- Godot 4.7 Mono Console 运行 `--headless --path . --quit`，退出码 0；运行时 `CardCatalogSelfCheck` 通过。静态核对 `CardCatalogService` 与 `CardAuthoringValidationBridge` 均调用 `CardCatalogSemanticValidator`；目标文件 `git diff --check` 退出码 0。

## 残留风险

- editor headless 退出码为 0，但 Godot Editor 仍输出既有 RID/ObjectDB 泄漏警告；本轮未证明其由本次 Dock/Bridge 引入。
- 自动化覆盖了 staging、语义拒绝与文件回滚；仍需用户在 Godot 编辑器中用真实草稿确认字段级错误展示和首次正式 manifest 创建符合预期。

---

# 2026-07-22 | BUGFIX-20260722-UI-EDITOR-001 地图 overlay 与插件加载

## 变更

- 地图 overlay 的 TopBar 下方输入层改为透明 `ContentInputShieldColor`：地图卷轴/面板仍可见并阻断节点页业务输入，但 Battle、Lingmai、Shop、胜利奖励等底层页面不再被不透明全屏底页遮住；关闭地图后保持既有页面与奖励实例。
- 同步更新 overlay 自检：验证透明输入屏障、TopBar 未覆盖、内容区输入仍受保护，以及地图关闭不取消 CardReward。
- Card Authoring 的 `plugin.cfg` 将插件脚本改为相对 addon 目录路径 `card_authoring_plugin.gd`，避免 Godot 将 `res://addons/card_authoring/` 与绝对路径重复拼接。

## 修改文件

- `scripts/UI/MapOverlayController.cs`
- `scripts/Core/OverlayCoordinatorSelfCheck.cs`
- `addons/card_authoring/plugin.cfg`
- `docs/开发记录-code.md`

## 验证

- 隔离 `APPDATA/LOCALAPPDATA`、复用本机 NuGet 缓存后运行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console 运行 `--headless --path . --quit`，退出码 0；输出 `[OverlayCoordinatorSelfCheck] PASS modal stack and resolution catalog`。
- Godot 4.7 Mono Console 运行 `--headless --editor --path . --quit`，退出码 0；完成插件扫描与编辑器布局加载，未见 Card Authoring 重复路径加载错误。
- 目标文件静态扫描确认插件脚本为相对路径、没有重复 `res://addons/card_authoring/res://` 前缀；`git diff --check` 退出码 0。

## 残留风险

- headless 可以确认配置解析与 Dock 资源扫描，但不能替代窗口内“启用 -> 禁用 -> 再启用”及地图卷轴视觉层次的人工确认。

---

# 2026-07-22 | BUGFIX-20260722-CARD-AUTHORING-UX-001 Dock 反馈、滚动与尺寸适配

## 变更

- 将 Dock 工具栏改为 `HFlowContainer`，窄 Dock 时按钮自动换行，避免固定横向工具栏裁切关键操作。
- 将 Catalog/字段/预览区域放入 `ContentScroll`；长表单可垂直滚动访问，取消 Catalog 列表的固定宽度下限，保留 `HSplitContainer` 的可调整分栏。
- 状态区移为 Dock 底部的固定可见 `RichTextLabel` 并启用自身滚动。字段校验、预览/Diff、保存、取消和无草稿反馈均写入该区域；预览/Diff 无草稿时新增明确错误反馈。
- 扩展现有 Dock 自检，核对垂直滚动路径、状态区归属，以及预览/Diff 与校验失败的可见状态文本；未修改表单字段、保存事务或 C# Tool bridge。

## 修改文件

- `addons/card_authoring/CardAuthoringDock.tscn`
- `addons/card_authoring/card_authoring_dock.gd`
- `docs/开发记录-code.md`

## 验证

- Godot 4.7 Mono Console 在隔离环境运行 `--headless --editor --path . --script res://addons/card_authoring/card_authoring_form_selfcheck.gd`，退出码 0，输出 `[CardAuthoringFormSelfCheck] PASS`。
- 同环境运行 `--headless --editor --path . --script res://addons/card_authoring/card_authoring_transaction_selfcheck.gd`，退出码 0，输出 `[CardAuthoringTransactionSelfCheck] PASS shared semantic validation and three-file rollback`。
- 目标文件 `git diff --check` 退出码 0。本轮未修改 C#，未重复运行 `dotnet build`。

## 残留风险

- editor headless 能解析 Dock、执行表单与事务自检，但仍需用户在实际 Godot Dock 的宽/窄、高/低尺寸下确认工具栏换行、长表单滚动和多行错误文本的可读性。

---

# 2026-07-27 | TITLE-TECH-001 标题页与共享设置界面去硬编码治理

## 变更

- `TitleController` 改为由 `Title.tscn` 显式注入按钮、版本标签、角色选择 `PackedScene` 与共享设置 `PackedScene`；不再依赖节点层级字符串或角色选择运行时路径。标题页移除时间线按钮及动态占位弹窗。
- 标题页版本标签只读取 `ProjectSettings.application/config/version`；版本值写入 `project.godot`，场景和 C# 不再保存重复版本字面量。
- 标题场景改用锚点、`VBoxContainer`、`CenterContainer`、`MarginContainer` 和集中 `TitleTheme.tres` 入口。此轮仅治理装配与响应式结构，未接入视频或最终视觉样式。
- 新增可复用 `SettingsDialog.tscn` 与 `SettingsDialogController`，承接分辨率、返回标题、退出和关闭输入。`SettingsHelper` 仅负责单实例生命周期、OverlayCoordinator 注册及显式意图路由，不再动态构造控件、样式或文案。
- Battle、Map、Lingmai、Shop、Event 和 Title 均通过场景导出的同一 `SettingsDialogScene` 调用共享设置入口；缺失装配会记录错误并拒绝创建半成品弹窗。

## 修改文件

- `project.godot`
- `scripts/UI/TitleController.cs`
- `scenes/Title/Title.tscn`
- `scenes/Title/TitleTheme.tres`
- `scripts/UI/SettingsHelper.cs`
- `scripts/UI/SettingsDialogController.cs`
- `scenes/Settings/SettingsDialog.tscn`
- `scripts/UI/BattleController.cs`
- `scripts/UI/MapController.cs`
- `scripts/UI/LingmaiController.cs`
- `scripts/UI/ShopController.cs`
- `scripts/UI/EventController.cs`
- `scenes/Battle/Battle.tscn`
- `scenes/Map/Map.tscn`
- `scenes/Lingmai/Lingmai.tscn`
- `scenes/Shop/Shop.tscn`
- `scenes/Event/Event.tscn`
- `docs/开发记录-code.md`

## 验证

- 默认 `dotnet build .\AfterHongHuang.csproj` 受用户目录 `NuGet.Config` 读取权限阻断；使用隔离 `APPDATA/LOCALAPPDATA` 并复用本机 NuGet 缓存运行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`，退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console 在隔离用户目录下运行 `--headless --path . --quit`，退出码 0；既有启动自检全部 PASS，未出现标题场景装配错误。
- 同一环境运行 `--headless --editor --path . --quit`，退出码 0；Title、Settings 及直接调用页面场景通过编辑器扫描/解析。
- 静态扫描确认：Title/Settings 控制器中无 `GetNode` 层级路径、时间线/占位弹窗、角色选择 `res://` 路径、版本字面量或动态 `AcceptDialog`/控件/主题样式构造；唯一角色选择路径为 `Title.tscn` 的声明式 `PackedScene` 引用。所有设置调用均传递 `SettingsDialogScene`。

## 残留风险

- headless 环境无法验证真实窗口的分辨率变化、系统退出以及标题开始按钮后的可见跳转；需用户窗口手测开始游戏、设置重复打开/关闭、分辨率应用、返回标题和退出。
- `GameManager.GoToTitle()` 仍是既有核心路由实现；本轮未扩大为全局场景路由改造。

---

# 2026-07-27 | TITLE-TECH-001 用户窗口验收退回修复

## 根因与修复

- `Title.tscn` 使用 PascalCase 写入 C# Node 导出属性，Godot 4.7 Mono 未将 `NodePath` 注入对应 `Button/Label` 属性，`TitleController` 因装配校验失败主动禁用了三个按钮。
- 标题场景重建时遗漏旧 `Background`、标题和版本的颜色/字体声明；空 `ColorRect` 显示为默认白色，导致标题与版本在窗口中不可见。
- 保留显式场景装配：`CharacterSelectScene`/`SettingsDialogScene` 使用有效的 C# `PackedScene` 属性；按钮与版本标签改由场景声明的 `unique_name_in_owner` 节点解析（`%StartButton` 等），不依赖脆弱父子层级路径。设置弹窗采用同一方式绑定内部控件。
- 恢复既有深色背景、金色标题、灰色版本号和基础标题按钮可见性；背景及容器 `MouseFilter=Ignore`，只有按钮接收点击。未恢复时间线、未接入 MP4 或最终视觉样式。

## 修改文件

- `scenes/Title/Title.tscn`
- `scripts/UI/TitleController.cs`
- `scenes/Settings/SettingsDialog.tscn`
- `scripts/UI/SettingsDialogController.cs`
- `scenes/Battle/Battle.tscn`
- `scenes/Map/Map.tscn`
- `scenes/Lingmai/Lingmai.tscn`
- `scenes/Shop/Shop.tscn`
- `scenes/Event/Event.tscn`
- `docs/开发记录-code.md`

## 验证

- 隔离 `APPDATA/LOCALAPPDATA` 并复用本机 NuGet 缓存运行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`：退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console 运行 `--headless --path . --quit`：退出码 0；不再输出 `[标题] 场景装配无效`，既有 Debug 自检继续通过。
- Godot 4.7 Mono Console 运行 `--headless --editor --path . --quit`：退出码 0，Title/Settings 及直接调用页面场景解析通过。
- `git diff --check`：通过。聚焦扫描未发现时间线、旧动态占位弹窗、重复版本号或 Title/Settings 中的运行时 `res://` 场景路径；标题场景仅保留声明式 PackedScene 引用。

## 残留风险

- 当前环境未执行真实窗口点击回归，仍需用户确认深色标题视觉、开始进入角色选择、设置重复打开/关闭与分辨率应用、退出按钮响应。

---

# 2026-07-27 | TITLE-RUNTIME-002 分辨率运行时修复与标题视频接入

## 根因与修复

- `ResolutionSettings.TryApply()` 原先直接向当前宿主请求尺寸并立即校验。窗口处于最大化或全屏时，原生窗口管理器会忽略 `WindowSetSize`；Godot 编辑器嵌入式运行也可能由宿主保持 `1920x1080`，因此用户看到实际尺寸未变。
- 现在应用目录项前先记录旧尺寸和窗口模式；若不是窗口化，先明确切换至窗口化，再请求尺寸并核验实际尺寸。任何模式切换、尺寸应用或设置文件写入失败都会恢复先前窗口状态，且不会写入持久化配置。
- 无法接受尺寸请求时，错误反馈明确指出运行宿主限制，并说明编辑器嵌入式游戏需改用独立游戏窗口或导出包测试；不会把失败选择写入配置。

## 标题视频状态

- 最终源文件为 `D:\用户目录\Pictures\comfyUI-Output\video\标题页S2.mp4`，大小 86,595,074 bytes，SHA-256 `8861AD968AC9B07642B9F58A00C6A02C797967BB32B3B5DD69BF4B22CE3CA853`。
- S2 参数为 3840×2160、16:9、24 fps、32.38 秒；视频为 H.264，音频为 48 kHz 立体声 AAC。
- 使用工作区临时工具 `D:\GameCode\洪荒之后\.tmp\tooling\ffmpeg\ffmpeg.exe`（FFmpeg 7.1，含 `libtheora`/`libvorbis`）原样转码为 `resources/video/title_background.ogv`。未对 S2 做裁剪、变速、补帧、删帧、倒放或端点处理。
- OGV 产物为 26,871,574 bytes，SHA-256 `763C71A056ACEEE57F82D9C2ECB713F17E2C0F02ECAEBB4A39B2405326B83B28`；探测参数为 3840×2160、16:9、24 fps、32.38 秒，视频编码 Theora，音频编码 Vorbis、48 kHz 立体声。
- 美术标题母版目录已替换为唯一正式母版 `洪荒之后-art/assets/ai_source/source/title/标题页s2.mp4`，旧 S1 移至工作区 `.tmp/title-background-s1-replaced.mp4` 作为可恢复备份；未保留两个并列正式母版。
- 新增 `resources/video/title_background.tres`，显式设置 Theora stream 循环；`Title.tscn` 声明式装配 `VideoStreamPlayer`，节点位于纯色背景之后、标题布局之前，覆盖根 Control，`autoplay=true`、`expand=true`、`mouse_filter=Ignore`。菜单按钮仍由原有 `TitleController` 接收输入。

## 修改文件

- `scripts/UI/ResolutionSettings.cs`
- `scripts/UI/TitleController.cs`
- `scenes/Title/Title.tscn`
- `resources/video/title_background.tres`
- `resources/video/title_background.ogv`
- `洪荒之后-art/assets/ai_source/source/title/标题页s2.mp4`
- `docs/开发记录-code.md`

## 验证

- 默认执行 `dotnet build .\AfterHongHuang.csproj`：退出码 1；MSBuild 因拒绝读取 `C:\Users\ASUS\AppData\Roaming\NuGet\NuGet.Config` 无法解析 `Godot.NET.Sdk/4.7.0`，属于用户目录配置访问限制，未由本轮代码引入。
- 隔离 `APPDATA/LOCALAPPDATA` 并复用本机 NuGet 缓存执行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`：退出码 0，0 警告、0 错误。
- FFmpeg 转码命令：`ffmpeg.exe -y -hide_banner -i "D:\用户目录\Pictures\comfyUI-Output\video\标题页S2.mp4" -map 0:v:0 -map 0:a:0 -c:v libtheora -q:v 7 -pix_fmt yuv420p -c:a libvorbis -q:a 5 -f ogv "resources/video/title_background.ogv"`，退出码 0。
- FFmpeg 对 S2 OGV 完整解码探测：退出码 0；确认 Theora/Vorbis、3840×2160、24 fps、32.38 秒，未发现解码错误。
- Godot 4.7 Mono Console 隔离运行 `--headless --editor --path . --quit`：退出码 0，Title 场景及 `VideoStreamTheora` 资源解析通过。
- Godot 4.7 Mono Console 隔离运行 `--headless --path . --quit`：退出码 0，项目启动自检全部通过，未出现标题视频资源加载错误。
- `TitleController` 增加了仅针对 `%TitleVideo` 节点和 `Stream` 的装配检查，缺失时记录明确错误；不包含媒体路径或视频参数。
- `git diff --check`：通过；静态核对确认 TitleController 未新增媒体路径，VideoStreamPlayer 使用场景资源引用且忽略鼠标输入。
- 静态核对确认 `Title.tscn`、`title_background.tres`、`TitleController.cs` 的声明式接入与 Finished 循环逻辑未因母版替换改变。

## 残留风险

- 当前执行环境不能进行独立窗口尺寸切换实测；需用户在独立游戏窗口或导出包中选择 1600x900，确认实际窗口尺寸变更和配置仅在成功后保存。

---

# 2026-07-29 | TITLE-RUNTIME-003 无音轨标题视频替换

## 变更

- 批准源：`D:\用户目录\Pictures\comfyUI-Output\video\标题页成品-无音轨.mp4`，39,394,426 bytes，SHA-256 `AA9D902FE4D11CA023AEB42E4F72FF55BAAC82151E608A62A79B8FF47A21C498`。
- 源媒体确认只有一个 H.264 视频流：3840×2160、16:9、24 fps、16.21 秒；无音频流。
- 使用 `D:\GameCode\洪荒之后\.tmp\tooling\ffmpeg\ffmpeg.exe` 仅映射视频流并设置 `-an`，覆盖 `resources/video/title_background.ogv`。未处理音频文件，未修改 `title_background.tres`。
- 新 OGV：Theora 视频，3840×2160、约 16.21 秒、24 fps，13,414,215 bytes，SHA-256 `57AFC0B7821577A5B6936F50A2BB1AAA21EBBB70697209C35845108DA0F5C10D`；无音频流。

## 修改文件

- `resources/video/title_background.ogv`
- `docs/开发记录-code.md`

`Title.tscn`、`resources/video/title_background.tres`、`scripts/UI/TitleController.cs` 本轮未修改，现有自动播放、循环、Finished 重播和输入穿透装配保持不变。

## 验证

- FFmpeg 转码命令：`ffmpeg.exe -y -hide_banner -i "D:\用户目录\Pictures\comfyUI-Output\video\标题页成品-无音轨.mp4" -map 0:v:0 -an -c:v libtheora -q:v 7 -pix_fmt yuv420p -f ogv "resources/video/title_background.ogv"`：退出码 0。
- FFmpeg 对最终 OGV 完整解码到结尾：退出码 0；输出仅有 Theora 视频流，探测显示 `audio:0KiB`。
- 隔离 APPDATA/LOCALAPPDATA 并复用本机 NuGet 缓存运行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`：0 警告、0 错误。
- Godot 4.7 Mono Console：`--headless --editor --path . --quit` 与 `--headless --path . --quit` 均退出码 0。
- 目标差异检查：仅覆盖运行时 OGV并追加本记录；Title 场景、循环资源、C# 控制器未发生本轮无关修改。
- `git diff --check`：通过。

## 残留风险

- 未执行真实窗口视觉播放；仍需用户确认约 16.21 秒视频循环、标题按钮可用性和无音轨表现。未启动 QA。

### 标题背景视频循环返工

- 根因：`VideoStreamTheora.loop=true` 在当前 Godot 4.7 .NET 运行链中未使 `VideoStreamPlayer` 在播放结束后重新开始，播放器在一次 `Finished` 后停留在结束位置。
- `TitleController` 现在只对场景中的同一个 `%TitleVideo` 绑定一次 `Finished`；回调将 `StreamPosition` 设为 0 并调用同一播放器的 `Play()`，不重载场景、不创建第二个播放器、不使用 Timer 或 `_Process` 轮询。
- `_ExitTree()` 在标题场景销毁时解除该信号，避免跨场景残留连接。重复进入 `_Ready()` 时由连接标记防止重复注册。

## 循环验证

- 隔离环境运行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`：退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console：`--headless --editor --path . --quit` 退出码 0；`--headless --path . --quit` 退出码 0。
- 静态核对 `TitleController.cs`：`Finished +=` 仅绑定一次，`Finished -=` 仅在 `_ExitTree()` 执行；未新增媒体路径、Timer 或 `_Process`。

## 循环返工限制

- headless 可验证脚本编译、场景加载和资源装配，但无法证明持续 20.33 秒后的真实窗口画面循环；需用户保持标题页超过一个视频时长，确认背景无停帧且菜单仍可操作。

---

# 2026-07-29 | TITLE-AUDIO-001 标题页 BGM 运行时接入

## 变更

- 批准源：`D:\用户目录\Music\afterhh\出发-on-the-way.wav`，31,350,156 bytes，SHA-256 `41D41C78F8D15A26199A808278D39E5666540D217979DAB0D8707CEF82501C5B`。
- 源媒体为 44,100 Hz、立体声、PCM 16-bit，完整时长 `00:02:57.72`，仅含音频流。
- 使用工作区临时 FFmpeg 7.1 的 `libvorbis` 原样转码为 `resources/audio/music/title_bgm_on_the_way.ogg`，未裁剪、变速或重混；产物为 3,356,136 bytes，SHA-256 `254A371F3DB1C3B617819294C5230D931FFCD1C553B52C3BE2371E50620CD5E1`。
- `Title.tscn` 新增一个场景内 `AudioStreamPlayer`，引用该 OGG，`autoplay=true`、`volume_db=-6.0`、`bus=&"Master"`；播放器随标题场景销毁，不新增全局音频服务。
- `title_bgm_on_the_way.ogg.import` 显式设置 `loop=true`、`loop_offset=0`，从 0 秒循环；标题视频及其 Finished 重播逻辑未改动。

## 修改文件

- `resources/audio/music/title_bgm_on_the_way.ogg`
- `resources/audio/music/title_bgm_on_the_way.ogg.import`
- `scenes/Title/Title.tscn`
- `docs/开发记录-code.md`

## 验证

- FFmpeg 完整解码最终 OGG 到结尾：退出码 0；探测为单一 Vorbis 音频流、44,100 Hz、立体声、`00:02:57.72`，无视频流。
- Godot 4.7 Mono Console `--headless --editor --path . --quit`：退出码 0；音频重新导入、Title 场景解析通过，循环参数保持 `loop=true`/`loop_offset=0`。
- Godot 4.7 Mono Console `--headless --path . --quit`：退出码 0；现有启动自检通过。引擎退出时仍报告项目既有的对象泄漏警告，但未导致非零退出。
- 隔离 `APPDATA/LOCALAPPDATA` 并复用本机 NuGet 缓存执行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`：退出码 0，0 警告、0 错误。
- `git diff --check`：通过；静态确认 Title 场景仅有一个标题 `AudioStreamPlayer`，TitleController、标题视频和 `title_background.tres` 未作本轮修改。

## 残留风险

- 未执行真实窗口音频播放与跨 2:57.72 的循环实测；需用户在标题页确认 BGM 自动播放、从 0 秒循环、离开标题停止，且菜单/视频行为不受影响。开发完成，待用户验收。

---

# 2026-07-29 | AUDIO-SETTINGS-001 全局音量分层与设置页

## 变更

- 新增 `default_bus_layout.tres`：建立 `Music -> Master`、`SFX -> Master`；`Master` 继续作为总音量层，并在 `project.godot` 注册默认总线布局。
- 新增 `scripts/Core/AudioSettingsService.cs` 并按批准范围注册为 Autoload。服务只负责三层百分比/显式静音、总线应用、`user://afterhonghuang-settings.cfg` 的 `audio` section 与错误结果；不持有播放器或音乐状态机。
- 默认音量为 Master `80%`、Music `65%`、SFX `80%`。百分比使用 0~100 线性值，0% 使用可靠静音；显式静音保留百分比，拖动到大于 0% 自动解除该层显式静音。
- 新增可复用 `VolumeRow.tscn` / `VolumeRowController.cs`，设置场景改为声明式单页 Control overlay，包含声音、显示、右上角关闭和 footer 左侧退出游戏；移除 `AcceptDialog` 底部关闭和“返回标题界面”。Esc、右上角关闭和再次点击共享设置入口均走同一关闭实例路径。
- `ResolutionSettings` 改为先加载现有配置再写入 `display` section，保留 `audio` section；音频保存同样先加载并保留 `display` section。失败时不报告假成功并恢复本次已改动的实际音量/窗口状态。
- `Title.tscn` 的唯一标题 BGM 播放器改走 `Music` bus，局部播放器、自动播放、循环和 `-6 dB` 保持不变；未修改音频文件、视频或 `TitleController.cs`。

## 修改文件

- `default_bus_layout.tres`
- `project.godot`
- `scripts/Core/AudioSettingsService.cs`（及 Godot 生成的 `.uid`）
- `scenes/Settings/SettingsDialog.tscn`
- `scenes/Settings/VolumeRow.tscn`
- `scripts/UI/SettingsDialogController.cs`（及 Godot 生成的 `.uid`）
- `scripts/UI/VolumeRowController.cs`（及 Godot 生成的 `.uid`）
- `scripts/UI/SettingsHelper.cs`
- `scripts/UI/ResolutionSettings.cs`
- `scenes/Title/Title.tscn`
- `docs/开发记录-code.md`

## 验证

- 隔离 `APPDATA/LOCALAPPDATA` 并复用本机 NuGet 缓存执行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`：退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console `--headless --editor --path . --quit`：退出码 0；默认总线布局、AudioSettingsService、Title/Settings/VolumeRow 场景解析通过。
- Godot 4.7 Mono Console `--headless --path . --quit`：退出码 0；现有项目启动自检通过，AudioSettingsService 随 Autoload 初始化。
- 静态核对确认 `Music`/`SFX` 发送到 `Master`，标题 BGM 唯一播放器使用 `Music`；设置控制器不直接写 AudioServer、分贝换算或配置文件。
- 静态核对确认设置场景不存在 `ReturnToTitleButton`、`ok_button_text` 或 `AcceptDialog`，共享入口调用仍由 `SettingsHelper.Show` 统一处理。
- `git diff --check`：通过；未整理或覆盖工作树中与本任务无关的既有修改。

## 残留风险

- 未执行真实窗口试听、三层音量/静音的听感验证、设置入口重复点击与 Esc 的窗口交互验证；需用户在独立窗口中确认默认音量、Master/Music/SFX 串联、0% 静音、重启持久化、1280×720 可读性和关闭路径。开发完成，待用户验收。

- 用户授权返工：设置层已从独占 Window 改为当前页面内 Control overlay；scrim 从 TopBar 下缘 44px 开始，根节点忽略输入以保留 TopBar，右上角/Esc/再次点击设置入口继续共用同一关闭路径。

### 用户窗口反馈返工：三路绑定、静音控件与动态遮罩

- 根因：`VolumeRow.tscn` 实例覆盖使用了 `display_label/channel`，没有写入 C# 导出的 `DisplayLabel/Channel`，三行因此都保留同一默认绑定；同时共享设置场景固定从 44px 开始遮罩，标题页顶部出现缝隙。
- 修复：三个实例改为显式 `DisplayLabel/Channel`（总音量/Master、音乐/Music、音效/Sfx）；`SettingsDialogController` 在接线前校验三行存在、顺序和唯一通道，`VolumeRowController` 拒绝缺失标签。每行事件继续携带自身 `AudioChannel`，不修改 `AudioSettingsService`。
- 静音控件改为“静音”Label 后的无文字 40×40 `CheckBox`；正常音频状态清空并隐藏，只有服务/保存失败显示错误；设置页退出游戏按钮及 `QuitRequested/QuitApplication` 链已移除，标题页独立退出按钮不变。
- `SettingsHelper` 通过宿主直接子节点的 `TopBar` 类型读取当前尺寸、最小尺寸和合并最小尺寸，调用控制器接口设置 scrim 顶部边界；无 TopBar 为 0，带 TopBar 使用实际高度。根节点继续 Ignore，scrim 继续 Stop。

## 本轮验证

- 隔离 `APPDATA/LOCALAPPDATA` 并复用本机 NuGet 缓存执行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`：退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console `--headless --editor --path . --quit`：退出码 0；Settings/VolumeRow/Title 资源与 C# 导出属性解析通过。
- Godot 4.7 Mono Console `--headless --path . --quit`：退出码 0；既有项目自检通过。
- 静态聚焦检查：三行绑定唯一、CheckBox 位于静音文字之后且具 40px 热区；无旧小写覆盖、固定 44px scrim、设置页退出链或正常成功占位文案；TopBar inset 只按类型解析；`git diff --check` 通过。

## 本轮限制

- 未执行用户窗口点击/听感复测；仍需确认三路滑块与静音分别生效、标题页全屏遮罩、游戏页面 TopBar 可点、右上角/Esc/重复入口关闭及 1280×720 布局。Godot headless 仍输出项目既有对象泄漏警告，但退出码为 0。

---

# 2026-07-29 | TITLE-RUNTIME-003 用户窗口返工：倒放标题视频无音轨替换

## 变更

- 批准源：`D:\用户目录\Pictures\comfyUI-Output\video\标题页已倒放-未去音轨.mp4`；大小 `86,595,074` bytes，SHA-256 `8861AD968AC9B07642B9F58A00C6A02C797967BB32B3B5DD69BF4B22CE3CA853`。
- 使用工作区 `D:\GameCode\洪荒之后\.tmp\tooling\ffmpeg\ffmpeg.exe`（FFmpeg 7.1）执行视频流直拷并显式排除音频：`-map 0:v:0 -c:v copy -an`；生成中间文件 `D:\GameCode\洪荒之后\.tmp\title-background-reversed-noaudio.mp4`。
- 派生 MP4 大小 `86,065,138` bytes，SHA-256 `0514FAE87D4BC76C7EC7722C438731503BECEE1D923552F50374EF347B56024F`；保留 H.264、3840×2160、24fps、16:9、32.38秒视频参数，移除音频流。
- 由派生 MP4 转换并覆盖 `resources/video/title_background.ogv`；仅含 Theora 视频流，3840×2160、24fps、32.38秒。正式 OGV 大小 `26,448,917` bytes，SHA-256 `AA4492CDD4C622354193C68831ECF1FB8D535D9B117F4B50850E93B9416194E7`。
- 本批次未修改 `title_background.tres`、`Title.tscn`、`TitleController.cs`、标题 BGM、设置或 art 工作树；继续复用既有循环、自动播放、Music 路由和按钮装配。

## 验证

- FFmpeg 派生 MP4 完整解码：退出码 0；音频流映射返回 `-22`（无匹配音频流）。
- FFmpeg 正式 OGV 完整解码：退出码 0；流报告为 `Video: theora`，音频流映射返回 `-22`。
- 隔离 `APPDATA/LOCALAPPDATA` 并复用本机 NuGet 缓存执行 `dotnet build -p:NuGetAudit=false .\AfterHongHuang.csproj`：退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console：`--headless --editor --path . --quit` 与 `--headless --path . --quit` 均退出码 0；玩家自检 PASS。退出时仍报告项目既有的 4 个 ObjectDB 泄漏和 2 个资源占用警告，但未导致非零退出。
- 静态核对：`Title.tscn` 仍为单一 `VideoStreamPlayer`/标题 BGM 播放器；视频 `mouse_filter=2`、`autoplay=true`，BGM 使用 `Music`；`TitleController.cs` 仍只绑定一次 `Finished`，并保留 `StreamPosition=0` 后 `Play()`；按钮与设置 PackedScene 引用未改动。
- `git diff --check`：通过。

## 限制与风险

- 当前工作区没有 `ffprobe.exe`，媒体流与参数使用 FFmpeg 7.1 的输入/解码报告及显式 stream map 验证；未伪造 ffprobe 结果。
- 未执行持续窗口播放和视觉验收；需用户确认标题页显示倒放视频、循环衔接、菜单可操作及 BGM/设置行为。开发完成，待用户验收。

---

# 2026-07-30 | BUGFIX-OVERLAY-CARD-INPUT-001 阶段 B：设置层与卡牌指针输入

## 根因与变更

- `OverlayCoordinator` 新增集中 `GlobalSettings` 语义层。设置打开时保留地图、套牌、胜利页和卡牌奖励实例；关闭设置或从其他 TopBar 工具入口切换时通过统一回调处理，不再由设置入口清理下层 overlay。
- `SettingsHelper` 改用专用设置层装配，并在场景退出时注销引用，保持同一设置实例的可恢复关闭路径。
- `BattleController` 将左键 Press、拖拽、Release、右键 Cancel 收口到单一指针手势状态；Release 不再依赖 `CardButton` 热区，右键取消会吞掉原左键 Release，新的左键 Press 才能开始下一次交互。敌方目标判定改为读取实际敌人控件的全局矩形。
- 新增 `CardPointerGestureSelfCheck`，并接入现有 `OverlayCoordinatorSelfCheck`。

## 修改文件

- `scripts/UI/OverlayCoordinator.cs`
- `scripts/UI/SettingsHelper.cs`
- `scripts/UI/BattleController.cs`
- `scripts/Core/OverlayCoordinatorSelfCheck.cs`
- `scripts/Core/CardPointerGestureSelfCheck.cs`
- `scripts/Core/CardPointerGestureSelfCheck.cs.uid`
- `docs/开发记录-code.md`

## 验证

- 隔离 `APPDATA/LOCALAPPDATA`、复用本机 NuGet 缓存执行 `dotnet build .\AfterHongHuang.csproj -p:NuGetAudit=false`：退出码 0，0 警告、0 错误。
- Godot 4.7 Mono Console 执行 `--headless --editor --path . --quit` 与 `--headless --path . --quit`：均退出码 0。
- 自检包含 GlobalSettings 层级、设置打开/关闭保留下层实例、其他 TopBar 入口先关闭设置，以及合法/非法 Release、右键取消吞 Release、新 Press 恢复；`CardPointerGestureSelfCheck` 与 `OverlayCoordinatorSelfCheck` 均输出 PASS。
- 聚焦扫描确认无固定敌人矩形 `EnemyPortraitCenter`、具体敌人/卡牌/内容 ID分支；`git diff --check` 通过。

## 定向返工：地图已开时的 TopBar toggle 顺序

- 根因：`NodePageNavigationCoordinator.TryToggleMap` 原先在 `IsOverlayAlive()` 分支直接关闭地图，绕过了 `OverlayCoordinator.TryPrepareMap` 的 GlobalSettings 关闭负责人。
- 修复：生产 toggle 现在始终先执行集中地图准备，再按已有地图状态关闭或进入打开流程；Battle、Lingmai、Shop 共用同一实现，未复制页面逻辑。
- 自检：`NodePageNavigationSelfCheck` 增加真实 toggle 顺序的聚焦证明，验证已有地图分支先关闭 GlobalSettings，再关闭地图，不重建地图、不触发路由。

## 限制与风险

- 未执行用户窗口点击/拖拽复测；仍需在实际窗口确认设置覆盖地图/胜利/CardReward 时实例恢复、TopBar 可用，以及卡牌拖出按钮热区后的合法/非法释放和右键取消体验。headless 不能替代窗口输入验证。
- 状态：开发完成，待用户窗口验收。

### 2026-07-30 合并返工：全屏设置、地图局部输入、放弃本局与卡牌事务

- 设置改为全视口 GlobalSettings 语义层：scrim 覆盖包括 TopBar 在内的完整 viewport，设置内容、右上角关闭和 Esc 保留输入，关闭后不销毁下层地图/胜利/CardReward 实例；新增游戏内“返回主菜单”意图，由 `GameManager.TryAbandonRunToTitle` 清理当前局临时状态并路由标题，不提交奖励、节点结果或路线。
- `MapOverlayController` 移除地图滚轮/拖拽的全局 `_Input`，改由地图局部交互区域及节点控件接收 `GuiInput`，保留短按/拖拽阈值并在释放、离开、关闭和退树时清理手势；`MapRenderer` 的当前节点显示优先读取活动节点生命周期，未提前推进 `CurrentMapNodeId`。
- `GameManager.PlayCard` 在扣灵力前完整校验冻结的 `ResolvedCardExecution`；成功后使用同一冻结结果提交，后续失败恢复战斗状态、牌区、日志 trace 与最近解析对象；真实过期计划仍零副作用拒绝。`CardExecutionC7SelfCheck` 覆盖非零费敌方目标、自身、无目标和 stale plan。
- 直接相关聚焦自检通过：`OverlayCoordinatorSelfCheck`、`CardPointerGestureSelfCheck`、`NodePageNavigationSelfCheck`、`CardExecutionC7SelfCheck` 及既有 MapGraph 500-seed 自检。隔离 NuGet 缓存 Debug/Release 构建均为 0 警告/0 错误，Godot player/editor headless 退出码均为 0；player 退出时仍有项目既有 4 个 ObjectDB 泄漏和 2 个资源占用警告，未宣称无泄漏。
- 未做用户窗口输入/视觉复测；需确认全屏设置确实阻断 TopBar、地图局部滚轮/拖拽、放弃本局返回标题及三类非零费卡牌实机行为。状态：开发完成，待用户窗口验收。
