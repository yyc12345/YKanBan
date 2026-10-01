# YKanBan 项目计划（v1.7）

> 本文档是 YKanBan 的完整项目计划，由需求讨论定稿，供执行者照此实施。
> 所有设计决策均为定稿结论；如实施中发现问题，应回到计划维护者处修订，而不是自行偏离。
> v1.1 修订：数据模型细节定稿（字段更名与约束、时间戳刷新语义、级联清点、事务要求）；搜索文法新增 `column:` 限定符与短语转义；列新增 description 字段；模态居中规则。
> v1.2 修订：全局存储拆分为 app.db（结构化）与 app.json（KV 配置，Newtonsoft.Json）；CI 发布改为手动触发、仅上传 artifact。
> v1.3 修订：新增国际化章节（英文默认 + 简体中文，ResX 重启生效）；导出文件名与时间戳 i18n 化。
> v1.4 修订：新增亮/暗/跟随系统主题（默认跟随系统，即时生效）与自定义颜色硬规则；窗口规则（标准 Avalonia Window，禁止无边框与自绘标题栏）。
> v1.5 修订：CI 改为单一手动 workflow（每 RID build→test→artifact）；开发期测试一律本地 Windows，跨平台验证无限期推迟。
> v1.6 修订：**放弃分立项目原则**——合并为单项目 `YKanBan` + `YKanBan.Tests`；撤销 i18N provider 模式（直接读 ResX）；Cli 彻底移除；测试纪律改为"不写 UI 交互测试"（接受测试连带 Avalonia 依赖）。
> v1.7 修订（终版）：用户文档整合——取消 docs/SEARCH.md，全部用户须知（gitignore、WAL 三文件复制、搜索语法）合并为根目录 USAGE.md，README 引用之。

## 1. 项目概述

个人使用的本地/离线看板工具。跨平台桌面应用（Windows / Linux / macOS），类 Git 的工作方式：在被管理项目根目录下创建 `.ykanban` 文件夹存储看板数据。Avalonia UI，MVVM 模式，单实例程序。界面默认英文，附简体中文（ResX，切换重启生效，见 §8）；主题支持亮色/暗色/跟随系统（默认跟随系统，即时生效，见 §5.10）。

## 2. 存储设计

### 2.1 存储模型（类 Git）

- 每个工作区（定义见 §4）= 一个其下存在 `.ykanban` 的路径；`.ykanban/` 内含 `ykanban.db`（SQLite 数据库）与 `ykanban.lock`（锁文件）
- 打开文件夹时**只检查当前目录**是否存在 `.ykanban`，不向上递归查找；不存在则询问用户是否在当前目录初始化

### 2.2 数据库配置

- `PRAGMA journal_mode=WAL` + `PRAGMA synchronous=NORMAL`
- 每个连接显式 `PRAGMA foreign_keys=ON`（Microsoft.Data.Sqlite 默认关闭，不开则外键与 CASCADE 均不生效）
- 不做退出时 checkpoint
- 备份/迁移必须同时复制 `ykanban.db`、`ykanban.db-wal`、`ykanban.db-shm` 三个文件（此要求写入 README 用户须知）
- `PRAGMA user_version` 管理 schema 版本，启动时按版本增量迁移

### 2.3 独占访问

- 每个 `.ykanban` 同一时刻仅允许一个 YKanBan 实例打开
- 机制：独占打开 `ykanban.lock`（.NET `FileStream` + `FileShare.None`）持有至退出；锁文件内写 PID/机器名/时间用于诊断；程序崩溃/断电由 OS 自动释放，无残留问题
- 锁冲突场景：主要为他机经网络共享打开同一 `.ykanban`；界面仅显示锁冲突提示页，不提供其他操作

### 2.4 与 Git 的关系

- `.ykanban` 不应提交进 Git，建议用户将其加入全局 gitignore（写入 README 用户须知，与 WAL 三文件复制要求并列）

## 3. 看板数据模型

单看板，列 → 卡片两级。时间戳一律 INTEGER Unix **秒**。

```
columns: id INTEGER PK AUTOINCREMENT,
         title TEXT NOT NULL UNIQUE CHECK (length(title) > 0),
         description TEXT NOT NULL DEFAULT '',
         created_at INT NOT NULL, updated_at INT NOT NULL
cards:   id INTEGER PK AUTOINCREMENT,
         column_id INT NOT NULL → columns.id ON DELETE CASCADE,
         title TEXT NOT NULL DEFAULT '',
         content TEXT NOT NULL DEFAULT '',
         created_at INT NOT NULL, updated_at INT NOT NULL
tags:    id INTEGER PK AUTOINCREMENT,
         name TEXT NOT NULL UNIQUE CHECK (length(name) > 0),
         color TEXT NOT NULL CHECK (GLOB '#'+六位十六进制，即 #RRGGBB 形态),
         description TEXT NOT NULL DEFAULT ''
card_tags: (card_id, tag_id) 复合主键，双向 ON DELETE CASCADE
```

行为约定：

- 数据校验尽量在 DDL 层完成（CHECK / UNIQUE / FOREIGN KEY），保证入库即合法：
  - `columns.title`：非空、全表唯一（BINARY 严格比较），无字符集限制
  - `tags.name`：仅要求非空串，**完全放开**——无字符集/长度限制，允许空格、emoji、保留字符；UI 输入不做 trim
  - `tags.color`：仅接受 `#RRGGBB` 形态（GLOB 校验）
  - 所有 description 字段无长度上限
- 时间戳刷新规则：
  - `cards.updated_at` 仅在卡片编辑模态点"确认"写入时刷新（新建卡片时 created_at = updated_at）
  - 菜单"移动到列"不刷新卡片 updated_at，但作为一次数据库写入会刷新工作区 `last_edited_at`（已接受的不对称）
  - `columns.updated_at` 仅在列自身标题或 description 变更时刷新
  - 编辑模态确认时内容无变化 → 不写库、不刷新时间戳
- 卡片编辑确认涉及 cards + card_tags 多表写入，必须包在单个事务中
- 级联删除全场景由 DDL 覆盖，无需触发器（前提 `PRAGMA foreign_keys=ON`）：

  | 删除操作 | 级联链 |
  |---|---|
  | 删 column | → 该列所有 cards → 这些卡片的 card_tags（级联链式传导） |
  | 删 card | → 该卡的 card_tags |
  | 删 tag | → 相关 card_tags（= 卡片卸载该标签，卡片保留） |

- 禁止手动排序：列内/列间顺序由显示规则计算（id 默认 / 创建时间 / 最后修改时间 / 标题字母序），界面随时可切换、不逐工作区记忆，全局一份存 `app.json`（见 §4.4）；所有排序一律以 id 升序兜底；标题排序按码点（BINARY collation）
- 无标题的唯一表示是空串（`title TEXT NOT NULL DEFAULT ''`）；无标题卡片列内不显示占位标题；按标题排序时视为空串
- 删列 → 级联删卡（UI 二次确认，提示将删 N 张卡片）；卡片硬删除（二次确认）
- tag 永不自动清理（`tags` 表保留无引用标签），配标签管理界面
- 初始化预置三列：初始化时按当前语言直接从 ResX 资源读取（英文 To Do / In Progress / Done；中文 待办 / 进行中 / 已完成），随**创建时刻**的语言写入，此后即为数据、不再随语言切换变化（见 §8）
- 数量级参考：卡片 ≤ 10000，列 ≤ 100

## 4. 应用工作流与主界面骨架

**术语**：工作区 = 其下存在 `.ykanban` 的路径

### 4.1 单例与启动

- 封装为跨平台 `ISingleInstanceService`；平台相关代码集中于平台服务层
- 单例检测：.NET 命名 Mutex；IPC：命名管道
- `ykanban`（无参）→ 通知既有实例显示并置前窗口
- `ykanban <path>` → 置前并切换到该工作区（未打开则加入）
- 首次启动 → 欢迎页
- 路径比较：规范化绝对路径后比较，Windows 忽略大小写、Unix/macOS 区分大小写；不解析符号链接

### 4.2 侧边栏（自上而下）

- 欢迎页按钮、打开新项目按钮（弹出系统文件夹选择框）
- 分隔符 + 工作区列表（不限长、持久保存所有已知工作区）+ 列表排序选项（A-Z / Z-A / 最近更新 = 内容最后编辑时间）
- 底部：设置按钮 + 关于按钮（水平并排）

### 4.3 连接管理

- 容量 8 的 LRU 连接池（锁 + 数据库连接）；淘汰仅释放资源，条目保留在侧边栏，点击重新打开（拿锁失败 → 锁冲突页）
- 活跃度事件：点击条目 / 打开 / 切换
- 移除工作区：释放资源 → 删除条目（无保存步骤）

### 4.4 全局存储

位置：`%APPDATA%/YKanBan/`（各平台等价目录）。分界原则：**结构化数据 → app.db 开新表；琐碎 KV 配置 → app.json**。

**`app.db`**（SQLite，同工作区库的 PRAGMA 配置）：

```
workspaces(path TEXT PK, last_edited_at INT, added_at INT)
```

- 对工作区库的任何一次成功写入（卡片/列/标签增删改）均刷新 `last_edited_at`（频繁写入且需崩溃安全，留在 SQLite）

**`app.json`**（Newtonsoft.Json 14.0.1，回退 13.0.4）：

- 强类型根对象 `AppConfig` 整体序列化，根含 `"version": 1` 迁移钩子
- 承载：语言（`Language`，`"en"` 默认）、主题（`Theme`，`"FollowSystem"` 默认）、卡片排序选择、列宽、四类确认开关、侧边栏列表排序选项等琐碎 KV
- 写入时机：**应用退出时一次性写盘**（直接写，不做原子替换）；崩溃/断电丢失当次会话的设置变更（已接受：配置低价值）
- 读取：文件缺失 → 默认值启动照常运行（下次退出自然创建）；解析失败 → 备份为 `app.json.bak` → 以默认值启动

### 4.5 看板核心界面状态页

正常看板 / 未初始化页（含初始化按钮）/ 锁冲突页（仅提示，无操作）/ 目录不存在错误页

## 5. 看板核心界面

### 5.1 顶部显示区域

- 工作区名称（= 文件夹名，侧边栏同此文字）+ 小一号字显示完整路径
- 最右侧：导出按钮

### 5.2 TabControl 两选项卡

**标签选项卡**（标签管理器，风格类 GitHub issue labels 页）：

- 条目 = 颜色标记 + 名称 + 描述 + 使用数（N 张卡片）
- 支持新增、删除（提示"将从 N 张卡片移除该标签"）、重命名、改颜色、改描述

**看板选项卡**：

- 搜索栏（点击搜索按钮才执行，无实时搜索，带 × 一键清空）；右侧依次：新增列按钮、列宽调整按钮、排序 combobox
- 列宽：模态选择预设窄/标准/宽或手动像素值（范围 160–720，默认"标准"= 320），全局生效，持久化到 app.json
- 看板区域（与 GitHub Projects 基本一致）：列左→右横排，卡片上→下展开；**无拖拽**
- 列右上角：＋新建卡片、⋯菜单（编辑、删除）；"编辑" = 标题 + description 编辑模态
- 卡右上角：⋯菜单（删除、移动到列…）；点击卡片本体 → 编辑模态
- 卡片条目仅显示 3 样内容：`#id`（加粗）与标题同行（无标题则仅 `#61`；有标题如 `#61 Foo bar`）+ 内容按固定字数截断（约 200 字）。不显示标签、不显示时间戳

### 5.3 模态窗口原则

非 SPA。新增列 / 新增卡片 / 编辑卡片 / 各类删除确认等操作一律通过模态对话框弹出。

- 所有模态窗口在父窗口中央弹出（Avalonia `WindowStartupLocation="CenterOwner"` + `ShowDialog(owner)`）
- 新增列 / 编辑列模态：标题（必填，受 DDL 非空+唯一约束）+ description（可空）

### 5.4 卡片编辑模态

- 字段：标题（可空，空串 = 无标题）、内容（多行）、标签区、只读 `#id` / 创建时间 / 修改时间（新增时未生成则不显示）
- 底部：确认（当前数据一次性写入数据库）/ 取消（丢弃更改）
- 编辑对话框是一个工作单元：确认时一次性落库（单事务，见 §3），取消不产生任何写入；其余操作（删除、移动等）仍即时写库
- 确认时内容无变化 → 不写库、不刷新时间戳
- 标签指派：编辑模态标签区显示已指派标签（各带 × 移除），"添加标签"按钮弹**嵌套模态** = 已有标签搜索/列表 + 新建标签入口，选中即添加，可反复打开继续添加下一个

### 5.5 确认对话框开关

按类型分设独立开关：删除卡片 / 删除列 / 删除标签 / 丢弃编辑确认，承载于设置模态，存 app.json。

### 5.6 设置模态

入口 = 侧边栏底部"设置"按钮，承载各类全局设置：

- 语言选择（英文 / 简体中文），切换后显示"重启后生效"提示（提示文本本身为资源键）
- 主题选择（亮色 / 暗色 / 跟随系统），切换后**即时生效**，无重启提示（与语言项的行为差异）
- 四类确认开关（见 §5.5）

### 5.7 关于模态

应用名、版本号、仓库链接、许可证。入口与设置按钮水平并排。

### 5.8 欢迎页

极简：应用名/Logo 显示于提示语"从左侧打开或新建工作区"上方，别无他物。

### 5.9 导出

- 点击后弹系统保存文件对话框，默认文件名按当前语言生成（模式为资源键，**精确到秒**）：英文 `MyRepo-export-20260930-142537.md`；中文 `MyRepo-导出-20260930-142537.md`
- 人类可读格式，无回导需求；模板文本直接读取 ResX 资源（见 §8），导出语言跟随当前界面语言：

```markdown
# 工作区名
> 导出时间：2026-09-30 14:25:37        （时间戳格式随语言：英文 yyyy-MM-dd HH:mm:ss；中文 yyyy年M月d日 HH:mm）

## 列：进行中            （列按 id 序，标题后附卡片数；列 description 紧随标题下显示）

### #61 Foo bar
正文内容…

- 标签：`bug` `ui`
- 创建：2026-09-01 10:00　修改：2026-09-28 18:30

## 标签总表
| 名称 | 颜色 | 描述 | 使用数 |
```

- 空列也保留（体现看板结构）；无标题卡片以 `### #61` 呈现

### 5.10 主题（亮 / 暗 / 跟随系统）

- 三态选项：亮色 / 暗色 / 跟随系统，**默认跟随系统**
- 机制：`Application.Current.RequestedThemeVariant` = `ThemeVariant.Light / Dark / Default`（Default = 继承系统偏好）；Semi.Avalonia 两套配色经标准 ThemeVariant 机制生效，运行时切换**即时生效**（含支持平台上窗口标题栏装饰变体）
- 持久化：`AppConfig.Theme`（`"FollowSystem"` 默认 / `"Light"` / `"Dark"`）入 app.json，退出时落盘；启动时在主窗口创建前读取应用
- **自定义颜色硬规则**：界面中任何自定义 Brush/Color 资源必须以 `ThemeDictionaries` 成对定义 Light/Dark 两份并经 `DynamicResource` 引用，禁止裸用 `StaticResource` 引颜色，保证主题切换时界面完整变色
- 例外：标签颜色是用户数据（固定 `#RRGGBB`），两种主题下均直接显示原色，不做暗色变体

### 5.11 其他

- 主窗口尺寸/位置不持久化
- **窗口规则**：所有窗口直接使用标准 Avalonia Window（系统原生标题栏与装饰）；禁止无边框窗口（borderless / chromeless），禁止自绘标题栏（关闭/最小化/最大化、拖拽区域等一律不做自定义实现）

## 6. 搜索语法

实现：Pidgin 库做 Lexer + Parser；语法文档并入根目录 `USAGE.md`（README 引用，见附录）。

### 6.1 文法（EBNF）

```
query   := orExpr
orExpr  := andExpr ( OR andExpr )*
andExpr := operand ( (AND)? operand )*      ← 空格相邻 = 隐式 AND
operand := atom | '(' orExpr ')'
atom    := word | phrase | qualifier
qualifier := key ':' (word | phrase)
key     := tag | title | content | id | column
phrase  := '"' 任意文本 '"'   （内部支持转义：\" → 字面引号，\\ → 字面反斜杠）
```

### 6.2 语义

- `AND`（含隐式空格）优先于 `OR`，括号强制结合
- 仅大写 `AND`/`OR` 为运算符，小写即普通裸词；搜索字面量大写 AND/OR 需加引号（写入文档）
- Atom 语义：裸词/短语 → 标题**或**内容子串匹配；`tag:` 标签精确匹配；`column:` 列标题精确匹配（列名 UNIQUE，无歧义）；`title:` / `content:` 字段子串；`id:` 精确匹配；全部不区分大小写；引号支持含空格值
- 短语内部转义：`\"` → 字面引号，`\\` → 字面反斜杠；搜索含空格/保留字符/字面 `AND` 等特殊标签名时，用（必要时转义的）引号值（写入文档）
- 宽松降级：不可解析片段（括号不配对、运算符悬空等）退化为字面文本匹配，永不报错
- 无时间维度；解析器按可扩展限定符表设计（预留 `created:`、`NOT` 等扩展点）
- 搜索语法保留字**永不本地化**

### 6.3 交互

- 点搜索按钮才执行；空查询 = 全显；筛选后空列保留显示（维持看板结构感）；切换工作区时清空重置

## 7. 技术选型

版本已经 NuGet 实测调研锁定：

| 包 | 版本 | 备注 |
|---|---|---|
| Avalonia | **11.3.6 锁定** | 不允许升级 |
| Semi.Avalonia | 11.2.1.10 | 11.3.x 全系依赖 Avalonia ≥ 11.3.7 会强制升级，故降用 11.2 线最新；含亮/暗两套配色 |
| IconPacks.Avalonia.Lucide | 2.0.0 | 仅此子库，不装全库 IconPacks.Avalonia |
| Microsoft.Data.Sqlite | 9.0.20 | 与 net9.0 对齐；手写 SQL |
| Pidgin | 3.5.1 | 搜索 Lexer + Parser |
| MSTest | 4.4.1 | 遇问题降 3.11.1 |
| Newtonsoft.Json | 14.0.1 | app.json 序列化；回退 13.0.4 |
| CommunityToolkit.Mvvm | 8.2.1 | 沿用现有 |

### 7.1 工程结构（单项目原则）

**不拆分 Lib/Gui**——本项目无明确前后端边界，强行二分只会制造功能归属争议。沿用现有 `YKanBan/` 项目**原地改造**（清理模板内容），重组为（同步更新 `YKanBan.slnx`）：

- `YKanBan`：唯一应用项目——模型、schema/迁移、仓库层、搜索解析、导出、单例 IPC、平台服务层、Avalonia UI（MVVM）；持有全部 ResX 资源；全部包依赖进同一 csproj
- `YKanBan.Tests`：MSTest 测试库，引用主项目（**连带 Avalonia 依赖，已接受**）；纪律规则：**不编写任何 UI 交互测试**（窗口、模态、点击流、数据绑定、主题切换、状态页渲染一律人工验证；不引入 headless UI 测试基础设施）

**测试范围**：解析器（文法全分支 + 宽松降级 + SQL 编译）、schema/DDL 约束实测、`user_version` 迁移、PRAGMA 生效、锁文件独占与释放、app.json 缺失/损坏降级、CRUD 与三条级联链、编辑确认事务原子性、`last_edited_at` 写通、LRU 池、路径规范化比较（**平台规则以参数注入**，保证 Unix 分支在 Windows 本地可测）、导出结构与本地化断言（直接设 `Resources.Culture` 断言 en / zh-Hans 真实输出）；单例 IPC 仅测协议编解码与命令行参数解析，进程互斥与窗口置前人工验证。一切测试在本地 Windows 运行。

### 7.2 平台与发布

- 锁文件、IPC、路径比较等平台相关代码集中于跨平台接口的平台服务层（内部组织习惯，非强制架构）
- 发布目标：win-x64 / linux-x64 / osx-x64 / osx-arm64
- **单一 GitHub workflow**：仅 `workflow_dispatch` 手动触发；4 RID 矩阵，每个平台作业依次执行 **build → test → 测试通过后上传该 RID 的 artifact**（压缩包）；测试失败该 RID 不出产物；不创建 GitHub Release
- 开发机访问不到 GitHub：**禁止任何依赖 push 触发的 CI**
- 开发期测试策略：一切验证在**本地 Windows** 进行（`dotnet build` + `dotnet test`）；各里程碑的"单测/人工"验证均指本地 Windows 执行；Linux/macOS 行为验证**无限期推迟**至程序完工后（明确接受平台 bug 风险）

## 8. 国际化（I18N）

### 8.1 语言与资源

- 支持语言：**英文（默认）+ 简体中文（zh-Hans）**，仅此两种
- ResX 位于唯一主项目：`Resources.resx`（英文 neutral，missing key 回落英文）+ `Resources.zh-Hans.resx`；XAML 用 `{x:Static lang:Resources.Key}`
- 公共 Resources 类用跨平台 MSBuild 方案生成（`Generator=MSBuild:Compile` + `StronglyTypedFileName`（写入 obj/）+ `StronglyTypedNamespace` + `StronglyTypedClassName=Resources` + `PublicClass=true`），不用 VS 专属生成器
- M5 起硬性规则：**所有 UI 文本一律资源键**，英文文本也只写在 Resources.resx，禁止硬编码

### 8.2 资源访问与异常策略

- 需要本地化文本的代码（Markdown 导出生成器、初始化预置列名等）**直接读取 ResX 资源**（`Resources` 类全局可用），无任何注入层；导出测试可直接设 `Resources.Culture` 断言 en / zh-Hans 真实输出
- 异常策略（保留）：异常消息**一律英文、永不本地化**——异常携带本地化文本是坏实践（抛出与显示时刻 culture 不一致、日志难检索）；对需区分展示的场景（锁冲突、未初始化、目录不存在、schema 版本问题等）定义**类型化异常**，UI 按异常类型映射资源键显示本地化文案

### 8.3 语言切换与启动时序

- `AppConfig.Language`（`"en"` 默认）存 app.json；设置模态中切换（英文/简体中文）并提示"重启后生效"
- 启动时序：读 app.json → 在 `OnFrameworkInitializationCompleted` **创建主窗口之前**设置 `Resources.Culture = new CultureInfo(language)` → 同时按 `CultureInfo.TextInfo.IsRightToLeft` 检测并设置主窗口 `FlowDirection`
- **RTL 检测代码必须实现**——当前两语言均为 LTR 也不许省略，作为未来语言扩展的基础设施
- 与 app.json 退出写盘策略天然吻合：切换语言 → 退出时落盘 → 重启生效

## 9. 里程碑

| 阶段 | 内容 | 验证 |
|---|---|---|
| M0 工程重组 | 现有项目原地改造（清理模板内容）+ 新建 `YKanBan.Tests`、按版本表装依赖 | 本地 Windows build + test 通过 |
| M1 存储层 | 工作区 schema（含 CHECK/UNIQUE 约束）+ user_version 迁移、PRAGMA、锁文件、app.db + app.json、初始化预置三列（列名直接读 ResX） | 单测 |
| M2 仓库层 | CRUD/移动/级联/标签指派、last_edited_at 写通、LRU 连接池、路径规范化比较（平台规则参数注入） | 单测 |
| M3 搜索与导出 | Pidgin 解析器、查询→SQL 编译、Markdown 导出（直接读 ResX，en / zh-Hans 输出均有测试） | 单测（解析全分支） |
| M4 单例 IPC | ISingleInstanceService、参数解析、窗口置前 | 单测 + 人工 |
| M5 GUI 框架 | 主题/图标接入、主窗口布局、侧边栏全套、欢迎页、设置/关于模态；**ResX 基础设施、语言设置项、RTL/FlowDirection 检测、全 UI 文本资源化（硬规则生效）；亮/暗/跟随系统主题基础设施与设置项、ThemeDictionaries 颜色硬规则** | 人工 |
| M6 看板界面 | 顶部区、TabControl、状态页、看板 tab 全部交互与模态 | 人工 |
| M7 标签与搜索集成 | 标签 tab、搜索执行与空列保留、导出接线（含文件名/时间戳 i18n 模式） | 人工 |
| M8 发布与文档 | 单一 workflow（4 RID 矩阵 build→test→artifact）文件就绪（结构自查）、根目录 USAGE.md、README（引用 USAGE.md）、人工验收清单（含语言切换重启生效、导出语言跟随界面、主题三态切换三项） | workflow 结构自查通过；实际触发与产物验证视网络条件推迟 |

依赖链：M0 → M1 → M2 → M3 ∥ M4 → M5 → M6 → M7 → M8

## 附录：用户文档（USAGE.md）

`README.md` 引用根目录 `USAGE.md`；`USAGE.md` 必载以下三项：

1. `.ykanban` 不应提交进 Git，建议加入全局 gitignore
2. 备份/迁移必须同时复制 `ykanban.db`、`ykanban.db-wal`、`ykanban.db-shm` 三个文件
3. 搜索语法全文（文法、语义、转义规则、保留字说明，见 §6）
