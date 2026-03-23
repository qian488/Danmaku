using System.Collections;
using UnityEngine;
using TMPro;

namespace DemoFrameWork.UI
{
    /// <summary>
    /// Toast 窗口：淡入淡出，显示一段时间后自动关闭。
    /// 预制体需有：AnimIn（FadeIn）、AnimOut（FadeOut）、名为 message 的 TMP_Text。
    /// 可选：带 BackdropClosesWindow 的遮罩节点，打开时通过 Properties.IsPopup 控制是否显示遮罩。
    /// </summary>
    public class ToastWindowController : WindowController<ToastWindowProperties>
    {
        private TMP_Text messageText;
        private Coroutine autoCloseRoutine;

        protected override void Awake()
        {
            base.Awake();
            var t = transform.Find("message");
            if (t != null) messageText = t.GetComponent<TMP_Text>();
        }

        protected override void OnPropertiesSet()
        {
            if (messageText != null && Properties != null)
                messageText.text = Properties.Message ?? string.Empty;

            if (autoCloseRoutine != null) StopCoroutine(autoCloseRoutine);
            autoCloseRoutine = StartCoroutine(AutoCloseAfter(Properties != null ? Properties.Duration : 2f));
        }

        private IEnumerator AutoCloseAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            autoCloseRoutine = null;
            UI_Close();
        }

        protected override void WhileHiding()
        {
            if (autoCloseRoutine != null)
            {
                StopCoroutine(autoCloseRoutine);
                autoCloseRoutine = null;
            }
        }
    }
}
