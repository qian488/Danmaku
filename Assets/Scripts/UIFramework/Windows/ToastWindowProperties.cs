using UnityEngine;

namespace DemoFrameWork.UI
{
    /// <summary>
    /// Toast 窗口属性：淡入淡出、自动消失。
    /// 可选是否显示背景遮罩；无遮罩时仍可点击空白区域关闭（若预制体有 BackdropClosesWindow）。
    /// </summary>
    public class ToastWindowProperties : WindowProperties
    {
        public string Message { get; set; }
        public float Duration { get; set; } = 2f;

        public ToastWindowProperties() : base(true) { }

        public ToastWindowProperties(string message, float duration = 2f, bool useBackdropMask = false)
            : base(true)
        {
            Message = message;
            Duration = duration;
            IsPopup = useBackdropMask;
        }
    }
}
