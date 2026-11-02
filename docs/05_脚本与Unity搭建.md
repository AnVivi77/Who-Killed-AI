# 05 · 脚本与 Unity 搭建

下列是职责边界，允许合并小型实现，但不能让 UI、Vuforia 回调分别维护互相冲突的进度。

| 位置/脚本 | 负责什么 | 不负责什么 |
| --- | --- | --- |
| Core/AppBootstrap | 初始化唯一服务、验证配置、加载开场 | UI 绘制 |
| Core/CaseRuntimeState | 本轮发现集合和事件完成事实 | 写死人数、资源加载 |
| Core/CaseProgressService | DiscoverClue、去重、查询人物完成度、发出结果 | 播放视频 |
| Core/CaseFlowController | 状态机、扫描门禁、事件队列、结尾门槛 | SDK 追踪细节 |
| Core/SceneFlowController | 异步场景切换、加载状态 | 剧情判断 |
| Data/CaseConfig、SuspectData、ClueData | 内容资产 | 玩家进度 |
| Data/CaseValidator | 必需引用、ID、两线索、正式内容检查 | 隐式补造剧情 |
| Scanning/IClueScanner | 统一 clueId 输入协议 | 引用 Vuforia 类型 |
| Scanning/MockClueScanner | 隐藏键盘快捷键/调试输入触发 | 假冒真实识别或在观众画面显示控件 |
| Vuforia/VuforiaClueTarget | 目标绑定、跟踪事件转换 | 打开具体档案或修改进度 |
| Vuforia/VuforiaScannerAdapter | SDK 状态过滤、防抖、输入接入 | 管理结局 |
| UI/DossierPanel、ClueSlotView | 根据数据和状态刷新档案 | 自增发现数量 |
| UI/ProgressPanel、ScanFeedbackView | （当前观众运行画面不显示）内部进度与扫描状态 | 决定结尾 |
| Video/VideoPlaybackService | 准备/播放/结束/取消/失败，返回实际结果 | 独立切剧情场景 |
| Video/VideoOverlayPanel | 无按钮的视频画面层 | 计数 |
| Core/ExhibitionResetController | 取消任务、清空本轮、重新开场 | 删除用户素材 |
| Persistence/SessionPersistence | 显式启用时保存/恢复进度 | 默认沿用上个观众状态 |
| Debug/DebugPanel | 隐藏键盘模拟扫描、重置和诊断 | 观众画面提示或按钮 |
| Editor/ExhibitionSetupTool | 生成或补齐场景/预制体/配置、绑定资源 | 覆盖手工美术调整 |
| Editor/CaseContentImporter | JSON → 资产引用/ScriptableObject、生成映射报告 | 双向悄悄修改内容源 |

核心进度尽量使用不依赖 MonoBehaviour 的 C# 类型，便于测试。UI 订阅状态变化并从单一事实源重绘。

## 可选 SDK 边界

基础程序集不引用 Vuforia。仅在 SDK 存在时编译适配程序集或启用受控条件编译。包检测依据实际 Packages 清单，程序集名称从实际包获得。不能只写一个 `#if` 却留下无法解析的无条件程序集引用。

若 SDK 暂无，提供 Mock 与清楚的适配说明，安装 SDK 后才编译/校验真实桥接代码。不能声明真实扫描已经完成。

## 调查场景层级

```text
02_Investigation
  SceneContext
  EnvironmentRoot
    Background
    CorpseRepresentation（有素材时）
    Props（有素材时）
  ScanRoot
    ARCamera（Vuforia 模式）
    MockCamera（Mock 模式，与 ARCamera 互斥）
    ImageTargets
  Canvas
    InvestigationHUD
    ScanFeedback
    DossierPanel
      PortraitAndProfile
      ClueSlot1
      ClueSlot2
      ContinueButton
      ReplayButton
    VideoOverlay
    ErrorPanel
    AdminPanel
  EventSystem
```

使用屏幕 Canvas 显示档案，不能因 ImageTarget 失去跟踪连带隐藏档案。只激活当前模式的摄像头和音频监听器。

## 编辑器搭建工具

`Tools/Exhibition/Setup Demo` 应执行：检查工程状态 → 创建缺失目录 → 导入内容 → 创建缺失的 UI/视频/系统预制体 → 创建四个场景 → 绑定序列化引用 → 配置场景构建列表 → 保存 → 运行校验。

运行第二次不产生重复服务/预制体/事件绑定；已有手工改动则保留并报告。场景生成依赖 Unity Editor API，不用手填 YAML 和 GUID。提供明确的重建选项，但只有用户要求时才能覆盖已修改资产。

`Tools/Exhibition/Validate Case` 显示可定位的错误：例如“CL_S02_B 的 targetName 与导入数据库不一致”，而不是只有 NullReferenceException。

首版 UI 用 Unity 自带能力及已安装的文本组件即可。不得为简单界面自动购买插件、引入后端或建立云识别服务。
