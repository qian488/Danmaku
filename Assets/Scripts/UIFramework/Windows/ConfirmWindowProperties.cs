using System;

namespace DemoFrameWork.UI
{
    /// <summary>
    /// 确认窗口属性：标题、内容、确认/取消按钮文案与回调。
    /// UseBackdropMask=true 时显示半透明遮罩；false 时无遮罩，点击空白处仍可关闭。
    /// </summary>
    public class ConfirmWindowProperties : WindowProperties
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string ConfirmText { get; set; } = "确定";
        public string CancelText { get; set; } = "取消";
        public Action OnConfirm { get; set; }
        public Action OnCancel { get; set; }
        /// <summary>true=显示半透明遮罩；false=无遮罩，点击空白仍可关闭。</summary>
        public bool UseBackdropMask { get; set; } = true;

        public ConfirmWindowProperties() : base(true) { }

        public ConfirmWindowProperties(string title, string message, string confirmText = "确定", string cancelText = "取消",
            Action onConfirm = null, Action onCancel = null, bool useBackdropMask = true)
            : base(true)
        {
            Title = title;
            Message = message;
            ConfirmText = confirmText;
            CancelText = cancelText;
            OnConfirm = onConfirm;
            OnCancel = onCancel;
            UseBackdropMask = useBackdropMask;
            IsPopup = useBackdropMask;
        }
    }
}
