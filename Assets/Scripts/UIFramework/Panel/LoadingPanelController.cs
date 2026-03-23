using UnityEngine;
using UnityEngine.UI;

namespace DemoFrameWork.UI
{
    /// <summary>
    /// 框架级场景加载面板：挂在 <b>LoadingPanel</b> 预制体根节点，在 <see cref="UISettings"/> 的 Screens To Register 中注册，
    /// ScreenId 与预制体资源名一致（默认与 <see cref="DemoFrameWork.Scene.SceneSettings.loadingPanelId"/>：<c>LoadingPanel</c>）。<br/>
    /// 由 <see cref="DemoFrameWork.Scene.SceneLoadingOverlay"/> 在异步切场景时 Show/Hide 并调用 <see cref="SetLoadingProgress"/>。
    /// </summary>
    public class LoadingPanelController : APanelController<LoadingPanelProperties>
    {
        [Tooltip("可选：仅「不用 Slider、单独用 Image」时填写，且 Image Type 须为 Filled。使用标准 Slider 时请留空，勿把 Slider 子物体 Fill 拖到这里。")]
        [SerializeField] private Image _progressFill;

        [Tooltip("推荐：Unity Slider（min=0 max=1）。进度由 Slider 驱动 Fill 区域宽度；与上面 Fill 二选一即可。")]
        [SerializeField] private Slider _progressSlider;

        [Tooltip("勾选后在 Console 输出每次 SetLoadingProgress 的数值，用于确认是否在刷新")]
        [SerializeField] private bool _debugLogProgress;

        private bool _warnedNoRefs;
        private bool _warnedFillType;
        private bool _warnedBothRefs;

        protected override void OnPropertiesSet()
        {
            if (Properties != null)
                Properties.Priority = PanelPriority.Blocker;
        }

        /// <summary>由 <see cref="DemoFrameWork.Scene.SceneLoadingOverlay"/> 或主场景预加载逻辑调用。</summary>
        public void SetLoadingProgress(float progress)
        {
            float p = Mathf.Clamp01(progress);

            if (_progressFill == null && _progressSlider == null && !_warnedNoRefs)
            {
                _warnedNoRefs = true;
                Debug.LogWarning(
                    "[LoadingPanelController] 未绑定 Progress Fill 或 Progress Slider，进度不会显示。请在 LoadingPanel 根节点此组件上拖入 Slider 或 Filled Image。");
            }

            if (_progressSlider != null && _progressFill != null && !_warnedBothRefs)
            {
                _warnedBothRefs = true;
                Debug.Log(
                    "[LoadingPanelController] 同时绑定了 Slider 与 Progress Fill，将只使用 Slider。请把 Progress Fill 留空（不要把 Slider 下的 Fill 拖进 Fill 槽）。");
            }

            // 标准 Slider：子物体 Fill 多为 Simple/Sliced，由 Slider.value 改 FillRect 宽度，与 Image.fillAmount 无关
            if (_progressSlider != null)
            {
                _progressSlider.value = p;
            }
            else if (_progressFill != null)
            {
                if (_progressFill.type != Image.Type.Filled && !_warnedFillType)
                {
                    _warnedFillType = true;
                    Debug.LogWarning(
                        "[LoadingPanelController] Progress Fill 的 Image Type 不是 Filled，fillAmount 无效。请改为 Filled，或改用 Slider。");
                }
                _progressFill.fillAmount = p;
            }

            if (_debugLogProgress)
                Debug.Log($"[LoadingPanelController] progress={p:F3} (fill={_progressFill != null}, slider={_progressSlider != null})");
        }

        protected override void AddListeners()
        {
        }

        protected override void RemoveListeners()
        {
        }
    }
}
