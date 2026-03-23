using UnityEngine;
using UnityEngine.UI;
using DemoFrameWork;
using DemoFrameWork.GameLogic.Danmaku;
using DemoFrameWork.Generated;
using DemoFrameWork.UI;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 弹幕暂停窗口（WindowController）。
    /// 由 DanmakuGameController 在暂停时打开；预制体根节点名须为 <c>PauseWindow</c>（与 <see cref="ScreenIds.PauseWindow"/> 一致）。
    /// </summary>
    public class DanmakuPauseWindow : WindowController<DanmakuPauseWindowProperties>
    {
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _returnHomeButton;

        protected override void AddListeners()
        {
            if (_resumeButton     != null) _resumeButton.onClick.AddListener(OnResumeClicked);
            if (_returnHomeButton != null) _returnHomeButton.onClick.AddListener(OnReturnHomeClicked);
        }

        protected override void RemoveListeners()
        {
            if (_resumeButton     != null) _resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (_returnHomeButton != null) _returnHomeButton.onClick.RemoveListener(OnReturnHomeClicked);
        }

        private void OnResumeClicked()
        {
            DanmakuAudio.PlayUiCancelSfx();
            UI_Close();
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.ResumeRequested);
        }

        private void OnReturnHomeClicked()
        {
            DanmakuAudio.PlayUiConfirmSfx();
            UI_Close();
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.ReturnHomeRequested);
        }

        /// <summary>从外部打开暂停窗口的静态入口。</summary>
        public static void Open()
        {
            DemoGameEntry.UI?.OpenWindow(ScreenIds.PauseWindow,
                new DanmakuPauseWindowProperties());
        }
    }
}
