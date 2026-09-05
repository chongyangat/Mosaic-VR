# VBSOED 论文配套源码快照

本仓库是 VBSOED Unity 工程的精简源码快照，用于公开分享、论文配套和代码审阅。它不是完整游戏工程，也不包含可直接运行或构建所需的全部第三方依赖与美术资源。

## 来源与版本

- 原始完整仓库提交：`159447c26485714716aa38c5353bf4c158d770aa`
- 原始提交日期：2026-08-25
- 原始提交说明：`build(quest): refresh bundles for standard APK`
- Unity 版本：2022.3.62f3
- 公开导出日期：2026-09-05

本仓库经过筛选并重新建立 Git 历史，因此本仓库提交 SHA 与原始提交 SHA 不同。上面的 SHA 仅用于追溯内部完整版本。

## 包含内容

- `Assets/GameScripts/`：项目核心业务、实验流程、眼动记录、网络通信和运行时逻辑。
- `Assets/Editor/`：与 Quest 构建、网络测试和 Shader 裁剪相关的项目级编辑器脚本。
- `Assets/XR/`：XR Loader、OpenXR、Oculus 和模拟器配置。
- `Packages/MetaQuestPro/EyeGaze/`：Meta Quest Pro 眼动采集、UDP、注视点和精度相关源码。
- `Packages/manifest.json`、`Packages/packages-lock.json`：保留原始依赖声明，方便审阅和版本追踪。
- `ProjectSettings/`：Unity 项目配置。
- 与保留文件对应的 Unity `.meta` 文件，用于维持 GUID 稳定性。

## 有意排除的内容

- 美术模型、纹理、字体、音频、视频、PSD、场景素材和 AssetBundle。
- `Assets/Arts/`、`Assets/AssetRaw/` 及主要游戏场景。
- Mirror、Proxima、Didimo、YooAsset、HybridCLR 等第三方或商业资源的内嵌源码和样例。
- 原生 DLL、SO、EXE、SDK、压缩包、APK 和其他构建产物。
- 证书、签名材料、个人数据、日志以及原完整仓库的 Git 历史。

## 依赖与限制

原始 `Packages/manifest.json` 引用了以下本地包，但这些包未包含在公开快照中：

- `com.aovisvision.asuframework002`
- `com.code-philosophy.hybridclr`
- `com.cysharp.unitask`
- `com.didimo.sdk.core`
- `com.tuyoogame.yooasset`

项目源码还会引用 Mirror、Oculus/Meta XR 等组件。因此，直接用 Unity 打开时可能出现缺失程序集、材质或场景引用。这不影响阅读核心研究代码，但若需完整运行或构建，应在具备相应许可的前提下，从内部完整仓库恢复依赖和资源。

## 公开发布与许可

本次导出没有自动指定开源许可证。在添加明确的 `LICENSE` 文件前，公开可见不等于授予复制、修改或再分发权。请由项目作者根据论文和团队要求选择合适许可证。

GitHub Desktop 的上传步骤见 [GITHUB_DESKTOP_UPLOAD.md](GITHUB_DESKTOP_UPLOAD.md)。

