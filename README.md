<h1 align="center">MechWright</h1>

<p align="center"><strong>RimWorld 可视化机械体设计工具</strong></p>

<p align="center">
  <a href="./README.md"><img src="https://img.shields.io/badge/%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87-%E5%BD%93%E5%89%8D-238636?style=for-the-badge" alt="简体中文（当前）"></a>
  <a href="./README.en.md"><img src="https://img.shields.io/badge/English-Switch-0969da?style=for-the-badge" alt="Switch to English"></a>
</p>

<p align="center">
  <a href="./LICENSE"><img src="https://img.shields.io/badge/License-Apache--2.0-2ea043?style=flat-square" alt="Apache 2.0 License"></a>
  <img src="https://img.shields.io/badge/RimWorld-1.7-986f4b?style=flat-square" alt="RimWorld 1.7">
  <img src="https://img.shields.io/badge/Platform-Windows%20x64-3776ab?style=flat-square" alt="Windows x64">
  <a href="https://github.com/NineteenReincarnation/MechWright/stargazers"><img src="https://img.shields.io/github/stars/NineteenReincarnation/MechWright?style=flat-square&color=e3b341" alt="GitHub Stars"></a>
</p>

---

设计 RimWorld 机械体时，身体结构、属性、武器与生成配置常常分散在不同的 XML 文件里。

**MechWright** 将这些定义集中在一套复古数码风格的可视化工作台中。你可以创建新的机械体工程，也可以导入现有 Mod 继续修改。

## 主要功能

- **图形化机械体设计**：侦察、标准、强攻、重装机体卡片预设，直观调节 CP、护甲、速度和体型。
- **身体与战斗预设**：复用本机原版机械体身体结构，通过图形控件调整部件覆盖率，选择近战攻击或原版远程装备方案。
- **可视化编辑**：调整身体部件、覆盖率与原生 XML 列表，保留未知字段。
- **原版资料库**：读取本机 RimWorld Core 及已安装 DLC 的机械体定义，查看属性、身体结构、武器信息并进行数据对照。
- **战斗与贴图预览**：查看武器连发时序、理论 DPS 和本地机械体贴图。
- **工程管理**：编辑多份 XML、检查引用与冲突，管理项目贴图、音频和程序集。
- **导出**：生成标准 Mod 目录与 XML，可选择附带工程自己的资源文件。

## 下载与运行

Windows x64：下载或克隆仓库，进入 [app](./app/) 文件夹，运行 [MechWright.App.exe](./app/MechWright.App.exe)。请保留该文件夹内的全部运行文件。

程序采用自包含构建，无需另外安装 .NET。查阅原版机械体资料时，需要选择本机 RimWorld 安装目录。

MechWright 仍在开发中。导出的 XML 需要在对应的游戏与 DLC 环境中测试。

## 开发与许可

源码构建需要 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。项目采用 [Apache License 2.0](./LICENSE) 许可证。

---

<div align="center">
  如果 MechWright 对你有帮助，欢迎点击 <strong>Star</strong> 支持开发。
</div>
