# UIFramework 使用说明

本文档说明 UIFramework 的用法与注意事项，便于在接入项目中直接使用（接入项目仅包含 UIFramework，不包含示例工程）。

---

## 一、概念与结构

### 1.1 两种界面类型

| 类型 | 用途 | 特点 |
|------|------|------|
| **Window（窗口）** | 主界面、全屏页、弹窗 | 有历史栈与队列，同一时刻通常只显示一个；支持排队、返回上一窗口、Popup 蒙层 |
| **Panel（面板）** | 常驻 UI（导航栏、体力条、Toast 等） | 无历史/队列，可多个同时显示，按优先级挂到不同子层 |

### 1.2 核心类关系

- **UIFrame**：对外唯一入口，相当于 UIManager。提供 `OpenWindow` / `CloseWindow`、`ShowPanel` / `HidePanel`、`RegisterWindow` / `RegisterPanel` 等。
- **UIScreenController&lt;TProps&gt;**：所有界面的基类，负责显示/隐藏、进出场动画、属性、回调。
- **WindowController / WindowController&lt;TProps&gt;**：窗口基类，实现 `IWindowController`。
- **PanelController / APanelController&lt;T&gt;**：面板基类，实现 `IPanelController`。

### 1.3 层级结构（UIFrame 预制体）

- 根节点：`UIFrame`（Canvas + UIFrame）
- 子层：`PanelLayer`（PanelUILayer）、`WindowLayer`（WindowUILayer）、`PriorityWindowLayer`（WindowParaLayer，弹窗 + 蒙黑）
- 面板可按 `PanelPriority` 分配到不同子节点（None、Prioritary、Tutorial、Blocker）

### 1.4 UIFrame 与场景：单场景实例 vs 跨场景常驻

框架**最早、最省事**的用法是：**每个玩法场景里各放（或各生成）一套 UIFrame**，切场景时**整棵 UI 随场景卸载**，新场景再 `CreateUIInstance` 或场景里摆好的新实例。此时：

- 窗口进出场动画若被打断，**旧实例已销毁**，一般不会把「关掉的 `GraphicRaycaster`、未清掉的过渡计数」**遗留到下一场景**；
- 因此**文档与早期接入**都更偏向这种模型，**没有单独强调「跨场景」**。

若改为 **`DontDestroyOnLoad` / 全局单例、同一个 `UIFrame` 实例活过多次 `LoadScene`**，则会出现新问题：

- 窗口在过渡时会 **`RequestScreenBlock`**，主 Canvas 上的 **`GraphicRaycaster` 会被暂时关闭**；正常应在动画结束 **`RequestScreenUnblock`** 后恢复。
- **切场景**或**销毁界面**可能**打断关窗动画**，导致 `Unblock` 不来、`WindowUILayer` 里过渡状态卡住 → **新场景里整块 UI 点不了**。

为此 `UIFrame` 增加了：

- **`SceneManager.sceneLoaded`** 里调用 **`RecoverWindowInputAfterSceneLoad()`**：清 `WindowUILayer` 的过渡残留并**强制打开** `GraphicRaycaster`；
- **`OnDestroy`** 里取消 `sceneLoaded` 与 Block/Unblock 订阅，避免实例销毁后仍被回调；
- **不重载场景**但会打断窗口流程的业务（例如同场景内重开一局），需**自行**再调一次 **`RecoverWindowInputAfterSceneLoad()`**（`sceneLoaded` 不会触发）。

**结论**：不是「框架禁止跨场景」，而是**跨场景/常驻 UIFrame 属于进阶用法**，需要上述恢复逻辑兜底；**一场景一实例**时往往碰不到同类问题，但那是**生命周期上的巧合**。接入常驻 UIFrame 时请依赖 `RecoverWindowInputAfterSceneLoad`；弹窗与 `PriorityWindowLayer` 的 Prefab 配置见 **八、注意事项** 第 5 条及 **3.3**。

---

## 二、快速接入

### 2.1 复制内容

- **框架**：复制 `Assets/UIFramework` 即可，无 External 强依赖（仅用 Unity 自带 API）。
- **可选**：若需要 DOTween 做动画，自行接入 DOTween；事件/解耦可用项目内已有的消息或 Signal 机制。

### 2.2 创建 UIFrame

- 菜单：**Assets → Create → UI → UI Frame in Scene** 或 **UI Frame Prefab**，会自动生成带 PanelLayer、WindowLayer、PriorityWindowLayer、蒙黑等结构的根节点。
- 或手动按同结构搭建并挂载对应组件。

### 2.3 用 UISettings 注册界面

1. **Assets → Create → UI → UI Settings** 创建配置资源。
2. 指定 **Template UI Prefab**：上一步的 UIFrame 预制体。
3. **Screens To Register**：拖入所有要注册的窗口/面板预制体（根节点上必须有 `WindowController` 或 `PanelController`）。
4. 注册时使用的 **ScreenId = 预制体名称**（`gameObject.name`），因此预制体命名要与代码里使用的 ID 一致（建议用常量，见 2.4）。

**入口示例**：在场景中放一个负责 UI 的 MonoBehaviour，Awake 时创建 UIFrame（`CreateUIInstance()` 默认会实例化并注册所有 Screens To Register 中的预制体），之后在业务里用其引用打开界面。若需手动注册可传 `CreateUIInstance(instanceAndRegisterScreens: false)`。

```csharp
[SerializeField] private UISettings defaultUISettings = null;
private UIFrame uiFrame;

private void Awake() {
    uiFrame = defaultUISettings.CreateUIInstance();
}
private void Start() {
    uiFrame.OpenWindow("StartGameWindow");  // 或用你的 ScreenId 常量
}
```

### 2.4 ScreenId 与常量（建议）

- 建议用**常量类**统一管理 ScreenId，避免手写字符串出错，例如 `MyScreenIds.MainMenu`、`MyScreenIds.ConfirmDialog`。
- 常量值需与 **UISettings 里预制体的 name** 一致（因为注册时用预制体名作为 ScreenId）。
- **可选：自动生成**  
  若希望根据“界面预制体所在文件夹”自动生成常量类，可自建 Editor 脚本：扫描指定目录下带 `IScreenController` 的预制体，用预制体名生成 `public const string Xxx = "Xxx";`，写入如 `ScreenIds.cs`。菜单触发一次或通过 `AssetPostprocessor` 在预制体增删改时触发。框架内无内置工具，接入项目按需实现即可。

---

## 三、如何做一个窗口（Window）

### 3.1 无参窗口

- 继承 `WindowController`（即 `WindowController<WindowProperties>`）。
- Prefab 根节点挂该脚本，在 Inspector 中配置 **Window Properties**（见 3.3）。
- 关闭：在按钮等逻辑里调用 `UI_Close()`。

**无参窗口**：继承 `WindowController`，按钮等逻辑里发事件或调业务代码即可。**空窗口**：仅需占位或过渡时，继承 `WindowController` 不写任何逻辑即可。

```csharp
public class MainMenuWindowController : WindowController {
    public void UI_Start() {
        // 发项目内事件、调 GameManager 等
    }
}

// 占位/过渡用空窗口
public class EmptyWindowController : WindowController { }
```

### 3.2 带参数窗口（Open 时传参）

- 继承 `WindowController<TProps>`，`TProps` 实现 `IWindowProperties`（一般继承 `WindowProperties`）。
- 在 **OnPropertiesSet()** 里根据 `Properties` 刷新 UI。
- 打开时：`uiFrame.OpenWindow(screenId, new MyWindowProperties(...))`。

**做法**：自定义类继承 `WindowProperties`（或实现 `IWindowProperties`），在构造函数或属性里放业务数据；Controller 继承 `WindowController<TProps>`，在 `OnPropertiesSet()` 里用 `Properties` 刷新 UI。打开时：`uiFrame.OpenWindow(screenId, new MyWindowProperties(...))`。

```csharp
// 属性类：携带打开时传入的数据
public class LevelListWindowProperties : WindowProperties {
    public readonly List<LevelData> Levels;
    public LevelListWindowProperties(List<LevelData> data) { Levels = data; }
}

public class LevelListWindowController : WindowController<LevelListWindowProperties> {
    protected override void OnPropertiesSet() {
        // 用 Properties.Levels 刷新列表等
    }
}
```

### 3.3 窗口属性（Window Properties）

在 Prefab 或代码中可配置：

- **WindowQueuePriority**：`ForceForeground`（立即顶替）/ `Enqueue`（排队，等当前关闭后再显示）。
- **HideOnForegroundLost**：被新窗口顶替时是否隐藏。
- **IsPopup**：是否为弹窗。弹窗会进 PriorityWindowLayer、背后蒙黑。
- **SuppressPrefabProperties**：Open 时传入的 Properties 是否覆盖 Prefab 上配置的上述行为。

**弹窗（IsPopup）与两套 Properties（重要）**：

- **`RegisterScreen` / `ReparentScreen`（注册瞬间）**：读的是 **Prefab 实例化后、根节点 Controller 上序列化好的 `properties` 字段**（Inspector 里 **Screen properties / Window Properties**），据此决定窗口父节点是否在 **PriorityWindowLayer**（与蒙黑同层）。
- **`OpenWindow(screenId, new MyWindowProperties(...))`（每次打开）**：读的是 **传入的那份对象**，主要影响 **`Show` 时** 的队列/蒙黑判断等；若子类使用 **`SuppressPrefabProperties == true`**，不会用 Prefab 覆盖这份传入值，也**不会**把传入值写回 Prefab。
- 因此：**代码里 `new XxxProperties { IsPopup = true }` 不能代替 Prefab 上勾选 IsPopup**；若 Prefab 未序列化成功，`ReparentScreen` 仍会把窗口挂在普通 `WindowLayer`，运行时只能由 `EnsureScreenForPopup` 改父节点并打警告（有 Canvas 重建开销）。
- **配置方式**：务必在 Unity 中打开窗口 Prefab，在根 Controller 上展开属性，**勾选 Is Popup** 后 **Apply / Save**。**不要**只靠手写 `.prefab` 里的 YAML 补 `properties`：泛型 `WindowController<TProps>` 的嵌套类型有时需由编辑器写入完整 typetree，手写片段可能无法被反序列化，表现为 Inspector 里仍像「没配」、运行继续报警告。
- 带参弹窗若 Properties 含标题、回调等，参数顺序以实际类为准，例如 `new ConfirmDialogProperties("标题", "内容", "确定", onConfirm, "取消", onCancel)`。

### 3.4 关闭与回调

- 在 Controller 内：`UI_Close()` → 触发 `CloseRequest`，由 `WindowUILayer` 执行关闭、历史栈与队列。
- 确认框等可在自定义 Properties 里带 `Action ConfirmAction` / `Action CancelAction`，在按钮回调里先 `UI_Close()` 再执行对应 Action，避免关闭前访问已销毁的 UI。

---

## 四、如何做一个面板（Panel）

- 继承 `PanelController` 或 `APanelController<T>`（需自定义属性时）。
- Prefab 根节点挂该脚本；可在 **Panel Properties** 中设置 **Priority**（None、Prioritary、Tutorial、Blocker），用于放入不同子层。
- 显示/隐藏：`uiFrame.ShowPanel(screenId)` / `uiFrame.HidePanel(screenId)`，无历史与队列。

**常见用法**：导航栏等可在 `OnPropertiesSet()` 或 `AddListeners()` 里根据配置动态生成按钮，点击时通过项目内事件或直接调 `uiFrame.OpenWindow(targetId)` 切换窗口；Toast 等可在 `AddListeners()` 里监听数据/事件，收到后播动画。面板可多个同时显示，无栈无队列。

---

## 五、动画（AniComponent）

- 每个界面可配置 **Anim In** / **Anim Out**（`AniComponent` 引用）。
- 显示时播 AnimIn，隐藏时播 AnimOut，播完后框架会收尾（如 SetActive(false)）。

### 5.1 框架自带

- **FadeAni**（UIFramework）：渐显/渐隐，不依赖 DOTween。
- **ScaleBounceAni**（UIFramework）：窗口从小变大再回弹到正常大小（可配置起始缩放、峰值缩放与时长），依赖 DOTween；适合作为窗口的 **Anim In**。
- **ShakeAni**（UIFramework）：窗口抖动（对 RectTransform 的 anchoredPosition 做短时抖动），可配置强度、时长等，依赖 DOTween；可用于错误提示、强调等。

### 5.2 自定义动画（可选）

- 继承 `AniComponent`，实现 `Animate(Transform target, Action callWhenFinished)`，在动画结束时**必须调用** `callWhenFinished()`，否则框架会一直等待。
- 可用 Unity 协程、DOTween、Legacy Animation 等任意方式实现；若用 DOTween，可做缩放、滑动等效果（In 与 Out 通常用两个组件或同一组件上不同 bool 区分）。

---

## 六、在业务中打开/关闭界面

- 持有 **UIFrame** 引用（如通过 `UISettings.CreateUIInstance()` 得到）。
- **面板**：`ShowPanel(screenId)` / `HidePanel(screenId)`；带参：`ShowPanel<T>(screenId, properties)`。
- **窗口**：`OpenWindow(screenId)` 或 `OpenWindow(screenId, properties)`；关闭：`CloseWindow(screenId)` 或 `CloseCurrentWindow()`。
- 不区分类型时可用 `ShowScreen(screenId)`，框架按注册类型自动选 Window 或 Panel。

**典型流程**：

- 游戏/场景启动：`uiFrame.OpenWindow("MainMenu")` 打开主菜单。
- 需要常驻 UI：`uiFrame.ShowPanel("TopBar")`、`uiFrame.ShowPanel("Toast")`。
- 切换主界面：先 `uiFrame.CloseCurrentWindow()`，再 `uiFrame.OpenWindow("LevelSelect")` 或 `uiFrame.OpenWindow("PlayerInfo", new PlayerInfoProperties(...))`。
- 打开弹窗：`uiFrame.OpenWindow("ConfirmDialog", new ConfirmDialogProperties(标题, 内容, 确认文案, onConfirm, 取消文案, onCancel))`（参数以实际类为准）；不关当前窗口也可再 Open 一个 IsPopup 的窗口。

---

## 七、窗口队列与优先级

- **WindowPriority.ForceForeground**：立即显示；当前窗口若 `HideOnForegroundLost == true` 会被隐藏。
- **WindowPriority.Enqueue**：不立即显示，进入队列，当前窗口关闭后自动显示下一个。
- **IsPopup == true**：不参与“单主窗口”规则，显示在 PriorityWindowLayer，并带蒙黑；关闭弹窗后会刷新蒙层显隐。

---

## 八、注意事项（协作与踩坑）

1. **先注册再显示**  
   所有窗口/面板必须先通过 UISettings 或 `RegisterWindow` / `RegisterPanel` 注册，否则 `OpenWindow` / `ShowPanel` 会报错。

2. **预制体名 = ScreenId（使用 UISettings 时）**  
   `UISettings.CreateUIInstance()` 用预制体的 **name** 作为 ScreenId 注册。预制体命名需与代码里使用的 ID 一致（建议统一用 ScreenIds 常量）。

3. **事件监听成对 Add/Remove**  
   在 `AddListeners()` 里订阅的全局事件（如 Signal），一定要在 `RemoveListeners()` 里取消订阅，否则界面销毁后仍会收到回调导致空引用或逻辑错乱。基类在 `Awake` 调 AddListeners、`OnDestroy` 调 RemoveListeners。

4. **关闭窗口用 UI_Close()**  
   在 Controller 内部应调用 `UI_Close()`，由框架统一处理历史栈、队列和动画结束，不要直接 `gameObject.SetActive(false)` 或自己调 `Hide()`。

5. **弹窗与蒙黑**  
   弹窗需在 Prefab 上勾选 **IsPopup**，并保证 UIFrame 预制体中有 **WindowParaLayer** 及其 DarkenBG 配置，否则不会有蒙层效果。

6. **Properties 只读与线程**  
   自定义 Properties 若在 Open 时传入，建议用只读字段或属性，避免在动画/异步过程中被外部修改；与 UI 相关的刷新放在主线程（OnPropertiesSet 或 Unity 回调中）。

7. **空窗口**  
   仅需占位或过渡时，继承 `WindowController` 不写任何逻辑即可。

8. **ScreenId 自动生成**  
   若自建 Editor 工具生成 ScreenId 常量，需把扫描路径、命名空间、输出类名配置成当前项目；框架不包含该工具，按需实现。

9. **跨场景或常驻 UIFrame**  
   若 `UIFrame` 不随场景销毁，务必了解 **1.4**：依赖 **`RecoverWindowInputAfterSceneLoad`**（场景加载已自动调用；同场景内打断关窗流程时需业务手动调用），避免 `GraphicRaycaster` 永久关闭。

---

以上为 UIFramework 的使用方法与注意事项。新界面按「继承 Controller → 配 Prefab/Properties → 在 UISettings 或代码中注册 → 用 UIFrame 打开/关闭」即可在接入项目中直接使用。
