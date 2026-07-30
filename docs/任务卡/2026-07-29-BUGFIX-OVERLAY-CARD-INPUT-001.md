# BUGFIX-OVERLAY-CARD-INPUT-001 设置覆盖层与战斗卡牌指针交互返工

## 状态

- 严重级别：P0（攻击牌主交互阻断）/ P1（设置覆盖层次序错误）
- 当前状态：Done（2026-07-30 用户窗口验收通过）
- 责任顺序：UI/UX 部 → 开发部（串行）
- QA：默认不启动；仅在代码证据无法确定覆盖层契约时，使用用户本次给出的条件授权做一次限定范围只读咨询

## 用户反馈与证据

用户于 2026-07-29 提供三张窗口截图：

- `C:\Users\ASUS\AppData\Local\Temp\codex-clipboard-a02e514c-0002-4c34-b82a-f2761fa4b95b.png`
- `C:\Users\ASUS\AppData\Local\Temp\codex-clipboard-3c4ab445-9906-45f9-b717-401b68925a3c.png`
- `C:\Users\ASUS\AppData\Local\Temp\codex-clipboard-e7e75ad9-23c2-407d-a3c9-dc2a7b5f487a.png`

现象：

1. 地图 overlay 打开时点击 TopBar 设置，地图被关闭；期望设置临时叠在地图之上，关闭后恢复同一个地图实例及其滚动/交互状态。
2. 战斗胜利面板存在时点击设置，设置被胜利面板遮挡；期望设置成为当前内容区域的最上层全局模态，同时保留 TopBar 全局入口契约。
3. 敌方目标攻击牌拖向敌人后无结算反应。
4. 左键按住卡牌拖出时按右键，视觉已取消；继续松开左键后卡牌又进入选中/吸附状态。

## 已确认根因

1. `OverlayCoordinator.TryPrepareUtilityOverlay()` 直接关闭 `_mapOverlay`，设置入口复用了该通用互斥策略，因而地图必然被销毁。
2. 设置场景作为普通 `Control` 挂在页面根节点，未进入独立的语义化全局模态平面；胜利层和地图层已有较高固定范围，设置仍处于默认相对层。
3. 敌方目标牌的拖拽完成依赖 `CardButton._GuiInput` 在鼠标离开卡牌热区后继续收到左键释放，输入所有权不可靠；敌方合法目标仍使用固定屏幕矩形，未消费实际目标控件矩形。
4. 右键取消清除了 `_pressing/_dragging`，但同一物理左键手势后续的 Release 未被标记为已取消，随后被普通点击路径重新解释。

## 架构决策方向

不建立新框架或全局事件总线。渐进修正现有 `OverlayCoordinator`：

- 页面内容、胜利、卡牌奖励、地图、全局设置、转场/致命错误使用集中命名的语义平面或容器契约。
- 允许少量集中、命名清楚的层级边界；禁止各控制器散落魔法 `ZIndex` 或用 `Reparent/MoveChild` 掩盖归属。
- 设置是全局临时模态：打开时不销毁地图、胜利、CardReward 或节点页状态；关闭后露出原实例。
- 设置遮罩阻断 TopBar 以下的全部输入；TopBar 仍按现有不变量可见、可交互。再次点击设置、右上角和 `Esc` 关闭同一实例。
- 其他 TopBar 入口在设置打开时先按明确策略关闭设置，再执行各自职责；不创建两个设置实例。

战斗卡牌输入采用一次主指针手势的显式状态：

- 左键 Press 开始；拖拽达到阈值后由战斗输入负责人持有直到 Release/Cancel。
- Release 无论发生在卡牌热区、敌方目标或空白处，都只结算一次。
- 右键取消会终止本次左键手势，并吞掉该手势随后到来的左键 Release；只有新的左键 Press 才可开始下一次交互。
- 敌方目标判定读取实际目标控件的全局矩形或明确装配的目标区，不按敌人名称、关卡或固定屏幕坐标分支。

## 阶段与允许范围

### 阶段 A：UI/UX 契约修订

允许修改：

- `洪荒之后-main/docs/06-UI交互/10-全局音量与设置页规格.md`
- 必要时最小修订 `洪荒之后-main/docs/06-UI交互/01-MVP界面交互.md` 的覆盖层验收段

目标：将“地图/套牌/设置一律互斥”的旧口径修订为设置全局模态的明确压栈/恢复契约，给出语义层级表和输入规则。不得修改代码或扩展为通用 UI 框架设计。

### 阶段 B：开发实现

建议允许修改：

- `洪荒之后-code/scripts/UI/OverlayCoordinator.cs`
- `洪荒之后-code/scripts/Core/OverlayCoordinatorSelfCheck.cs`
- `洪荒之后-code/scripts/UI/SettingsHelper.cs`
- `洪荒之后-code/scripts/UI/SettingsDialogController.cs`
- `洪荒之后-code/scenes/Settings/SettingsDialog.tscn`
- `洪荒之后-code/scripts/UI/BattleController.cs`
- 必要的一个聚焦卡牌指针状态自检文件
- `洪荒之后-code/docs/开发记录-code.md`

只有证据证明现有接口无法满足验收时，才可申请增加 `MapOverlayController.cs` 或 `NodePageNavigationCoordinator.cs`；未升级前只读。

## 禁止范围

- 不修改 `GameManager`、RunState、卡牌效果/费用/目标策略定义、敌人 Definition、遭遇池、奖励与节点结算。
- 不重构全部 UI、不引入事件总线、新插件、新 Autoload 或通用窗口框架。
- 不用散落魔法 ZIndex、固定敌人名/关卡/内容 ID、`Reparent`、`MoveChild` 解决问题。
- 不修改音量/分辨率持久化、标题 BGM/视频、美术资产。
- 不启动自动 QA、窗口自动化、轮询或多轮返工。

## 验收标准

### 覆盖层

1. 地图打开后点击设置：地图实例不关闭、不重建，设置显示在地图之上；关闭设置后地图滚动位置、只读/可交互状态和节点页均保持。
2. 胜利面板、CardReward 或地图存在时打开设置：设置面板与遮罩位于当前内容区最上层，下面任何控件不可点击；TopBar 仍可见可用。
3. 右上角、`Esc`、再次点击设置入口关闭同一设置实例，不提交奖励、不关闭地图、不切场景。
4. 层级规则只在协调职责中集中定义，并有自检反证设置高于胜利/CardReward/地图且不会销毁下层实例。

### 战斗卡牌

1. 任意敌人 Definition 使用同一战斗入口时，敌方目标牌拖到实际敌人目标区并释放只结算一次；扣费、伤害、日志和手牌刷新正常。
2. 敌方目标牌在非法区域释放会回位，不扣费、不造成效果。
3. 左键拖拽过程中按右键：立即取消回位；随后松开原左键不重新选中、不吸附、不扣费、不结算。
4. 取消后进行一次新的左键 Press，卡牌可正常再次选择/拖拽。
5. 点击选中再点敌人、默认自身/无目标牌现有交互、箭头表现和胜利流程不回归。

## 最低验证

- `dotnet build .\AfterHongHuang.csproj`
- Godot 4.7 Mono editor/player headless 与 Battle/Settings 场景解析
- `OverlayCoordinatorSelfCheck` 增补设置压栈、保留地图/胜利/CardReward 实例及层级断言
- 聚焦卡牌输入自检覆盖：合法敌方 Release、非法 Release、右键取消后吞掉旧左键 Release、新 Press 可恢复
- 硬编码扫描：触及的 `scripts/UI/`、`scripts/Core/` 不新增具体敌人名、卡牌名、内容 ID、固定关卡分支或散落层级魔法值
- 目标 `git diff --check`
- 一轮开发自检失败只允许一轮定向修正；仍失败则停止上报

## 用户窗口复测

- 地图 → 设置 → 关闭设置，确认仍是同一地图状态。
- 胜利面板/卡牌奖励 → 设置，确认设置可见且下层不可点击。
- 后续关卡使用攻击牌：拖到敌人并释放可命中。
- 左拖中右键取消，再松左键不回吸；下一次新点击可正常交互。

## 最终交付

- UI/UX 已修订设置覆盖层和战斗卡牌手势契约，移除“三类 overlay 一律互斥”的过期口径。
- `OverlayCoordinator` 新增集中 `GlobalSettings` 语义层和专用关闭负责人；设置打开不销毁地图、套牌、胜利或 CardReward。
- `BattleController` 持有完整主指针手势；敌方合法/非法 Release 不依赖卡牌热区，目标区读取实际敌人控件矩形；右键取消吞掉旧左键 Release。
- PM 回收发现已有地图分支绕过设置关闭后，唯一一轮定向返工已修正 `NodePageNavigationCoordinator.TryToggleMap` 的真实调用顺序。
- `dotnet build` 0 警告/错误；Godot 4.7 Mono editor/player headless、`OverlayCoordinatorSelfCheck`、`CardPointerGestureSelfCheck`、`NodePageNavigationSelfCheck` 与 `git diff --check` 均通过。
- 未启动 QA；未进行真实窗口鼠标操作，最终状态为开发完成、待用户窗口验收。

## 2026-07-30 用户窗口验收返工

本节及其后续交付覆盖上文与“设置期间 TopBar 可交互、再次点击设置关闭”相冲突的旧验收口径。

### 用户反馈与批准范围

用户窗口实测否决了上一轮交付，并明确批准在原任务内合并修复以下问题，不创建连续返工任务：

1. 设置打开后改为全屏独占模态；TopBar 可见但不可交互，仅设置面板、右上角关闭与 `Esc` 接收输入。
2. 地图在设置上层存在时仍可拖动。不得在现有全局 `_Input` 上新增“设置是否打开”的特殊判定；必须把地图滚轮/拖拽迁移到地图视口或专用交互控件的局部 GUI 输入边界。
3. 游戏内设置新增“返回主菜单”。当前没有完整 RunState 持久化/恢复系统，本批不伪造存档；返回主菜单按明确放弃本局处理，清理当前节点/战斗临时状态后进入标题页。标题页自己的设置不显示该入口。
4. 战斗中地图仍把道韵起点显示为当前位置。保留 `CurrentMapNodeId` 的已结算路线位置语义，地图表现改为优先消费 `ActiveNode` / `NodeLifecycleState.Active` 显示正在进行的节点。
5. 敌方目标牌、自身牌和无目标牌均无法打出。实机日志证明提交路径已到达 `PlayCard`，但冻结执行计划被错误判定失效。

### 已确认根因

- 设置遮罩仍按 TopBar 高度设置 `ContentTopInset`，因此没有覆盖整个屏幕。
- 地图滚轮/拖拽位于 `MapOverlayController._Input`，绕过 Godot GUI 命中与设置遮罩；普通按钮走 GUI 路径，所以出现“按钮点不到但地图仍能拖”的不一致。
- `RunState` 只有当前进程内状态，没有正式存档序列化、版本兼容和恢复入口。
- `MapRenderer` 使用 `CurrentMapNodeId` 判定 `isCurrent`；战斗节点在结果提交前按设计不会推进该字段，虽然 `ActiveNode` 和 `NodeStates[nodeId]=Active` 已正确存在。
- `GameManager.PlayCard(card, resolved)` 在校验冻结状态指纹前先扣除 `PlayerLingli`，而指纹包含灵力值，导致所有非零费计划必然被判为旧计划。用户实机日志连续记录“出牌解析结果已失效，拒绝执行旧计划”。

### 修订后的架构约束

- `OverlayCoordinator` 只管理覆盖层语义归属、叠放与生命周期，不要求地图在每个输入事件中查询设置状态。
- 设置全屏 `ContentScrim` 通过 GUI 命中规则自然独占输入。
- 地图视口拥有自己的 Press / Motion / Release / Wheel 手势；拖拽跨出视口时保持本次指针所有权，Release、关闭、退树或失去所有权时必须清理状态。
- 节点短按与地图拖拽继续由阈值区分；拖拽后抑制节点点击，不允许重复消费事件。
- `CurrentMapNodeId` 不因进入战斗而提前推进；活动节点视觉由现有活动生命周期事实派生。
- 卡牌冻结计划必须先验证，再在同一个可回滚事务内扣费与执行；真正陈旧的计划仍必须被拒绝。

### 串行执行范围

阶段 A 由 UI/UX 部只修改：

- `洪荒之后-main/docs/06-UI交互/10-全局音量与设置页规格.md`
- `洪荒之后-main/docs/06-UI交互/01-MVP界面交互.md` 中直接相关覆盖层、地图输入和返回主菜单口径
- 本任务卡的 UI/UX 交付摘要

阶段 B 在 UI/UX 回收后由开发部实现。建议范围：

- `洪荒之后-code/scenes/Settings/SettingsDialog.tscn`
- `洪荒之后-code/scripts/UI/SettingsDialogController.cs`
- `洪荒之后-code/scripts/UI/SettingsHelper.cs`
- `洪荒之后-code/scripts/UI/OverlayCoordinator.cs`
- `洪荒之后-code/scripts/UI/MapOverlayController.cs`
- `洪荒之后-code/scripts/UI/MapRenderer.cs`
- `洪荒之后-code/scripts/UI/BattleController.cs`
- `洪荒之后-code/scripts/Core/GameManager.cs`
- 与上述根因直接相关的聚焦自检
- `洪荒之后-code/docs/开发记录-code.md`

只有证明当前页面调用接口无法表达“标题页隐藏、游戏内显示返回主菜单”时，才允许最小修改相关页面调用点；不得建立新 Autoload、事件总线、通用窗口框架、正式存档系统或大规模 GameManager 重构。

### 本轮最低验收

1. 设置覆盖整个视口，TopBar 和所有下层覆盖层均不可交互；关闭设置后恢复同一地图/胜利/奖励实例及地图滚动位置。
2. `MapOverlayController` 不再以全局 `_Input` 处理地图滚轮/拖拽；在节点按钮、空白地图和视口边缘均能正确区分点击与拖拽，设置出现时地图完全停止响应。
3. 游戏内设置显示“返回主菜单”，标题设置不显示；点击后不保存、不提交奖励、不推进节点，清理当前运行上下文并进入标题页。
4. 进入第一场及后续战斗时，地图标记实际 `ActiveNode`；起点仅表示已结算路线位置，不再冒充当前战斗节点。
5. 至少用非零费敌方目标牌、非零费自身/无目标牌完成真实 `TryPrepareCardExecution -> PlayCard` 自检，验证费用与效果各结算一次；旧快照仍被拒绝。
6. `dotnet build .\AfterHongHuang.csproj`、Godot 4.7 Mono editor/player headless、Battle/Settings/Map 场景解析、相关聚焦自检、硬编码扫描和 `git diff --check` 通过。
7. 同一返工批次只允许一轮自动定向修正；未启动 QA，最终仍由用户进行窗口验收。

### 2026-07-30 开发交付

- 设置遮罩已覆盖完整 viewport；游戏内新增“返回主菜单”，标题页设置隐藏该入口。放弃本局由 `GameManager.TryAbandonRunToTitle` 集中清理当前运行上下文并进入标题页，不提交节点结果、奖励或路线。
- `MapOverlayController` 已移除地图滚轮/拖拽的全局 `_Input`，改由地图局部交互区域与节点控件共同接收 `GuiInput`；地图手势在释放、离开视口、关闭与退树时清理。
- `MapRenderer` 的当前节点表现优先读取 `ActiveNode` 与 `NodeLifecycleState.Active`，未改变 `CurrentMapNodeId` 的已结算路线语义。
- `GameManager.PlayCard` 已改为在扣除灵力前验证冻结执行计划，并在同一可回滚事务中扣费与执行。聚焦自检覆盖非零费敌方、自身、无目标牌，以及真正陈旧计划的零副作用拒绝。
- PM 复核当前差异范围与关键调用顺序后，复跑 `dotnet build .\AfterHongHuang.csproj -p:NuGetAudit=false`：0 警告、0 错误；开发部报告 Godot 4.7 Mono editor/player headless、相关 SelfCheck 与 `git diff --check` 均通过。
- 未启动 QA，未完成真实窗口鼠标与视觉验证。当前状态为开发完成，待用户窗口验收。

### 2026-07-30 用户窗口验收

- 地图上打开设置后，地图实例与滚动状态保留，设置覆盖完整屏幕，TopBar 与下层地图均不可交互；关闭后恢复正常。
- 胜利/CardReward 上的设置层级与下层输入阻断通过。
- 战斗期间地图正确标记实际活动节点，不再把道韵起点显示为当前战斗位置。
- 非零费卡牌及敌方、自身、无目标交互路径均可正常出牌；右键取消后的旧左键释放不再重新吸附或提交卡牌。
- 游戏内返回主菜单、标题页入口显隐和新开局状态清理通过。
- 用户明确回复“通过”。本任务关闭为 Done；未启动 QA。
