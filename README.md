# MechWright

![MechWright icon](assets/branding/icon-dark-256.png)

**MechWright** is a retro-digital desktop workbench for designing native RimWorld mechanoid XML.

**当前状态：桌面工程、XML 编辑器与基础定义目录已经实现并通过 CI 构建与单元测试。完整版功能仍在开发中。**

## 已实现
- .NET 10 + Avalonia 12 桌面界面（复古数码配色及原始芯片标志）
- 创建 XML 根文档、导入现有 Def 文件，按定义浏览、按字段检查与编辑
- BodyDef 部件树、coverage 相对份额与累积份额的同步视图
- XML 预览、叶子字段撤销/重做、基础诊断
- 从本机 RimWorld `Data` 目录**只读**索引 Core 和五个 DLC 的 Def XML
- 导出标准 XML，明确选择输出目录；覆盖文件时保留 `.bak` 备份
- 保留未知 XML 字段/属性/注释；未经修改时逐字节保持原始文本

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
