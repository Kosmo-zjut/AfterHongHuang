# TITLE-RUNTIME-003 无音轨标题视频替换

## 背景

用户已确认当前标题画面内容可用，并批准使用以下无音轨版本替换游戏内标题视频：

- 用户批准源：`D:\用户目录\Pictures\comfyUI-Output\video\标题页成品-无音轨.mp4`
- 文件大小：39,394,426 bytes
- SHA-256：`AA9D902FE4D11CA023AEB42E4F72FF55BAAC82151E608A62A79B8FF47A21C498`
- 参数：H.264、3840×2160、16:9、24 fps、约 16.21 秒，无音频流

当前游戏仍使用由旧 32.38 秒 S2 转码得到的 `resources/video/title_background.ogv`，其中包含 Vorbis 音轨。本任务只替换视频，不接入新标题 BGM。

## 目标行为与验收标准

1. 美术正式母版 `洪荒之后-art/assets/ai_source/source/title/标题页s2.mp4` 与用户批准源文件完全一致。
2. `洪荒之后-code/resources/video/title_background.ogv` 由批准源转码而来，为 Theora、3840×2160、24 fps、约 16.21 秒，且不含任何音频流。
3. OGV 可由 FFmpeg 完整解码，Godot 可解析并播放。
4. 保持 `title_background.tres` 的循环配置和 `Title.tscn` 的自动播放、输入穿透及现有按钮行为。
5. 开始游戏、设置和退出功能不因视频替换发生变化。

## 分工与允许修改范围

### 美术部

- 允许覆盖：`洪荒之后-art/assets/ai_source/source/title/标题页s2.mp4`
- 只执行批准源文件的正式母版归档与哈希核对。
- 不修改 `洪荒之后-code/` 或其他美术资源。

### 开发部

- 允许覆盖：`洪荒之后-code/resources/video/title_background.ogv`
- 允许追加一次记录：`洪荒之后-code/docs/开发记录-code.md`
- 只有验证证明现有循环装配不兼容时，才可对 `resources/video/title_background.tres` 做最小修正；不得修改标题 UI 样式、设置逻辑或页面流程。
- 不修改 `洪荒之后-art/`。

两个部门分别直接读取同一个用户批准源，修改文件互不重叠，可以并行执行。

## 禁止范围

- 不接入或处理 `出发-on-the-way.wav`。
- 不修改 C#、标题 UI、设置、角色选择、玩法、地图或战斗。
- 不删除用户源文件或其他标题视频。
- 不引入插件、音频系统、Autoload 或新架构。
- 不启动 QA、自动化、轮询或窗口控制。

## 最低验证

### 美术部

- 核对正式母版大小为 39,394,426 bytes。
- 核对正式母版 SHA-256 为 `AA9D902FE4D11CA023AEB42E4F72FF55BAAC82151E608A62A79B8FF47A21C498`。

### 开发部

- 核对批准源确实无音频流。
- 转码时只映射视频流，不创建静音音轨。
- FFmpeg 完整解码新 OGV。
- `dotnet build .\AfterHongHuang.csproj`；若受用户目录 NuGet 配置权限影响，可使用此前已记录的隔离配置方式，并如实记录。
- Godot 4.7 Mono editor/player headless 解析。
- 静态检查 `Title.tscn`、`title_background.tres` 与 `TitleController.cs` 的既有装配未被无关修改。
- 对本任务目标文件执行差异检查。

## 文档要求

- 开发部只在 `洪荒之后-code/docs/开发记录-code.md` 追加一次真实交付记录。
- 总项目经理只在部门交付回收后更新一次任务看板状态。
- 不更新项目上下文快照、风险记录或验收清单。

## 升级条件

遇到以下情况立即停止并向总项目经理报告：

- 用户批准源无法完整解码或参数与本卡不一致。
- Godot 4.7 无法解析无音轨 OGV。
- 必须修改标题 UI、C#、设置或页面流程才能完成替换。
- 发现目标文件包含无法区分的其他未提交修改，覆盖会造成丢失。
- 工具无法使用 `gpt-5.6-luna`、`max` 执行部门任务。

## 2026-07-29 用户窗口返工

### 现象与最新事实源

用户窗口检查后确认，上一次批准的 `标题页成品-无音轨.mp4` 播放方向错误。最新批准源改为：

- `D:\用户目录\Pictures\comfyUI-Output\video\标题页已倒放-未去音轨.mp4`
- 大小：86,595,074 bytes
- SHA-256：`8861AD968AC9B07642B9F58A00C6A02C797967BB32B3B5DD69BF4B22CE3CA853`

本节覆盖本卡中旧批准源、16.21 秒时长及其哈希的当前效力；旧内容只保留为前次交付历史。

### 目标行为和验收

1. 使用 FFmpeg 视频流直拷生成 `D:\GameCode\洪荒之后\.tmp\title-background-reversed-noaudio.mp4`；不得重新编码视频，不得保留或创建任何音频流。该文件只作为本任务可追溯的中间派生文件，不进入 Git。
2. 完整验证派生 MP4 可解码，并记录其视频参数、时长、大小和 SHA-256。
3. `洪荒之后-code/resources/video/title_background.ogv` 必须仅包含 Theora 视频流，并由该无音轨派生 MP4 转码生成。
4. `洪荒之后-art/assets/ai_source/source/title/标题页s2.mp4` 最终必须与同一无音轨派生 MP4 完全一致。
5. 保持现有 `title_background.tres`、`Title.tscn`、`TitleController.cs`、独立标题 BGM及音量设置不变。
6. 开发自检通过后状态为“开发完成，待用户窗口验收”；用户需确认播放方向、完整循环、无视频内嵌声音及标题按钮。

### 串行分工

#### 开发部

- 先检查 `洪荒之后-code` 当前 status/diff，保护非本任务改动。
- 允许在工作区根 `.tmp` 创建上述无音轨派生 MP4；创建前须核实目标绝对路径位于 `D:\GameCode\洪荒之后\.tmp`，不得覆盖其他临时文件。
- 允许覆盖 `洪荒之后-code/resources/video/title_background.ogv`。
- 允许在 `洪荒之后-code/docs/开发记录-code.md` 追加一次交付记录。
- 仅当现有资源装配无法解析新视频时，才可最小修改 `resources/video/title_background.tres`；触及 C#、场景、BGM 或设置前必须升级。

#### 美术部

- 仅在开发部交付无音轨派生 MP4并给出哈希后开始。
- 允许覆盖 `洪荒之后-art/assets/ai_source/source/title/标题页s2.mp4`，并验证与派生 MP4 的大小、SHA-256 完全一致。
- 不修改代码工作树或其他美术资源。

### 最低验证

- FFprobe 证明原始批准源包含视频流，并核实其音频流现状。
- FFmpeg 以 `-map 0:v:0 -c:v copy -an` 或等价显式映射生成派生 MP4；FFprobe 证明派生文件只有视频流。
- 派生 MP4 与运行时 OGV 均执行完整解码。
- 运行 `dotnet build .\AfterHongHuang.csproj`。
- Godot 4.7 Mono editor/player headless 解析。
- 静态确认标题视频循环装配、标题 BGM Music 路由、按钮与设置未被本任务修改。
- 对本任务目标文件执行 `git diff --check`。
- 不启动 QA、窗口自动化或额外返工链。

### 阻塞处置

开发部首次执行时已确认源文件参数和完整解码均正常，但部门线程向用户图片目录写入派生 MP4 被系统拒绝，退出码 `-13`；未产生派生文件、未覆盖 OGV、未修改开发记录。用户随后明确要求继续，故仅将中间派生文件调整到可写的工作区根 `.tmp`，最终运行时资源、正式美术母版、无损去音轨要求和全部禁止范围不变。

### 最终交付

- 无音轨派生 MP4：86,065,138 bytes，SHA-256 `0514FAE87D4BC76C7EC7722C438731503BECEE1D923552F50374EF347B56024F`，仅含视频轨道。
- 正式美术母版与派生 MP4 逐字节一致。
- 正式运行时 OGV：Theora、3840×2160、24fps、32.38 秒、无音频，SHA-256 `AA4492CDD4C622354193C68831ECF1FB8D535D9B117F4B50850E93B9416194E7`。
- 派生 MP4/OGV 完整解码、`dotnet build`、Godot 4.7 Mono editor/player headless、标题循环/BGM/按钮/设置静态检查及 `git diff --check` 均通过。
- 未启动 QA；状态为开发与美术归档完成，待用户窗口验收。
