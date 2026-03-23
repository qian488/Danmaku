using UnityEngine;
using UnityEngine.UI;

namespace DemoFrameWork.UI
{
    /// <summary>
    /// 挂到“点击空白处关闭”的遮罩节点上（如 BlackCurtain、bg、Backdrop）。
    /// 会自动确保有 Button 并绑定父级 IWindowController 的关闭；未挂此组件的窗口不会有点击空白关闭行为。
    /// 也可通过 CreateBackdrop 在代码中动态创建全屏遮罩（可选是否显示黑色）。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BackdropClosesWindow : MonoBehaviour
    {
        /// <summary>
        /// 在 parent 下创建全屏点击关闭遮罩，作为第一个子节点。withVisibleMask 为 true 时显示半透明黑，为 false 时透明但可点击。
        /// </summary>
        public static GameObject CreateBackdrop(Transform parent, bool withVisibleMask)
        {
            var go = new GameObject("Backdrop");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            var image = go.AddComponent<Image>();
            image.color = withVisibleMask ? new Color(0f, 0f, 0f, 0.5f) : new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            go.AddComponent<BackdropClosesWindow>();
            return go;
        }
        Button _button;
        IWindowController _window;

        void Awake()
        {
            _window = GetComponentInParent<IWindowController>();
            if (_window == null) return;

            _button = GetComponent<Button>();
            if (_button == null) _button = gameObject.AddComponent<Button>();

            _button.onClick.AddListener(OnBackdropClick);
        }

        void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnBackdropClick);
        }

        void OnBackdropClick()
        {
            _window?.UI_Close();
        }
    }
}
