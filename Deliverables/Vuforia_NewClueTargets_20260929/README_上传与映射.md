# 新线索 Vuforia Target 上传与映射

整理日期：2026-09-29  
源目录：`SourceAssets/00_Inbox_待整理/素材合集/素材/素材/`  
图片目录：`01_TargetImages/`

## 先看结论

- 收到 11 张 JPG：5 位主线人物各 2 张，另有 Detective 信息彩蛋 1 张。按文件名和画面主题都能对应到当前人物；`secretary.jpg` 根据画面中的电话和原有 `Secretary_Infor.png` 归为 Secretary Information。`psychologis_behavior.jpg` 是拼写少了一个 `t`，输出文件名已修正。
- `01_TargetImages/` 中的文件是原图逐字节复制，仅改文件名；没有裁剪、旋转、修图、压缩或覆盖 Inbox 原图。
- 这些 JPG 是拍摄线索道具时连桌面/周边一起拍下来的照片。它们只有在最终要扫描的平面图案与整张 JPG 完全相同时，才能直接用作 Image Target。如果要识别照片里的实物，应先制作/拍摄实际会被摄像头看到的平面目标图；Vuforia Image Target 不会把桌面照片自动变成对瓶子、光盘、手表、电话或工具的 3D 识别目标。
- 目前没有 Vuforia Target Manager 星级结果，也没有实际摄像头测试。本目录代表已整理和命名，不代表这些候选图已达到稳定识别质量。

## 映射表

Target Manager 的 Name 请使用下面的 `targetName`，保持小写和下划线；Unity 进度继续使用 `clueId`。不要用中文人物名或文件数组顺序做 ID。

| 新文件名 | Vuforia Target Name | 当前 clue ID | 人物 / 线索槽 | 图片画面主题 | 人物归属判断 |
| --- | --- | --- | --- | --- | --- |
| `clue_s01_behavior.jpg` | `clue_s01_behavior` | `CL_S01_BEHAVIOR` | Cleaning Robot / Behavior | 稀释比例瓶 | 高；文件名和清洁用品主题一致 |
| `clue_s01_information.jpg` | `clue_s01_information` | `CL_S01_INFORMATION` | Cleaning Robot / Information | Cleaning Unit 操作手册 | 高；文件名和清洁机器人手册一致 |
| `clue_s02_behavior.jpg` | `clue_s02_behavior` | `CL_S02_BEHAVIOR` | Engineer / Behavior | 系统日志 / 删除记录纸张 | 高；工程系统日志主题 |
| `clue_s02_information.jpg` | `clue_s02_information` | `CL_S02_INFORMATION` | Engineer / Information | 光盘 | 高；与工程师线索主题一致 |
| `clue_s03_behavior.jpg` | `clue_s03_behavior` | `CL_S03_BEHAVIOR` | Psychologist / Behavior | Diagnostic Record / 心理诊断报告 | 高；与心理/诊断主题一致 |
| `clue_s03_information.jpg` | `clue_s03_information` | `CL_S03_INFORMATION` | Psychologist / Information | Psychiatric Division 档案 | 高；与心理档案主题一致 |
| `clue_s04_behavior.jpg` | `clue_s04_behavior` | `CL_S04_BEHAVIOR` | Repairman / Behavior | 棕色手工/工具状物件，照片中难以进一步确认 | 中；人物主题合理，但需负责人确认它是不是预期道具 |
| `clue_s04_information.jpg` | `clue_s04_information` | `CL_S04_INFORMATION` | Repairman / Information | 标有 ToolKit 的工具箱 | 高；与 Repairman 主题一致 |
| `clue_s05_behavior.jpg` | `clue_s05_behavior` | `CL_S05_BEHAVIOR` | Secretary / Behavior | 手表 | 高；与秘书线索主题一致 |
| `clue_s05_information.jpg` | `clue_s05_information` | `CL_S05_INFORMATION` | Secretary / Information | Philips 手机 | 高；与秘书线索主题一致 |
| `detective_information.jpg` | `detective_information` | `CL_DETECTIVE_INFORMATION` | Detective / Information Easter egg | Detective Card | 高；与现有 Detective 彩蛋映射一致 |

## 与当前英文正文的冲突，请先核定

本次只按新文件名和图片主题建立候选映射，没有改 `content/case.json`、`content/assets.manifest.json`、Unity 场景或现有数据库。发现以下现有文字与新图不一致：

1. Cleaning Robot：当前 `CL_S01_BEHAVIOR` 正文写的是 operating manual；新 Behavior 图是稀释比例瓶，而新 Information 图是操作手册。
2. Engineer：当前 `CL_S02_BEHAVIOR` 正文写的是难以修复的光盘；新 Behavior 图是系统日志纸张，新 Information 图是光盘。
3. Repairman：当前 `CL_S04_BEHAVIOR` 正文说是一张女儿照片；新 Behavior 图看起来是棕色物件，无法辨认为照片。
4. Secretary：当前 `CL_S05_BEHAVIOR` 正文写电话录音；新 Behavior 图是手表，新 Information 图是电话。

因此“人物大类归属”有图像证据支持，但 Behavior/Information 的叙事文字至少有几项可能需要交换或重写。请在正式接入前确认：保留上述文件名代表的 Behavior/Information 分配，还是以当前 `case.json` 的正文含义为准调整分配。没有收到确认前，不覆盖现有线索源配置。

## 图片信息与物理尺寸

图片尺寸来自实际文件读取：

- 9 张为 `1279 × 1706`；`detective_info.jpg`、`repairman_behavior.jpg` 为 `1706 × 1279`。
- 文件大小约 285 KB–522 KB，均为 JPG。
- 像素尺寸不等于实体宽度。Target Manager 的 Width 必须填写最终打印/展示目标的真实宽度，单位为米；例如测得 10 cm 才填写 `0.10`。
- `case.json` 里主线宽度目前是工程配置值，交接记录仍注明实体宽度未确认；不要把旧值当成实测结果。
- Detective 的 Image Target 宽度目前由 `VuforiaWebcamSetupTool.cs` 默认写为 `0.1 m`。如果 Detective 实际目标不是 10 cm，需要在 Unity 设置目标后同步修正这段默认值，否则再次运行 `Setup Vuforia Webcam` 会重新写入 0.1 m。

## 在 Vuforia Target Manager 中创建 / 更新

建议更新项目正在使用的 `CaseTargets` Device Database，避免创建新数据库后 Unity 场景仍加载旧库。

1. 登录 [Vuforia Developer Portal](https://developer.vuforia.com/) 并打开 Target Manager。
2. 打开现有 Device Database `CaseTargets`。如果已经有相同 `targetName`，先替换对应旧条目；不要在同一数据库中重复创建同名 target。
3. 对 `01_TargetImages/` 中的图片逐张添加 **Single Image / Image Target**。
4. `Name` 逐字使用映射表中的小写 `targetName`；图片文件名已与它一致，去掉 `.jpg` 后就是 Name。
5. `Width` 填最后实际印出的平面目标宽度（米），不是图片像素、相机画面宽度或整台道具的高度。
6. 查看每张图的特征星级。低星或 0 星图不要直接作为稳定展览目标；先改善图案/选择平面区域，再重新生成数据库。
7. 所有目标完成后，从 Target Manager 下载 Unity `.unitypackage`，数据库名保持 `CaseTargets`。
8. 下载 Unity `.unitypackage` 后先保留一个带日期的备份。最简单的方式是把包发给项目维护者，由维护者检查数据库名和 target 列表，再集成进当前工程。
9. 如果自行导入，在 Unity 选择 `Assets > Import Package > Custom Package...`，选中下载的 `.unitypackage` 并导入；检查 `Assets/StreamingAssets/Vuforia/` 中的数据库文件已更新。
10. 之后运行 `Tools > Exhibition > Repair Vuforia Generated Targets`、`Tools > Exhibition > Setup Vuforia Webcam`、`Tools > Exhibition > Validate Vuforia Setup`。如果图片变化，还要把新 target source 登记到 `assets.manifest.json` 并确认 `case.json` 的 `targetImageAssetId` 指向正确资源；如果档案继续显示旧设计图，保持 `displayImageAssetId` 不变，让 target 与档案显示图分离。
11. 如果改变了主线实体宽度，还要把实际测量值同步到 `content/case.json` 对应 clue 的 `physicalWidthMeters`，再运行 `Setup Demo` / `Setup Vuforia Webcam`。Detective 宽度目前由工具默认 0.1 m，需要单独同步工具配置。
12. 用外接 webcam 对每张目标逐一测试，记录日志中的 `targetName -> TRACKED` 和对应 `clueId`。模拟器或数据库校验不能替代真实视觉识别验收。

## 直接上传前的图像准备建议

对每个平面目标，上传图应是摄像头实际能看到的那一面：

- 纸张/卡片：沿纸张真实外边缘透视校正并裁掉桌面，然后打印同一张校正后的图。
- 瓶子、光盘、手表、电话、工具箱等实物：优先把线索图案印在平面卡片/标签上并把该平面作为 Image Target；只拍一张整件 3D 道具的桌面照片并上传，不会使 Vuforia 自动识别这件实物。
- 若打算把整个 JPG 当作目标打印出来，则摄像头前也必须展示这整张相同 JPG，包含相同桌面背景和边界；不能用该文件去追踪照片里单独的实物。
- 不要先旋转/裁剪上传图，却仍拿未修改的另一张图扫描。Vuforia 数据库输入与实际目标表面需要对应。

## 当前目录的使用范围

```text
Deliverables/Vuforia_NewClueTargets_20260929/
├── 01_TargetImages/                 # 原图副本，仅按现有 targetName 重命名
└── README_上传与映射.md             # 映射、正文冲突、宽度要求和 Target Manager 步骤
```

原始照片仍留在 Inbox。该候选集尚未写入 manifest，也未替换 Unity 正在使用的 `CaseTargets` 数据库。
