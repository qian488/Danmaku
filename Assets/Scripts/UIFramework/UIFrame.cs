using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DemoFrameWork.UI
{
    /// <summary>
    /// 所有的对外接口都在这，相当于UIManager之类的
    /// </summary>
    public class UIFrame : MonoBehaviour
    {
        [Tooltip("如果您想手动初始化此UI框架，请将其设置为false")]
        [SerializeField] private bool initializeOnAwake = true;
        
        private PanelUILayer panelLayer;
        private WindowUILayer windowLayer;

        private Canvas mainCanvas;
        private GraphicRaycaster graphicRaycaster;

        /// <summary>
        /// 主Canvas
        /// </summary>
        public Canvas MainCanvas {
            get {
                if (mainCanvas == null) {
                    mainCanvas = GetComponent<Canvas>();
                }

                return mainCanvas;
            }
        }

        /// <summary>
        /// 主Canvas的摄像机
        /// </summary>
        public Camera UICamera {
            get { return MainCanvas.worldCamera; }
        }

        private void Awake() {
            if (initializeOnAwake) {
                Initialize();    
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void Initialize() {
            if (panelLayer == null) {
                panelLayer = gameObject.GetComponentInChildren<PanelUILayer>(true);
                if (panelLayer == null) {
                    Debug.LogError("[UI Frame] UI Frame lacks Panel Layer!");
                }
                else {
                    panelLayer.Initialize();
                }
            }

            if (windowLayer == null) {
                windowLayer = gameObject.GetComponentInChildren<WindowUILayer>(true);
                if (windowLayer == null) {
                    Debug.LogError("[UI Frame] UI Frame lacks Window Layer!");
                }
                else {
                    windowLayer.Initialize();
                    windowLayer.RequestScreenBlock += OnRequestScreenBlock;
                    windowLayer.RequestScreenUnblock += OnRequestScreenUnblock;
                }
            }

            SceneManager.sceneLoaded -= OnSceneLoadedRecoverInput;
            SceneManager.sceneLoaded += OnSceneLoadedRecoverInput;

            graphicRaycaster = MainCanvas.GetComponent<GraphicRaycaster>();

            var canvasScaler = MainCanvas.GetComponent<CanvasScaler>();
            if (canvasScaler != null)
            {
                SafeAreaAdapter.Init(canvasScaler);
            }
        }

        /// <summary>
        /// 仅通过id显示一个面板
        /// </summary>
        public void ShowPanel(string screenId) {
            panelLayer.ShowScreenById(screenId);
        }

        /// <summary>
        /// 通过id和属性显示面板
        /// </summary>
        public void ShowPanel<T>(string screenId, T properties) where T : IPanelProperties {
            panelLayer.ShowScreenById<T>(screenId, properties);
        }

        /// <summary>
        /// 仅通过id隐藏面板
        /// </summary>
        public void HidePanel(string screenId) {
            panelLayer.HideScreenById(screenId);
        }

        /// <summary>
        /// 根据 id 获取已注册的面板 controller，用于预加载等；返回是否为 Panel。
        /// </summary>
        public bool TryGetPanelController(string panelId, out IPanelController controller) {
            if (panelLayer != null && panelLayer.TryGetScreen(panelId, out var ctl)) {
                controller = ctl;
                return true;
            }
            controller = null;
            return false;
        }

        /// <summary>
        /// 仅通过id显示窗口
        /// </summary>
        public void OpenWindow(string screenId) {
            windowLayer.ShowScreenById(screenId);
        }

        /// <summary>
        /// 仅通过id关闭窗口
        /// </summary>
        public void CloseWindow(string screenId) {
            windowLayer.HideScreenById(screenId);
        }
        
        /// <summary>
        /// 关闭当前的窗口
        /// </summary>
        public void CloseCurrentWindow() {
            if (windowLayer.CurrentWindow != null) {
                CloseWindow(windowLayer.CurrentWindow.ScreenId);    
            }
        }

        /// <summary>
        /// 根据id打开窗口并且传递属性参数
        /// </summary>
        public void OpenWindow<T>(string screenId, T properties) where T : IWindowProperties {
            if (windowLayer == null) {
                Debug.LogError("[UIFrame] windowLayer 未初始化，请检查 UIFrame 预制体下是否有 WindowUILayer 组件，且 UIFrame 已执行 Initialize。");
                return;
            }
            windowLayer.ShowScreenById<T>(screenId, properties);
        }

        /// <summary>
        /// 二次包装了方法，给id就显示，搜到什么算什么了
        /// </summary>
        /// <param name="screenId">The Screen id.</param>
        public void ShowScreen(string screenId) {
            Type type;
            if (IsScreenRegistered(screenId, out type)) {
                if (type == typeof(IWindowController)) {
                    OpenWindow(screenId);
                }
                else if (type == typeof(IPanelController)) {
                    ShowPanel(screenId);
                }
            }
            else {
                Debug.LogError(string.Format("Tried to open Screen id {0} but it's not registered as Window or Panel!",
                    screenId));
            }
        }

        /// <summary>
        /// 扫描层级下所有 <see cref="IScreenController"/>，以物体名为 ScreenId 注册（用于场景内已摆好 UIFrame 与各面板时，避免再 Instantiate 一套）。<br/>
        /// 须在 <see cref="Initialize"/> 之后调用（或依赖 Awake 中 initializeOnAwake 已执行 Initialize）。
        /// </summary>
        public void RegisterScreensDiscoveredInHierarchy()
        {
            var controllers = GetComponentsInChildren<IScreenController>(true);
            for (int i = 0; i < controllers.Length; i++)
            {
                var ctl = controllers[i];
                if (ctl == null) continue;
                var mb = ctl as MonoBehaviour;
                if (mb == null) continue;
                string id = mb.gameObject.name;
                RegisterScreen(id, ctl, mb.transform);
            }
        }

        /// <summary>
        /// 注册一个界面，如果传了screenTransform，就相当于制定了父节点
        /// </summary>
        public void RegisterScreen(string screenId, IScreenController controller, Transform screenTransform) {
            IWindowController window = controller as IWindowController;
            if (window != null) {
                windowLayer.RegisterScreen(screenId, window);
                if (screenTransform != null) {
                    windowLayer.ReparentScreen(controller, screenTransform);
                }

                return;
            }

            IPanelController panel = controller as IPanelController;
            if (panel != null) {
                panelLayer.RegisterScreen(screenId, panel);
                if (screenTransform != null) {
                    panelLayer.ReparentScreen(controller, screenTransform);
                }
            }
        }

        /// <summary>
        /// 注册一个面板，不注册是显示不出来的
        /// </summary>
        public void RegisterPanel<TPanel>(string screenId, TPanel controller) where TPanel : IPanelController {
            panelLayer.RegisterScreen(screenId, controller);
        }

        /// <summary>
        /// 注销一个面板
        /// </summary>
        public void UnregisterPanel<TPanel>(string screenId, TPanel controller) where TPanel : IPanelController {
            panelLayer.UnregisterScreen(screenId, controller);
        }

        /// <summary>
        /// 注册一个窗口，同理，不注册显示不出来
        /// </summary>
        public void RegisterWindow<TWindow>(string screenId, TWindow controller) where TWindow : IWindowController {
            windowLayer.RegisterScreen(screenId, controller);
        }

        /// <summary>
        /// 注销窗口
        /// </summary>
        public void UnregisterWindow<TWindow>(string screenId, TWindow controller) where TWindow : IWindowController {
            windowLayer.UnregisterScreen(screenId, controller);
        }

        /// <summary>
        /// 根据面板id检测是否开启中
        /// </summary>
        public bool IsPanelOpen(string panelId) {
            return panelLayer.IsPanelVisible(panelId);
        }

        /// <summary>
        /// 隐藏所有界面
        /// </summary>
        public void HideAll(bool animate = true) {
            CloseAllWindows(animate);
            HideAllPanels(animate);
        }

        /// <summary>
        /// 隐藏所有面板层的界面
        /// </summary>
        public void HideAllPanels(bool animate = true) {
            panelLayer.HideAll(animate);
        }

        /// <summary>
        /// 隐藏所有窗口层的界面
        /// </summary>
        public void CloseAllWindows(bool animate = true) {
            windowLayer.HideAll(animate);
        }

        /// <summary>
        /// 检查界面是否被注册过了
        /// </summary>
        public bool IsScreenRegistered(string screenId) {
            if (windowLayer.IsScreenRegistered(screenId)) {
                return true;
            }

            if (panelLayer.IsScreenRegistered(screenId)) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 跟上面一样，只不过多了个类型的返回
        /// </summary>
        public bool IsScreenRegistered(string screenId, out Type type) {
            if (windowLayer.IsScreenRegistered(screenId)) {
                type = typeof(IWindowController);
                return true;
            }

            if (panelLayer.IsScreenRegistered(screenId)) {
                type = typeof(IPanelController);
                return true;
            }

            type = null;
            return false;
        }

        private void Update() {
            ReddotManager.Instance.Update();
        }

        private void OnDestroy() {
            SceneManager.sceneLoaded -= OnSceneLoadedRecoverInput;
            if (windowLayer != null) {
                windowLayer.RequestScreenBlock -= OnRequestScreenBlock;
                windowLayer.RequestScreenUnblock -= OnRequestScreenUnblock;
            }
        }

        private void OnSceneLoadedRecoverInput(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode) {
            RecoverWindowInputAfterSceneLoad();
        }

        /// <summary>
        /// 场景加载完成后恢复窗口层射线检测（避免切场景打断窗口关闭动画导致 Raycaster 永久关闭）。
        /// </summary>
        public void RecoverWindowInputAfterSceneLoad() {
            if (windowLayer != null)
                windowLayer.ClearStaleTransitionState();
            if (graphicRaycaster != null)
                graphicRaycaster.enabled = true;
        }

        private void OnRequestScreenBlock() {
            if (graphicRaycaster != null) {
                graphicRaycaster.enabled = false;
            }
        }

        private void OnRequestScreenUnblock() {
            if (graphicRaycaster != null) {
                graphicRaycaster.enabled = true;
            }
        }
    }
}
