using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DemoFrameWork;
using DemoFrameWork.GameLogic.Danmaku;
using DemoFrameWork.Generated;
using DemoFrameWork.UI;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 弹幕 Demo 启动器：挂在 Home 场景（与 <see cref="DemoGameEntry"/> 同场景）。
    /// <para>默认流程：按「主菜单清单」逐项 <c>Resources.LoadAsync</c>（见 <see cref="DanmakuResourcePreloadUtility"/>）；关卡用资源在弹幕场景由 <see cref="DanmakuGameController"/> 另表预载。</para>
    /// <para>进度条在 <see cref="_loadingBarCapUntilDone"/>（默认 80%）内反映加载进度，全部完成后再满条 → 关闭 LoadingPanel → 显示 <see cref="ScreenIds.HomePanel"/>。</para>
    /// <para>勾选 <see cref="_skipHomeAndLaunchDanmaku"/> 可跳过主菜单直接进弹幕（调试用）。</para>
    /// </summary>
    public class DanmakuLauncher : MonoBehaviour
    {
        private const string DanmakuSceneName = "Danmaku";

        [Tooltip("勾选则跳过 HomePanel，直接加载弹幕场景（调试用）")]
        [SerializeField] private bool _skipHomeAndLaunchDanmaku;

        [Tooltip(
            "主菜单阶段预加载清单（.txt）：仅含 BGM home、主界面 UI 音效、logo 等；战斗/敌弹/关卡 BGM 见 Resources 下 Demos/Danmaku/GamePreloadList。\n" +
            "每行一条 Resources 路径（无扩展名），# 为注释。若留空则尝试下方 Resources 路径。")]
        [SerializeField] private TextAsset _homePreloadManifest;

        [Tooltip(
            "当未拖入清单时：Resources 下 TextAsset 的路径（无扩展名），例如 Config/HomePreloadList。\n" +
            "与上一项二选一即可；都空则不做预加载。")]
        [SerializeField] private string _homePreloadManifestResourcesPath;

        [Tooltip("未配置清单时，Loading 条最短展示时间（秒）。0 = 下一帧直接满条并进入主界面；>0 则在这段时间内从 0 匀速拉到「条上限」再瞬间满条。")]
        [SerializeField] private float _minLoadingDisplayWhenNoPreload;

        [Tooltip(
            "预加载过程中进度条最多走到该比例（如 0.8），在全部资源真正加载完之前不会满条；全部完成后再设为 100% 并进入主界面。")]
        [SerializeField] [Range(0.5f, 0.95f)] private float _loadingBarCapUntilDone = 0.8f;

        [Tooltip("在 Console 输出预加载进度")]
        [SerializeField] private bool _debugLogPreloadProgress;

        [Header("敌弹贴图缓存（与 BulletManager 样式区间一致即可）")]
        [Tooltip("清单每项 LoadAsync 完成后会登记敌弹路径到字典；收尾再 Warmup 建索引。未列清单时由 Warmup 内按编号探测。")]
        [SerializeField] private int _bulletSpriteCacheStyleMin = 0;

        [SerializeField] private int _bulletSpriteCacheStyleMax = 18;

        private void Start()
        {
            if (_skipHomeAndLaunchDanmaku)
                StartCoroutine(LaunchGameNextFrame());
            else
                StartCoroutine(HomeEntryFlowCoroutine());
        }

        /// <summary>预加载 → 关 Loading → 开主界面。</summary>
        private IEnumerator HomeEntryFlowCoroutine()
        {
            yield return null;

            if (DemoGameEntry.UI == null)
            {
                Debug.LogError("[DanmakuLauncher] DemoGameEntry.UI 为空，请检查 DemoGameEntry / UISettings。");
                yield break;
            }

            string loadingId = DemoGameEntry.Scene != null
                ? DemoGameEntry.Scene.LoadingPanelId
                : "LoadingPanel";

            DemoGameEntry.UI.TryGetPanelController(loadingId, out var loadingCtl);
            var loadingPanel = loadingCtl as LoadingPanelController;
            if (loadingPanel == null)
            {
                Debug.LogWarning(
                    $"[DanmakuLauncher] 未找到 ScreenId \"{loadingId}\" 的 {nameof(LoadingPanelController)}，进度条不会更新。请确认 LoadingPanel 根节点挂了该脚本。");
            }

            // 切场景结束时 SceneLoadingOverlay 会 HidePanel(Loading)；若不在此再次 Show，预加载阶段进度条在「已隐藏」的 Loading 上更新，界面看起来像没加载界面。
            // 以前场景里多一套 UIFrame 时，那份 Loading 可能未被 Hide，反而误打误撞一直可见。
            if (DemoGameEntry.UI.IsScreenRegistered(loadingId))
                DemoGameEntry.UI.ShowPanel(loadingId);

            yield return PreloadHomeResourcesCoroutine(loadingPanel);

            if (DemoGameEntry.UI.IsScreenRegistered(loadingId))
                DemoGameEntry.UI.HidePanel(loadingId);

            if (!DemoGameEntry.UI.IsScreenRegistered(ScreenIds.HomePanel))
            {
                Debug.LogError(
                    "[DanmakuLauncher] 未注册 HomePanel：请在 UIFrame 下摆放 HomePanel 或在 UISettings → Screens To Register 中加入 HomePanel 预制体。");
                yield break;
            }

            DemoGameEntry.UI.ShowPanel(ScreenIds.HomePanel);
        }

        private IEnumerator PreloadHomeResourcesCoroutine(LoadingPanelController loadingPanel)
        {
            loadingPanel?.SetLoadingProgress(0f);
            yield return null;

            var manifest = ResolveManifestTextAsset();
            if (manifest == null)
            {
                yield return RunWhenNoManifest(loadingPanel);
                yield break;
            }

            if (DanmakuResourcePreloadUtility.CountValidManifestLines(manifest.text) == 0)
            {
                Debug.LogWarning("[DanmakuLauncher] 预加载清单为空（无有效行），跳过预加载。");
                yield return RunWhenNoManifest(loadingPanel);
                yield break;
            }

            yield return DanmakuResourcePreloadUtility.CoLoadManifestAsync(manifest.text, _bulletSpriteCacheStyleMin,
                _bulletSpriteCacheStyleMax, _loadingBarCapUntilDone, p => loadingPanel?.SetLoadingProgress(p),
                _debugLogPreloadProgress);

            yield return CoPrewarmDanmakuBgmsOnHomeEntry();

            if (_debugLogPreloadProgress)
                Debug.Log("[DanmakuLauncher] 预加载完成 → 1.000");
        }

        /// <summary>主菜单进入时预热关卡与 Home 的 BGM，减轻进关后首次解码尖刺。</summary>
        private static IEnumerator CoPrewarmDanmakuBgmsOnHomeEntry()
        {
            var audio = DemoGameEntry.Audio;
            if (audio == null)
                yield break;

            audio.PrewarmClipFromResources(DanmakuAudio.Paths.BgmHome);
            yield return null;
            audio.PrewarmClipFromResources(DanmakuAudio.Paths.BgmGame);
        }

        private TextAsset ResolveManifestTextAsset()
        {
            if (_homePreloadManifest != null)
                return _homePreloadManifest;

            var key = _homePreloadManifestResourcesPath;
            if (string.IsNullOrWhiteSpace(key))
                return null;

            key = key.Trim();
            return Resources.Load<TextAsset>(key);
        }

        /// <summary>未配置清单：可选最短展示时间。</summary>
        private IEnumerator RunWhenNoManifest(LoadingPanelController loadingPanel)
        {
            if (_minLoadingDisplayWhenNoPreload <= 0f)
            {
                DanmakuBulletResourceIndex.WarmupIfNeeded(_bulletSpriteCacheStyleMin, _bulletSpriteCacheStyleMax);
                yield return CoPrewarmDanmakuBgmsOnHomeEntry();
                loadingPanel?.SetLoadingProgress(1f);
                yield return null;
                if (_debugLogPreloadProgress)
                    Debug.Log("[DanmakuLauncher] 无预加载清单，已跳过（条直接满）。");
                yield break;
            }

            float dur = _minLoadingDisplayWhenNoPreload;
            float cap = _loadingBarCapUntilDone;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                loadingPanel?.SetLoadingProgress(Mathf.Clamp01(t / dur) * cap);
                yield return null;
            }

            DanmakuBulletResourceIndex.WarmupIfNeeded(_bulletSpriteCacheStyleMin, _bulletSpriteCacheStyleMax);
            yield return CoPrewarmDanmakuBgmsOnHomeEntry();
            loadingPanel?.SetLoadingProgress(1f);
        }

        private IEnumerator LaunchGameNextFrame()
        {
            yield return null;
            LaunchGame();
        }

        /// <summary>进入弹幕游戏场景（会隐藏 HomePanel）。由 HomePanel「开始游戏」按钮或调试用。</summary>
        public void LaunchGame()
        {
            if (DemoGameEntry.Scene == null)
            {
                Debug.LogError("[DanmakuLauncher] DemoGameEntry.Scene 为空，请确保场景中有 DemoGameEntry。");
                return;
            }

            if (DemoGameEntry.UI != null && DemoGameEntry.UI.IsScreenRegistered(ScreenIds.HomePanel))
                DemoGameEntry.UI.HidePanel(ScreenIds.HomePanel);

            DemoGameEntry.Scene.LoadScene(DanmakuSceneName);
        }

        /// <summary>与 <see cref="LaunchGame"/> 相同，保留给旧按钮引用。</summary>
        public void Launch() => LaunchGame();
    }
}
