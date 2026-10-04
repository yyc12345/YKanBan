# YKanBan 项目计划（v1.9）

> 本文档是 YKanBan 的完整项目计划，由需求讨论定稿，供执行者照此实施。
> 所有设计决策均为定稿结论；如实施中发现问题，应回到计划维护者处修订，而不是自行偏离。

### v1.9 修订摘要（相对 v1.8）

- 目标框架定为 **net10.0 LTS**（.NET 9 于 2026-11-10 结束支持），Microsoft.Data.Sqlite 随之升 10.x（§7）
- `columns.title` / `tags.name` 唯一约束改为 `COLLATE NOCASE`；首个发布前 schema v1 未冻结，可原地修改（§3）
- 搜索：定义 `word` 字符集；`id:` 值须为正整数；限定符值不得为裸 `AND`/`OR`；大小写折叠改为 Unicode（C# 自定义 SQL 函数）（§6）
- 排序 combobox 仅作用于卡片，列固定 id 序；选项收敛为 id 升序 / 最后修改时间降序 / 标题升序（§3）
- 筛选状态下写库后自动重新应用最近一次成功的表达式（§6.3）
- 嵌套模态中新建标签立即写库，作为编辑工作单元的明确例外（§5.4）
- app.json：仅脏时写盘；字段级非法值回退默认（§4.3）
- 新增"工作区打开失败页"（schema 版本过新 / 数据库无法打开 / IO 与权限错误）（§4.4）
- 网络共享明确不支持；命令行参数边界；macOS 无参启动行为写入文档（§2.3、§4.1、附录）
- 补充：导出转义规则、卡片列表虚拟化、模态窗口 FlowDirection、`Resources` 类名使用规则、CI osx-x64 运行器（§5、§7、§8）
- 依赖：Avalonia 11.3.6 → 11.3.22，Semi.Avalonia 11.2.1.10 → 11.3.22（§7）
- 实现回补：搜索括号嵌套上限 64 层、单个表达式至多 256 个原子，空短语、短语紧贴文本、`key:` 后空白均为非法（§6.1、§6.2）；导出表格单元格转义 `\`、默认文件名剔除非法字符（§5.8）；初始化中途锁冲突进锁冲突页、数据库文件缺失不静默新建（§4.4）；app.json 读不出 / 备份写不出 / 退出写盘失败均不阻断启动与退出（§4.3）；主题下拉选中即预览、取消恢复（§5.6）；空白新卡片确认不创建（§5.4）；app.json 脏标记按"改动过"判定（改回原值仍写盘）（§4.3）
- 里程碑：标注 M0–M4 已完成，新增 **M4R（v1.9 回补）**（§9）

## 1. 项目概述

个人使用的本地/离线看板工具。跨平台桌面应用（Windows / Linux / macOS），类 Git 的工作方式：在被管理项目根目录下创建 `.ykanban` 文件夹存储看板数据。Avalonia UI，MVVM 模式。每个 YKanBan 进程管理一个工作区，可多实例并行（同一工作区由锁文件互斥）。界面默认英文，附简体中文（ResX，切换重启生效，见 §8）；主题支持亮色/暗色/跟随系统（默认跟随系统，即时生效，见 §5.9）。

仓库：<https://code.blumia.cn/yyc12345/YKanBan>（计划正本位于仓库 `docs/PLAN.md`）

## 2. 存储设计

### 2.1 存储模型（类 Git）

- 每个工作区（定义见 §4）= 一个其下存在 `.ykanban` 的路径；`.ykanban/` 内含 `ykanban.db`（SQLite 数据库）与 `ykanban.lock`（锁文件）
- 打开工作区时**只检查参数路径本身**是否存在 `.ykanban`，不向上递归查找；不存在则显示未初始化页（见 §4.4），由用户点击初始化按钮在该路径初始化

### 2.2 数据库配置

- `PRAGMA journal_mode=WAL` + `PRAGMA synchronous=NORMAL`
- 每个连接显式 `PRAGMA foreign_keys=ON`（Microsoft.Data.Sqlite 默认关闭，不开则外键与 CASCADE 均不生效）
- 每个连接注册搜索用的大小写折叠自定义函数（见 §6.2）
- 不做退出时 checkpoint
- 备份/迁移必须同时复制 `ykanban.db`、`ykanban.db-wal`、`ykanban.db-shm` 三个文件（此要求写入 USAGE.md）
- `PRAGMA user_version` 管理 schema 版本，启动时按版本增量迁移；数据库版本高于程序支持版本 → 抛类型化异常，显示工作区打开失败页（见 §4.4）
- **schema 冻结时点**：首个对外发布（M7 产物）之前，schema v1 可原地修改，开发期数据库直接重建，无需写迁移；发布后任何 schema 变更必须走 `user_version` 增量迁移

### 2.3 独占访问

- 每个 `.ykanban` 同一时刻仅允许一个 YKanBan 实例打开
- 机制：独占打开 `ykanban.lock`（.NET `FileStream` + `FileShare.None`）持有至退出；锁文件内写 PID/机器名/时间，仅供事后人工诊断（持锁期间冲突方无法读取，锁冲突页不展示这些信息）；程序崩溃/断电由 OS 自动释放，无残留问题
- 锁冲突场景：同机多实例指向同一工作区；界面显示锁冲突页，不提供其他操作
- **网络共享明确不支持**：SQLite WAL 依赖共享内存，无法在网络文件系统上工作；Unix 上 `FileShare.None` 底层为建议锁，经 NFS/SMB 亦不可靠。此限制写入 USAGE.md，程序不做检测

### 2.4 与 Git 的关系

- `.ykanban` 不应提交进 Git，建议用户将其加入全局 gitignore（写入 USAGE.md，与 WAL 三文件复制要求并列）

## 3. 看板数据模型

单看板，列 → 卡片两级。时间戳一律 INTEGER Unix **秒**。

```
columns: id INTEGER PK AUTOINCREMENT,
         title TEXT NOT NULL CHECK (length(title) > 0),
         description TEXT NOT NULL DEFAULT '',
         created_at INT NOT NULL, updated_at INT NOT NULL,
         UNIQUE (title COLLATE NOCASE)      ← 仅唯一性 NOCASE，列本身保持 BINARY（排序按码点）
cards:   id INTEGER PK AUTOINCREMENT,
         column_id INT NOT NULL → columns.id ON DELETE CASCADE,
         title TEXT NOT NULL DEFAULT '',
         content TEXT NOT NULL DEFAULT '',
         created_at INT NOT NULL, updated_at INT NOT NULL
tags:    id INTEGER PK AUTOINCREMENT,
         name TEXT NOT NULL CHECK (length(name) > 0),
         color TEXT NOT NULL CHECK (GLOB '#'+六位十六进制，即 #RRGGBB 形态),
         description TEXT NOT NULL DEFAULT '',
         UNIQUE (name COLLATE NOCASE)
card_tags: (card_id, tag_id) 复合主键，双向 ON DELETE CASCADE
```

行为约定：

- 数据校验尽量在 DDL 层完成（CHECK / UNIQUE / FOREIGN KEY），保证入库即合法：
  - `columns.title`：非空、全表唯一（NOCASE：ASCII 字母大小写不敏感，`Done` 与 `done` 不能共存；非 ASCII 字母按原样比较），无字符集限制
  - `tags.name`：非空、全表唯一（同上 NOCASE）；除此之外**完全放开**——无字符集/长度限制，允许空格、emoji、保留字符；UI 输入不做 trim
  - `tags.color`：仅接受 `#RRGGBB` 形态（GLOB 校验）
  - 所有 description 字段无长度上限
  - 违反约束（空标题、重名）时：在当前模态内显示本地化错误提示，模态不关闭、保留用户输入（UI 按类型化异常映射资源键，见 §8.2）
- 时间戳刷新规则：
  - `cards.updated_at` 仅在卡片编辑模态点"确认"写入时刷新（新建卡片时 created_at = updated_at）
  - 菜单"移动到列"只改 `column_id`，不刷新卡片 updated_at
  - `columns.updated_at` 仅在列自身标题或 description 变更时刷新
  - 编辑模态确认时内容无变化 → 不写库、不刷新时间戳
- 卡片编辑确认涉及 cards + card_tags 多表写入，必须包在单个事务中
- 级联删除全场景由 DDL 覆盖，无需触发器（前提 `PRAGMA foreign_keys=ON`）：

  | 删除操作 | 级联链 |
  |---|---|
  | 删 column | → 该列所有 cards → 这些卡片的 card_tags（级联链式传导） |
  | 删 card | → 该卡的 card_tags |
  | 删 tag | → 相关 card_tags（= 卡片卸载该标签，卡片保留） |

- 禁止手动排序，顺序由显示规则计算：
  - **列**：固定按 id 升序，不受排序选项影响（与导出一致）
  - **卡片**（列内）：界面排序 combobox 三选一，全局一份存 `app.json`（见 §4.3）：
    - id 升序（默认）
    - 最后修改时间降序（最近修改在上）
    - 标题升序（按码点，BINARY collation；空标题视为空串、排最前）
  - 所有排序一律以 id 升序兜底
- 无标题的唯一表示是空串（`title TEXT NOT NULL DEFAULT ''`）；无标题卡片列内不显示占位标题
- 删列 → 级联删卡（UI 二次确认，提示将删 N 张卡片）；卡片硬删除（二次确认）
- tag 永不自动清理（`tags` 表保留无引用标签），配标签管理界面
- 初始化预置三列：初始化时按当前语言直接从 ResX 资源读取（英文 To Do / In Progress / Done；中文 待办 / 进行中 / 已完成），随**创建时刻**的语言写入，此后即为数据、不再随语言切换变化（见 §8）
- 数量级参考：卡片 ≤ 10000，列 ≤ 100

## 4. 应用工作流与主界面骨架

**术语**：工作区 = 其下存在 `.ykanban` 的路径

### 4.1 实例模型与启动

- 每个 YKanBan 进程独立管理一个工作区；多实例可并行运行；同一工作区由锁文件互斥（见 §2.3）
- `ykanban <path>`：本实例打开该工作区（唯一启动方式）
- 参数规则：
  - 恰好一个位置参数为合法；相对路径以进程当前工作目录解析为绝对路径
  - 无参数、多于一个参数、或参数以 `-` 开头（含 `--help` 等任何选项形式）→ **参数错误页**
  - 路径不存在或存在但不是目录 → 目录不存在页
- macOS 双击 `.app` 时无参数，只会显示参数错误页——已接受（纯命令行定位）；USAGE.md 说明 macOS 启动方式：`open -n -a YKanBan --args <绝对路径>` 或直接运行包内可执行文件（`-n` 保证已有实例运行时仍另起实例；经 `open` 启动时当前目录为 `/`，故须绝对路径）
- 窗口标题：`工作区名 - YKanBan`；无有效工作区（错误页状态）时仅 `YKanBan`

### 4.2 连接管理

- 单连接 + 单锁：打开工作区成功即持有，进程生命周期内不释放；正常退出时释放，崩溃/断电由 OS 自动释放
- 打开顺序：先取锁，再打开数据库（初始化同理：创建 `.ykanban` → 取锁 → 建库）

### 4.3 全局配置

全局配置为 `app.json`（Newtonsoft.Json 13.0.4），位置 `%APPDATA%/YKanBan/`（各平台等价目录）：

- 强类型根对象 `AppConfig` 整体序列化，根含 `"version": 1` 迁移钩子
- 字段：语言（`Language`：`"en"` 默认 / `"zh-Hans"`）、主题（`Theme`：`"follow-system"` 默认 / `"light"` / `"dark"`）、卡片排序选择（`"id"` 默认 / `"updated-at"` / `"title"`）、列宽、四类确认开关；枚举值序列化为 kebab-case 字符串
- 写入时机：**应用退出时，仅当本实例在会话内修改过任一设置（脏标记）才整体写盘**（直接写，不做原子替换）；脏标记按"是否改动过"判定：设置项被赋予与当前不同的值即置脏，改后又改回原值仍为脏；赋予相同值（如设置模态未改任何项直接确认）不置脏；读取时的字段回退不置脏；错误页状态的实例、未改过设置的实例退出时不写。多实例均改过设置时后退出者覆盖（已接受）；崩溃/断电丢失当次会话的设置变更（已接受：配置低价值）
- 读取：
  - 文件缺失 → 默认值启动照常运行（下次脏退出时自然创建）
  - JSON 本身无法解析 → 备份为 `app.json.bak`（覆盖旧备份）→ 以默认值启动
  - JSON 完好但个别字段非法（未知枚举值、未知语言、列宽越界 160–720、类型不符等）→ **仅该字段回退默认值**，其余字段保留，不生成备份
  - 文件存在但无法读取（IO / 权限错误）→ 以默认值启动；`.bak` 备份写不出时跳过备份，照常以默认值启动
- 退出写盘失败（IO / 权限错误）→ 忽略，等同丢失当次会话的设置变更；无论写盘成败都释放工作区锁与连接

### 4.4 全窗口错误页体系

所有错误页**覆盖整个窗口内容区**（替代一切正常显示）：

| 错误页 | 触发条件 | 内容 |
|---|---|---|
| 参数错误页 | 参数不合法（见 §4.1） | 参数错误提示 + 正确用法说明（`ykanban <path>`） |
| 目录不存在页 | 参数路径不存在或不是目录 | 提示 |
| 未初始化页 | 路径存在但无 `.ykanban` | 提示 + 初始化按钮（唯一带操作的错误页） |
| 锁冲突页 | 锁文件被占用（含点击初始化时锁已被其他实例抢先持有） | 仅提示，无任何操作 |
| 工作区打开失败页 | schema 版本高于程序支持版本；`.ykanban` 存在但 `ykanban.db` 缺失；数据库无法打开/损坏；IO 或权限错误（含初始化失败） | 按异常类型显示本地化提示 + 只读可复制的英文异常详情；无其他操作 |

- 未初始化页点击初始化失败 → 切换至工作区打开失败页；例外：失败原因为锁冲突（其他实例抢先持有锁）→ 切换至锁冲突页
- `.ykanban` 存在但 `ykanban.db` 缺失 → 工作区打开失败页，提示从备份恢复或删除 `.ykanban` 后重新初始化；**绝不静默新建空库**
- 未被识别的异常类型归入工作区打开失败页的通用提示

正常视图 = 顶部显示区域 + TabControl（见 §5）。

## 5. 看板核心界面

### 5.1 顶部显示区域

- 工作区名称（= 文件夹名）+ 小一号字显示完整路径
- 最右侧：**导出、设置、关于**三按钮同排

### 5.2 TabControl 两选项卡

**标签选项卡**（标签管理器，风格类 GitHub issue labels 页）：

- 条目 = 颜色标记 + 名称 + 描述 + 使用数（N 张卡片）
- 支持新增、删除（提示"将从 N 张卡片移除该标签"）、重命名、改颜色、改描述

**看板选项卡**：

- 搜索栏（点击搜索按钮才执行，无实时搜索，带 × 一键清空）；右侧依次：新增列按钮、列宽调整按钮、排序 combobox
- 列宽：模态选择预设窄/标准/宽或手动像素值（窄 = 240 / 标准 = 320 / 宽 = 480；手动范围 160–720，默认"标准"），全局生效，持久化到 app.json
- 看板区域（与 GitHub Projects 基本一致）：列左→右横排，卡片上→下展开；**无拖拽**
- 列内卡片列表必须使用 UI 虚拟化（如 `VirtualizingStackPanel`），满足 10000 张卡片量级的流畅显示
- 列右上角：＋新建卡片、⋯菜单（编辑、删除）；"编辑" = 标题 + description 编辑模态
- 卡右上角：⋯菜单（删除、移动到列…）；点击卡片本体 → 编辑模态
- 卡片条目显示 4 样内容：`#id`（加粗）与标题同行（无标题则仅 `#61`；有标题如 `#61 Foo bar`）、内容按固定字数截断（约 200 字）、标签列于内容下方（彩色徽章：颜色 + 名称）。不显示时间戳

### 5.3 模态窗口原则

非 SPA。新增列 / 新增卡片 / 编辑卡片 / 各类删除确认等操作一律通过模态对话框弹出。

- 所有模态窗口在父窗口中央弹出（Avalonia `WindowStartupLocation="CenterOwner"` + `ShowDialog(owner)`）
- 所有模态窗口与主窗口一样设置 `FlowDirection`（FlowDirection 不跨窗口继承，见 §8.3）
- 搜索表达式错误对话框：纯提示型模态（无确认语义），同样父窗口居中（见 §6.2）
- 新增列 / 编辑列模态：标题（必填，受 DDL 非空+唯一约束，违反时模态内提示，见 §3）+ description（可空）

### 5.4 卡片编辑模态

- 字段：标题（可空，空串 = 无标题）、内容（多行）、标签区、只读 `#id` / 创建时间 / 修改时间（新增时未生成则不显示）
- 底部：确认（当前数据一次性写入数据库）/ 取消（丢弃更改）
- 编辑对话框是一个工作单元：确认时一次性落库（单事务，见 §3），取消不产生任何卡片/指派写入；其余操作（删除、移动等）仍即时写库
- 确认时内容无变化 → 不写库、不刷新时间戳；新建卡片时即标题、内容、标签均为空 → 确认不创建卡片（等同取消）
- 标签指派：编辑模态标签区显示已指派标签（各带 × 移除），"添加标签"按钮弹**嵌套模态** = 已有标签搜索/列表 + 新建标签入口，选中即添加，可反复打开继续添加下一个
- **例外**：嵌套模态中新建的标签**立即写入 `tags` 表**（标签是全局实体）；外层编辑取消时仅丢弃指派关系，新建的标签保留（与"tag 永不自动清理"一致）

### 5.5 确认对话框开关

按类型分设独立开关：删除卡片 / 删除列 / 删除标签 / 丢弃编辑确认，承载于设置模态，存 app.json。

### 5.6 设置模态

入口 = 顶部显示区域右侧"设置"按钮，承载各类全局设置：

- 语言选择（英文 / 简体中文），切换后显示"重启后生效"提示（提示文本本身为资源键）
- 主题选择（亮色 / 暗色 / 跟随系统），下拉框选中即**即时生效**（预览），无重启提示（与语言项的行为差异）；点确认才写入配置，取消或关闭设置模态则恢复打开时的主题
- 四类确认开关（见 §5.5）

### 5.7 关于模态

应用名、版本号、仓库链接（<https://code.blumia.cn/yyc12345/YKanBan>）、许可证。入口 = 顶部显示区域右侧，与导出、设置按钮同排。

### 5.8 导出

- 点击后弹系统保存文件对话框，默认文件名按当前语言生成（模式为资源键，**精确到秒**）：英文 `MyRepo-export-20260930-142537.md`；中文 `MyRepo-导出-20260930-142537.md`；工作区名中任一平台的文件名非法字符（`<>:"/\|?*` 与控制字符）剔除，剔除后为空（如工作区位于 `/`）则用 `YKanBan`
- 人类可读格式，无回导需求；模板文本直接读取 ResX 资源（见 §8），导出语言跟随当前界面语言
- 导出范围为工作区全量数据，不受当前搜索筛选与卡片排序选项影响：列按 id 升序，列内卡片按 id 升序
- 全文时间戳（导出时间与卡片创建/修改时间）格式随语言：英文 `yyyy-MM-dd HH:mm:ss`；中文 `yyyy年M月d日 HH:mm`
- 结构示例（英文时间格式）：

```markdown
# 工作区名
> 导出时间：2026-09-30 14:25:37

## 列：进行中 (1)            （标题后附卡片数；列 description 紧随标题下显示）

### #61 Foo bar
正文内容…

- 标签：`bug` `ui`
- 创建：2026-09-01 10:00:00　修改：2026-09-28 18:30:00

## 标签总表
| 名称 | 颜色 | 描述 | 使用数 |
```

- 空列也保留（体现看板结构）；无标题卡片以 `### #61` 呈现
- 转义规则：
  - 标签名以行内代码呈现，定界反引号数量 = 名称内最长连续反引号串长度 + 1（名称以反引号开头/结尾时两侧加空格）
  - 标签总表单元格内 `\` → `\\`（先于 `|` 处理，否则 `a\|b` 中的 `|` 会被当成列分隔符），`|` → `\|`，换行 → `<br>`
  - 卡片正文与列 description **原样输出**（接受其中 Markdown 结构干扰文档层级，已接受）

### 5.9 主题（亮 / 暗 / 跟随系统）

- 三态选项：亮色 / 暗色 / 跟随系统，**默认跟随系统**
- 机制：`Application.Current.RequestedThemeVariant` = `ThemeVariant.Light / Dark / Default`（Default = 继承系统偏好）；Semi.Avalonia 两套配色经标准 ThemeVariant 机制生效，运行时切换**即时生效**（含支持平台上窗口标题栏装饰变体）
- 持久化：`AppConfig.Theme`（`"follow-system"` 默认 / `"light"` / `"dark"`）入 app.json，按 §4.3 脏标记规则退出时落盘；启动时在主窗口创建前读取应用
- **自定义颜色硬规则**：界面中任何自定义 Brush/Color 资源必须以 `ThemeDictionaries` 成对定义 Light/Dark 两份并经 `DynamicResource` 引用，禁止裸用 `StaticResource` 引颜色，保证主题切换时界面完整变色
- 例外：标签颜色是用户数据（固定 `#RRGGBB`），两种主题下均直接显示原色，不做暗色变体

### 5.10 其他

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
qualifier := key ':' value
key     := tag | title | content | id | column
value   := word | phrase                    （tag/title/content/column）
         | '#'? digit+                       （id：十进制正整数，可带前缀 #）
word    := 一个或多个字符，不含空白及 ( ) " : \  ；且不等于 AND / OR
phrase  := '"' 一个或多个字符 '"'   （内部支持转义：\" → 字面引号，\\ → 字面反斜杠）
```

- 裸词中出现 `:` 时其左侧必须是合法 key，否则为"未知限定符"错误；含 `:` `(` `)` `\` 等字符的搜索值须用引号（如 `"http://x"`）
- 短语不得为空（`""`）；短语与相邻裸词/短语之间须有空白或括号（`a"b"`、`"a"b`、`"a""b"` 非法），唯一例外是紧贴 key 的限定符值 `key:"…"`；`key:` 与短语值之间不得有空白（`tag: "x"` 非法）

### 6.2 语义

- `AND`（含隐式空格）优先于 `OR`，括号强制结合
- 仅大写 `AND`/`OR` 为运算符，小写即普通裸词；搜索字面量大写 AND/OR 需加引号（写入文档）
- Atom 语义：裸词/短语 → 标题**或**内容子串匹配；`tag:` 标签名精确匹配；`column:` 列标题精确匹配；`title:` / `content:` 字段子串；`id:` 精确匹配；引号支持含空格值
- **大小写折叠**：全部匹配不区分大小写，采用 Unicode 折叠——每个连接注册 C# 自定义 SQL 函数（`ToLowerInvariant` 后按 Ordinal 做子串/相等比较），不使用 `LIKE`（因而 `%` `_` 无通配含义，无需转义）。注：唯一约束 NOCASE 仅折叠 ASCII，故 `Ä` 与 `ä` 可作为两个标签共存而 `tag:ä` 同时匹配二者——已接受
- 短语内部转义：`\"` → 字面引号，`\\` → 字面反斜杠；搜索含空格/保留字符/字面 `AND` 等特殊标签名时，用（必要时转义的）引号值（写入文档）
- **严格校验**：非法表达式弹出错误对话框，不执行搜索。非法集合：
  - 括号不配对
  - 括号嵌套超过 64 层（防止递归解析栈溢出；顺序并列的括号组不计入深度）
  - 原子（裸词、短语、限定符）超过 256 个（编译出的 SQL 把全部原子串成一个表达式，SQLite 表达式深度上限为 1000）
  - 运算符悬空（`bug AND`）、连续运算符（`a AND OR b`）
  - 限定符无值（`tag:`）、限定符值为裸 `AND`/`OR`（`tag:AND`，须写 `tag:"AND"`）
  - 短语引号未闭合、无效转义（`tag:"a\x"`）、裸词中出现 `\`
  - 空短语（`""`、`tag:""`）；短语紧贴其他文本（`a"b"`、`"a"b`）；`key:` 与短语值之间有空白（`tag: "x"`）
  - 未知限定符 key（合法 key 仅 tag / title / content / id / column）
  - `id:` 值不是十进制正整数（`id:abc`、`id:0`、`id:"61"`）
- 错误对话框：模态、父窗口居中；文案 = 资源模板"查询表达式有误，详情：{0}"（i18n），{0} 处拼接解析器返回的**非 i18n 英文错误描述串**（对位置信息无要求）
- 空查询与纯空白查询 = 合法（显示全部）
- 无时间维度；解析器按可扩展限定符表设计（预留 `created:`、`NOT` 等扩展点）
- 搜索语法保留字**永不本地化**

### 6.3 交互

- 点搜索按钮才执行（校验也仅发生在点击时，无实时报错）；空查询 = 全显；筛选后空列保留显示（维持看板结构感）；搜索状态在会话内自然存续
- 表达式非法时：错误对话框关闭后，搜索框保留原文待修改，看板维持上一次筛选结果（错误不破坏现状）
- **写库后刷新**：任何写库操作（增删改卡片/列/标签、移动、指派）完成后，以"最近一次成功执行的表达式"（而非搜索框当前文本）重新查询刷新看板；不再匹配的卡片立即消失，新建但不匹配的卡片不显示

## 7. 技术选型

版本已经 NuGet 实测调研锁定：

| 包 / 框架 | 版本 | 备注 |
|---|---|---|
| 目标框架 | **net10.0** | LTS；主项目与测试项目一致 |
| Avalonia | **11.3.22 锁定** | 由 11.3.6 升至 11.3 线最新补丁（修复间接依赖 Tmds.DBus.Protocol 0.21.2 的 NU1903 高危通告，随之为 0.21.3）；不升 12.x |
| Semi.Avalonia | 11.3.22 | 与 Avalonia 同版本号配套；含亮/暗两套配色 |
| IconPacks.Avalonia.Lucide | 2.0.0 | 仅此子库，不装全库 IconPacks.Avalonia |
| Microsoft.Data.Sqlite | 10.0.x | 与 net10.0 对齐，取迁移时 10.0 线最新补丁并在 csproj 锁定具体版本；手写 SQL |
| Pidgin | 3.5.1 | 搜索 Lexer + Parser |
| MSTest | 4.4.1 | 遇问题降 3.11.1 |
| Newtonsoft.Json | 13.0.4 | app.json 序列化 |
| CommunityToolkit.Mvvm | 8.2.1 | 沿用现有 |

### 7.1 工程结构（单项目原则）

沿用现有 `YKanBan/` 项目**原地改造**（M0 已完成），结构为（`YKanBan.slnx` 同步）：

- `YKanBan`：唯一应用项目——模型、schema/迁移、仓库层、搜索解析、导出、平台服务层、Avalonia UI（MVVM）；持有全部 ResX 资源；全部包依赖进同一 csproj
- `YKanBan.Tests`：MSTest 测试库，引用主项目（连带引入 Avalonia 依赖）；纪律规则：**不编写任何 UI 交互测试**（窗口、模态、点击流、数据绑定、主题切换、状态页渲染一律人工验证；不引入 headless UI 测试基础设施）

**测试范围**：解析器（成功文法全分支 + **错误检测全分支**（每个非法输入断言解析失败且错误消息非空）+ SQL 编译 + Unicode 大小写折叠）、schema/DDL 约束实测（含 NOCASE 唯一性）、`user_version` 迁移（含版本过新抛类型化异常）、PRAGMA 生效、锁文件独占与释放、app.json 缺失/损坏降级/字段级回退/脏标记写盘、CRUD 与三条级联链、编辑确认事务原子性、**命令行参数解析**（无参/合法/非法/相对路径/选项形式）、导出结构、转义与本地化断言（直接设 `Resources.Culture` 断言 en / zh-Hans 真实输出）。一切测试在本地 Windows 运行。

### 7.2 平台与发布

- 锁文件等平台相关代码集中于跨平台接口的平台服务层（内部组织习惯，非强制架构）
- 发布目标：win-x64 / linux-x64 / osx-x64 / osx-arm64
- **单一 GitHub workflow**：仅 `workflow_dispatch` 手动触发；4 RID 矩阵，每个平台作业依次执行 **build → test → 测试通过后上传该 RID 的 artifact**（压缩包）；测试失败该 RID 不出产物；不创建 GitHub Release
- 运行器：osx-x64 作业须使用 Intel 运行器（`macos-15-intel`；`macos-13` 已下线），osx-arm64 用 `macos-15`，其余用 `windows-latest` / `ubuntu-latest`
- 开发机访问不到 GitHub：**禁止任何依赖 push 触发的 CI**
- 开发期测试策略：一切验证在**本地 Windows** 进行（`dotnet build` + `dotnet test`）；各里程碑的"单测/人工"验证均指本地 Windows 执行；Linux/macOS 行为验证**无限期推迟**至程序完工后（明确接受平台 bug 风险）

## 8. 国际化（I18N）

### 8.1 语言与资源

- 支持语言：**英文（默认）+ 简体中文（zh-Hans）**，仅此两种
- ResX 位于唯一主项目（`Assets/Locales/`）：`Resources.resx`（英文 neutral，missing key 回落英文）+ `Resources.zh-Hans.resx`；XAML 用 `{x:Static lang:Resources.Key}`
- 公共 Resources 类用跨平台 MSBuild 方案生成（`Generator=MSBuild:Compile` + `StronglyTypedFileName`（写入 obj/）+ `StronglyTypedNamespace` + `StronglyTypedClassName=Resources` + `PublicClass=true`），不用 VS 专属生成器
- **类名使用规则**：生成类 `Resources` 与 Avalonia 实例属性 `StyledElement.Resources` / `Application.Resources` 同名；在控件、Window、Application 的代码隐藏中一律写全限定名 `YKanBan.Resources`，禁止裸写 `Resources.Key`（否则会解析到实例属性）
- M4 起硬性规则：**所有 UI 文本一律资源键**，英文文本也只写在 Resources.resx，禁止硬编码

### 8.2 资源访问与异常策略

- 需要本地化文本的代码（Markdown 导出生成器、初始化预置列名等）**直接读取 ResX 资源**（`Resources` 类全局可用）；导出测试可直接设 `Resources.Culture` 断言 en / zh-Hans 真实输出
- 异常策略：异常消息**一律英文、永不本地化**——异常携带本地化文本是坏实践（抛出与显示时刻 culture 不一致、日志难检索）；对需区分展示的场景（锁冲突、未初始化、目录不存在、schema 版本过新、数据库无法打开、约束冲突等）定义**类型化异常**，UI 按异常类型映射资源键显示本地化文案

### 8.3 语言切换与启动时序

- `AppConfig.Language`（`"en"` 默认 / `"zh-Hans"`）存 app.json；设置模态中切换（英文/简体中文）并提示"重启后生效"
- 启动时序：读 app.json → 在 `OnFrameworkInitializationCompleted` **创建主窗口之前**设置 `Resources.Culture = new CultureInfo(language)` → 同时按 `CultureInfo.TextInfo.IsRightToLeft` 检测 `FlowDirection`，应用于主窗口及**每个模态窗口**（FlowDirection 不跨窗口继承）
- **RTL 检测代码必须实现**——当前两语言均为 LTR 也不许省略，作为未来语言扩展的基础设施
- 与 app.json 退出写盘策略天然吻合：切换语言（置脏）→ 退出时落盘 → 重启生效

## 9. 里程碑

| 阶段 | 状态 | 内容 | 验证 |
|---|---|---|---|
| M0 工程重组 | 已完成 | 现有项目原地改造（清理模板内容）+ 新建 `YKanBan.Tests`、按版本表装依赖 | 本地 Windows build + test 通过 |
| M1 基础设施 | 已完成 | 工作区 schema（含 CHECK/UNIQUE 约束）+ user_version 迁移、PRAGMA、锁文件、app.json、命令行参数解析、**ResX 资源基础设施与类型化异常**、初始化预置三列（列名直接读 ResX） | 单测 |
| M2 仓库层 | 已完成 | CRUD/移动/级联/标签指派、单连接生命周期 | 单测 |
| M3 搜索与导出 | 已完成 | Pidgin 解析器（严格校验，非法输入返回英文错误消息）、查询→SQL 编译、Markdown 导出（直接读 ResX，en / zh-Hans 输出均有测试） | 单测（成功与错误全分支） |
| M4 GUI 框架 | 已完成 | 主题/图标接入、主窗口布局（顶部区 + TabControl + 全窗口错误页体系）、设置/关于模态；全 UI 文本资源化（硬规则生效）、语言设置项、RTL/FlowDirection 检测；亮/暗/跟随系统主题基础设施与设置项、ThemeDictionaries 颜色硬规则 | 人工 |
| **M4R v1.9 回补** | 已完成 | 见下方清单 | 本地 Windows build + test 通过；新增页面人工 |
| M5 看板界面 | 已完成（待人工验收） | 看板 tab 全部交互与模态（含约束冲突模态内提示、卡片列表虚拟化、嵌套模态新建标签立即写库、写库后按最近成功表达式刷新） | 人工 |
| M6 标签与搜索集成 | 已完成（待人工验收） | 标签 tab、搜索执行与空列保留、导出接线（含文件名/时间戳 i18n 模式） | 人工 |
| M7 发布与文档 | 已完成（待人工验收；workflow 实际触发推迟） | 单一 workflow（4 RID 矩阵 build→test→artifact，osx-x64 用 Intel 运行器）文件就绪（结构自查）、根目录 USAGE.md、README（引用 USAGE.md）、人工验收清单（含语言切换重启生效、导出语言跟随界面、主题三态切换、app.json 多实例脏写四项） | workflow 结构自查通过；实际触发与产物验证视网络条件推迟 |

**M4R 清单**（v1.9 对已完成里程碑的回补，进入 M5 前完成）：

1. TFM 迁移至 net10.0（主项目与测试项目），Microsoft.Data.Sqlite 升至 10.0 线最新补丁并锁定
2. schema v1 原地修改：`columns.title` / `tags.name` 加 `COLLATE NOCASE`；补 NOCASE 唯一性测试
3. 搜索：按 §6.1 收紧 `word` 字符集、`id:` 正整数校验、限定符值禁裸 `AND`/`OR`；匹配改用注册的 Unicode 折叠自定义函数（替换 LIKE/lower 方案，若现有实现使用）；补对应成功/错误分支测试
4. 排序选项删除"创建时间"（`CardSortOption.CreatedAt`）；`updated-at` 为降序；列排序固定 id 升序
5. app.json：脏标记写盘、字段级非法值回退；补测试
6. 导出：§5.8 转义规则与时间格式核对；补测试
7. 新增工作区打开失败页（§4.4），接入 `SchemaVersionException` 等类型化异常；命令行参数按 §4.1 边界核对
8. 模态窗口 FlowDirection 设置（§8.3）；代码隐藏中 `Resources` 全限定名核查（§8.1）

依赖链：M0 → M1 → M2 → M3 → M4 → M4R → M5 → M6 → M7

## 附录：用户文档（USAGE.md）

`README.md` 引用根目录 `USAGE.md`；`USAGE.md` 必载以下五项：

1. `.ykanban` 不应提交进 Git，建议加入全局 gitignore
2. 备份/迁移必须同时复制 `ykanban.db`、`ykanban.db-wal`、`ykanban.db-shm` 三个文件；不支持将工作区放在网络共享（SMB/NFS）上
3. 搜索语法全文（文法、语义、`word` 字符集、转义规则、保留字、大小写折叠、严格校验与非法表达式清单、错误对话框行为，见 §6）
4. 命令行启动规则（`ykanban <path>`；参数规则与各错误页，见 §4.1）
5. macOS 启动方式（`open -n -a YKanBan --args <绝对路径>` 或直接运行包内可执行文件；`-n` 保证另起实例，`open` 启动时当前目录为 `/` 故须绝对路径；双击 `.app` 只会显示参数错误页）
