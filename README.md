# Danmaku（弹幕射击）

基于 Unity 的 **2D 横版弹幕射击（STG）** 项目：在密集弹幕中移动、擦弹、成长火力，并挑战多关卡与 Boss。工程内同时包含一套可复用的 **DemoFrameWork**（UI、场景、资源、存档等），弹幕玩法集中在 `GameLogic/Danmaku` 与 `Demos/Danmaku`。

---

## 游戏简介

玩家操控自机在 **可配置玩法区**（支持视口换算，便于适配 HUD 与竖屏/横屏布局）内闪避敌弹、击坠敌机；系统提供 **得分、擦弹（Graze）、Power、经验与火力档位、道具拾取** 等经典 STG 要素，并通过 **ScriptableObject 数值表** 统一调节难度曲线与奖励节奏。

### 玩法

- **移动与射击**：在玩法区内移动自机；默认 **自动连射**（可在 `PlayerController` 关闭 `_autoFire` 改为手动）；火力随 **XP 升档**（多档子弹形态，含高阶追踪等），击坠敌机与拾取可补充 Power / 经验。  
- **擦弹（Graze）**：贴近敌弹未中弹时积累擦弹与 XP，半径由数值表相对受击半径倍率控制。  
- **残机与炸弹**：难度（简单/普通/困难/无尽等）影响初始 **残机数、炸弹数** 与敌方弹幕速度倍率；无尽模式规则单独配置。  
- **关卡与敌人**：由 `StageData` / `StageRunner` 驱动波次与弹幕；含 **Boss**（体型、血量、弹幕射速等相对杂兵倍率可在数值表中调）。  
- **道具**：击坠掉落多种拾取物（火力、移速、射速、回复、攻击强化等），部分计入 **拾取分**；掉落区域可与玩法区、收集线对齐。  
- **胜负与 UI**：暂停、游戏结束与最高分展示；主菜单选 **角色、章节、难度** 后经 `DanmakuRunSettings` 进入对应关卡。

#### 键位与触摸

**键盘（PC / Editor，`PlayerController` + `DanmakuGameController`）**

- **移动**：方向键 **`↑` `↓` `←` `→`** 或 **`W` `A` `S` `D`**
- **低速移动 / 判定点**：按住 **`Left Shift`**（低速移动；同时显示机体**判定点**便于穿缝）
- **射击**：默认 **自动连射**（`_autoFire`）；若关闭自动连射，则按住 **`Z`**
- **炸弹**：**`Space`**（清屏敌弹 + 短无敌；无炸弹时不触发）
- **暂停**：**`Esc`**（打开/关闭暂停界面）

**触摸（移动平台，`DanmakuTouchJoystick` + `DanmakuTouchInput`）**

- **虚拟摇杆**：仅在 **`Application.isMobilePlatform`** 时启用；在**非 UI 上**按下屏幕，在触点处显示摇杆并控制移动；触点落在按钮等 UI 上时不抢输入
- **低速 / 判定点**：**双指** ≈ 键盘 **`Left Shift`**（低速 + 显示判定点）
- **炸弹（Android）**：无单独放 bomb 键；当 **当前残机 ≤ 开局残机的一半**、仍持有炸弹且将被击中时，会**自动消耗一颗炸弹抵消本次伤害**（见 `PlayerController.TakeDamage`）。**非 Android** 仍用 **`Space`** 手动放 bomb

---

## 设计要点

| 方向 | 说明 |
|------|------|
| **数值中枢** | `DanmakuGameplayBalance`：时间分曲线、击坠/擦弹/Power 计分、火力 5 档与 XP、掉落权重、Boss 体型/血量/射速倍率等，策划可在 Inspector 调参而少改代码。 |
| **难度与会话** | `DanmakuDifficulty` + `DanmakuRunSettings`：难度影响弹幕速度、初始残机与炸弹等；主菜单与弹幕场景之间用静态会话数据传递角色、章节、难度。 |
| **关卡驱动** | `StageData` / `StageRunner`：时间轴式关卡与敌弹波次；可与 Boss 击破后解锁下一阶段等逻辑配合。 |
| **表现与资源** | 敌弹支持多 **样式目录 + 变体贴图** 随机或指定；`BulletManager` 与预载清单配合，减少进关瞬时卡顿。 |
| **UI 流程** | `DanmakuLauncher`：主菜单阶段按清单 **异步预载** Resources，Loading 条与 `LoadingPanel` 联动；`DanmakuGameController` 负责弹幕场景内预载、自机生成与 HUD/暂停/GameOver 等。 |

---

## 技术亮点

- **框架与玩法解耦**：`DemoGameEntry` 统一初始化 UIFrame、音频、场景、资源（默认 `ResourcesLoader`，可替换为 AB 等实现）、配置、输入与对象池；弹幕逻辑不绑定具体资源管线。
- **大量弹幕下的性能**：敌弹由 `BulletManager` 集中更新与碰撞；与 **GameObject 对象池**、关内/暂停时池裁剪等策略配合，控制实例数量与 GC 压力。
- **加载体验**：主菜单与战斗场景的 **预载清单**（TextAsset 路径列表）拆分，进度映射到 Loading UI，避免「场景已进但资源未就绪」时逻辑抢跑。
- **工具链**：Editor 下含图集批量、Excel 转表、导入设置批量等，便于内容迭代。

---

## 性能优化

1. **敌弹与高频实体**：`BulletManager` 用 **预分配容量的存活列表**（如初始 256）集中 **逐帧更新、越界回收与碰撞**，敌弹生成走 **`GameObjectPool` + `PooledObject`**，避免频繁 `Instantiate/Destroy`。自机弹、拾取物等同样通过入口处的对象池 `Spawn`。  
2. **池预热与分帧**：`DanmakuGameController` 对弹幕核心预制体路径做 **`Prewarm`**，并按路径 **分帧预热**，减少首开火、首刷敌时的同步加载尖峰；暂停时可按桶 **裁剪空闲池对象上限**，降低长期驻留内存。  
3. **敌弹贴图索引**：`DanmakuBulletResourceIndex` 维护 **(样式, 变体) → Sprite** 缓存；支持主界面清单 **LoadAsync 后登记**、`WarmupIfNeeded` **避免重复全量重建**；无清单时用 **`CoWarmupIfNeededAsync` 探测变体时分帧 yield**，减轻单帧同步 IO。  
4. **异步加载与清单预载**：`ResourcesLoader` 对 `Resources.LoadAsync` 用全局 **`ResourceAsyncRunner`** 协程收尾；`DanmakuLauncher` / 关内 **`GamePreloadList` 等清单** 异步拉资源并驱动 Loading 条，关内还可预热 **拾取图集缓存、音效 `PrewarmClipFromResources`**。  
5. **帧率目标**：弹幕场景可设置 **`Application.targetFrameRate`**（默认 60），减轻高刷屏与发热（与平台垂直同步策略以项目设置为准）。

---

## 环境要求

- **Unity**：2022.3 LTS（工程记录版本：`2022.3.62f2c1`）
- **渲染 / 功能包**：2D Feature、UGUI、TextMeshPro 等（见 `Packages/manifest.json`）
- **依赖**：`Newtonsoft.Json`（Unity 官方 NuGet 包 `com.unity.nuget.newtonsoft-json`）

---

## 目录导览（简要）

```
Assets/
├── Scripts/                 # 通用框架（UI、场景、资源、音频、存档、表数据等）
│   ├── Core/                # DemoGameEntry
│   ├── GameLogic/Danmaku/   # 弹幕核心玩法（子弹、敌机、关卡、碰撞、拾取…）
│   └── UIFramework/         # UIFrame、Panel/Window
├── Demos/Danmaku/           # 弹幕 Demo：启动器、主菜单/游戏内 UI、难度与持久化分数等
└── …
```

---

## 运行说明

1. 使用 **Unity 2022.3 LTS** 打开本仓库根目录。  
2. 在 **Build Settings** 中确认包含主菜单场景与弹幕场景（工程中弹幕场景名为 `Danmaku`，具体以 `SceneSettings` / 场景资源为准）。  
3. 运行入口场景：需包含 **`DemoGameEntry`** 与 **`DanmakuLauncher`**（及已配置的 UI / Resources 清单），按 Launcher 注释完成 Home 预载后即可进入主菜单与战斗。

---

## 许可与致谢

弹幕玩法与框架代码中包含第三方/社区框架风格的结构与注释（如 Game Framework 系习惯）；若对外发布请自行核对各资源与依赖的许可证。

---

*若需对外文档，可再补充：操作说明、键位、截图与已知问题列表。*
