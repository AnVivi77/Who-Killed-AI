# 12 · Vuforia 素材交付包与后续维护

核对日期：2026-09-14

> 你消息中的“Euphoria”按上下文理解为 **Vuforia**。本项目使用的是 Vuforia Engine for Unity 的 **Image Target + Device Database**，不是 Vuforia Studio、Area Target 或 Model Target。

## 一、我已经制作的交付物

在项目根目录运行 `Tools/PackageVuforiaHandoff.ps1` 后，会生成两个压缩包：

```text
Deliverables/Vuforia_CaseTargets_YYYYMMDD.zip
Deliverables/ExhibitionAssetHandoff_YYYYMMDD.zip
```

本次交付对应 20260914：

- `Vuforia_CaseTargets_20260914.zip`：只包含可以上传到 Vuforia Target Manager 的 11 张目标图、目标命名/ID 映射、校验信息和上传说明。
- `ExhibitionAssetHandoff_20260914.zip`：包含当前所有已登记的视频、档案底图、人物图、线索图、配置源，以及 inbox 中发现但尚未绑定到配置的额外人物图，便于后续继续制作。
- `PackageSummary_20260914.json`：记录本次打包数量、Unity 版本、缺失源文件和数据库状态。
- 本次真实生成结果：Vuforia 包 15 个条目、7,153,000 bytes；完整交接包 56 个条目、81,535,539 bytes；manifest 源文件缺失数为 0。脚本解析和压缩包内容检查均已完成。

脚本不会把真实许可 key 写入包，也不会伪造 `CaseTargets.unitypackage`。Vuforia 数据库必须在你的 Vuforia Developer Portal 账号中生成，因此当前数据库状态仍是“待生成”。

## 二、给 Vuforia Target Manager 的包

### 目标数量和命名

上传包包含 10 条主线线索和 1 条 Detective 彩蛋线索：

| Vuforia target name | 项目 clue ID | 归属 |
| --- | --- | --- |
| `Clue_S01_Behavior` | `CL_S01_BEHAVIOR` | Cleaning Robot / Behavior |
| `Clue_S01_Information` | `CL_S01_INFORMATION` | Cleaning Robot / Information |
| `Clue_S02_Behavior` | `CL_S02_BEHAVIOR` | Engineer / Behavior |
| `Clue_S02_Information` | `CL_S02_INFORMATION` | Engineer / Information |
| `Clue_S03_Behavior` | `CL_S03_BEHAVIOR` | Psychologist / Behavior |
| `Clue_S03_Information` | `CL_S03_INFORMATION` | Psychologist / Information |
| `Clue_S04_Behavior` | `CL_S04_BEHAVIOR` | Repairman / Behavior |
| `Clue_S04_Information` | `CL_S04_INFORMATION` | Repairman / Information |
| `Clue_S05_Behavior` | `CL_S05_BEHAVIOR` | Secretary / Behavior |
| `Clue_S05_Information` | `CL_S05_INFORMATION` | Secretary / Information |
| `Detective_Information` | `CL_DETECTIVE_INFORMATION` | Detective 彩蛋 |

目标文件在包内的 `01_TargetImages/`。命名只使用英文、数字和下划线，避免中文姓名、数组顺序或原始随机文件名成为运行时进度主键。图片字节从当前源素材直接复制，没有为 Vuforia 上传包做裁剪、旋转或 AI 水印处理；Unity 档案显示层的裁剪只作用于显示，不改变扫描源。

### 上传前必须补的值

每张实体目标图必须测量真实打印宽度，并在 Target Manager 中按米填写。例如纸张实际宽度为 10 cm，应填 `0.10`，不是填屏幕宽度，也不是填图片像素宽度。

当前 11 张目标图的本地检查结果：

- 文件格式为 PNG 或 JPEG。
- 宽度均大于 320 px。
- 文件大小均未超过 2.25 MB。
- 当前目标图为 RGB 24-bit，无 alpha 通道。
- `PhysicalWidthMeters` 和 `PrintedWidthCm` 仍为空，因为项目中没有真实打印尺寸记录。

### 在官网创建数据库

1. 打开 [Vuforia Engine Developer Portal](https://developer.vuforia.com/)，注册或登录开发者账号。
2. 进入 Target Manager，创建 **Device Database**，数据库名使用 `CaseTargets`。
3. 逐张添加 `Vuforia_CaseTargets_20260914.zip` 中 `01_TargetImages/` 的 11 张图。
4. Target 类型选择 **Single Image / Image Target**；名称按上表使用；输入每张图的真实物理宽度。
5. 查看 Vuforia 给出的图像特征评级。若某张评级较低，先保留评级截图和目标名，不要擅自换图或改变 clue ID。
6. 全部目标接受后，下载数据库时选择 **Unity `.unitypackage`**，不要只下载给 Android/iOS 原生工程用的 ZIP。
7. 把下载的文件保存为：

   ```text
   SourceAssets/06_Vuforia/Database/CaseTargets.unitypackage
   ```

Vuforia 官方说明中，Device Database 下载为 Unity `.unitypackage` 后可从 Unity 的 `Assets > Import Package > Custom Package` 导入；数据库会包含运行所需的目标数据。数据库发生任何目标增删或尺寸修改后，都要重新下载并重新导入。

## 三、应该下载什么

### 必需

1. **Vuforia Engine for Unity**：从 [Vuforia Engine Package for Unity](https://developer.vuforia.com/library/vuforia-engine/unity-extension/vuforia-engine-package-unity/) 官方页面的 Developer Portal 下载 Unity 包，或使用 Unity Package Manager 的官方 Vuforia 包入口。
2. **Vuforia Developer Portal 账号**：Target Manager 和 License Manager 都需要账号；账号本身不是项目素材，不要把密码或 key 写入项目。
3. **Vuforia Device Database**：按上面的 11 张目标图生成并下载，最终文件为 `CaseTargets.unitypackage`。

### 当前工程的版本策略

现有工程已经固定为 **Unity 2022.3.62f3c1**，不要为了装最新 Vuforia 自动升级整个工程。请选择 Vuforia 包页面/发行说明中明确支持 Unity 2022.3 LTS 的版本，并记录实际安装版本。

官方发行说明显示，较新的 Vuforia Engine 版本可能把最低 Unity 版本提高到 Unity 6 LTS；因此不要看到“latest”就直接导入。如果下载页只提供要求 Unity 6 的包，应先停在这里，由项目负责人决定“保留 Unity 2022.3 并获取兼容的 10.x 包”，还是另开 Unity 6 升级分支。当前代码没有对未知 Vuforia API 做假引用。

### 不要下载/不需要

- 不需要 Vuforia Studio 来生成这 11 个 Image Target。
- 不需要 Area Target Generator 或 Model Target Generator。
- 不需要把视频、档案底图、人物图上传到 Target Manager；Vuforia 只需要用于识别的目标图。
- 不要把 License Key 放进 `case.json`、`assets.manifest.json`、压缩包或 Git。

如果最终设备是 Android/iOS，还需要根据设备安装 Unity 对应的 Android/iOS Build Support；如果最终设备是 Windows/UWP，还必须按 Vuforia 当前支持矩阵和现场摄像头重新确认。现在没有最终设备型号，因此本项目不把某个发布平台标为已通过。

## 四、导入数据库后我会如何接回工程

收到以下三项后即可继续完成真实 Vuforia 接入：

1. Vuforia Engine for Unity 的实际包文件和版本号。
2. `CaseTargets.unitypackage`。
3. 11 张目标的真实打印宽度，以及最终运行设备、操作系统和摄像头信息。

届时接入顺序为：

1. 导入 Vuforia Engine 包，确认实际程序集名称和 API。
2. 导入 `CaseTargets.unitypackage`，确认数据库文件进入项目的 Vuforia/StreamingAssets 位置。
3. 配置 ARCamera 和本地 License Key；key 只写入 Unity 的 Vuforia Configuration 或安全注入位置。
4. 为 11 个 Image Target 绑定稳定 `clueId`：目标名只做数据库校验，进度仍以 `CL_Sxx_BEHAVIOR` / `CL_Sxx_INFORMATION` 为主键。
5. 把 `case.json` 中各线索的 `physicalWidthMeters` 更新为真实测量值，运行 `Tools > Exhibition > Setup Demo` 和 `Tools > Exhibition > Validate Case`。
6. 逐张实体扫描验证直接追踪、防抖、丢失后不撤销发现、重复扫描不重复计数。
7. 用完整 5 人流程验证最后一个事件视频自然结束后才进入 ending；Detective Information 在 ending 开始后不再播放。

## 五、当前工程的实现方式

- 根 UI 画布固定为 `3840 × 1152`。
- 开场使用黑底，只播放 `opening`；opening 完成后 `background.mp4` 循环并持续作为背景。
- 非背景视频通过 Canvas 上的播放器区域显示，按源视频宽高比完整适配，不做非等比拉伸；不能填满的区域保留黑边。
- 档案底图是完整 16:9 artwork，使用中央 aspect-fitted stage；人物名、简介、进度、Behavior 和 Information 文字以归一化坐标覆盖在相应白卡/红色条上。
- 每人固定两条线索，发现状态使用稳定 ID 并只更新一次；重复扫描允许重新查看档案，但不会重复入队或自动重播事件。
- 只发现一条线索时，另一栏隐藏正文、标题和图片，仅显示 `? NOT FOUND`。
- 两条线索齐全后先展示完整档案，再按配置延迟约 5 秒自动播放事件视频；当前 `case.json` 中 `dossierAutoPlayDelaySeconds` 为 5。
- Detective 不计入主线人数；直接发现 `CL_DETECTIVE_INFORMATION` 播放 `video_detective_egg`，ending 已开始后不再播放。
- 未安装 Vuforia 时使用 Mock 按钮也能编译和运行；真正 Vuforia 回调需在安装的 SDK API 确认后接入。

主要工程文件：

```text
UnityProject/Assets/ExhibitionCase/Scripts/Core/ExhibitionApp.cs
UnityProject/Assets/ExhibitionCase/Scripts/Core/CaseProgressService.cs
UnityProject/Assets/ExhibitionCase/Scripts/UI/ExhibitionRuntimeUI.cs
UnityProject/Assets/ExhibitionCase/Scripts/UI/DossierLayoutData.cs
UnityProject/Assets/ExhibitionCase/Scripts/Scanning/ScanningAdapters.cs
UnityProject/Assets/ExhibitionCase/Editor/ExhibitionSetupTool.cs
UnityProject/Assets/ExhibitionCase/Editor/DossierCanvasPreviewEditor.cs
UnityProject/Assets/ExhibitionCase/Resources/DossierLayout.asset
content/case.json
content/assets.manifest.json
```

## 六、后续修改素材、文字和排版

### 替换视频、档案图或线索图

1. 保留原来的稳定 ID，例如 `video_s03` 或 `clue_s03_information`。
2. 把新文件放入 `SourceAssets/00_Inbox_待整理/`，在 `content/assets.manifest.json` 修改对应 `sourcePath`；`suggestedPath` 只负责规范化交付文件名。
3. 运行：

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\Tools\PackageVuforiaHandoff.ps1
   ```

4. 在 Unity 执行 `Tools > Exhibition > Setup Demo`，再执行 `Tools > Exhibition > Validate Case`。
5. 如果改变的是目标图，必须在 Target Manager 删除/替换相应目标并重新下载 `CaseTargets.unitypackage`；不要只替换 Unity 文件就认为 Vuforia 数据库已更新。

### 修改英文文字

编辑 `content/case.json` 中的 `title`、`profile`、clue `title` 和 `body`。不要把 `CaseConfig.asset` 当作内容源直接改，否则下次导入会被源 JSON 覆盖。修改后运行 Setup Demo 和 Validate Case，再在 Mock 流程中检查文字。

### 调整档案排版

- 档案文字和线索卡布局保存在 `UnityProject/Assets/ExhibitionCase/Resources/DossierLayout.asset`。
- 坐标是相对于 16:9 档案 stage 的归一化坐标，原点为左下角；不要用 3840×1152 的根画布坐标直接填写。
- `DossierTextLayout` 控制人物名、简介、进度；`DossierClueLayout` 控制线索图、卡片旋转、标题正文区域、字号、行距和 `uvRect` 裁剪。
- 在 Unity 中用 `Tools > Exhibition > Build Dossier Canvas Preview`，然后在 `04_DossierPreview` Scene View 切换 S01–S05 检查。这个预览不需要进入 Play Mode。
- 需要裁掉图片右下角水印时只调整对应线索的 `uvRect`，不要修改源文件；扫描目标图仍应使用未经裁剪的原图，除非你决定重新制作实体印刷图并重新训练数据库。

### 增加人物

1. 在 `case.json` 的 `suspects` 增加唯一 ID，例如 `S06`，并填写 displayName、profile、dossier、portrait、event video。
2. 增加恰好两条 clue，ID 例如 `CL_S06_BEHAVIOR` 和 `CL_S06_INFORMATION`，分别使用 slot 1/2；不要复制数组顺序作为主键。
3. 在 `assets.manifest.json` 增加新素材映射，并保证所有 `sourcePath` 真实存在。
4. 在 `DossierLayout.asset` 为新人物补 `DossierTextLayout`；没有特殊设计时可以从默认布局开始，再在 Canvas Preview 中微调。
5. 重新运行打包脚本、Setup Demo 和 Validate Case。
6. 在 Target Manager 新增两张 Image Target，名称和 `targetName` 一致，填写实际打印宽度，重新下载数据库。
7. 重新执行完整 Mock 流程，再做实体扫描和最终设备验收。人数和进度显示会从配置读取，不需要改死代码中的人数。

### 删除人物或减少线索

先从 `case.json` 移除人物及其两条线索，再从 manifest 移除不再使用的绑定；重新运行校验和打包。然后在 Target Manager 中移除对应目标并重新下载数据库。不要直接删除原始 inbox 文件，确认交付包和历史版本无误后再由项目负责人清理。

## 七、当前真实验证和未完成项

已实际验证：

- Unity 2022.3.62f3c1 工程可打开。
- Setup Demo、Validate Case、编辑器 Canvas 档案预览已执行。
- 11 张目标图已做本地格式/尺寸/大小/颜色模式检查。
- Mock 流程和编辑器排版有历史真实记录；五份档案已在 Scene View 检查。

尚未验证、不能标为通过：

- Vuforia Engine 包导入。
- Target Manager 数据库生成、目标评级和 `.unitypackage` 导入。
- License Key、摄像头、实体打印尺寸和真实追踪。
- 正式发布设备、完整五人事件自然结束后的 ending 回归。

这些状态会继续写入 `docs/08_当前状态与交接.md`，不会因为素材已压缩就被标成完成。

## 官方参考

- [Vuforia Engine Package for Unity](https://developer.vuforia.com/library/vuforia-engine/unity-extension/vuforia-engine-package-unity/)
- [Device Databases](https://developer.vuforia.com/library/vuforia-engine/images-and-objects/image-targets/device-databases/device-databases/)
- [Target Manager](https://developer.vuforia.com/library/vuforia-engine/getting-started/engine-developer-portal/vuforia-target-manager/)
- [Image Targets](https://developer.vuforia.com/library/vuforia-engine/images-and-objects/image-targets/image-targets/)
- [Supported Versions](https://developer.vuforia.com/library/vuforia-engine/platform-support/supported-versions/)
- [Vuforia Engine Release Notes](https://developer.vuforia.com/library/vuforia-engine/release-notes/vuforia-engine-release-notes/)
