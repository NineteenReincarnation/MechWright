# RimWorld 1.7 单位 XML 字段覆盖追踪

> 基线：2026-11-13；此表记录可审计的证据级别，不是完成率声明。

## 证据等级

- **已确认可写入 XML**：有对应游戏版本的原版 XML 实例，且编辑后通过往返测试；实际游戏加载仍须单独验证。
- **仅在样本出现**：从本机 Core / DLC Def XML 观察到；样本不代表所有合法字段，也不代表任意 Def 可插入。
- **仅在 DLL 中观察到**：Assembly-CSharp.dll 的公开字段或枚举候选；尚未确认 XML 装载规则。
- **未验证**：无足够样本、元数据或测试，不向用户宣称已支持。

## 当前实现与缺口

| 范围 | 现有实现依据 | 状态与下一步 |
| --- | --- | --- |
| ThingDef / PawnKindDef 基础标量 | InspectorFieldCatalog、NativeFieldCatalog、GameAssemblyFieldCatalog | 部分可编辑；逐字段补充原版 XML + 游戏加载证据 |
| RaceProperties / graphicData | 内置字段目录、DLL 元数据一层嵌套 | 部分可编辑；复杂子类和覆盖规则未验证 |
| statBases | 已观察到的原版 XML 字段与内置常用字段 | 部分可编辑；StatDef 引用合法性未全面验证 |
| comps / verbs / tools / lifeStages | 原生列表编辑器及原始 XML 保留路径 | 列表成员不得当作独立标量插入；需 Class、li、顺序往返测试 |
| BodyDef / BodyPartDef | BodyStructureEditor 及其测试 | 部分支持；实际 DLC 全量字段和树约束待核对 |
| Hediff / Ability / Recipe / 武器 / 弹丸 | 通用 Def 与专项编辑器 | 未完成全字段验证；需分类型构建样本矩阵 |
| Royalty / Ideology / Biotech / Anomaly / Odyssey | DefCatalog 按安装目录分别扫描 | 扫描能力不等于覆盖；每个 DLC 的实测字段数量待记录 |
| 继承 / Class / MayRequire / PatchOperation | DefDocument 原始 XML 模型 | 无损往返与变更最小化仍需系统性回归 |
| 未知 Mod 扩展 | XML 原始节点可保留 | 不应自动作为已验证可插入字段 |

## 本轮改进

- NativeFieldCatalog 对 `li`、`comps`、`verbs`、`tools` 等集合/多态路径禁止直接标量插入；仍保留样本字段供参考。
- 新增针对集合路径、正常 race 标量及拒绝插入后 XML 不变性的回归测试。
- 未获取本机 RimWorld 1.7 全 DLC 的真实 XML 和 DLL，不能生成可信的全量字段数量或覆盖百分比。

## 下一轮优先级

1. 从合法安装的 Core + DLC 扫描结果导出字段清单，分别统计观察到的路径、DLL 候选、内置字段和缺口。
2. 对每种集合建立有来源证据的元素类型/属性/排序规则，不用单纯路径猜测替代 schema。
3. 增加 ParentName、Class、Inherit、MayRequire、注释与未知节点的多文件往返及撤销测试。
4. 在 UI 中显式展示四种证据状态，并为不可信字段阻止静默写入。

## 2026-11-14 增量：多态父节点保护

- NativeFieldCatalog 现在检查样本字段的祖先是否含 `Class` 属性：位于多态对象内部的叶子字段仍作为样本展示，但不提供直接标量插入。避免把 `race Class="..."` 下观察到的字段误当作可安全新建的普通 `race` 子项。
- Apply 再次执行路径安全检查，防止调用方构造 `CanInsert=true` 的集合路径绕过扫描阶段的限制。
- 新增两项回归测试：多态父节点与普通 statBases 字段对照；伪造集合路径被拒绝且 XML 和 dirty 状态保持不变。
- 证据等级：这些是**编辑器安全约束及合成 XML 测试**，不是 RimWorld 1.7 原版 / DLC 实测字段，也不是游戏加载验证。覆盖百分比仍未知。
- 待办：实际安装的 Core + 全 DLC 样本统计、程序集候选和游戏加载验证；另需核对所有合法多态节点的 XML 装载规则，避免把安全阻止误写成字段本身不合法。
