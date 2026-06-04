using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DemoFrameWork;
using DemoFrameWork.Generated;
using DemoFrameWork.UI;
using DemoFrameWork.GameLogic.Danmaku;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 弹幕场景主控制器。
    /// <para><b>UI</b>：GamePanel / PauseWindow / GameOverWindow 由主场景 UISettings 注册。</para>
    /// <para><b>自机</b>：场景引用、单预制体 <see cref="_playerPrefab"/>，或与主菜单一致的 <see cref="_playerPrefabs"/> 数组（下标来自 <see cref="DanmakuRunSettings.CharacterIndex"/>）。</para>
    /// <para><b>关卡</b>：<see cref="_stages"/> 与主菜单章节下标对应；若为空则用 <see cref="_stageData"/>。</para>
    /// <para><b>预载</b>：主菜单仅载 <c>HomePreloadList</c>；进关时若 <see cref="SceneSettings.deferLoadingHideUntilScriptSceneNames"/> 包含本场景名且 <see cref="GameSceneManager"/> 因此延后关 Loading，则沿用同一块面板并把关内清单进度映射到 0.9~1；否则由本脚本自行 Show/Hide LoadingPanel。</para>
    /// </summary>
    public class DanmakuGameController : MonoBehaviour
    {
        /// <summary>Inspector 未绑定或 GUID 损坏时，从 Resources 实例化自机（与 <see cref="HomePreloadList"/> 一致）。</summary>
        private const string DefaultPlayerPrefabResourcesPath = "Demos/Danmaku/Prefabs/player";

        private static readonly string[] CoreDanmakuPoolPrewarmPaths =
        {
            "Demos/Danmaku/Prefabs/BulletCircle",
            "Demos/Danmaku/Prefabs/Enemy",
            "Demos/Danmaku/Prefabs/player",
            "Demos/Danmaku/Prefabs/PickupItem",
            "Demos/Danmaku/Prefabs/PlayerBullet",
        };

        [Header("帧率")]
        [Tooltip("目标帧率；0 表示不修改（跟随项目/平台默认）。建议 60，减轻高刷屏与低端机发热。")]
        [SerializeField] private int _targetFrameRate = 60;

        [Header("自机")]
        [Tooltip("若场景里已放自机，拖这里（优先级最高）")]
        [SerializeField] private PlayerController _playerController;

        [Tooltip("与主菜单角色列表对应；非空时按 DanmakuRunSettings.CharacterIndex 选用")]
        [SerializeField] private GameObject[] _playerPrefabs;

        [Tooltip("未配置数组时使用的单个预制体")]
        [SerializeField] private GameObject _playerPrefab;

        [Tooltip("生成自机的父节点，空则挂在场景根下")]
        [SerializeField] private Transform _playerSpawnParent;

        [Header("游戏逻辑")]
        [SerializeField] private BulletManager _bulletManager;
        [SerializeField] private EnemyManager _enemyManager;

        [Tooltip("多关卡时使用，下标与 HomePanel 章节子物体顺序一致")]
        [SerializeField] private StageData[] _stages;

        [Tooltip("未配置 _stages 时使用的单个关卡")]
        [SerializeField] private StageData _stageData;

        [Header("横屏 STG：可移动区域")]
        [Tooltip("勾选则用主相机视口换算世界 Rect，排除左侧 HUD，右侧整屏可到达；不勾选则用下方 _playArea 手写")]
        [SerializeField] private bool _useViewportPlayArea = true;

        [Tooltip("玩法区视口：左边缘（0~1），约等于左侧 HUD 宽度，例如 0.2 = 左 20% 不可进）")]
        [SerializeField] private float _gameplayViewportXMin = 0.2f;

        [Tooltip("手机端玩法区左边缘覆盖值；用于 20:9 横屏减少左侧不可移动区。")]
        [SerializeField] private float _mobileGameplayViewportXMin = 0.08f;

        [SerializeField] private float _gameplayViewportXMax = 1f;

        [SerializeField] private float _gameplayViewportYMin = 0f;

        [SerializeField] private float _gameplayViewportYMax = 1f;

        [Header("道具掉落区（视口）")]
        [Tooltip("道具只生成在 [此视口 X, 1]×[Ymin,Ymax] 对应的世界矩形内（「道具收集线」右侧至屏幕右缘）；与 Game 里竖线位置对齐后调此值）")]
        [SerializeField] private float _itemDropViewportXMin = 0.72f;

        [Tooltip("手机端道具掉落区左边缘覆盖值。")]
        [SerializeField] private float _mobileItemDropViewportXMin = 0.7f;

        [Tooltip("左下角 x,y + width height；仅当 _useViewportPlayArea 为 false 时使用")]
        [SerializeField] private Rect _playArea = new Rect(-8f, -4f, 14f, 8f);

        [Tooltip("自机在玩法区左缘的内缩（世界单位）；_useViewportPlayArea 时从计算出的左边界起算")]
        [SerializeField] private float _playerSpawnInsetFromPlayLeft = 0.9f;

        [SerializeField] private Vector3 _playerSpawnPosition = new Vector3(-6f, 0f, 0f);

        [Tooltip("关内开始时把自机移到生成点；若场景已摆好位可关掉")]
        [SerializeField] private bool _snapPlayerToSpawnOnStart = true;

        [Tooltip("为 true 时忽略内缩，强制使用 _playerSpawnPosition（仅调试用）")]
        [SerializeField] private bool _useManualSpawnPositionOnly = false;

        [Header("拾取")]
        [Tooltip("留空则自动在同一物体上添加 DanmakuPickupManager")]
        [SerializeField] private DanmakuPickupManager _pickupManager;

        [Header("得分 / Power / Graze / 掉落（数值中枢）")]
        [SerializeField] private DanmakuGameplayBalance _gameplayBalance;

        [Header("对象池")]
        [Tooltip("暂停时裁剪各桶「空闲」实例上限，减轻内存占用；≤0 表示不裁剪。")]
        [SerializeField] private int _poolTrimIdlePerBucketOnPause = 48;

        [Header("关卡预加载（与 Home 清单拆分）")]
        [Tooltip("进入弹幕场景后、关卡时间轴开始前异步预载；空则用下方 Resources 路径加载 TextAsset。")]
        [SerializeField] private TextAsset _gamePreloadManifest;

        [Tooltip("未拖清单时：Resources 下 TextAsset 路径（无扩展名），默认 Demos/Danmaku/GamePreloadList")]
        [SerializeField] private string _gamePreloadManifestResourcesPath = "Demos/Danmaku/GamePreloadList";

        [Tooltip("与 DanmakuLauncher / BulletManager 敌弹样式区间一致")]
        [SerializeField] private int _gamePreloadBulletStyleMin = 0;

        [SerializeField] private int _gamePreloadBulletStyleMax = 18;

        [Tooltip("调试：跳过关卡清单预载（依赖首帧懒加载，可能有短暂卡顿）")]
        [SerializeField] private bool _skipGameScenePreload;

        /// <summary>
        /// 关卡 <see cref="CoPreloadGameResources"/> 完成且 GamePanel 已显示后为 true。
        /// 在此之前不跑玩法 Update，避免场景 Loading 已关但资源/时间轴未就绪时自机自动开火、HUD 未监听事件等问题。
        /// </summary>
        private bool _warmupComplete;

        private StageData _resolvedStage;
        private StageRunner _stageRunner;
        private bool _isPaused;
        private bool _isGameOver;

        private DanmakuGameplayBalance _runtimeBalanceFallback;
        private float _runElapsedForScore;
        private float _timeScoreAccumulator;
        private int _killCount;
        private int _pickupScoreAccumulator;
        private int _manualScoreBonus;
        private int _powerStat;
        private int _grazeCount;
        private float _xp;
        private int _fireTier;
        private int _displayedScore;

        private void Awake()
        {
            ApplyTargetFrameRate();
            EnsureGameplayAspectAdapter();
            EnsureRuntimeBalanceFallback();
            ResolveStageData();
            ResolvePlayer();
            InitGameLogic();
            EnsureMobileTouchJoystick();
            // 与切场景 Loading 连贯时 GameSceneManager 已保持面板打开，勿再 Hide→Show 造成闪屏
            if (DemoGameEntry.Scene == null || !DemoGameEntry.Scene.IsLoadingHideDeferred)
                ShowLoadingPanelForWarmup();
            SubscribeEvents();
        }

        private void EnsureGameplayAspectAdapter()
        {
            if (!DanmakuMobileRuntime.IsMobileLike)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;
            if (cam.GetComponent<DanmakuGameplayAspectAdapter>() != null)
                return;
            cam.gameObject.AddComponent<DanmakuGameplayAspectAdapter>();
        }

        private void EnsureMobileTouchJoystick()
        {
            if (!DanmakuMobileRuntime.IsMobileLike)
                return;
            if (FindObjectOfType<DanmakuTouchJoystick>() != null)
                return;
            var go = new GameObject("DanmakuTouchJoystick");
            go.AddComponent<DanmakuTouchJoystick>();
        }

        private void ApplyTargetFrameRate()
        {
            if (_targetFrameRate <= 0)
                return;
            Application.targetFrameRate = _targetFrameRate;
            // 避免部分平台以垂直同步为准而忽略 targetFrameRate；若需锁显示器刷新可改 vSyncCount
            QualitySettings.vSyncCount = 0;
        }

        private void Start()
        {
            StartCoroutine(CoStartAfterGamePreload());
        }

        private IEnumerator CoStartAfterGamePreload()
        {
            if (!_skipGameScenePreload)
                yield return CoPreloadGameResources();

            SetWarmupLoadingBar(1f);

            // 给 Loading 至少一帧绘制（满条后再关）
            yield return null;

            if (DemoGameEntry.Scene != null && DemoGameEntry.Scene.IsLoadingHideDeferred)
                DemoGameEntry.Scene.CompleteDeferredLoadingHide();
            else
                HideLoadingPanelAfterWarmup();
            ShowGamePanelIfRegistered();
            NotifyHudListenersAfterGamePanelShown();

            _warmupComplete = true;

            _stageRunner?.Start();
            yield return CoKickRewardIntroFallback();
        }

        /// <summary>异步预载 <c>GamePreloadList</c>，登记敌弹贴图并 Warmup；拾取图集与池预热始终尽量执行。</summary>
        private IEnumerator CoPreloadGameResources()
        {
            var ta = _gamePreloadManifest;
            if (ta == null && !string.IsNullOrWhiteSpace(_gamePreloadManifestResourcesPath))
                ta = Resources.Load<TextAsset>(_gamePreloadManifestResourcesPath.Trim());

            bool manifestOk = ta != null && !string.IsNullOrEmpty(ta.text) &&
                              DanmakuResourcePreloadUtility.CountValidManifestLines(ta.text) > 0;

            if (manifestOk)
            {
                yield return DanmakuResourcePreloadUtility.CoLoadManifestAsync(ta.text, _gamePreloadBulletStyleMin,
                    _gamePreloadBulletStyleMax, 1f, SetWarmupLoadingBarDuringSceneLoadChain, false);
            }
            else if (!_skipGameScenePreload)
            {
                yield return DanmakuBulletResourceIndex.CoWarmupIfNeededAsync(_gamePreloadBulletStyleMin,
                    _gamePreloadBulletStyleMax);
            }

            DanmakuPickupManager.PrewarmPickupSpritesCache();
            yield return null;

            yield return CoPrewarmPoolsChunked();
            yield return CoPrewarmDanmakuAudioClips();
        }

        /// <summary>
        /// 分帧预热弹幕相关 <see cref="AudioClip"/>，避免战斗中首次播放触发 Profiler 中的 <c>SoundManager.LoadFMODSound</c> 尖刺。
        /// </summary>
        private IEnumerator CoPrewarmDanmakuAudioClips()
        {
            var audio = DemoGameEntry.Audio;
            if (audio == null)
                yield break;

            foreach (var path in DanmakuAudio.EnumerateCombatAudioResourcePaths())
            {
                audio.PrewarmClipFromResources(path);
                yield return null;
            }
        }

        /// <summary>核心预制体与关卡敌机路径分帧 <see cref="GameObjectPool.Prewarm"/>，避免首_spawn 同步 Load。</summary>
        private IEnumerator CoPrewarmPoolsChunked()
        {
            var pool = DemoGameEntry.Pool;
            if (pool == null)
                yield break;

            const int corePrewarmCount = 2;
            foreach (var path in CoreDanmakuPoolPrewarmPaths)
            {
                pool.Prewarm(path, corePrewarmCount);
                yield return null;
            }

            var enemyPaths = CollectDistinctEnemyPrefabPaths(_resolvedStage);
            foreach (var path in enemyPaths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;
                bool coreListed = false;
                for (int i = 0; i < CoreDanmakuPoolPrewarmPaths.Length; i++)
                {
                    if (string.Equals(CoreDanmakuPoolPrewarmPaths[i], path, StringComparison.Ordinal))
                    {
                        coreListed = true;
                        break;
                    }
                }

                if (coreListed)
                    continue;

                pool.Prewarm(path, 1);
                yield return null;
            }
        }

        private static HashSet<string> CollectDistinctEnemyPrefabPaths(StageData stage)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (stage?.Tasks == null)
                return set;

            for (int i = 0; i < stage.Tasks.Count; i++)
            {
                var t = stage.Tasks[i];
                if (t.TaskType != StageTaskType.SpawnEnemy)
                    continue;
                if (string.IsNullOrEmpty(t.EnemyPrefabPath))
                    continue;
                set.Add(t.EnemyPrefabPath.Trim());
            }

            return set;
        }

        /// <summary>
        /// 切场景 Loading 未关时，进度条 0~1 映射到 0.9~1，与 <see cref="GameSceneManager"/> 的 0~0.9 衔接。
        /// </summary>
        private void SetWarmupLoadingBarDuringSceneLoadChain(float manifestLocal01)
        {
            float p = Mathf.Clamp01(manifestLocal01);
            if (DemoGameEntry.Scene != null && DemoGameEntry.Scene.IsLoadingHideDeferred)
                SetWarmupLoadingBar(0.9f + 0.1f * p);
            else
                SetWarmupLoadingBar(p);
        }

        private IEnumerator CoKickRewardIntroFallback()
        {
            yield return null;
            yield return null;
            yield return null;
            var panel = UnityEngine.Object.FindObjectOfType<DanmakuGamePanel>();
            panel?.KickRewardIntroIfNeeded();
        }

        private void Update()
        {
            if (_isGameOver) return;

            if (!_warmupComplete)
                return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                TogglePause();

            if (_isPaused) return;

            float dt = Time.deltaTime;
            // 先跑关卡时间轴，便于本帧内 Boss 阵亡时解除 IsTimelinePausedForBoss，再计时间分
            _stageRunner?.LogicUpdate(dt);
            if (_stageRunner == null || !_stageRunner.IsTimelinePausedForBoss)
                TickTimeScore(dt);
            _bulletManager?.LogicUpdate(dt);
            _enemyManager?.LogicUpdate(dt);
            if (_playerController != null)
                _playerController.LogicUpdate(dt);

            _pickupManager?.LogicUpdate(dt);

            TryNotifyStageVictory();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();

            // 预载协程未跑完就卸场景时，避免 Loading 永远挂在 DDOL UI 上
            DemoGameEntry.Scene?.CompleteDeferredLoadingHide();

            _playerController?.ClearActivePlayerBullets();
            _bulletManager?.ClearBullets();
            _enemyManager?.DespawnAllSilently();
            _pickupManager?.StopAmbientSpawns();
            _pickupManager?.ClearAllDrops();

            if (DemoGameEntry.UI != null)
            {
                // 栈顶只有一个 CurrentWindow；连续 CloseWindow(暂停/结束) 会触发 WindowUILayer 报错并可能留下脏状态。
                DemoGameEntry.UI.CloseAllWindows(false);
                DemoGameEntry.UI.HidePanel(ScreenIds.GamePanel);
            }
        }

        private void ResolveStageData()
        {
            _resolvedStage = null;
            if (_stages != null && _stages.Length > 0)
            {
                int i = Mathf.Clamp(DanmakuRunSettings.StageIndex, 0, _stages.Length - 1);
                _resolvedStage = _stages[i];
            }
            if (_resolvedStage == null)
                _resolvedStage = _stageData;
        }

        private void ResolvePlayer()
        {
            // Inspector 若拖的是「预制体资源」上的组件而非场景实例，gameObject.scene.IsValid() 为 false，
            // 此时不能跳过 Instantiate，否则场景里没有自机，逻辑仍跑在预制体资产上、机体不渲染。
            if (_playerController != null && _playerController.gameObject.scene.IsValid())
                return;

            _playerController = null;

            GameObject prefab = null;
            if (_playerPrefabs != null && _playerPrefabs.Length > 0)
            {
                int i = Mathf.Clamp(DanmakuRunSettings.CharacterIndex, 0, _playerPrefabs.Length - 1);
                prefab = _playerPrefabs[i];
            }
            if (prefab == null)
                prefab = _playerPrefab;
            if (prefab == null)
                prefab = Resources.Load<GameObject>(DefaultPlayerPrefabResourcesPath);

            if (prefab != null)
            {
                var parent = _playerSpawnParent;
                var go = parent != null
                    ? Instantiate(prefab, _playerSpawnPosition, Quaternion.identity, parent)
                    : Instantiate(prefab, _playerSpawnPosition, Quaternion.identity);
                _playerController = go.GetComponent<PlayerController>();
                if (_playerController == null)
                    Debug.LogError("[DanmakuGameController] 自机预制体上缺少 PlayerController。");
                return;
            }

            _playerController = FindObjectOfType<PlayerController>();
            if (_playerController == null)
                Debug.LogError("[DanmakuGameController] 未找到 PlayerController：请配置 _playerPrefabs/_playerPrefab 或在场景放置自机。");
        }

        private void InitGameLogic()
        {
            if (_playerController == null) return;

            var b = EffectiveBalance;

            _bulletManager.Initialize(_playerController, DemoGameEntry.Pool, b.grazeRadiusMultiplier, OnPlayerGrazed);
            _enemyManager.Initialize(_playerController, _bulletManager, DemoGameEntry.Pool);

            DanmakuRunSettings.GetStartingLivesAndBombs(out int lives, out int bombs);
            _playerController.Initialize(_bulletManager, _enemyManager, lives, bombs, DanmakuRunSettings.CharacterIndex);
            _playerController.SetPersistentFlatAttackBonus(DanmakuDailyAttackBonus.RegisterBattleParticipation());
            _playerController.SetWeaponBalance(b);
            _playerController.ResetRunStatMultipliers();
            _playerController.ApplyFireTier(0, b);

            Rect area = ComputeGameplayWorldRect();
            Rect itemDropArea = ComputeItemDropWorldRect();
            DanmakuStageSpawn.GameplayWorldRect = area;
            _bulletManager.SetPlayArea(area);
            _playerController.SetPlayArea(area);

            _enemyManager.SetEnemyExitTargetX(area.xMin + 0.45f);

            if (_snapPlayerToSpawnOnStart)
                _playerController.transform.position = ComputePlayerSpawnWorldPosition(area);

            _playerController.OnDamaged += OnPlayerDamaged;
            _playerController.OnGameOver += OnPlayerGameOver;
            _playerController.OnBombUsed += OnBombUsed;

            float mul = DanmakuRunSettings.GetBulletSpeedMultiplier();
            bool endless = DanmakuRunSettings.IsEndlessMode;

            _stageRunner = new StageRunner();
            _stageRunner.Initialize(_resolvedStage, _bulletManager, _enemyManager, DemoGameEntry.Audio, mul, endless,
                _playerController, b);

            EnsurePickupManager();
            _pickupManager?.Initialize(_playerController);
            _pickupManager?.SetBalanceAndSpawnRect(b, area, itemDropArea);
            _pickupManager?.SetPickupCollectedCallback(HandlePickupCollected);
            _pickupManager?.StartAmbientSpawns();

            ResetRunStatsForNewGame();
            RefreshDisplayedScore();
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PowerChanged, _powerStat);
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.GrazeChanged, _grazeCount);
        }

        private DanmakuGameplayBalance EffectiveBalance =>
            _gameplayBalance != null ? _gameplayBalance : _runtimeBalanceFallback;

        private void EnsureRuntimeBalanceFallback()
        {
            if (_runtimeBalanceFallback != null) return;
            _runtimeBalanceFallback = ScriptableObject.CreateInstance<DanmakuGameplayBalance>();
            _runtimeBalanceFallback.name = "RuntimeDanmakuBalanceFallback";
            // CreateInstance 会触发 OnEnable，但若曲线仍空则补默认
            if (_runtimeBalanceFallback.scorePointsPerSecondByElapsedTime == null ||
                _runtimeBalanceFallback.scorePointsPerSecondByElapsedTime.length < 2)
                _runtimeBalanceFallback.scorePointsPerSecondByElapsedTime =
                    AnimationCurve.Linear(0f, 8f, 240f, 28f);
        }

        private void ResetRunStatsForNewGame()
        {
            _runElapsedForScore = 0f;
            _timeScoreAccumulator = 0f;
            _killCount = 0;
            _pickupScoreAccumulator = 0;
            _manualScoreBonus = 0;
            _powerStat = 0;
            _grazeCount = 0;
            _xp = 0f;
            _fireTier = 0;
            _displayedScore = 0;
        }

        private void TickTimeScore(float dt)
        {
            _runElapsedForScore += dt;
            _timeScoreAccumulator += EffectiveBalance.EvaluateScoreRate(_runElapsedForScore) * dt;
            if (ComputeDisplayedScore() == _displayedScore)
                return;
            RefreshDisplayedScore();
        }

        private int ComputeDisplayedScore()
        {
            var b = EffectiveBalance;
            double raw = _timeScoreAccumulator
                         + _killCount * b.perKillScore
                         + _pickupScoreAccumulator
                         + _powerStat * b.perPowerScore
                         + _grazeCount * b.perGrazeScore
                         + _manualScoreBonus;
            raw *= b.globalScoreMultiplier;
            return Mathf.Max(0, Mathf.RoundToInt((float)raw));
        }

        private void RefreshDisplayedScore()
        {
            int s = ComputeDisplayedScore();
            if (s == _displayedScore) return;
            _displayedScore = s;
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.ScoreChanged, _displayedScore);
        }

        private void OnPlayerGrazed()
        {
            if (_isGameOver || _isPaused) return;
            var b = EffectiveBalance;
            _grazeCount++;
            _xp += b.xpPerGraze;
            TryConsumeXpForFireTier();
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.GrazeChanged, _grazeCount);
            DanmakuAudio.PlayGrazeSfx();
            RefreshDisplayedScore();
        }

        private void TryConsumeXpForFireTier()
        {
            var b = EffectiveBalance;
            if (b.xpThresholdPerTier == null || b.xpThresholdPerTier.Length == 0)
                return;

            while (_fireTier < b.MaxFireTierIndex)
            {
                if (_fireTier >= b.xpThresholdPerTier.Length)
                    break;
                float need = b.xpThresholdPerTier[_fireTier];
                if (_xp < need)
                    break;
                _xp -= need;
                _fireTier++;
                _playerController?.ApplyFireTier(_fireTier, b);
                DanmakuAudio.PlayPowerUpSfxRandom();
            }
        }

        private void HandlePickupCollected(DanmakuPickupKind kind)
        {
            if (_isGameOver) return;
            var b = EffectiveBalance;
            _pickupScoreAccumulator += b.GetPickupScoreContribution(kind);

            switch (kind)
            {
                case DanmakuPickupKind.Power:
                    _powerStat += b.powerFromPowerPickup;
                    DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PowerChanged, _powerStat);
                    break;
                case DanmakuPickupKind.MoveSpeed:
                    _playerController?.ApplyPickupMoveSpeed(b.moveSpeedBonusPerPickup);
                    break;
                case DanmakuPickupKind.FireRate:
                    _playerController?.ApplyPickupFireRate(b.fireRateBonusPerPickup);
                    break;
                case DanmakuPickupKind.Heal:
                    _playerController?.HealLives(b.healPerPickup);
                    if (_playerController != null)
                        DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PlayerDamaged, _playerController.Lives);
                    break;
                case DanmakuPickupKind.Attack:
                    _playerController?.ApplyPickupAttack(b.attackBonusPerPickup);
                    break;
                case DanmakuPickupKind.Xp:
                    _xp += b.xpFromPickupXpKind;
                    TryConsumeXpForFireTier();
                    break;
            }

            RefreshDisplayedScore();
        }

        private void EnsurePickupManager()
        {
            if (_pickupManager != null) return;
            _pickupManager = GetComponent<DanmakuPickupManager>();
            if (_pickupManager == null)
                _pickupManager = gameObject.AddComponent<DanmakuPickupManager>();
        }

        /// <summary>
        /// 将主相机视口 [xMin,1]×[0,1] 转为世界空间玩法矩形，使左侧 HUD 区域不可达、右侧全宽可达。
        /// </summary>
        private Rect ComputeGameplayWorldRect()
        {
            if (!_useViewportPlayArea)
                return _playArea;

            var cam = Camera.main;
            if (cam == null)
                return _playArea;

            float dist = Mathf.Abs(cam.transform.position.z);
            if (dist < 0.01f)
                dist = 10f;

            Vector3 bl = cam.ViewportToWorldPoint(new Vector3(EffectiveGameplayViewportXMin, _gameplayViewportYMin, dist));
            Vector3 tr = cam.ViewportToWorldPoint(new Vector3(_gameplayViewportXMax, _gameplayViewportYMax, dist));
            float xmin = Mathf.Min(bl.x, tr.x);
            float xmax = Mathf.Max(bl.x, tr.x);
            float ymin = Mathf.Min(bl.y, tr.y);
            float ymax = Mathf.Max(bl.y, tr.y);
            return new Rect(xmin, ymin, xmax - xmin, ymax - ymin);
        }

        /// <summary>
        /// 道具生成区：视口 X∈[<see cref="_itemDropViewportXMin"/>, 1] 与玩法区同 Y 范围，对应「收集线右至屏右」。
        /// </summary>
        private Rect ComputeItemDropWorldRect()
        {
            if (!_useViewportPlayArea)
            {
                float t = Mathf.Clamp01(EffectiveItemDropViewportXMin);
                float x0 = _playArea.xMin + _playArea.width * t;
                float w = Mathf.Max(0.01f, _playArea.xMax - x0);
                return new Rect(x0, _playArea.yMin, w, _playArea.height);
            }

            var cam = Camera.main;
            if (cam == null)
                return _playArea;

            float dist = Mathf.Abs(cam.transform.position.z);
            if (dist < 0.01f)
                dist = 10f;

            float vx0 = Mathf.Clamp01(EffectiveItemDropViewportXMin);
            Vector3 bl = cam.ViewportToWorldPoint(new Vector3(vx0, _gameplayViewportYMin, dist));
            Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, _gameplayViewportYMax, dist));
            float xmin = Mathf.Min(bl.x, tr.x);
            float xmax = Mathf.Max(bl.x, tr.x);
            float ymin = Mathf.Min(bl.y, tr.y);
            float ymax = Mathf.Max(bl.y, tr.y);
            return new Rect(xmin, ymin, xmax - xmin, ymax - ymin);
        }

        private float EffectiveGameplayViewportXMin =>
            DanmakuMobileRuntime.IsMobileLike ? _mobileGameplayViewportXMin : _gameplayViewportXMin;

        private float EffectiveItemDropViewportXMin =>
            DanmakuMobileRuntime.IsMobileLike ? _mobileItemDropViewportXMin : _itemDropViewportXMin;

        private static string ResolveLoadingPanelId()
        {
            return DemoGameEntry.Scene != null && !string.IsNullOrEmpty(DemoGameEntry.Scene.LoadingPanelId)
                ? DemoGameEntry.Scene.LoadingPanelId
                : ScreenIds.LoadingPanel;
        }

        private void SetWarmupLoadingBar(float progress)
        {
            if (DemoGameEntry.UI == null)
                return;
            string id = ResolveLoadingPanelId();
            if (!DemoGameEntry.UI.TryGetPanelController(id, out var ctl))
                return;
            if (!(ctl is LoadingPanelController loading))
                return;
            loading.SetLoadingProgress(progress);
        }

        private void ShowLoadingPanelForWarmup()
        {
            if (DemoGameEntry.UI == null)
                return;

            string id = ResolveLoadingPanelId();
            if (!DemoGameEntry.UI.IsScreenRegistered(id))
                return;

            DemoGameEntry.UI.ShowPanel(id);
            SetWarmupLoadingBar(0f);
        }

        private void HideLoadingPanelAfterWarmup()
        {
            if (DemoGameEntry.UI == null)
                return;

            string id = ResolveLoadingPanelId();
            if (DemoGameEntry.UI.IsScreenRegistered(id))
                DemoGameEntry.UI.HidePanel(id);
        }

        /// <summary>
        /// InitGameLogic 在 Awake 里已触发过分数/Power/Graze 等事件，此时 GamePanel 尚未 Show、监听器未挂上，需在显示面板后补发一次。
        /// </summary>
        private void NotifyHudListenersAfterGamePanelShown()
        {
            if (DemoGameEntry.Event == null)
                return;

            RefreshDisplayedScore();
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.ScoreChanged, _displayedScore);
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PowerChanged, _powerStat);
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.GrazeChanged, _grazeCount);
            if (_playerController != null)
            {
                DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PlayerDamaged, _playerController.Lives);
                DemoGameEntry.Event.EventTrigger(DanmakuEventNames.BombUsed, _playerController.Bombs);
            }
        }

        private void ShowGamePanelIfRegistered()
        {
            if (DemoGameEntry.UI == null)
            {
                Debug.LogError("[DanmakuGameController] DemoGameEntry.UI 为空，请从主场景进入且 UISettings 已配置 UIFrame。");
                return;
            }

            if (!DemoGameEntry.UI.IsScreenRegistered(ScreenIds.GamePanel))
            {
                Debug.LogError(
                    "[DanmakuGameController] 未注册 GamePanel：请在主场景 DemoGameEntry 的 UISettings → Screens To Register 中加入 " +
                    "GamePanel 预制体（根节点挂 DanmakuGamePanel）。PauseWindow / GameOverWindow 同样在此注册。");
                return;
            }

            DemoGameEntry.UI.ShowPanel(ScreenIds.GamePanel);
        }

        private void SubscribeEvents()
        {
            DemoGameEntry.Event.AddEventListener(DanmakuEventNames.PauseRequested, OnPauseRequested);
            DemoGameEntry.Event.AddEventListener(DanmakuEventNames.ResumeRequested, OnResumeRequested);
            DemoGameEntry.Event.AddEventListener(DanmakuEventNames.ReturnHomeRequested, OnReturnHomeRequested);
            DemoGameEntry.Event.AddEventListener(DanmakuEventNames.RestartRunRequested, OnRestartRunRequested);

            if (_enemyManager != null)
                _enemyManager.EnemyDied += OnEnemyKilledForScore;
        }

        private void UnsubscribeEvents()
        {
            if (DemoGameEntry.Event == null) return;
            DemoGameEntry.Event.RemoveEventListener(DanmakuEventNames.PauseRequested, OnPauseRequested);
            DemoGameEntry.Event.RemoveEventListener(DanmakuEventNames.ResumeRequested, OnResumeRequested);
            DemoGameEntry.Event.RemoveEventListener(DanmakuEventNames.ReturnHomeRequested, OnReturnHomeRequested);
            DemoGameEntry.Event.RemoveEventListener(DanmakuEventNames.RestartRunRequested, OnRestartRunRequested);

            if (_enemyManager != null)
                _enemyManager.EnemyDied -= OnEnemyKilledForScore;

            if (_playerController != null)
            {
                _playerController.OnDamaged -= OnPlayerDamaged;
                _playerController.OnGameOver -= OnPlayerGameOver;
                _playerController.OnBombUsed -= OnBombUsed;
            }
        }

        private void OnPlayerDamaged()
        {
            if (_playerController == null) return;
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PlayerDamaged, _playerController.Lives);
        }

        private void OnBombUsed(int bombsLeft)
        {
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.BombUsed, bombsLeft);
        }

        private void OnEnemyKilledForScore(DanmakuEnemy e)
        {
            if (_isGameOver) return;
            var b = EffectiveBalance;
            _killCount++;
            int pAdd = UnityEngine.Random.Range(b.powerPerKillMin, b.powerPerKillMax + 1);
            _powerStat += pAdd;
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PowerChanged, _powerStat);
            if (e != null && e.IsBoss)
            {
                _manualScoreBonus += b.bonusScorePerBossKill;
                _xp += b.xpPerBossKill;
                TryConsumeXpForFireTier();
            }

            RefreshDisplayedScore();
        }

        private void OnPlayerGameOver()
        {
            _isGameOver = true;
            _stageRunner?.Pause();
            _pickupManager?.SetAmbientSpawnsEnabled(false);
            // 游戏结束不关 BGM；敌弹回池 + 敌机静默回池（不计分、不掉落）
            _bulletManager?.ClearBullets();
            _enemyManager?.DespawnAllSilently();

            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.GameOver, _displayedScore);
            DanmakuGameOverWindow.OpenLoss(_displayedScore);
        }

        /// <summary>非无尽模式下关卡任务全部播完且玩家仍存活 → 胜利。</summary>
        private void TryNotifyStageVictory()
        {
            if (_isGameOver || _isPaused) return;
            if (_stageRunner == null) return;
            if (_stageRunner.IsTimelinePausedForBoss) return;
            if (_playerController == null || !_playerController.IsAlive) return;
            if (DanmakuRunSettings.IsEndlessMode) return;
            if (_resolvedStage == null || _resolvedStage.Tasks == null || _resolvedStage.Tasks.Count == 0)
                return;
            if (!_stageRunner.IsFinished) return;

            // 最后一项任务的 TriggerTime。若≤0（任务全堆在 0 秒），首帧就会跑完 IsFinished，
            // 必须用「至少经过若干秒」再判胜，否则会进场立刻 Victory。
            float lastTrigger = _resolvedStage.Tasks[_resolvedStage.Tasks.Count - 1].TriggerTime;
            const float minElapsedWhenTimelineStartsAtZero = 1f;
            float minElapsed = lastTrigger > 0.001f ? lastTrigger : minElapsedWhenTimelineStartsAtZero;
            if (_stageRunner.ElapsedTime < minElapsed)
                return;

            OnStageVictory();
        }

        private void OnStageVictory()
        {
            _isGameOver = true;
            _stageRunner?.Pause();
            _pickupManager?.SetAmbientSpawnsEnabled(false);
            // 游戏结束不关 BGM；清场后进胜利结算（与失败一致：弹、敌机、掉落物）
            _bulletManager?.ClearBullets();
            _enemyManager?.DespawnAllSilently();
            _pickupManager?.ClearAllDrops();

            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.GameOver, _displayedScore);
            DanmakuGameOverWindow.OpenVictory(_displayedScore);
        }

        private void TogglePause()
        {
            _isPaused = !_isPaused;

            if (_isPaused)
            {
                _pickupManager?.SetAmbientSpawnsEnabled(false);
                DanmakuAudio.PlayPauseMenuSfx();
                _stageRunner?.Pause();
                // 暂停不关 BGM，关卡音乐持续播放（仅停游戏逻辑与打开暂停窗）
                DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PauseChanged, true);
                if (_poolTrimIdlePerBucketOnPause > 0 && DemoGameEntry.Pool != null)
                    DemoGameEntry.Pool.TrimExcessIdle(_poolTrimIdlePerBucketOnPause);
                DanmakuPauseWindow.Open();
            }
            else
            {
                DoResume();
            }
        }

        private void DoResume()
        {
            _isPaused = false;
            _pickupManager?.SetAmbientSpawnsEnabled(true);
            _stageRunner?.Resume();
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PauseChanged, false);
        }

        private void OnPauseRequested()
        {
            if (_isGameOver) return;
            if (_isPaused) return;
            TogglePause();
        }

        private void OnResumeRequested()
        {
            if (!_isPaused) return;
            DoResume();
        }

        private void OnReturnHomeRequested()
        {
            DemoGameEntry.Scene?.LoadScene("Home");
        }

        private void OnRestartRunRequested()
        {
            RestartRunAfterLoss();
        }

        /// <summary>失败结算「继续」：不重载场景，清场并重新开始本关。</summary>
        public void RestartRunAfterLoss()
        {
            if (!_isGameOver || _playerController == null || _stageRunner == null) return;

            _pickupManager?.ClearAllDrops();
            _bulletManager?.ClearBullets();
            _enemyManager?.DespawnAllSilently();

            ResetRunStatsForNewGame();
            var b = EffectiveBalance;
            _playerController.SetWeaponBalance(b);
            _playerController.ResetRunStatMultipliers();
            _playerController.ApplyFireTier(0, b);
            _pickupManager?.SetBalanceAndSpawnRect(b, ComputeGameplayWorldRect(), ComputeItemDropWorldRect());

            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.ScoreChanged, _displayedScore);
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PowerChanged, _powerStat);
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.GrazeChanged, _grazeCount);

            DanmakuRunSettings.GetStartingLivesAndBombs(out int lives, out int bombs);
            _playerController.ClearActivePlayerBullets();
            _playerController.Initialize(_bulletManager, _enemyManager, lives, bombs, DanmakuRunSettings.CharacterIndex);
            _playerController.SetPersistentFlatAttackBonus(DanmakuDailyAttackBonus.RegisterBattleParticipation());
            _playerController.transform.position = ComputePlayerSpawnWorldPosition(ComputeGameplayWorldRect());

            _stageRunner.ResetTimelineWithoutBgm();
            _isGameOver = false;
            _isPaused = false;
            _pickupManager?.SetAmbientSpawnsEnabled(true);

            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PlayerDamaged, _playerController.Lives);
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.BombUsed, _playerController.Bombs);
            DemoGameEntry.Event.EventTrigger(DanmakuEventNames.PauseChanged, false);

            // 失败重开不重载场景，不会触发 sceneLoaded；结算关窗可能仍卡在窗口过渡中，需恢复 Raycaster。
            DemoGameEntry.UI?.RecoverWindowInputAfterSceneLoad();

            StartCoroutine(CoKickRewardIntroFallback());
        }

        /// <param name="area">玩法区矩形；与 <see cref="ComputeGameplayWorldRect"/> 一致。</param>
        private Vector3 ComputePlayerSpawnWorldPosition(Rect area)
        {
            if (!_snapPlayerToSpawnOnStart)
                return _playerController.transform.position;
            if (_useManualSpawnPositionOnly)
                return _playerSpawnPosition;
            float cy = (area.yMin + area.yMax) * 0.5f;
            float sx = area.xMin + _playerSpawnInsetFromPlayLeft;
            return new Vector3(sx, cy, _playerSpawnPosition.z);
        }

        /// <summary>额外加分（任务奖励等）；并入统一显示分公式中的手动加项。</summary>
        public void AddScore(int delta)
        {
            _manualScoreBonus += delta;
            RefreshDisplayedScore();
        }

        /// <summary>供 HUD 阶段进度条：0~1。</summary>
        public float GetStageProgressNormalized() => _stageRunner != null ? _stageRunner.GetProgress01() : 0f;

        /// <summary>Boss 战期间关卡任务时间轴暂停（HUD 计时/时间分暂停；场上弹幕与掉落逻辑仍更新）。</summary>
        public bool IsStageTimelinePausedForBoss() =>
            _stageRunner != null && _stageRunner.IsTimelinePausedForBoss;
    }
}
