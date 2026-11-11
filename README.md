# MechWright

![MechWright icon](assets/branding/icon-dark-256.png)

**MechWright** is a retro-digital desktop workbench for designing native RimWorld mechanoid XML.

**当前状态：已实现多 XML 工程、普通机械体三 Def 装配向导、原版/DLC 只读图鉴、BodyDef、原生列表与素材管理。Windows CI 构建与单元测试覆盖主要逻辑；完整版仍在开发中。**

## 已实现
- .NET 10 + Avalonia 12 桌面界面（复古数码配色及原始芯片标志）
- 创建 XML 根文档、导入现有 Def 文件，按定义浏览、按字段检查与编辑
- 高级 XML 源码编辑（支持任意原生及未知 XML 字段），安全重新解析，整次修改可撤销
- BodyDef 部件树、coverage 相对份额与累积份额的同步视图
- XML 预览、叶子字段撤销/重做、基础诊断
- 从本机 RimWorld `Data` 目录**只读**索引 Core 和五个 DLC 的 Def XML
- **原版机械体图鉴**：扫描 Core + 已安装 DLC 的机械体，按名称、重量及来源搜索；查看 CP、属性继承来源、BodyDef 部件、PawnKind、候选武器、培育配方及原始 XML，按 texPath 尝试预览本地 PNG；不内置受版权保护的原版数据
  - 点击顶部“原版 / DLC 目录”，选择 RimWorld 游戏根目录或 `Data` 目录；随后点击“原版机械体图鉴”打开独立可缩放资料窗口，支持与编辑器并排使用
  - 显示内容来自静态 XML；**不执行 PatchOperation 或游戏 C# 默认数值计算**，来源未明时会标注未解析
  - 若用户本机没有某个 DLC 的 `Defs`，会显示未找到，不假造该 DLC 的数据
  - “设计对照”可以将当前打开的自定义 ThingDef 与图鉴中的原版机械体逐项比较；只使用明确 XML 数据，未显式填写的值保持未定义。
- 导出标准 XML，明确选择输出目录；覆盖文件时保留 `.bak` 备份
- 保留未知 XML 字段/属性/注释；未经修改时逐字节保持原始文本

### 新增的工程工作流

- **多文件 Mod 工程**：导入、新增、编辑 `Defs`、`Patches`、`Languages` 下的 XML；独立保留各文件修改；跨文件 DefName 冲突与引用检查。
- **新机械体装配**：点击“新建工程”，再点击“新机械体装配”，输入名称、CP、护甲等数据，预览并确认生成相互关联的 `ThingDef`、`PawnKindDef`、`BodyDef`。这是**最小的常规机械体 XML 骨架**，不是已验证可直接在游戏生成和作战的完整单位；身体子部件、武器、图像、配方仍需设计者补齐。
- **原生列表结构**：对 `verbs`、`tools`、`comps`、`modExtensions` 等原生 `li` 列表进行 XML 节点级新增、修改、复制、调整顺序和撤销。复杂项允许保留未知字段和属性。
- **原版字段库**：基于本地安装的原版/DLC XML 汇集已观察的字段、示例和来源；未知或复杂节点保留原始 XML 编辑通道。不把某个样例值当作运行时默认值。
- **工程资源管理**：仅将当前 Mod 自己的贴图（PNG/JPG）、音频（OGG/WAV）、程序集（DLL）和 `About/Preview.png` 登记到资源清单。你可以主动添加其他本机自有文件；导出时**必须勾选**复制才会随 Mod 一起打包。资源路径、大小、类型和链接目录受检查，失败时不会发布半成品目标文件夹。**从不自动打包游戏 Core/DLC 资源**。
- **贴图预览**：可在导出前预览已经登记的方向 PNG；源文件只读，效果是游戏外近似预览。

### 仍需完善

图鉴静态 XML 解析不执行 PatchOperation、Mod 加载顺序或游戏 C# 默认值。专业化武器表单、生命阶段渲染、全部原生字段的强类型校验、自动草稿与实机加载验收仍在推进，不能把编译通过等同于 RimWorld 加载成功。

## 运行

需安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。

```shell
dotnet restore MechWright.slnx
dotnet build MechWright.slnx -c Release
dotnet test MechWright.slnx -c Release --no-build
dotnet run --project src/MechWright.App/MechWright.App.csproj -c Release
```

也可从 GitHub Actions 的成功运行记录下载 Windows 发布包（`MechWright-win-x64`）。其构建是否成功以该次 CI 结果为准。

## 开发目标

见 [完整工程方案](docs/PLAN.md) 和 [AGENTS.md](AGENTS.md)。最终覆盖 Core、Royalty、Ideology、Biotech、Anomaly、Odyssey 的可配置原版字段，包括完整定义生成、字段来源/继承、原版校验、可视化武器编辑、贴图预览和批量 Mod 导出。**当前不宣称上述功能已全部实现，也不宣称 XML 已在游戏中加载通过。**

License: [Apache License 2.0](LICENSE). RimWorld game files are read locally and are not redistributed.
