# 寻仙 DPK 资源浏览器

一个基于 WinUI 3 的本地资源工具，直接读取《新寻仙》客户端的 `whpackage1.0` DPK 包。

## 直接使用

普通用户可以从 GitHub Releases 下载单文件版 `XunxianDpkViewer.exe`，无需安装 .NET，也不需要把其他 DLL 放在程序旁边。首次启动时选择《新寻仙》客户端或 `res` 目录即可。

## 已支持

- 图标与贴图：PNG、JPG、DDS 等缩略图和大图预览。
- 声音：OGG、WAV 直接播放。
- 模型：解析 PMF 顶点、UV 与三角形索引，并沿 CCT/CMF 配置自动关联真实 DDS 贴图。
- 模型预览：可在贴图、实体着色和线框之间切换，也可直接选择同一模型关联的不同贴图资源。
- 模型导出：将 PMF 完整几何、UV 和重建法线转换为通用 Wavefront OBJ。
- 解包：导出单个资源，或按搜索条件批量导出并保留包名和内部目录结构。
- 首次启动手动选择游戏目录或 `res` 目录，随后记住路径；换电脑后会重新显示路径引导。

## 构建

需要 .NET 10 SDK。项目使用 Windows App SDK 2.2，目标为 Windows x64：

```powershell
dotnet restore .\XunxianDpkViewer.csproj
dotnet build .\XunxianDpkViewer.csproj -c Release -p:Platform=x64
```

这是自包含的非 MSIX WinUI 3 项目。运行发布脚本后，会在仓库根目录生成可直接双击的单文件程序：

```powershell
.\publish-single.cmd
```

发布后的程序直接位于 `XunxianDpkViewerPublish\XunxianDpkViewer.exe`。也可以给程序传入 `--self-test`，用已安装的客户端资源执行 DPK、PNG、OGG 和 PMF 解码自检，结果写入 `%LOCALAPPDATA%\XunxianDpkViewer\self-test.log`。

## 格式说明

DPK/WHSC 解码使用客户端实际密钥与块链格式。PMF 实体预览覆盖寻仙常见的静态、带法线、多 UV、颜色以及骨骼权重顶点声明。OBJ 会导出完整几何和首个 UV 通道；客户端专用材质着色器、骨骼动画与多个 PMF 部件的自动组装仍需要继续解析对应配置文件。
