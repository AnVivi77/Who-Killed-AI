# ExhibitionCase 运行说明

1. 在 Unity 2022.3.62f3c1 打开 `UnityProject/`。
2. 执行菜单 `Tools/Exhibition/Setup Demo`。该工具读取项目根目录的 `content/case.json` 与 `content/assets.manifest.json`，复制并导入素材，生成 `CaseConfig.asset`、四个场景、预制体和构建列表。
3. 执行 `Tools/Exhibition/Validate Case`，再打开 `Assets/ExhibitionCase/Scenes/00_Boot.unity` 按 Play。
4. Opening 黑底只播放 opening；结束后 background 持续循环。正式配置由 Vuforia 目标扫描触发线索。
5. 调查画面不显示按钮、HUD、状态提示或倒计时。档案（包括仅发现一条线索和重复查看）静默显示配置时长，当前为 3 秒后自动继续；新集齐两条线索时自动播放对应事件。五位事件都结束后播放 `Normalending`。
6. Mock 测试时将 `content/case.json` 的 `scannerMode` 临时设为 `Mock`，运行 `Tools/Exhibition/Setup Demo` 后用键盘顶部数字键 `1`–`0` 按 `case.clues` 数组顺序模拟线索扫描；`F9` 触发 Detective 彩蛋。`F12` 为隐藏重置键，从 opening 开始新一轮。屏幕不显示这些工作人员快捷键。

Vuforia 包、数据库、许可 key、实体目标宽度和真实设备尚未提供；`VuforiaScannerAdapter` 只提供稳定 `clueId` 转接协议，不代表真实扫描已验证。安装 SDK 后应按当前实际 Vuforia API 将直接 `TRACKED` 回调接入 `VuforiaClueTarget.OnDirectTrackingFound`。
