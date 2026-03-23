using UnityEngine;
using DemoFrameWork;
using DemoFrameWork.UI;

namespace DemoFrameWork.Scene
{
    /// <summary>
    /// 框架级组件：绑定 <see cref="GameSceneManager"/> 的 Loading 回调，负责通过 UIFrame 显示 <c>LoadingPanel</c>、刷新进度与加载 BGM。<br/>
    /// 须在 <see cref="UISettings"/> 中注册与 <see cref="SceneSettings.loadingPanelId"/> 一致的预制体（根节点 <see cref="LoadingPanelController"/>）。
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneLoadingOverlay : MonoBehaviour
    {
        [Tooltip("Resources 路径（无扩展名），默认通用加载 BGM")]
        [SerializeField] private string _loadingBgmResourcePath = "Audio/BGM/1：0.618(loop)";

        [Tooltip("开始加载时停止当前主 BGM，避免与加载音乐重叠")]
        [SerializeField] private bool _stopMainBgmWhenLoading = true;

        [Tooltip("加载 BGM 音量 0~1")]
        [SerializeField] [Range(0f, 1f)] private float _loadingBgmVolume = 0.75f;

        private GameSceneManager _scene;
        private string _loadingPanelId = "LoadingPanel";
        private LoadingPanelController _loadingPanel;
        private bool _loadingPanelShownForCurrentLoad;

        private AudioSource _loadingBgm;

        public void Initialize(GameSceneManager scene)
        {
            if (scene == null) return;
            _scene = scene;
            _loadingPanelId = scene.LoadingPanelId;

            _loadingPanel = null;
            if (DemoGameEntry.UI == null)
            {
                Debug.LogWarning(
                    "[SceneLoadingOverlay] DemoGameEntry.UI 为空，无法显示 Loading 面板与加载 BGM。请配置 UISettings / UIFrame。");
            }
            else if (!DemoGameEntry.UI.TryGetPanelController(_loadingPanelId, out var ctl))
            {
                Debug.LogWarning(
                    $"[SceneLoadingOverlay] 未注册 ScreenId \"{_loadingPanelId}\" 的面板。请在 UISettings → Screens To Register 中加入 LoadingPanel 预制体（根节点挂 {nameof(LoadingPanelController)}）。");
            }
            else
            {
                _loadingPanel = ctl as LoadingPanelController;
                if (_loadingPanel == null)
                {
                    Debug.LogWarning(
                        $"[SceneLoadingOverlay] 面板 \"{_loadingPanelId}\" 根节点须挂 {nameof(LoadingPanelController)}。");
                }
            }

            _scene.OnShowLoading += OnShowLoading;
            _scene.OnHideLoading += OnHideLoading;
        }

        private void OnDestroy()
        {
            if (_scene != null)
            {
                _scene.OnShowLoading -= OnShowLoading;
                _scene.OnHideLoading -= OnHideLoading;
            }
        }

        private void OnShowLoading(float progress)
        {
            if (_loadingPanel == null || DemoGameEntry.UI == null)
                return;

            if (!_loadingPanelShownForCurrentLoad)
            {
                DemoGameEntry.UI.ShowPanel(_loadingPanelId);
                _loadingPanelShownForCurrentLoad = true;
                if (_stopMainBgmWhenLoading && DemoGameEntry.Audio != null)
                    DemoGameEntry.Audio.StopBGM(0f);
                EnsureLoadingBgmSource();
                TryPlayLoadingBgm();
            }

            _loadingPanel.SetLoadingProgress(progress);
        }

        private void OnHideLoading()
        {
            _loadingPanelShownForCurrentLoad = false;

            if (_loadingPanel == null || DemoGameEntry.UI == null)
                return;

            StopLoadingBgm();
            DemoGameEntry.UI.HidePanel(_loadingPanelId);
        }

        private void EnsureLoadingBgmSource()
        {
            if (_loadingBgm != null) return;
            _loadingBgm = gameObject.AddComponent<AudioSource>();
            _loadingBgm.playOnAwake = false;
            _loadingBgm.loop = true;
            _loadingBgm.spatialBlend = 0f;
        }

        private void TryPlayLoadingBgm()
        {
            if (_loadingBgm == null) return;
            if (_loadingBgm.clip == null && !string.IsNullOrEmpty(_loadingBgmResourcePath))
                _loadingBgm.clip = Resources.Load<AudioClip>(_loadingBgmResourcePath);
            if (_loadingBgm.clip != null && !_loadingBgm.isPlaying)
            {
                _loadingBgm.loop = true;
                _loadingBgm.volume = _loadingBgmVolume;
                _loadingBgm.Play();
            }
        }

        private void StopLoadingBgm()
        {
            if (_loadingBgm == null) return;
            _loadingBgm.Stop();
            _loadingBgm.clip = null;
        }
    }
}
