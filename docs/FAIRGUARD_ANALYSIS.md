# FairGuard UnityFS 专项分析（初审）

## 结论先说

仓库并非完全没有 FairGuard 相关代码：核心目录已有 AssetStudio/Crypto/FairGuardUtils.cs，其中实现了自定义解码流程；AssetStudio/BundleFile.cs 在块读取时，会对 Arknights Endfield 对应游戏类型的首块调用该解码器。因此，不应另起一套算法，也不应把用户提供的 Python 脚本直接翻译成第二套实现。

当前真正缺的是：对用户提供的这类包进行通用识别、诊断与可验证的适配；目前解码调用被游戏类型条件限制，非该游戏类型时不会自动尝试这一路径。是否能直接复用现有 FairGuardUtils，需要用用户的真实样本验证。

## 用户脚本说明

脚本 fairguard_unityfs.py 声称目标为 Unity 2022.3.x 的特定 UnityFS 包，常见特征包括 flags 约为 0x243、目录节点名为 CAB-<guid>、uncompressedDataHash 为全零、首个数据块疑似加密，其余块仍可按 Unity LZ4/LZ4HC 解压。

脚本目前只做结构解析和 best-effort 解压：当块解压失败时将该块替换为等长零字节占位，输出的 payload 不是完整还原文件；它没有恢复首块真实明文。若节点偏移落在该块范围内，导出的节点可能损坏。

## 已发现的代码路径

- AssetStudio/Crypto/FairGuardUtils.cs：现有 FairGuard 解码实现，包含 seed 生成、CRC 和 RC4 风格处理。
- AssetStudio/BundleFile.cs：ReadBlocks 中已有游戏专用调用点，在特定游戏类型和首块条件下调用 FairGuardUtils.Decrypt。
- BundleFile.ReadBlocksInfoAndDirectory：已解析块信息与目录信息，可用于建立候选检测特征；但当前代码未把零 hash / CAB 命名作为面向用户的诊断状态。

## 下一步实现顺序

1. 先为现有 FairGuardUtils 和 BundleFile 解析建立最小回归测试，避免破坏现有游戏专用行为。
2. 将检测结果与解码结果分开：检测候选不应自动宣称解密成功；首块解码后必须以 LZ4 解压尺寸、节点范围和后续 SerializedFile 解析结果验证。
3. 在候选包中优先尝试现有解码器，但必须保留原始压缩字节并在失败时安全回退；普通 UnityFS 不能因误判被破坏。
4. 将 UI/CLI 状态区分为 Standard、SuspectedFairGuard、FairGuardPartial、FairGuardReadable，并在日志中记录命中的特征及失败原因。
5. 用用户样本和普通 UnityFS 对照样本做 golden-file 测试，覆盖 Unity 版本、块压缩、首块大小、flags、hash、节点名称等变化。

## 已知限制

- 当前会话没有可供验证的 FairGuard 真实资源样本，只有用户提供的解析脚本；因此暂不能证明现有 C# 解码器与该脚本所描述的包完全兼容。
- 没有在本会话中运行本地 .NET 构建或样本提取；该专项仍处于静态审计阶段。
- 对无法还原的加密块，工具必须明确报告部分提取，而不是将零占位文件输出标记为成功。

## 新增实现（待构建/样本验证）

- C# 侧新增 `FairGuardManifestParser`，按脚本观察到的 `[u32 little-endian length][ASCII bytes][4-byte alignment]` 规则解析字符串表，并检查长度、可打印 ASCII 与边界。
- 对候选包，如果首块常规解压与现有 FairGuard 解码都失败，会用零占位保留首块长度，继续尝试处理后续块；字符串扫描会跳过这个占位块，复刻脚本的部分恢复策略。
- CLI 在检测到类似 `s2_manifest` 的字符串表时，会在输出目录生成 `<原文件名>.fairguard-manifest.json`，包含字符串、`s2/` 路径、首块是否跳过、尾部二进制长度等信息。
- **限制：当前 JSON 只解析字符串表，没有解码尾部二进制索引表**。上传脚本自身也只统计尾部二进制节长度，没有实现索引表解码。不会将它标记为完整 manifest 配置解析。
- 新增的 parser 单测尚未执行；需等 CI 构建结果与真实 FairGuard/普通 UnityFS 样本验证。
