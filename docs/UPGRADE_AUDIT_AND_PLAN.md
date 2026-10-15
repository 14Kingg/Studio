# Studio 全面升级：初始审计与实施计划

> 状态：Phase 0（只读审计）完成；实现尚未开始。
> 基线：main，审计日期：2026-10-15。
> 开发分支：upgrade/audit-and-refactor-2026-10。
> 原则：保留现有功能与 CLI 参数语义；先补测试/可复现构建，再逐步重构；不把识别成功等同于完整解密或完整导出。

## 1. 当前结构

仓库包含 351 个 Git tree 项目，主要组件：
- AssetStudio：资源格式解析核心。
- AssetStudio.Utility：纹理/资源辅助处理与外部库调用。
- AssetStudio.GUI：Windows Forms 桌面 UI。
- AssetStudio.CLI：命令行入口和批量导出。
- AssetStudio.FBXWrapper、AssetStudio.FBXNative、AssetStudio.PInvoke：FBX/本机代码边界。
- .github/workflows/build.yml：Windows 构建与产物工作流。

解决方案包含 C# SDK-style 项目和一个 Visual C++ 原生项目；这意味着跨平台目标不能简单地把所有项目统一改为跨平台目标框架。GUI 与 FBX 原生部分应保留 Windows 优先路径，核心解析/CLI 应逐步解耦。

## 2. 初始问题清单（基于静态检查，待构建与运行验证）

### P0 — 构建、依赖与发布可信度
1. 多个项目同时面向 .NET 7 和 .NET 8；.NET 7 已结束官方支持，应制定迁移到受支持 LTS 的计划，同时验证旧游戏格式/依赖兼容性。
2. AssetStudio.CLI 使用 System.CommandLine 2.0.0-beta4... 和 System.Configuration.ConfigurationManager 8.0.0-preview...，属于较旧/预览依赖组合。需要先建立依赖清单与升级测试，再批量升级包。
3. 当前工作流使用较旧的 Actions 版本，且 CLI 的两个上传步骤路径指向 GUI 的 publish 目录；需要核实真实产物布局并分别打包 GUI/CLI。
4. 仓库没有发现独立测试项目或测试文件。格式解析、导出和 CLI 参数缺少自动化回归门禁。
5. 本次审计环境通过 GitHub 连接器读取了源码与工作流，但未在本地克隆仓库，也未运行 MSBuild/.NET 编译；当前没有声称基线构建通过。GitHub Actions API 返回当前未发现历史 workflow runs。

### P1 — 架构、性能与错误恢复
6. 核心、UI、CLI、FBX 原生依赖边界需要梳理；CLI 仍以 Windows TFM 构建，而核心库已有非 Windows TFM，适合分阶段拆分。
7. 需要对大文件读取、解压、对象解析、依赖解析和导出做性能基线，之后再决定缓存、并行度、内存池及取消机制，避免盲目并发造成内存峰值。
8. 批量任务需要明确任务状态、进度、取消、失败隔离、重试和可恢复日志；单个文件失败不应默认中止整个批次。
9. 导出功能需要以真实样本建立矩阵：Texture2D、Sprite、Mesh、SkinnedMeshRenderer、骨骼、AnimationClip/Animator、音频、字体、Shader 等，并记录输出完整性而不只记录无异常。
10. GUI 主窗体和 Designer 文件较大，UI 改造应先抽离任务/导出服务，避免把解析逻辑继续堆进窗体事件。

### P1 — FairGuard UnityFS 专项
11. 已找到用户上传的 fairguard_unityfs.py。其当前说明限定于 Unity 2022.3.x、flags 约为 0x243 的特定样本；不能据此推断所有 FairGuard 版本都相同。
12. 脚本会尝试解压各数据块，并在失败时用零字节占位。它并未还原首块加密内容，因此输出 payload 可能只是部分可读；零占位可能让依赖首块的节点数据损坏。
13. 代码读取了 combined、info_at_end 标志，但解析路径没有完整实现所有对应布局；需用标准 UnityFS 测试样本覆盖这些分支。
14. 解压异常被统一记录为加密/不可解压块，错误诊断过于宽泛。新实现应区分：格式识别、块边界错误、压缩格式不支持、校验失败、疑似首块加密、部分可恢复、完整提取。
15. 目录节点名 CAB-<guid>、零 hash 和首块解压失败只能作为多个弱信号；应使用置信度/诊断原因标记，不能单凭一个信号确定 FairGuard。

### P2 — 文档、可维护性与扩展
16. CLI 当前已有 --type、--filter、--game、--map_op、--map_type、--group_assets_type 等参数；升级时应提供兼容测试与参数迁移说明。
17. 需要统一日志结构、错误码、导出清单（manifest）、版本/环境信息与可复现问题报告。
18. 需要补充架构图、支持格式/版本矩阵、导出格式说明、开发环境配置、Windows 一键构建、跨平台 CLI 说明和故障排查指南。

## 3. 分阶段实施计划

### Phase 0 — 基线与审计（当前）
- [x] 确认仓库、默认分支、权限与目录结构。
- [x] 确认项目文件、目标框架、依赖和现有构建工作流。
- [x] 检查是否存在测试项目、CI 历史记录。
- [x] 找到并读取用户上传的 FairGuard Python 脚本。
- [ ] 在可执行的 Windows/.NET 环境中完成真实基线构建（当前会话尚未运行）。
- [ ] 收集脱敏/合法样本：普通 UnityFS、不同压缩类型、FairGuard 样本、Texture/Sprite/FBX/动画样本。

### Phase 1 — 先建立质量门禁
- 新建核心测试项目，覆盖 UnityFS 头/块目录解析、边界检查、压缩失败、节点切片、路径安全和 CLI 参数。
- 固化当前 CLI 帮助输出与关键参数行为，作为兼容性快照。
- 修正 CI 产物路径，分离 GUI/CLI 产物；增加 PR 构建、测试、格式/静态检查。
- 在升级依赖前保存构建日志与已知风险列表。

### Phase 2 — 核心解析与 FairGuard 诊断
- 把 Bundle header / blocks info / data blocks / directory parsing 拆成可单测的组件。
- 增加输入长度、块数量、节点数量、偏移、大小、整数溢出与输出路径校验。
- 先实现 FairGuard 检测、诊断和部分恢复标记，并在 UI/CLI 中输出状态：
  - Standard：标准包；
  - SuspectedFairGuard：特征吻合但证据不足；
  - FairGuardPartial：可解压部分块，关键块仍不可读；
  - FairGuardReadable：样本验证后可完整解析。
- 不假设通用密钥或通用解密算法存在；仅在合法样本与可验证算法依据明确时才实现相应解码路径。
- 为格式变体编写 golden-file 测试，确保未知布局安全失败而不是挂起或产生伪成功。

### Phase 3 — 导出、性能和批量任务
- 建立统一导出服务与资产导出器接口，保持旧导出路径可回退。
- 为 Texture2D/Sprite、Mesh/Skin、Animation/Animator、Audio 等建立导出样本和自动验收。
- 引入有界并发、取消令牌、进度事件、失败隔离和 JSONL/JSON 任务报告。
- 以大包和混合资源样本比较耗时、峰值内存、成功率和导出数量；性能改动必须有数据对比。

### Phase 4 — UI 与 CLI
- UI 先改进任务列表、进度、日志、失败项重试、筛选和导出结果定位；避免一次性重写全部 WinForms 代码。
- CLI 保留旧参数，补充稳定的退出码、JSON 输出、批处理清单、取消与错误摘要。
- 核心解析/CLI 逐步跨平台；GUI 和 FBX Native 明确标记 Windows 限制，必要时使用条件构建。
- 提供 Windows 脚本与跨平台 CLI 使用文档。

### Phase 5 — 插件、发布与文档
- 设计版本化的导入器/导出器/游戏适配器接口；插件加载需有版本检查、异常隔离与安全边界。
- 补充架构/开发/测试/打包文档、资源类型支持矩阵和已知限制。
- CI 发布独立 GUI 与 CLI 工件，生成版本信息、校验和与变更说明。

## 4. 验收标准
- 默认分支不被直接修改；所有改动在独立分支审查。
- 每个阶段均有变更清单、测试命令、真实结果和已知限制。
- 原有 CLI 参数行为有兼容性测试；未覆盖的行为必须在文档中标明。
- 导出验证检查文件存在、非空、格式可读、数量统计和关键元数据，而不只检查进程退出码。
- FairGuard 结果明确区分检测、部分恢复和完整解析；不能将零占位 payload 误标为完整资源。
- Windows 构建与跨平台 CLI 构建分别验证；未实际运行的测试不得标记为通过。