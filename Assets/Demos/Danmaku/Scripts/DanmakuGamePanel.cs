using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DemoFrameWork;
using DemoFrameWork.GameLogic.Danmaku;
using DemoFrameWork.UI;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 挂在 <b>GamePanel</b> 预制体根节点上（ScreenId：<c>GamePanel</c>）。
    /// <para><b>布局约定</b>：<c>RewardArea</c> 为掉落物收集区域；<c>hp</c> 残机；顶部阶段进度；右侧 Max 为<b>当前难度存档最高分</b>（<see cref="DanmakuScorePersistence"/>），Score 为当前局分数。</para>
    /// 未绑定的引用不会刷新，逻辑仍可照常运行。
    /// </summary>
    public class DanmakuGamePanel : APanelController<DanmakuGamePanelProperties>
    {
        [Header("左侧：血量（残机） / 阶段进度")]
        [Tooltip("建议 Whole Numbers，表示剩余残机数")]
        [SerializeField] private Slider _hpSlider;

        [Tooltip("0~1，与关卡最后一项任务的 TriggerTime 对应")]
        [SerializeField] private Slider _stageProgressSlider;

        [Header("暂停")]
        [Tooltip("点击后暂停并打开 PauseWindow；空则按子物体名 pause 查找 Button")]
        [SerializeField] private Button _pauseButton;

        [Header("Reward 收集区（Rect 用于判定；开局仅闪烁 line 装饰）")]
        [SerializeField] private RectTransform _rewardArea;

        [Tooltip("RewardArea 下的竖线等装饰；空则按子物体名 line 查找")]
        [SerializeField] private GameObject _rewardLine;

        [Tooltip("line 闪烁总时长（秒），约 2s 后关掉装饰")]
        [SerializeField] private float _rewardDecorFlashTotalSeconds = 2f;

        [Tooltip("单次「亮」时长")]
        [SerializeField] private float _rewardFlashOnSeconds = 0.12f;

        [Tooltip("单次「暗」时长")]
        [SerializeField] private float _rewardFlashOffSeconds = 0.1f;

        [Header("右侧：数值")]
        [SerializeField] private TMP_Text _maxScoreText;
        [SerializeField] private TMP_Text _rightScoreText;
        [SerializeField] private TMP_Text _powerText;
        [SerializeField] private TMP_Text _grazeText;

        [Header("可选：旧版文案 / 备用")]
        [SerializeField] private TMP_Text _livesText;
        [SerializeField] private TMP_Text _bombsText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private TMP_Text _bulletCountText;

        [Header("HUD 角色立绘")]
        [Tooltip("角色 0 显示、角色 1 隐藏；空则按子物体名 Playerlogo1 查找")]
        [SerializeField] private GameObject _playerLogo1;

        [Tooltip("角色 1 显示、角色 0 隐藏；空则按子物体名 Playerlogo2 查找")]
        [SerializeField] private GameObject _playerLogo2;

        private float _elapsedTime;
        private bool _running;
        private int _lastPowerForSfx = int.MinValue;
        private DanmakuGameController _gameCtrl;
        private Coroutine _rewardIntroRoutine;
        private Coroutine _coRewardIntroWait;
        private bool _rewardIntroPending;

        [Header("HUD 刷新（减负 Canvas）")]
        [Tooltip("阶段条数值变化低于该阈值时不写 Slider，减少布局重建")]
        [SerializeField] private float _stageProgressDirtyEpsilon = 0.002f;

        private float _lastStageProgressShown = -1f;
        private int _lastElapsedWholeSeconds = int.MinValue;

        /// <summary>开局闪烁结束后仍保留，用于掉落物飞向收集区的 Rect 判定（世界坐标用 RectTransform 转边界）。</summary>
        public static RectTransform RewardCollectionRect { get; private set; }

        /// <summary>
        /// 玩法层世界坐标是否落在 Reward 收集区内（用于掉落物进栏判定）。<br/>
        /// 依赖 <see cref="RewardCollectionRect"/> 与所属 Canvas（Overlay 时传 null 相机给 RectTransformUtility）。
        /// </summary>
        public static bool IsWorldPointInRewardCollectionZone(Vector3 worldPos, Camera gameplayCamera)
        {
            if (RewardCollectionRect == null || gameplayCamera == null) return false;

            var canvas = RewardCollectionRect.GetComponentInParent<Canvas>();
            if (canvas == null) return false;

            Vector2 screenPoint = gameplayCamera.WorldToScreenPoint(worldPos);
            Camera uiEventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(RewardCollectionRect, screenPoint, uiEventCamera);
        }

        protected override void AddListeners()
        {
            EnsurePauseButtonRef();
            if (_pauseButton != null)
                _pauseButton.onClick.AddListener(OnPauseButtonClicked);

            DemoGameEntry.Event.AddEventListener<int>(DanmakuEventNames.PlayerDamaged, OnPlayerDamaged);
            DemoGameEntry.Event.AddEventListener<int>(DanmakuEventNames.BombUsed, OnBombUsed);
            DemoGameEntry.Event.AddEventListener<int>(DanmakuEventNames.ScoreChanged, OnScoreChanged);
            DemoGameEntry.Event.AddEventListener<bool>(DanmakuEventNames.PauseChanged, OnPauseChanged);
            DemoGameEntry.Event.AddEventListener<int>(DanmakuEventNames.PowerChanged, OnPowerChanged);
            DemoGameEntry.Event.AddEventListener<int>(DanmakuEventNames.GrazeChanged, OnGrazeChanged);
            // 占坑：EventCenter 需先有人监听才会注册事件名，否则触发 GameOver 会报「尝试触发不存在的事件」
            DemoGameEntry.Event.AddEventListener<int>(DanmakuEventNames.GameOver, OnGameOverBroadcast);
        }

        protected override void RemoveListeners()
        {
            if (_pauseButton != null)
                _pauseButton.onClick.RemoveListener(OnPauseButtonClicked);

            DemoGameEntry.Event.RemoveEventListener<int>(DanmakuEventNames.PlayerDamaged, OnPlayerDamaged);
            DemoGameEntry.Event.RemoveEventListener<int>(DanmakuEventNames.BombUsed, OnBombUsed);
            DemoGameEntry.Event.RemoveEventListener<int>(DanmakuEventNames.ScoreChanged, OnScoreChanged);
            DemoGameEntry.Event.RemoveEventListener<bool>(DanmakuEventNames.PauseChanged, OnPauseChanged);
            DemoGameEntry.Event.RemoveEventListener<int>(DanmakuEventNames.PowerChanged, OnPowerChanged);
            DemoGameEntry.Event.RemoveEventListener<int>(DanmakuEventNames.GrazeChanged, OnGrazeChanged);
            DemoGameEntry.Event.RemoveEventListener<int>(DanmakuEventNames.GameOver, OnGameOverBroadcast);
        }

        /// <summary>结算时再写一次最高分（与 <see cref="DanmakuGameOverWindow"/> 内写入重复但可兜底）。</summary>
        private void OnGameOverBroadcast(int finalScore)
        {
            DanmakuScorePersistence.TryUpdateHighScore(DanmakuRunSettings.Difficulty, finalScore, out _);
            RefreshBestScoreText();
        }

        private void EnsurePlayerLogoRefs()
        {
            if (_playerLogo1 == null)
            {
                var t = FindChildTransformByName(transform, "Playerlogo1");
                if (t != null) _playerLogo1 = t.gameObject;
            }
            if (_playerLogo2 == null)
            {
                var t = FindChildTransformByName(transform, "Playerlogo2");
                if (t != null) _playerLogo2 = t.gameObject;
            }
        }

        /// <summary>
        /// 与主菜单一致：<see cref="DanmakuRunSettings.CharacterIndex"/> 0→角色1（player/1、Home 的 playerImg1），1→角色2。
        /// </summary>
        private void RefreshPlayerLogoVisibility()
        {
            int i = DanmakuRunSettings.CharacterIndex;
            if (_playerLogo1 != null) _playerLogo1.SetActive(i == 0);
            if (_playerLogo2 != null) _playerLogo2.SetActive(i == 1);
        }

        private static Transform FindChildTransformByName(Transform root, string goName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == goName) return t;
            }
            return null;
        }

        private void EnsurePauseButtonRef()
        {
            if (_pauseButton != null) return;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "pause") continue;
                var btn = t.GetComponent<Button>();
                if (btn != null)
                {
                    _pauseButton = btn;
                    return;
                }
            }
        }

        private void OnPauseButtonClicked()
        {
            DemoGameEntry.Event?.EventTrigger(DanmakuEventNames.PauseRequested);
        }

        private void BindRewardZoneDelegate()
        {
            DanmakuRewardZone.IsWorldPointInZone = worldPos =>
                IsWorldPointInRewardCollectionZone(worldPos, Camera.main);
        }

        private void OnDisable()
        {
            DanmakuRewardZone.IsWorldPointInZone = null;
        }

        private void OnEnable()
        {
            ConfigureMobileLayoutIfNeeded();
            BindRewardZoneDelegate();
            if (_rewardIntroPending)
                TryStartRewardIntroWhenReady();
        }

        protected override void OnPropertiesSet()
        {
            ConfigureMobileLayoutIfNeeded();
            _gameCtrl = UnityEngine.Object.FindObjectOfType<DanmakuGameController>();
            _elapsedTime = 0f;
            _running = true;
            _lastPowerForSfx = int.MinValue;

            DanmakuRunSettings.GetStartingLivesAndBombs(out int startLives, out int startBombs);

            if (_hpSlider != null)
            {
                _hpSlider.minValue = 0f;
                _hpSlider.maxValue = Mathf.Max(1, startLives);
                _hpSlider.wholeNumbers = true;
                _hpSlider.value = startLives;
            }

            if (_stageProgressSlider != null)
            {
                _stageProgressSlider.minValue = 0f;
                _stageProgressSlider.maxValue = 1f;
                _stageProgressSlider.value = 0f;
                _lastStageProgressShown = 0f;
            }
            else
                _lastStageProgressShown = -1f;

            if (_livesText != null) _livesText.text = $"残机 ×{startLives}";
            if (_bombsText != null) _bombsText.text = $"炸弹 ×{startBombs}";
            if (_scoreText != null) _scoreText.text = "分数 00000000";
            if (_rightScoreText != null) _rightScoreText.text = new string('0', 8);
            RefreshBestScoreText();
            if (_powerText != null) _powerText.text = "0";
            if (_grazeText != null) _grazeText.text = "0";

            _lastElapsedWholeSeconds = Mathf.FloorToInt(_elapsedTime);
            UpdateTimeText();

            if (_rewardArea != null)
                RewardCollectionRect = _rewardArea;
            else
                RewardCollectionRect = null;

            BindRewardZoneDelegate();

            EnsurePlayerLogoRefs();
            RefreshPlayerLogoVisibility();

            // UIScreenController.Show 先执行 OnPropertiesSet，再在 DoAnimation 里 SetActive(true)；此处可能仍为 inactive，不能 StartCoroutine。
            if (_coRewardIntroWait != null)
            {
                StopCoroutine(_coRewardIntroWait);
                _coRewardIntroWait = null;
            }

            if (_rewardIntroRoutine != null)
            {
                StopCoroutine(_rewardIntroRoutine);
                _rewardIntroRoutine = null;
            }

            _rewardIntroPending = _rewardArea != null;
            TryStartRewardIntroWhenReady();
        }

        private void ConfigureMobileLayoutIfNeeded()
        {
            DanmakuMobileLayoutUtility.ConfigureScreen(transform);
        }

        /// <summary>仅在 GameObject 已激活时启动「等两帧再闪 line」协程；否则挂起，由 <see cref="OnEnable"/> 再试。</summary>
        private void TryStartRewardIntroWhenReady()
        {
            if (!_rewardIntroPending || _rewardArea == null) return;
            if (!isActiveAndEnabled) return;

            _rewardIntroPending = false;
            _coRewardIntroWait = StartCoroutine(CoRewardIntroWhenReady());
        }

        /// <summary>供 GameController 兜底：若上面协程未启动成功则再试一次。</summary>
        public void KickRewardIntroIfNeeded()
        {
            if (_rewardArea == null) return;
            if (_rewardIntroRoutine != null || _coRewardIntroWait != null) return;
            _rewardIntroPending = true;
            TryStartRewardIntroWhenReady();
        }

        private IEnumerator CoRewardIntroWhenReady()
        {
            yield return null;
            yield return null;
            _coRewardIntroWait = null;
            RestartRewardLineIntro();
        }

        /// <summary>
        /// 仅对 <b>line</b> 闪烁约 <see cref="_rewardDecorFlashTotalSeconds"/> 秒后关掉；
        /// <see cref="_rewardArea"/> 根物体保持激活，仅用 Rect 做收集判定。
        /// </summary>
        private void RestartRewardLineIntro()
        {
            if (_rewardArea == null) return;
            EnsureRewardLineRef();
            if (_rewardLine == null) return;

            if (_rewardIntroRoutine != null)
                StopCoroutine(_rewardIntroRoutine);

            _rewardLine.SetActive(true);

            _rewardIntroRoutine = StartCoroutine(RewardLineDecorFlashThenHide());
        }

        private void EnsureRewardLineRef()
        {
            if (_rewardArea == null) return;
            if (_rewardLine == null)
                _rewardLine = FindChildByNameRecursive(_rewardArea, "line");

            if (_rewardLine == null)
                Debug.LogWarning(
                    "[DanmakuGamePanel] 未找到 RewardArea 下的 line，无法闪烁。请在 Inspector 拖 Reward Line 或保留名为 line 的子物体。",
                    this);
        }

        private static GameObject FindChildByNameRecursive(Transform root, string name)
        {
            foreach (Transform t in root)
            {
                if (t.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return t.gameObject;
                var found = FindChildByNameRecursive(t, name);
                if (found != null) return found;
            }
            return null;
        }

        private IEnumerator RewardLineDecorFlashThenHide()
        {
            EnsureRewardLineRef();
            if (_rewardLine == null)
            {
                _rewardIntroRoutine = null;
                yield break;
            }

            float end = Time.unscaledTime + Mathf.Max(0.1f, _rewardDecorFlashTotalSeconds);
            float on = Mathf.Max(0.01f, _rewardFlashOnSeconds);
            float off = Mathf.Max(0.01f, _rewardFlashOffSeconds);

            while (Time.unscaledTime < end)
            {
                _rewardLine.SetActive(true);
                yield return new WaitForSecondsRealtime(on);
                if (Time.unscaledTime >= end) break;
                _rewardLine.SetActive(false);
                yield return new WaitForSecondsRealtime(off);
            }

            _rewardLine.SetActive(false);

            _rewardIntroRoutine = null;
        }

        private void Update()
        {
            if (!_running) return;
            if (_gameCtrl == null || !_gameCtrl.IsStageTimelinePausedForBoss())
                _elapsedTime += Time.deltaTime;

            if (_timeText != null)
            {
                int whole = Mathf.FloorToInt(_elapsedTime);
                if (whole != _lastElapsedWholeSeconds)
                {
                    _lastElapsedWholeSeconds = whole;
                    UpdateTimeText();
                }
            }

            if (_stageProgressSlider != null && _gameCtrl != null)
            {
                float p = _gameCtrl.GetStageProgressNormalized();
                float eps = Mathf.Max(0.0001f, _stageProgressDirtyEpsilon);
                if (_lastStageProgressShown < 0f || Mathf.Abs(p - _lastStageProgressShown) > eps)
                {
                    _lastStageProgressShown = p;
                    _stageProgressSlider.value = p;
                }
            }
        }

        private void OnPlayerDamaged(int livesLeft)
        {
            int v = Mathf.Max(livesLeft, 0);
            if (_hpSlider != null)
                _hpSlider.value = v;

            if (_livesText != null)
                _livesText.text = $"残机 ×{v}";
        }

        private void OnBombUsed(int bombsLeft)
        {
            if (_bombsText != null)
                _bombsText.text = $"炸弹 ×{Mathf.Max(bombsLeft, 0)}";
        }

        private void OnScoreChanged(int score)
        {
            if (_scoreText != null)
                _scoreText.text = $"分数 {score:D8}";
            if (_rightScoreText != null)
                _rightScoreText.text = $"{score:D8}";

            if (DanmakuScorePersistence.TryUpdateHighScore(DanmakuRunSettings.Difficulty, score, out _))
                RefreshBestScoreText();
        }

        /// <summary>右侧「Max」位：当前难度存档最高分（本局内会随刷新提高）。</summary>
        private void RefreshBestScoreText()
        {
            if (_maxScoreText == null) return;
            int best = DanmakuScorePersistence.GetHighScore(DanmakuRunSettings.Difficulty);
            _maxScoreText.text = $"{best:D8}";
        }

        private void OnPowerChanged(int power)
        {
            if (_powerText != null)
                _powerText.text = $"{power}";

            if (_lastPowerForSfx != int.MinValue && power > _lastPowerForSfx)
                DanmakuAudio.PlayPowerUpSfxRandom();
            _lastPowerForSfx = power;
        }

        private void OnGrazeChanged(int graze)
        {
            if (_grazeText != null)
                _grazeText.text = $"{graze}";
        }

        private void OnPauseChanged(bool isPaused)
        {
            _running = !isPaused;
        }

        private void UpdateTimeText()
        {
            if (_timeText == null) return;
            int m = (int)(_elapsedTime / 60f);
            int s = (int)(_elapsedTime % 60f);
            _timeText.text = $"{m:D2}:{s:D2}";
        }

        protected override void OnDestroy()
        {
            if (_rewardArea != null && RewardCollectionRect == _rewardArea)
                RewardCollectionRect = null;
            base.OnDestroy();
        }
    }
}
