namespace DemoFrameWork.UI
{
    /// <summary>
    /// 通知窗口属性：标题、正文。
    /// UseBackdropMask=true 时显示半透明遮罩；false 时无遮罩，点击空白处仍可关闭。
    /// </summary>
    public class NotificationWindowProperties : WindowProperties
    {
        public string Title { get; set; }
        public string Message { get; set; }
        /// <summary>true=显示半透明遮罩；false=无遮罩，点击空白仍可关闭。</summary>
        public bool UseBackdropMask { get; set; } = true;

        public NotificationWindowProperties() : base(true) { }

        public NotificationWindowProperties(string title, string message, bool useBackdropMask = true)
            : base(true)
        {
            Title = title;
            Message = message;
            UseBackdropMask = useBackdropMask;
            IsPopup = useBackdropMask;
        }
    }
}
