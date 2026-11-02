# GitHub 提交与克隆说明

本仓库默认用于私有协作和备份。`SourceAssets/` 原始素材会纳入版本控制；Unity 缓存、构建目录、本地备份和可重新生成的交付 ZIP 不纳入。

## Vuforia 许可

`UnityProject/Assets/Resources/VuforiaConfiguration.asset` 在本机含有 Vuforia 许可字段，因此被 `.gitignore` 排除。克隆仓库后，在 Unity 中打开工程，并在 `Edit > Project Settings > Vuforia Engine` 重新填写你自己的许可。不要把许可写进 JSON、文档、压缩包、Issue 或 Git 提交。

本机 `UnityProject/Assets/WSATestCertificate.pfx` 是含私钥的签名证书文件，也被排除。克隆后如需侧载 UWP 包，请在本机创建/选择自己的签名证书；不要复用或上传该 `.pfx`。

## 大文件

项目依赖 `UnityProject/Packages/com.ptc.vuforia.engine-11.4.4.tgz`，通过 Git LFS 管理。克隆时需安装 Git LFS，并确保当前 GitHub 账户有足够的 LFS 存储和流量额度。重复的 Vuforia 引擎 `.unitypackage`、Unity 项目全量 ZIP 和生成式交付 ZIP 不提交；项目源码中保留 Target 数据及其运行时文件。

## 克隆与打开

1. 安装 Git LFS，然后克隆仓库并拉取 LFS 对象。
2. 用项目记录的 Unity 版本打开 `UnityProject/`；首次打开会重新生成被忽略的 `Library/`。
3. 在 Unity 的 Vuforia 设置中重新填写本机许可，并确认 `Packages/manifest.json` 能解析本地 Vuforia tarball。
4. 构建/扫描验收状态以 `docs/08_当前状态与交接.md` 为准；GitHub 上的源码本身不表示真实设备扫描已通过。
