using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DemoFrameWork;

namespace DemoFrameWork.Scene
{
    /// <summary>
    /// 场景管理：同步/异步切换、Loading 委托、通过 DemoGameEntry.Scene 访问。
    /// 类名避免与 UnityEngine.SceneManagement.SceneManager 冲突。
    /// </summary>
    public class GameSceneManager : MonoBehaviour
    {
        private bool _isLoading;
        private string _currentSceneName;
        private SceneSettings _settings;

        /// <summary>
        /// 为 true 表示本次切场景在激活后刻意未调用 <see cref="OnHideLoading"/>，须由目标场景（如弹幕关）预载结束后再
        /// <see cref="CompleteDeferredLoadingHide"/>。在 yield 激活新场景之前置位，以便新场景 Awake 可读。
        /// </summary>
        private bool _loadingHideDeferred;

        public string CurrentSceneName => _currentSceneName;
        public bool IsLoading => _isLoading;

        /// <summary>是否与「切场景 Loading」连贯、尚未 <see cref="CompleteDeferredLoadingHide"/>。</summary>
        public bool IsLoadingHideDeferred => _loadingHideDeferred;

        /// <summary>与 <see cref="SceneSettings.loadingPanelId"/> 一致，供 <see cref="SceneLoadingOverlay"/> 在 UIFrame 中查找/显示加载面板。</summary>
        public string LoadingPanelId =>
            _settings != null && !string.IsNullOrEmpty(_settings.loadingPanelId)
                ? _settings.loadingPanelId
                : "LoadingPanel";

        /// <summary>显示 Loading UI，参数为进度 0~1。</summary>
        public Action<float> OnShowLoading { get; set; }

        /// <summary>隐藏 Loading UI。</summary>
        public Action OnHideLoading { get; set; }

        /// <summary>可选：切场景前淡入（如全屏黑），完成后调用 onComplete。</summary>
        public Action<Action> OnFadeInBeforeLoad { get; set; }

        /// <summary>可选：加载完成后淡出，完成后调用 onComplete。</summary>
        public Action<Action> OnFadeOutAfterLoad { get; set; }

        /// <summary>同步切换场景（内部用异步并等待完成）。默认显示 Loading 遮罩与进度。</summary>
        public void LoadScene(string sceneName, bool showLoading = true)
        {
            if (_isLoading) return;
            StartCoroutine(LoadCoroutine(sceneName, null, null, showLoading));
            // 同步调用时由协程内部跑完，调用方不等待
        }

        /// <summary>异步切换场景，带进度与完成回调。</summary>
        public void LoadSceneAsync(string sceneName,
            Action<float> onProgress = null,
            Action onDone = null,
            bool showLoading = true)
        {
            if (_isLoading) return;
            StartCoroutine(LoadCoroutine(sceneName, onProgress, onDone, showLoading));
        }

        /// <summary>重载当前场景。</summary>
        public void ReloadCurrentScene(bool showLoading = true)
        {
            if (string.IsNullOrEmpty(_currentSceneName)) return;
            LoadScene(_currentSceneName, showLoading);
        }

        /// <summary>
        /// 关闭因 <see cref="SceneSettings.deferLoadingHideUntilScriptSceneNames"/> 而延后的 Loading 面板；若当前未延后则无操作。
        /// </summary>
        public void CompleteDeferredLoadingHide()
        {
            if (!_loadingHideDeferred)
                return;
            _loadingHideDeferred = false;
            OnHideLoading?.Invoke();
        }

        private bool ShouldDeferLoadingHideAfterSceneLoad(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || _settings == null)
                return false;

            var extra = _settings.deferLoadingHideUntilScriptSceneNames;
            if (extra == null || extra.Length == 0)
                return false;

            foreach (var n in extra)
            {
                if (!string.IsNullOrEmpty(n) && string.Equals(n, sceneName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private IEnumerator LoadCoroutine(string sceneName,
            Action<float> onProgress,
            Action onDone,
            bool showLoading)
        {
            _isLoading = true;

            if (_loadingHideDeferred)
            {
                _loadingHideDeferred = false;
                OnHideLoading?.Invoke();
            }

            if (OnFadeInBeforeLoad != null)
            {
                bool fadeDone = false;
                OnFadeInBeforeLoad(() => fadeDone = true);
                while (!fadeDone)
                    yield return null;
            }

            if (showLoading)
                OnShowLoading?.Invoke(0f);

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[GameSceneManager] LoadSceneAsync failed: {sceneName}");
                _isLoading = false;
                OnHideLoading?.Invoke();
                onDone?.Invoke();
                yield break;
            }
            op.allowSceneActivation = false;

            bool deferHideAfterActivation = showLoading && ShouldDeferLoadingHideAfterSceneLoad(sceneName);

            // Unity：allowSceneActivation=false 时 op.progress 只到 0.9；0.9→1 发生在「允许激活」之后。
            // 若把 0~0.9 映射成 UI 的 0~100%，会在「资源已加载完」就显示满条，而 yield return op 仍在等场景激活（Awake/首帧），造成「条满了却卡很久」。
            // 因此：加载阶段 UI 只走到 0.9；激活完成后再报告 1 并关闭 Loading。
            while (op.progress < 0.9f)
            {
                float p = Mathf.Clamp01(op.progress / 0.9f) * 0.9f;
                if (showLoading)
                    OnShowLoading?.Invoke(p);
                onProgress?.Invoke(p);
                yield return null;
            }

            if (showLoading)
                OnShowLoading?.Invoke(0.9f);
            onProgress?.Invoke(0.9f);

            op.allowSceneActivation = true;
            if (deferHideAfterActivation)
                _loadingHideDeferred = true;

            yield return op;

            _currentSceneName = sceneName;

            if (_settings != null && _settings.clearIdlePoolAfterSceneLoad && DemoGameEntry.Pool != null)
                DemoGameEntry.Pool.ClearAll();

            onProgress?.Invoke(1f);
            if (showLoading)
            {
                if (deferHideAfterActivation)
                    OnShowLoading?.Invoke(0.9f);
                else
                    OnHideLoading?.Invoke();
            }

            if (OnFadeOutAfterLoad != null)
            {
                bool fadeDone = false;
                OnFadeOutAfterLoad(() => fadeDone = true);
                while (!fadeDone)
                    yield return null;
            }

            _isLoading = false;
            onDone?.Invoke();
        }

        public void Initialize(SceneSettings settings = null)
        {
            _settings = settings;
            _currentSceneName = SceneManager.GetActiveScene().name;
        }

        public static GameSceneManager Create(GameObject host, SceneSettings settings = null)
        {
            var sm = host.AddComponent<GameSceneManager>();
            sm.Initialize(settings);
            return sm;
        }
    }
}
