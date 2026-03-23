using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 敌人基类，对应参考 Enemy 基类：HP、判定圆、简单移动行为、Shooter 联动。
    /// 通过继承可扩展为 Boss、道中小怪等。
    /// </summary>
    public class DanmakuEnemy : MonoBehaviour
    {
        [Header("基础属性")]
        [SerializeField] protected float _maxHp    = 100f;
        [SerializeField] protected float _hitRadius = 0.3f;
        [Tooltip("为 true 时死亡播放「击破boss」而非「炸裂」")]
        [SerializeField] private bool _isBoss;

        [Tooltip("Boss 时：使用 Demos/Danmaku/Img/boss/{此项}/Px 下序列帧（与工程内 boss 子文件夹名一致，如 Alice）")]
        [SerializeField] private string _bossFolderName = "";

        [Header("掉落")]
        [Tooltip("为 true 时在死亡位置生成可拾取掉落物（需场景中有 DanmakuPickupManager）")]
        [SerializeField] private bool _spawnPickupOnDeath;

        [SerializeField] private string _enemySpritePath = "Demos/Danmaku/Img/enemy/e1/1";

        [Header("序列帧动画")]
        [Tooltip("普通敌：enemy 子目录下帧名（如 e1/1 … e1/5）")]
        [SerializeField] private string[] _enemyAnimFrameSuffixes = { "1", "2", "3", "4", "5" };

        [Tooltip("Boss：Px 下帧名")]
        [SerializeField] private string[] _bossAnimFrameSuffixes =
        {
            "1", "1-1", "2", "2-1", "3", "3-1", "4", "4-1", "5"
        };

        [SerializeField] private float _enemyAnimFrameSeconds = 0.12f;

        [Tooltip("机体 Sprite 子物体缩放（仅视觉，不改碰撞半径）")]
        [SerializeField] private float _spriteVisualScale = 2.2f;

        [Tooltip("未注入 DanmakuGameplayBalance 时，Boss 受击半径与贴图缩放相对本预制体基础值的倍率（二者仍同步）")]
        [SerializeField] private float _bossScaleMultiplierFallback = 5f;

        [Tooltip("未注入 Balance 时，Boss 血量相对本预制体 MaxHp 的倍率")]
        [SerializeField] private float _bossHpMultiplierFallback = 100f;

        [Tooltip("未注入 Balance 时，Boss 发射子弹的碰撞半径相对预制体 _bulletHitRadius 的倍率")]
        [SerializeField] private float _bossBulletHitRadiusMultiplierFallback = 2.25f;

        [Tooltip("未注入 Balance 时，Boss 的 Shoot Delay / Shoot Interval 相对预制体的倍率；<1 更快")]
        [SerializeField] private float _bossShootTimingMultiplierFallback = 0.55f;

        [Header("判定可视化（Scene / Game 视图需打开 Gizmos）")]
        [SerializeField] private bool _drawHitRadiusGizmo = true;
        [SerializeField] private Color _hitRadiusGizmoColor = new Color(1f, 0.35f, 0.15f, 0.9f);

        [Header("移动（缓动到目标位置）")]
        [SerializeField] protected float _moveSpeed  = 2f;
        [SerializeField] protected Vector2 _targetPos = Vector2.zero;

        [Header("Boss 进场与走位（仅 IsBoss 时；忽略 EnemyManager 设的向左退场目标）")]
        [Tooltip("从生成点再向左移动的世界距离，到位后只上下移动")]
        [SerializeField] private float _bossApproachLeftWorld = 1.35f;

        [Tooltip("到位后上下摆动幅度（世界单位）")]
        [SerializeField] private float _bossVerticalAmplitude = 1.1f;

        [Tooltip("上下摆动角速度（弧度/秒）")]
        [SerializeField] private float _bossVerticalOmega = 1.15f;

        [Header("弹幕发射")]
        [SerializeField] protected DanmakuShooter _shooter = new DanmakuShooter
        {
            // 横屏：敌在右侧时弹幕朝左（180°），扇形朝玩家方向覆盖
            Ways = 5, Angle = 180f, VecAngle = 180f, Range = 100f, Radius = 0f, Spy = false
        };

        [Tooltip("首次发射前的延迟（秒）")]
        [SerializeField] protected float _shootDelay = 1f;

        [Tooltip("发射间隔（秒）")]
        [SerializeField] protected float _shootInterval = 1.5f;

        [Tooltip("子弹速度")]
        [SerializeField] protected float _bulletSpeed = 3f;

        [Tooltip("子弹碰撞半径")]
        [SerializeField] protected float _bulletHitRadius = 0.06f;

        [Tooltip("敌弹贴图样式：Resources …/bullet/{样式}/{变体}；为 -1 时交给 BulletManager（随机或默认）")]
        [SerializeField] protected int _bulletStyleFolderIndex = -1;

        [Tooltip("敌弹颜色变体编号（同目录下 0、1、2…）；为 -1 时由 BulletManager 在该样式目录内随机一张存在的图")]
        [SerializeField] protected int _bulletSpriteVariantIndex = -1;

        // ---- 运行时 ----
        public bool IsAlive { get; private set; } = true;

        /// <summary>是否为 Boss（含关卡 ApplyStageBossOverrides 覆盖）。</summary>
        public bool IsBoss => _isBoss;

        private float _cachedHitRadius;
        private float _cachedSpriteVisualScale;
        private float _cachedMaxHp;
        private float _cachedBulletHitRadius;
        private float _cachedShootDelay;
        private float _cachedShootInterval;
        private bool _cachedSpawnStats;

        /// <summary>当前这一实例的血量上限（含 Boss 倍率；对象池复用时由 Initialize / ApplyStageBossOverrides 重置）。</summary>
        private float _runtimeMaxHp;

        protected float _hp;
        private float _shootTimer;
        protected BulletManager _bulletManager;
        protected PlayerController _player;
        private EnemyManager _enemyManager;

        private SpriteRenderer _bodySpriteRenderer;
        private Sprite[] _animSprites;
        private float _animTimer;
        private int _animFrameIndex;

        private bool _bossMovementActive;
        private float _bossHoldX;
        private float _bossCenterY;
        private float _bossVerticalPhase;

        /// <summary>与 <see cref="ComputeAnimSpritesResourceKey"/> 一致时跳过字典查找与字符串拼接（池化复用时常见）。</summary>
        private ulong _resolvedAnimResourceKey;

        private static readonly Dictionary<ulong, Sprite[]> EnemyAnimSpritesByKey =
            new Dictionary<ulong, Sprite[]>(32);

        public event Action<DanmakuEnemy> OnDied;

        private Action<float, Vector2> _shootSpawnHandler;
        private int _pendingShootWaveStyle;
        private int _pendingShootWaveVariant;

        private void Awake()
        {
            CacheSpawnStatsIfNeeded();
            _shootSpawnHandler = OnShooterSpawnBullet;
        }

        private void CacheSpawnStatsIfNeeded()
        {
            if (_cachedSpawnStats) return;
            _cachedHitRadius = _hitRadius;
            _cachedSpriteVisualScale = _spriteVisualScale;
            _cachedMaxHp = _maxHp;
            _cachedBulletHitRadius = _bulletHitRadius;
            _cachedShootDelay = _shootDelay;
            _cachedShootInterval = _shootInterval;
            _cachedSpawnStats = true;
        }

        /// <summary>
        /// 在 StageData 任务里配置 Boss，无需单独 Boss 预制体：勾选 IsBossEnemy 并填 BossFolderName（如 Alice）。
        /// </summary>
        /// <param name="balance">为 null 时使用预制体上的 <see cref="_bossScaleMultiplierFallback"/> / <see cref="_bossHpMultiplierFallback"/>。</param>
        /// <param name="bossHpStageMultiplier">关卡任务递进倍率（≥1），非 Boss 时忽略。</param>
        public void ApplyStageBossOverrides(bool isBossEnemy, string bossFolderNameFromStage,
            DanmakuGameplayBalance balance = null, float bossHpStageMultiplier = 1f)
        {
            if (!isBossEnemy && string.IsNullOrEmpty(bossFolderNameFromStage))
                return;

            CacheSpawnStatsIfNeeded();

            if (isBossEnemy)
                _isBoss = true;
            if (!string.IsNullOrEmpty(bossFolderNameFromStage))
            {
                _bossFolderName = bossFolderNameFromStage.Trim();
                _isBoss = true;
            }

            ApplyBossScaleFromBalance(balance);
            ApplyBossHpFromBalance(balance, bossHpStageMultiplier);
            ApplyBossBulletHitRadiusFromBalance(balance);
            ApplyBossShootTimingFromBalance(balance);

            _animTimer = 0f;
            _animFrameIndex = 0;
            BuildEnemyVisuals();

            if (_isBoss)
            {
                SetupBossMovementFromSpawn();
                _shootTimer = -Mathf.Max(0.01f, _shootDelay);
            }
        }

        private void SetupBossMovementFromSpawn()
        {
            Vector2 p = transform.position;
            float approach = Mathf.Max(0f, _bossApproachLeftWorld);
            _bossHoldX = p.x - approach;

            if (DanmakuStageSpawn.GameplayWorldRect.width > 0.001f)
            {
                Rect r = DanmakuStageSpawn.GameplayWorldRect;
                float mx = Mathf.Max(_hitRadius, 0.35f);
                _bossHoldX = Mathf.Clamp(_bossHoldX, r.xMin + mx, r.xMax - mx);
                float my = Mathf.Max(_hitRadius, 0.25f);
                _bossCenterY = Mathf.Clamp(p.y, r.yMin + my, r.yMax - my);
            }
            else
            {
                _bossCenterY = p.y;
            }

            _bossVerticalPhase = 0f;
            _bossMovementActive = true;
        }

        private void ApplyBossScaleFromBalance(DanmakuGameplayBalance balance)
        {
            if (!_isBoss)
                return;
            float m = balance != null
                ? Mathf.Max(0.1f, balance.bossScaleMultiplierVsNormal)
                : Mathf.Max(0.1f, _bossScaleMultiplierFallback);
            float visOnly = balance != null
                ? Mathf.Clamp(balance.bossSpriteVisualScaleFactor, 0.05f, 2f)
                : 1f;
            _hitRadius = _cachedHitRadius * m;
            _spriteVisualScale = _cachedSpriteVisualScale * m * visOnly;
        }

        private void ApplyBossHpFromBalance(DanmakuGameplayBalance balance, float bossHpStageMultiplier)
        {
            if (!_isBoss)
                return;
            float baseMul = balance != null
                ? Mathf.Max(0.01f, balance.bossHpMultiplierVsNormal)
                : Mathf.Max(0.01f, _bossHpMultiplierFallback);
            float stageMul = Mathf.Max(0.01f, bossHpStageMultiplier);
            _runtimeMaxHp = _cachedMaxHp * baseMul * stageMul;
            _hp = _runtimeMaxHp;
        }

        private void ApplyBossBulletHitRadiusFromBalance(DanmakuGameplayBalance balance)
        {
            if (!_isBoss)
                return;
            float mul = balance != null
                ? Mathf.Clamp(balance.bossEnemyBulletHitRadiusMultiplier, 0.25f, 8f)
                : Mathf.Max(0.25f, _bossBulletHitRadiusMultiplierFallback);
            _bulletHitRadius = Mathf.Max(0.01f, _cachedBulletHitRadius * mul);
        }

        private void ApplyBossShootTimingFromBalance(DanmakuGameplayBalance balance)
        {
            if (!_isBoss)
                return;
            float mul = balance != null
                ? Mathf.Clamp(balance.bossShootTimingMultiplierVsNormal, 0.05f, 3f)
                : Mathf.Max(0.05f, _bossShootTimingMultiplierFallback);
            _shootDelay = Mathf.Max(0.02f, _cachedShootDelay * mul);
            _shootInterval = Mathf.Max(0.02f, _cachedShootInterval * mul);
        }

        public virtual void Initialize(BulletManager bulletMgr, PlayerController player, EnemyManager mgr)
        {
            CacheSpawnStatsIfNeeded();

            _bulletManager = bulletMgr;
            _player        = player;
            _enemyManager  = mgr;
            _shootDelay    = _cachedShootDelay;
            _shootInterval = _cachedShootInterval;
            _shootTimer    = -_shootDelay;
            IsAlive        = true;
            _animTimer     = 0f;
            _animFrameIndex = 0;

            _hitRadius = _cachedHitRadius;
            _spriteVisualScale = _cachedSpriteVisualScale;
            _bulletHitRadius = _cachedBulletHitRadius;
            _runtimeMaxHp = _cachedMaxHp;
            _hp = _runtimeMaxHp;
            _isBoss = false;
            _bossFolderName = "";
            _bossMovementActive = false;

            BuildEnemyVisuals();
        }

        /// <summary>每帧由 EnemyManager 调用。</summary>
        public virtual void LogicUpdate(float dt)
        {
            if (!IsAlive) return;

            MoveToTarget(dt);
            HandleShooting(dt);
            UpdateEnemyAnimation(dt);
        }

        protected virtual void MoveToTarget(float dt)
        {
            if (_isBoss && _bossMovementActive)
            {
                MoveBossAnchored(dt);
                return;
            }

            Vector2 pos = transform.position;
            Vector2 delta = _targetPos - pos;
            if (delta.sqrMagnitude > 0.0001f)
            {
                Vector2 move = delta.normalized * _moveSpeed * dt;
                if (move.sqrMagnitude >= delta.sqrMagnitude)
                    transform.position = (Vector3)(Vector2)_targetPos;
                else
                    transform.position += (Vector3)move;
            }
        }

        private void MoveBossAnchored(float dt)
        {
            Vector2 pos = transform.position;
            const float eps = 0.03f;
            float z = transform.position.z;

            if (pos.x > _bossHoldX + eps)
            {
                float step = _moveSpeed * dt;
                float nx = pos.x - step;
                if (nx < _bossHoldX)
                    nx = _bossHoldX;
                transform.position = new Vector3(nx, pos.y, z);
                return;
            }

            if (pos.x < _bossHoldX - eps)
                pos.x = _bossHoldX;

            _bossVerticalPhase += _bossVerticalOmega * dt;
            float amp = Mathf.Max(0f, _bossVerticalAmplitude);
            float ny = _bossCenterY + Mathf.Sin(_bossVerticalPhase) * amp;

            if (DanmakuStageSpawn.GameplayWorldRect.width > 0.001f)
            {
                Rect r = DanmakuStageSpawn.GameplayWorldRect;
                float my = Mathf.Max(_hitRadius, 0.25f);
                ny = Mathf.Clamp(ny, r.yMin + my, r.yMax - my);
            }

            transform.position = new Vector3(_bossHoldX, ny, z);
        }

        protected virtual void HandleShooting(float dt)
        {
            if (_bulletManager == null) return;
            _shootTimer += dt;
            if (_shootTimer < _shootInterval) return;
            _shootTimer = 0f;
            Shoot();
        }

        protected virtual void Shoot()
        {
            Vector2 origin      = transform.position;
            Vector2 playerPos   = _player != null ? (Vector2)_player.transform.position : origin;
            _pendingShootWaveStyle = _bulletManager.ResolveBulletStyleForWave(_bulletStyleFolderIndex);
            _pendingShootWaveVariant =
                _bulletManager.ResolveBulletVariantForWave(_pendingShootWaveStyle, _bulletSpriteVariantIndex);
            _shooter.Execute(origin, playerPos, _shootSpawnHandler);
            DanmakuAudio.PlayEnemyShootSfx();
        }

        private void OnShooterSpawnBullet(float angle, Vector2 pos)
        {
            if (_bulletManager == null)
                return;
            _bulletManager.SpawnBullet(pos, angle, _bulletSpeed, _bulletHitRadius, 0, null, _pendingShootWaveStyle,
                _pendingShootWaveVariant);
        }

        public virtual void TakeDamage(float dmg)
        {
            if (!IsAlive) return;
            _hp -= dmg;
            if (_hp <= 0f)
                Die();
        }

        protected virtual void Die()
        {
            IsAlive = false;
            DanmakuEnemyDeathVfx.PlayAt(transform.position, _isBoss);
            if (DanmakuPickupManager.Instance != null)
                DanmakuPickupManager.Instance.TryDeathDrop(transform.position, _spawnPickupOnDeath);
            DanmakuAudio.PlayEnemyDeathSfx(_isBoss);
            OnDied?.Invoke(this);
            _enemyManager?.RecycleEnemy(this);
        }

        /// <summary>
        /// 游戏结束清场或重开前：不触发 <see cref="OnDied"/>、不掉落、不播音效，仅回收到对象池。
        /// </summary>
        public void DespawnSilentlyToPool()
        {
            if (!IsAlive) return;
            IsAlive = false;
            _enemyManager?.RecycleEnemy(this);
        }

        public DanmakuCircle GetHitCircle()
        {
            Vector2 pos = transform.position;
            return new DanmakuCircle(pos.x, pos.y, _hitRadius);
        }

        /// <summary>RReimu 等资源目录为 <c>px</c>（小写），其余 Boss 为 <c>Px</c>。</summary>
        private static string GetBossPxDirectorySegment(string bossFolderName)
        {
            if (string.IsNullOrEmpty(bossFolderName)) return "Px";
            return string.Equals(bossFolderName.Trim(), "RReimu", StringComparison.Ordinal) ? "px" : "Px";
        }

        /// <summary>
        /// 无堆分配的动画资源键；须与 <see cref="BuildEnemyVisuals"/> 内选用 Boss/普通路径及后缀数组的逻辑一致。
        /// </summary>
        private static ulong ComputeAnimSpritesResourceKey(bool isBossWithPxPath, string bossFolderName,
            string enemySpritePath, string[] bossSuffixes, string[] enemySuffixes)
        {
            const ulong prime = 1099511628211UL;
            ulong h = 14695981039346656037UL;

            void MixChar(char c)
            {
                h ^= c;
                h *= prime;
            }

            void MixString(string s)
            {
                if (s == null) return;
                for (int i = 0; i < s.Length; i++)
                    MixChar(s[i]);
            }

            void MixTrimmed(string s)
            {
                if (string.IsNullOrEmpty(s)) return;
                int lo = 0, hi = s.Length - 1;
                while (lo <= hi && char.IsWhiteSpace(s[lo])) lo++;
                while (hi >= lo && char.IsWhiteSpace(s[hi])) hi--;
                for (int i = lo; i <= hi; i++)
                    MixChar(s[i]);
            }

            void MixPrefixBeforeSlash(string path)
            {
                if (string.IsNullOrEmpty(path)) return;
                int slash = path.LastIndexOf('/');
                int len = slash > 0 ? slash : path.Length;
                for (int i = 0; i < len; i++)
                    MixChar(path[i]);
            }

            void MixSuffixSequence(string[] arr)
            {
                if (arr == null)
                {
                    MixChar('\uFFFC');
                    return;
                }

                h ^= (ulong)(uint)arr.Length;
                h *= prime;
                for (int i = 0; i < arr.Length; i++)
                {
                    MixTrimmed(arr[i]);
                    MixChar('\x1E');
                }
            }

            if (isBossWithPxPath)
            {
                MixChar('\uFFF0');
                MixString("Demos/Danmaku/Img/boss/");
                MixTrimmed(bossFolderName);
                MixChar('/');
                MixString(GetBossPxDirectorySegment(bossFolderName));
                var sfx = bossSuffixes != null && bossSuffixes.Length > 0 ? bossSuffixes : enemySuffixes;
                MixSuffixSequence(sfx);
            }
            else
            {
                MixChar('\uFFF1');
                MixPrefixBeforeSlash(enemySpritePath);
                MixSuffixSequence(enemySuffixes);
            }

            MixChar('\x1F');
            MixString(enemySpritePath ?? "");
            return h;
        }

        /// <param name="logPrefix">仅慢路径加载失败时用于日志；快速路径不传，避免字符串分配。</param>
        private void ApplyEnemyBodyVisuals(string logPrefix = null)
        {
            if (_animSprites != null && _animSprites.Length > 0)
                _bodySpriteRenderer.sprite = _animSprites[0];
            else if (logPrefix != null)
            {
                if (_isBoss && !string.IsNullOrEmpty(_bossFolderName))
                {
                    Debug.LogWarning(
                        "[DanmakuEnemy] Boss 贴图在 Resources 中未命中：请核对 StageData 的 BossFolderName「" +
                        _bossFolderName.Trim() +
                        "」是否与 Assets/Resources/Demos/Danmaku/Img/boss 下文件夹名一致（区分大小写）；RReimu 使用子目录「px」。已尝试前缀: " +
                        logPrefix + "；回退路径: " + (_enemySpritePath ?? "null") + "。GameObject=" + gameObject.name,
                        this);
                }
                else
                    Debug.LogWarning($"[DanmakuEnemy] 敌机贴图未找到: prefix={logPrefix} fallback={_enemySpritePath}", this);
            }
            else
                Debug.LogWarning($"[DanmakuEnemy] 敌机贴图未找到: fallback={_enemySpritePath}", this);

            float sc = Mathf.Max(0.05f, _spriteVisualScale);
            _bodySpriteRenderer.transform.localScale = Vector3.one * sc;

            DanmakuSpriteUtil.ApplyGameplaySortingToRenderer(_bodySpriteRenderer);
        }

        private void BuildEnemyVisuals()
        {
            _bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_bodySpriteRenderer == null)
                _bodySpriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            bool bossPx = _isBoss && !string.IsNullOrEmpty(_bossFolderName);
            ulong resourceKey = ComputeAnimSpritesResourceKey(bossPx, _bossFolderName, _enemySpritePath,
                _bossAnimFrameSuffixes, _enemyAnimFrameSuffixes);

            if (resourceKey == _resolvedAnimResourceKey && _animSprites != null)
            {
                ApplyEnemyBodyVisuals();
                return;
            }

            if (EnemyAnimSpritesByKey.TryGetValue(resourceKey, out var cachedSprites))
            {
                _animSprites = cachedSprites;
                _resolvedAnimResourceKey = resourceKey;
                ApplyEnemyBodyVisuals();
                return;
            }

            string prefix;
            string[] suffixes;
            if (bossPx)
            {
                string bn = _bossFolderName.Trim();
                prefix = $"Demos/Danmaku/Img/boss/{bn}/{GetBossPxDirectorySegment(bn)}";
                suffixes = _bossAnimFrameSuffixes != null && _bossAnimFrameSuffixes.Length > 0
                    ? _bossAnimFrameSuffixes
                    : _enemyAnimFrameSuffixes;
            }
            else
            {
                prefix = GetSpriteFolderPrefix(_enemySpritePath);
                suffixes = _enemyAnimFrameSuffixes;
            }

            var list = new List<Sprite>();
            if (!string.IsNullOrEmpty(prefix) && suffixes != null)
            {
                foreach (var sfx in suffixes)
                {
                    if (string.IsNullOrWhiteSpace(sfx)) continue;
                    var sp = DanmakuSpriteUtil.TryLoadSprite($"{prefix}/{sfx.Trim()}");
                    if (sp != null)
                        list.Add(sp);
                }
            }

            _animSprites = list.ToArray();
            if (_animSprites.Length == 0)
            {
                if (!string.IsNullOrEmpty(_enemySpritePath))
                {
                    var one = DanmakuSpriteUtil.TryLoadSprite(_enemySpritePath);
                    if (one != null)
                        _animSprites = new[] { one };
                }
            }

            EnemyAnimSpritesByKey[resourceKey] = _animSprites;
            _resolvedAnimResourceKey = resourceKey;
            ApplyEnemyBodyVisuals(prefix);
        }

        private static string GetSpriteFolderPrefix(string pathToFirstFrame)
        {
            if (string.IsNullOrEmpty(pathToFirstFrame)) return pathToFirstFrame;
            int i = pathToFirstFrame.LastIndexOf('/');
            return i > 0 ? pathToFirstFrame.Substring(0, i) : pathToFirstFrame;
        }

        private void UpdateEnemyAnimation(float dt)
        {
            if (_animSprites == null || _animSprites.Length <= 1 || _bodySpriteRenderer == null)
                return;
            _animTimer += dt;
            if (_animTimer < _enemyAnimFrameSeconds)
                return;
            _animTimer -= _enemyAnimFrameSeconds;
            _animFrameIndex = (_animFrameIndex + 1) % _animSprites.Length;
            _bodySpriteRenderer.sprite = _animSprites[_animFrameIndex];
        }

        private void OnDrawGizmos()
        {
            if (!_drawHitRadiusGizmo) return;
            DanmakuGizmoDraw.WireCircleXY(transform.position, _hitRadius, _hitRadiusGizmoColor);
        }

        /// <summary>设置移动目标（可由 StageRunner 或外部调用）。</summary>
        public void SetTargetPosition(Vector2 target) => _targetPos = target;

        /// <summary>由 EnemyManager 生成后覆盖，降低向左移动过快。</summary>
        public void SetMoveSpeed(float unitsPerSecond) => _moveSpeed = Mathf.Max(0.02f, unitsPerSecond);
    }
}
