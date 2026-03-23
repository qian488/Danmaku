using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DemoFrameWork.UI
{
    /// <summary>
    /// 确认窗口：缩放动画，可选遮罩，点击空白关闭。
    /// 遮罩由代码根据 UseBackdropMask 动态创建：启用则全屏半透明黑，不启用则透明可点击关闭。
    /// 预制体需有：AnimIn/AnimOut（ScaleBounce）、message（TMP_Text）、confirmBtn、concelBtn（取消）。
    /// </summary>
    public class ConfirmWindowController : WindowController<ConfirmWindowProperties>
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text confirmBtnText;
        [SerializeField] private TMP_Text cancelBtnText;
        [SerializeField] private Button confirmBtn;
        [SerializeField] private Button cancelBtn;

        protected override void Awake()
        {
            base.Awake();
            if (messageText == null) TryFind(ref messageText, "message");
            if (confirmBtn == null) TryFind(ref confirmBtn, "confirmBtn");
            if (cancelBtn == null) TryFind(ref cancelBtn, "concelBtn");
            if (confirmBtnText == null && confirmBtn != null) confirmBtnText = confirmBtn.GetComponentInChildren<TMP_Text>();
            if (cancelBtnText == null && cancelBtn != null) cancelBtnText = cancelBtn.GetComponentInChildren<TMP_Text>();
        }

        private void TryFind<T>(ref T target, string childName) where T : Component
        {
            var t = transform.Find(childName);
            if (t != null) target = t.GetComponent<T>();
        }

        protected override void AddListeners()
        {
            if (confirmBtn != null) confirmBtn.onClick.AddListener(OnConfirmClick);
            if (cancelBtn != null) cancelBtn.onClick.AddListener(OnCancelClick);
        }

        protected override void RemoveListeners()
        {
            if (confirmBtn != null) confirmBtn.onClick.RemoveListener(OnConfirmClick);
            if (cancelBtn != null) cancelBtn.onClick.RemoveListener(OnCancelClick);
        }

        protected override void OnPropertiesSet()
        {
            if (Properties == null) return;

            if (titleText != null) titleText.text = Properties.Title ?? string.Empty;
            if (messageText != null) messageText.text = Properties.Message ?? string.Empty;
            if (confirmBtnText != null) confirmBtnText.text = Properties.ConfirmText ?? "确定";
            if (cancelBtnText != null) cancelBtnText.text = Properties.CancelText ?? "取消";

            EnsureBackdrop();
        }

        private void EnsureBackdrop()
        {
            var existing = transform.Find("Backdrop");
            if (existing != null) Destroy(existing.gameObject);
            BackdropClosesWindow.CreateBackdrop(transform, Properties.UseBackdropMask);
        }

        private void OnConfirmClick()
        {
            Properties?.OnConfirm?.Invoke();
            UI_Close();
        }

        private void OnCancelClick()
        {
            Properties?.OnCancel?.Invoke();
            UI_Close();
        }
    }
}
