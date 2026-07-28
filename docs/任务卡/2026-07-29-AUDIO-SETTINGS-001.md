# AUDIO-SETTINGS-001 全局音量分层与设置页改版

## 背景与现象

用户在试听标题页 BGM 后确认：

- 默认听感偏重；
- 设置页缺少音量控制；
- 需要“总音量 / 音乐 / 音效”三级设置；
- 标题页音乐必须同时受“音乐”和“总音量”控制；
- 设置页中的“返回标题界面”与底部内建“关闭”按钮多余；
- 现有设置页需要整理为更像正式游戏的共享设置界面。

当前标题 BGM 源 WAV 与运行时 OGG 的综合响度均约为 `-13.8 LUFS`，未做离线响度归一化；标题播放器当前另有 `-6 dB` 场景级混音修正。现有工程只有 `Master` bus，没有 `Music` / `SFX` 分层，也没有音频设置服务。

## 已批准方案

1. 使用 Godot 音频总线建立 `Music -> Master`、`SFX -> Master` 的层级。
2. 标题 BGM 继续使用标题场景内的局部播放器，但改走 `Music` bus；不建立全局常驻 BGM 播放器。
3. 新增职责单一、应用级生命周期的 `AudioSettingsService` Autoload，只负责音频设置加载、应用、保存和变更通知；不得扩充现有 `GameManager`。
4. 总音量控制 `Master`，音乐控制 `Music`，音效控制 `SFX`；各级线性音量按总线串联相乘，静音语义同样按层级生效。
5. 默认值为：总音量 `80%`、音乐 `65%`、音效 `80%`。保留标题播放器当前 `-6 dB` 的单曲混音修正，本任务不重制母带。
6. 音量设置即时生效并持久化到现有 `user://afterhonghuang-settings.cfg` 的独立 `audio` section。
7. 设置页保留显示/分辨率能力；删除“返回标题界面”和底部 `AcceptDialog` 内建“关闭”按钮，使用右上角关闭入口与 `Esc` 关闭。
8. 用户未要求删除“退出游戏”，本任务暂时保留该功能；UI/UX 可调整其位置和层级，但不得改变行为。

## 分工与阶段门

### 阶段 A：UI/UX 部

只提交一次可实施规格，不修改 `洪荒之后-code/`：

- 设置页信息架构、尺寸、布局、间距和状态说明；
- “声音 / 显示”两个分组或页签的明确选择；
- 总音量、音乐、音效三行控件的标签、滑块、百分比、静音和键鼠交互；
- 右上角关闭、`Esc`、重复点击设置入口的关闭语义；
- “退出游戏”的建议位置；
- 1920×1080 基准及 1280×720 下的可读性约束；
- 水墨厚涂、洪荒废墟、沉郁色调下可由现有 Godot Control/Theme 实现的视觉规格；
- 明确哪些现有设置节点/职责需要保留、删除或迁移。

完成后只向总项目经理提交最终规格与风险，不联系开发部，不启动实现。

### 阶段 B：开发部

只在总项目经理回收并下发阶段 A 规格后启动。允许的建议范围：

- `洪荒之后-code/default_bus_layout.tres`
- `洪荒之后-code/project.godot`
- 新增职责单一的音频设置服务脚本及必要聚焦自检
- `洪荒之后-code/scenes/Settings/SettingsDialog.tscn`
- `洪荒之后-code/scripts/UI/SettingsDialogController.cs`
- `洪荒之后-code/scripts/UI/SettingsHelper.cs`
- `洪荒之后-code/scripts/UI/ResolutionSettings.cs`
- `洪荒之后-code/scenes/Title/Title.tscn`
- `洪荒之后-code/docs/开发记录-code.md` 一次合并式记录

如果实际需要修改上述范围之外的实现文件，必须先升级给总项目经理。

## 目标行为与验收标准

1. 标题 BGM 的实际输出同时受到标题播放器单曲修正、`Music` bus 和 `Master` bus 控制。
2. 调低或静音音乐不影响 SFX；调低或静音总音量同时影响音乐和 SFX。
3. 音量滑块支持 `0%` 到 `100%`，使用线性 UI 值交给 Godot 的线性/分贝换算；`0%` 必须可靠静音，不使用不可表示的负无穷分贝。
4. 首次运行使用 `80% / 65% / 80%`；修改后立即生效，关闭设置、切换场景和重启游戏后保持。
5. 音频保存失败、配置类型错误、缺少必要 bus 或 Autoload 初始化失败时显式记录错误，不静默回退为假成功。
6. 音频与显示设置共用 `user://afterhonghuang-settings.cfg` 时，任何一方保存都必须先加载并保留另一 section；不得因修改分辨率覆盖音频，或因修改音量覆盖显示设置。
7. 设置页没有“返回标题界面”和底部内建“关闭”；右上角关闭、`Esc` 与再次点击设置入口均能关闭同一共享实例。
8. 保留分辨率选择及真实应用/错误提示；保留“退出游戏”既有行为。
9. 设置页使用正式 `.tscn` / Theme / 可复用控件装配，不在控制器内新增大型动态 UI 或散落长期样式常量。
10. 标题视频、BGM 文件内容、开始游戏、地图、战斗和其他玩法流程保持不变。

## 禁止范围

- 不把音频设置塞进 `GameManager`，不建立万能 Manager。
- 不建立全局常驻 BGM 播放器、音乐状态机或跨场景自动续播系统。
- 不重制、裁剪、EQ、压缩、限制或重新归一化用户批准的 WAV/OGG。
- 不为尚不存在的对白、环境音或多语言音频提前增加总线和 UI。
- 不重做分辨率系统，不新增第三方插件、框架或生产工具。
- 不修改玩法、地图、战斗、存档、标题视频或美术源资产。
- 不启动 QA、自动化、轮询或窗口代操作。
- 不丢弃、覆盖或整理三个脏工作树中的非本任务改动。

## 必读资料与建议文件

- 根目录 `AGENTS.md`
- 本任务卡
- `洪荒之后-main/docs/06-UI交互/00-UI交互总览.md`
- `洪荒之后-main/docs/06-UI交互/01-MVP界面交互.md` 中设置相关段落
- `洪荒之后-main/docs/04-美术音频/00-美术音频总览.md`
- `洪荒之后-main/docs/04-美术音频/01-视觉风格指南.md`
- `洪荒之后-code/scenes/Settings/SettingsDialog.tscn`
- `洪荒之后-code/scripts/UI/SettingsDialogController.cs`
- `洪荒之后-code/scripts/UI/SettingsHelper.cs`
- `洪荒之后-code/scripts/UI/ResolutionSettings.cs`
- `洪荒之后-code/scenes/Title/Title.tscn`
- `洪荒之后-code/project.godot`

技术依据：

- Godot Audio buses：`https://docs.godotengine.org/en/stable/tutorials/audio/audio_buses.html`
- Godot ConfigFile：`https://docs.godotengine.org/en/stable/classes/class_configfile.html`

## 最低验证

开发部只执行一次与本任务匹配的验证批次：

1. `dotnet build .\AfterHongHuang.csproj`
2. Godot 4.7 Mono editor/player headless 导入与设置/标题场景解析。
3. 聚焦自检：
   - `Music -> Master`、`SFX -> Master` 路由；
   - 标题 BGM 指向 `Music`；
   - 三个默认值、修改、静音、保存与重载；
   - 音频/显示 section 互不覆盖；
   - 设置共享实例、右上角关闭、`Esc`、移除两个指定按钮；
   - 分辨率既有行为未被破坏。
4. 对目标文件执行 `git diff --check` 与必要的硬编码扫描；非本任务既有改动不得计为本任务结论。

开发自检通过后的状态为“开发完成，待用户窗口验收”，不得声称 QA 通过。

## 文档要求

- UI/UX 部只更新一处直接相关 UI 规范，或在无法安全写入脏文档时仅向总项目经理回报可粘贴规格。
- 开发部只在 `洪荒之后-code/docs/开发记录-code.md` 追加一次真实交付记录。
- 总项目经理只在阶段 A 回收、阶段 B 正式下发和最终交付回收时更新任务卡/任务看板。
- 不更新项目上下文快照、风险记录或验收清单。

## 升级条件

遇到以下情况立即停止并报告：

- 工具无法使用 `gpt-5.6-luna`、推理 `max`。
- 音频 bus、Autoload 或设置场景目标包含无法安全区分的其他未提交改动。
- 必须修改 `GameManager`、新增第二套设置系统或扩大为全局音乐状态机。
- 需要重制母带、增加实时压缩/限制器或改变用户批准的音频内容。
- 一轮自我返工后仍无法通过最低验证。

## 2026-07-29 用户窗口返工记录

用户在真实窗口截图与操作中确认以下问题，并明确要求修复：

1. 三条音量行都显示为“音量”，拖动任意一条时三条一起变化。
2. 点击任意“静音”时三条一起静音。
3. 静音控件应改成“静音”文字后跟一个小方框，静音时方框打勾。
4. 删除正常状态下无意义的“声音设置已就绪/已应用”文案；错误状态仍须显式显示。
5. 删除设置页“退出游戏”按钮及对应事件/流程。
6. 标题页打开设置时遮罩顶部留有约 44px 缝隙；标题页必须全屏遮罩。带 TopBar 的游戏页面仍需按项目不变量保留 TopBar 可见、可交互，因此遮罩顶部边界必须依据实际宿主是否存在 TopBar 动态决定，不得在共享场景中固定留缝。

截图证据：`C:\Users\ASUS\AppData\Local\Temp\codex-clipboard-7fe27ed6-c272-499b-b7cd-6a60b16cd65f.png`。

### 本轮允许修改

- `洪荒之后-code/scenes/Settings/SettingsDialog.tscn`
- `洪荒之后-code/scenes/Settings/VolumeRow.tscn`
- `洪荒之后-code/scripts/UI/SettingsDialogController.cs`
- `洪荒之后-code/scripts/UI/VolumeRowController.cs`
- `洪荒之后-code/scripts/UI/SettingsHelper.cs`
- `洪荒之后-code/docs/开发记录-code.md` 中本任务记录的最小追加

### 本轮验收标准

- 三个复用行明确绑定且运行时校验为互不重复的 `Master`、`Music`、`Sfx`，标签分别为“总音量”“音乐”“音效”；缺少或重复绑定必须显式失败。
- 调整任一行只改变对应百分比和对应 bus，另外两行的百分比、静音状态和 bus 不变。
- 勾选任一静音框只静音对应 bus；方框位于“静音”文字之后，选中时显示勾选状态。
- 正常状态不显示声音就绪/应用成功占位文案；服务或保存失败时仍显示可读错误。
- 设置场景、控制器和 `SettingsHelper` 中不再存在退出游戏按钮、事件或退出调用。
- 标题页遮罩覆盖到窗口顶部；带 TopBar 的页面只为实际 TopBar 高度保留输入区域，下面内容不可穿透；不得修改 TopBar 或各页面控制器。
- 右上角、`Esc`、TopBar 设置入口 toggle、分辨率与音频持久化既有行为保持。

本轮是用户窗口反馈后的一个小范围修复批次；不启动 QA，不修改音频服务、总线、标题 BGM、玩法或其他页面。
