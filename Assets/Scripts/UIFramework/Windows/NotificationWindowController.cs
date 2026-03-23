using UnityEngine;
using TMPro;

namespace DemoFrameWork.UI
{
    /// <summary>
    /// 通知窗口：缩放动画，可选遮罩，点击空白关闭。
    /// 遮罩由代码根据 UseBackdropMask 动态创建：启用则全屏半透明黑，不启用则透明可点击关闭。
    /// 预制体需有：AnimIn/AnimOut（ScaleBounce）、titleText、message 下可滚动的 text（TMP_Text）。
    /// </summary>
    public class NotificationWindowController : WindowController<NotificationWindowProperties>
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;

        protected override void Awake()
        {
            base.Awake();
            if (titleText == null) TryFind(ref titleText, "titleText");
            if (messageText == null)
            {
                var content = transform.Find("message")?.Find("Viewport")?.Find("Content");
                if (content != null)
                {
                    var textTr = content.Find("text");
                    if (textTr != null) messageText = textTr.GetComponent<TMP_Text>();
                }
            }
        }

        private void TryFind<T>(ref T target, string childName) where T : Component
        {
            var t = transform.Find(childName);
            if (t != null) target = t.GetComponent<T>();
        }

        protected override void OnPropertiesSet()
        {
            if (Properties == null) return;

            if (titleText != null) titleText.text = Properties.Title ?? string.Empty;
            if (messageText != null) messageText.text = Properties.Message ?? string.Empty;

            EnsureBackdrop();
        }

        private void EnsureBackdrop()
        {
            var existing = transform.Find("Backdrop");
            if (existing != null) Destroy(existing.gameObject);
            BackdropClosesWindow.CreateBackdrop(transform, Properties.UseBackdropMask);
        }
    }
}
