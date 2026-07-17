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
