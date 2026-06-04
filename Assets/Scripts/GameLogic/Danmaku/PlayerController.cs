using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DemoFrameWork;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 玩家（自机）逻辑：移动、判定圆、生命/炸弹、无敌时间。
    /// 对应参考 BasicChar / CharReimu。
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("移动")]
        [Tooltip("正常移动速度（Unity 单位/秒）")]
        [SerializeField] private float _moveSpeed = 3.5f;

        [Tooltip("低速移动速度（按住 Shift）")]
        [SerializeField] private float _slowSpeed = 1.5f;

        [Header("判定")]
        [Tooltip("碰撞圆半径（Unity 单位）")]
        [SerializeField] private float _hitRadius = 0.04f;

        [Header("拾取")]
        [Tooltip("拾取吸引半径 = 受击半径 × 倍率")]
        [SerializeField] private float _pickupRadiusMultiplier = 6f;

        [Header("判定可视化（Scene / Game 视图需打开右上角 Gizmos）")]
        [SerializeField] private bool _drawHitRadiusGizmo = true;
        [SerializeField] private Color _hitRadiusGizmoColor = new Color(0f, 1f, 0.55f, 0.9f);

        [Tooltip("显示拾取范围（受击半径×倍率）")]
        [SerializeField] private bool _drawPickupRadiusGizmo;
        [SerializeField] private Color _pickupRadiusGizmoColor = new Color(0.35f, 0.75f, 1f, 0.45f);

        [Header("判定可视化 · 自机子弹")]
        [Tooltip("绘制当前存活自机子弹的碰撞圆（敌弹见 BulletManager；Scene / Game 需开 Gizmos）")]
        [SerializeField] private bool _drawAllPlayerBulletHitGizmos = true;
        [SerializeField] private Color _playerBulletHitGizmoColor = new Color(0.3f, 0.85f, 1f, 0.55f);

        [Header("初始资源")]
        [SerializeField] private int _initialLives = 3;
        [SerializeField] private int _initialBombs = 3;

        [Header("无敌时间")]
        [Tooltip("被击中后的无敌时间（秒）")]
        [SerializeField] private float _invincibleSeconds = 2.5f;

        [Header("游戏区域限制（横屏：宽 > 高，自机在左半区活动）")]
        [SerializeField] private Rect _playArea = new Rect(-8f, -4f, 14f, 8f);

        [Header("显示资源（Resources）")]
        [Tooltip("机体 SpriteRenderer；空则优先子物体 Circle，否则取第一个子 SpriteRenderer")]
        [SerializeField] private SpriteRenderer _shipSpriteRenderer;

        [Tooltip("机体循环帧：player/{角色}/文件名，按顺序播放；缺图则跳过。全空则用 0.png")]
        [SerializeField] private string[] _playerAnimFrameSuffixes =
        {
            "1", "2", "2-1", "3", "3-1", "4", "4-1", "5", "5-1"
        };

        [Tooltip("每帧停留时间（秒）")]
        [SerializeField] private float _playerAnimFrameSeconds = 0.1f;

        [Header("射击")]
        [Tooltip("自机子弹池化预制体；空则用下方 Resources 路径（须放在某 Resources 目录下）")]
        [SerializeField] private GameObject _playerBulletPrefab;

        [Tooltip("Resources 路径（无扩展名），默认与 Resources/Demos/Danmaku/Prefabs/PlayerBullet 对应")]
        [SerializeField] private string _playerBulletPrefabResourcesPath = "Demos/Danmaku/Prefabs/PlayerBullet";

        [Tooltip("1=灵梦系 bullet0…；2=魔理沙系 MarisaBullet0…（由角色下标自动选）")]
        [SerializeField] private int _characterFolderId = 1;
        [SerializeField] private float _shotInterval = 0.12f;
        [SerializeField] private float _playerBulletSpeed = 12f;
        [SerializeField] private float _playerBulletDamage = 1f;
        [SerializeField] private float _playerBulletHitRadius = 0.08f;
        [Tooltip("相对自机位置，向右发射时的枪口偏移")]
        [SerializeField] private Vector2 _shotOffset = new Vector2(0.45f, 0f);

        [Tooltip("true：自动连射，不按 Z")]
        [SerializeField] private bool _autoFire = true;

        [Tooltip("贴图已在资源里朝右：用 atan2 对齐；若贴图默认朝上（Unity 常见），关掉此项并改用 +90° 对齐")]
        [SerializeField] private bool _playerBulletSpriteFacesRight = true;

        // ---- 状态 ----
        public bool IsAlive { get; private set; } = true;
        public bool IsInvincible => _invincibleTimer > 0f;
        public int Lives { get; private set; }
        public int Bombs { get; private set; }

        /// <summary>被击中一次（不含炸弹）时回调。</summary>
        public event Action OnDamaged;
        /// <summary>残机耗尽、游戏结束时回调。</summary>
        public event Action OnGameOver;
        /// <summary>使用炸弹时回调，参数为剩余炸弹数。</summary>
        public event Action<int> OnBombUsed;

        private float _invincibleTimer;
        private BulletManager _bulletManager;
        private EnemyManager _enemyManager;
        private readonly List<PlayerBullet> _playerBullets = new List<PlayerBullet>(128);
        private float _shotTimer;

        /// <summary>被击坠后补充炸弹数（与开局炸弹一致，受难度覆盖影响）。</summary>
        private int _refillBombs;

        private float _moveMul = 1f;
        private float _fireMul = 1f;
        private float _damageMul = 1f;

        /// <summary>跨局累积的平加伤害（先算 <c>_playerBulletDamage * _damageMul</c> 再累加此项）。</summary>
        private float _persistentFlatAttackBonus;

        /// <summary>本局开局时的残机数，用于安卓「半血以下受击自动 bomb」判定。</summary>
        private int _livesAtRunStart = 1;
        private int _weaponTier;
        private DanmakuGameplayBalance _weaponBalance;

        private Sprite[] _shipAnimSprites;
        private float _shipAnimTimer;
        private int _shipAnimFrame;

        /// <summary>自机弹贴图缓存（lane 映射到 bullet0…3 / MarisaBullet0…3，共 4 张）。</summary>
        private readonly Sprite[] _playerBulletSpriteCache = new Sprite[4];

        /// <summary>对应下标是否已做过一次 Resources 查找（含结果为 null），避免每发子弹重复 Load + LogWarning。</summary>
        private readonly bool[] _playerBulletSpriteLookupDone = new bool[4];

        private int _playerBulletSpriteCacheCharacterId = -1;

        // 低速模式时显示的精确判定点（可选）
        [SerializeField] private GameObject _hitPointIndicator;

        [Header("火力升级（预制体子物体 Up / option）")]
        [Tooltip("空则查找子物体名 Up；贴图用 PlayerUp")]
        [SerializeField] private Transform _upgradeFxRoot;

        [Tooltip("空则查找子物体名 option；火力 Lv5（最高档）时显示，贴图 player/{角色}/option")]
        [SerializeField] private Transform _optionRoot;

        [SerializeField] private string _playerUpSpritePath = "Demos/Danmaku/Img/player/PlayerUp";

        private Coroutine _upgradeFxRoutine;
        private bool _upgradeFxPending;
        private Vector3 _upgradeFxBaseScale = Vector3.one;

        /// <param name="livesOverride">非空则覆盖 Inspector 中的初始残机。</param>
        /// <param name="bombsOverride">非空则覆盖初始炸弹。</param>
        /// <param name="characterIndex">主菜单角色下标 0→player/1，1→player/2。</param>
        public void Initialize(BulletManager bulletManager, EnemyManager enemyManager, int? livesOverride = null, int? bombsOverride = null, int characterIndex = 0)
        {
            _bulletManager = bulletManager;
            _enemyManager = enemyManager;
            Lives = livesOverride ?? _initialLives;
            _livesAtRunStart = Mathf.Max(1, Lives);
            Bombs = bombsOverride ?? _initialBombs;
            _refillBombs = Bombs;
            IsAlive = true;
            _invincibleTimer = 0f;
            _shotTimer = 0f;

            ApplyCharacterResources(characterIndex);
            RebuildPlayerShipVisuals();
            ResetUpgradeFxState();
            PrepareUpgradeAndOptionVisuals();
        }

        /// <summary>主菜单 <c>CharacterIndex</c>：0→资源文件夹 player/1，1→player/2。</summary>
        private void ApplyCharacterResources(int characterIndex)
        {
            _characterFolderId = Mathf.Clamp(characterIndex, 0, 1) + 1;
        }

        private string GetPlayerBulletSpritePathForLane(int laneIndex)
        {
            int idx = Mathf.Clamp(laneIndex, 0, 8);
            int fileIdx = Mathf.Min(idx, 3);
            if (_characterFolderId == 1)
                return $"Demos/Danmaku/Img/player/1/bullet{fileIdx}";
            return $"Demos/Danmaku/Img/player/2/MarisaBullet{fileIdx}";
        }

        private void InvalidatePlayerBulletSpriteCacheIfCharacterChanged()
        {
            if (_playerBulletSpriteCacheCharacterId == _characterFolderId)
                return;
            for (int i = 0; i < _playerBulletSpriteCache.Length; i++)
            {
                _playerBulletSpriteCache[i] = null;
                _playerBulletSpriteLookupDone[i] = false;
            }

            _playerBulletSpriteCacheCharacterId = _characterFolderId;
        }

        private Sprite GetCachedPlayerBulletSprite(int laneIndex)
        {
            InvalidatePlayerBulletSpriteCacheIfCharacterChanged();
            int fileIdx = Mathf.Min(Mathf.Clamp(laneIndex, 0, 8), 3);
            if (_playerBulletSpriteLookupDone[fileIdx])
                return _playerBulletSpriteCache[fileIdx];

            string path = GetPlayerBulletSpritePathForLane(laneIndex);
            var s = DanmakuSpriteUtil.TryLoadSprite(path);
            _playerBulletSpriteCache[fileIdx] = s;
            _playerBulletSpriteLookupDone[fileIdx] = true;
            if (s == null)
                Debug.LogWarning($"[PlayerController] 玩家子弹贴图未找到: {path}");

            return s;
        }

        public float GetHitRadius() => _hitRadius;

        public void SetWeaponBalance(DanmakuGameplayBalance balance) => _weaponBalance = balance;

        public void ApplyFireTier(int tier, DanmakuGameplayBalance balance)
        {
            if (balance != null)
                _weaponBalance = balance;
            int maxT = _weaponBalance != null ? _weaponBalance.MaxFireTierIndex : 4;
            int prev = _weaponTier;
            _weaponTier = Mathf.Clamp(tier, 0, maxT);
            if (_weaponTier > prev)
                PlayUpgradeFxIfNeeded();
            RefreshOptionVisibility();
        }

        public void ResetRunStatMultipliers()
        {
            _moveMul = 1f;
            _fireMul = 1f;
            _damageMul = 1f;
        }

        public void ApplyPickupMoveSpeed(float bonusMult) => _moveMul += bonusMult;

        public void ApplyPickupFireRate(float bonusMult) => _fireMul += bonusMult;

        public void ApplyPickupAttack(float bonusMult) => _damageMul += bonusMult;

        /// <summary>设置跨局平加攻击力（由弹幕每日战斗养成写入）。</summary>
        public void SetPersistentFlatAttackBonus(float flatBonus) =>
            _persistentFlatAttackBonus = Mathf.Max(0f, flatBonus);

        /// <summary>拾取加血；上限 99。</summary>
        public void HealLives(int amount)
        {
            if (amount <= 0 || !IsAlive) return;
            Lives = Mathf.Min(99, Lives + amount);
        }

        public void LogicUpdate(float dt)
        {
            if (!IsAlive) return;

            HandleMovement(dt);
            HandleBomb();
            HandleShooting(dt);
            UpdatePlayerBullets(dt);
            UpdateShipAnimation(dt);

            if (_invincibleTimer > 0f)
            {
                _invincibleTimer -= dt;
                // 闪烁效果（仅机体 Circle，不影响 Up/option）
                var sr = EnsureShipSpriteRenderer();
                if (sr != null)
                    sr.enabled = Mathf.FloorToInt(_invincibleTimer * 10f) % 2 == 0;
            }
            else
            {
                var sr = EnsureShipSpriteRenderer();
                if (sr != null) sr.enabled = true;
            }

            bool showHitPoint = UnityEngine.Input.GetKey(KeyCode.LeftShift);
            if (DanmakuMobileRuntime.IsMobileLike && DanmakuTouchInput.IsSlowHeld)
                showHitPoint = true;
            if (_hitPointIndicator != null)
                _hitPointIndicator.SetActive(showHitPoint);
        }

        private void HandleMovement(float dt)
        {
            bool slow = UnityEngine.Input.GetKey(KeyCode.LeftShift);
            if (DanmakuMobileRuntime.IsMobileLike && DanmakuTouchInput.IsSlowHeld)
                slow = true;
            float speed = slow ? _slowSpeed : _moveSpeed;

            float h = 0f, v = 0f;
            bool analogMove = false;
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)  || UnityEngine.Input.GetKey(KeyCode.A)) h -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow) || UnityEngine.Input.GetKey(KeyCode.D)) h += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow)  || UnityEngine.Input.GetKey(KeyCode.S)) v -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow)    || UnityEngine.Input.GetKey(KeyCode.W)) v += 1f;

            if (DanmakuMobileRuntime.IsMobileLike)
            {
                Vector2 j = DanmakuTouchInput.JoystickAxis;
                if (j.sqrMagnitude > 0.0004f)
                {
                    h = j.x;
                    v = j.y;
                    analogMove = true;
                }
            }

            Vector3 pos = transform.position;
            if (h != 0f || v != 0f)
            {
                Vector2 input = new Vector2(h, v);
                if (!analogMove && input.sqrMagnitude > 1f)
                    input.Normalize();
                else if (analogMove)
                    input = Vector2.ClampMagnitude(input, 1f);

                Vector2 move = input * (speed * _moveMul * dt);
                pos.x += move.x;
                pos.y += move.y;
            }

            // 限制在游戏区域内
            pos.x = Mathf.Clamp(pos.x, _playArea.xMin, _playArea.xMax);
            pos.y = Mathf.Clamp(pos.y, _playArea.yMin, _playArea.yMax);
            transform.position = pos;
        }

        private void HandleBomb()
        {
            // 安卓端由「半血以下受击」自动触发，不设手动键
            if (Application.platform == RuntimePlatform.Android)
                return;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
                TryUseBomb();
        }

        /// <summary>消耗一颗炸弹：清屏、无敌；成功返回 true。</summary>
        private bool TryUseBomb()
        {
            if (Bombs <= 0) return false;
            Bombs--;
            _invincibleTimer = _invincibleSeconds;
            _bulletManager?.ClearBullets();
            OnBombUsed?.Invoke(Bombs);
            return true;
        }

        private void HandleShooting(float dt)
        {
            _shotTimer -= dt;
            bool wantFire = _autoFire || UnityEngine.Input.GetKey(KeyCode.Z);
            if (!wantFire) return;
            if (_shotTimer > 0f) return;
            _shotTimer = _shotInterval / Mathf.Max(0.2f, _fireMul);

            SpawnPlayerBulletsBurst((Vector2)transform.position + _shotOffset);
        }

        private void SpawnPlayerBulletsBurst(Vector2 worldPos)
        {
            int n = _weaponBalance != null ? _weaponBalance.GetBulletCountForTier(_weaponTier) : 1;
            bool hom = _weaponBalance != null && _weaponBalance.TierUsesHoming(_weaponTier);
            float half = _weaponBalance != null ? _weaponBalance.multiShotSpreadHalfAngleDeg : 8f;
            float turn = _weaponBalance != null ? _weaponBalance.homingTurnRateDegPerSec : 540f;
            float dmg = _playerBulletDamage * _damageMul + _persistentFlatAttackBonus;
            float spd = _playerBulletSpeed;

            for (int i = 0; i < n; i++)
            {
                float angDeg = 0f;
                if (n > 1)
                    angDeg = Mathf.Lerp(-half, half, i / (float)(n - 1));
                Vector2 dir = RotateVectorDeg(Vector2.right, angDeg);
                SpawnSinglePlayerBullet(worldPos, dir, spd, dmg, hom, turn, i);
            }

            if (n > 0)
                DanmakuAudio.PlayPlayerShootSfx();
        }

        private static Vector2 RotateVectorDeg(Vector2 v, float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private void SpawnSinglePlayerBullet(Vector2 worldPos, Vector2 direction, float speed, float damage,
            bool homing, float homingTurnDeg, int laneIndex)
        {
            var sp = GetCachedPlayerBulletSprite(laneIndex);

            GameObject go = null;
            var pool = DemoGameEntry.Pool;
            if (pool != null)
            {
                if (_playerBulletPrefab != null)
                    go = pool.Spawn(_playerBulletPrefab, worldPos, Quaternion.identity);
                else if (!string.IsNullOrEmpty(_playerBulletPrefabResourcesPath))
                    go = pool.Spawn(_playerBulletPrefabResourcesPath, worldPos, Quaternion.identity);
            }

            if (go == null)
            {
                go = new GameObject("PlayerBullet");
                go.transform.position = worldPos;
                var srLegacy = go.AddComponent<SpriteRenderer>();
                if (sp != null)
                    srLegacy.sprite = sp;
                DanmakuSpriteUtil.ApplyGameplaySortingToRenderer(srLegacy);
                float z0 = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                if (!_playerBulletSpriteFacesRight)
                    z0 -= 90f;
                go.transform.rotation = Quaternion.Euler(0f, 0f, z0);
                var bulletLegacy = go.AddComponent<PlayerBullet>();
                bulletLegacy.Init(direction, speed, damage, _playerBulletHitRadius, homing, homingTurnDeg, _enemyManager);
                _playerBullets.Add(bulletLegacy);
                return;
            }

            go.transform.position = worldPos;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = go.AddComponent<SpriteRenderer>();
            if (sp != null)
                sr.sprite = sp;
            DanmakuSpriteUtil.ApplyGameplaySortingToRenderer(sr);

            float z = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (!_playerBulletSpriteFacesRight)
                z -= 90f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, z);

            var bullet = go.GetComponent<PlayerBullet>();
            if (bullet == null)
                bullet = go.AddComponent<PlayerBullet>();
            bullet.Init(direction, speed, damage, _playerBulletHitRadius, homing, homingTurnDeg, _enemyManager);
            _playerBullets.Add(bullet);
        }

        private void UpdatePlayerBullets(float dt)
        {
            int n = _playerBullets.Count;
            for (int i = 0; i < n; i++)
            {
                var b = _playerBullets[i];
                if (b == null || !b.IsAlive)
                    continue;
                b.LogicUpdate(dt, _playArea, _enemyManager);
            }

            CompactPlayerBullets();
        }

        /// <summary>单遍压实列表，避免多颗同时失效时 RemoveAt 造成 O(n²)。</summary>
        private void CompactPlayerBullets()
        {
            int w = 0;
            int c = _playerBullets.Count;
            for (int r = 0; r < c; r++)
            {
                var b = _playerBullets[r];
                if (b != null && b.IsAlive)
                    _playerBullets[w++] = b;
            }

            if (w < c)
                _playerBullets.RemoveRange(w, c - w);
        }

        private SpriteRenderer EnsureShipSpriteRenderer()
        {
            if (_shipSpriteRenderer != null)
                return _shipSpriteRenderer;
            var t = transform.Find("Circle");
            if (t != null)
                _shipSpriteRenderer = t.GetComponent<SpriteRenderer>();
            if (_shipSpriteRenderer == null)
                _shipSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            return _shipSpriteRenderer;
        }

        private void RebuildPlayerShipVisuals()
        {
            var list = new List<Sprite>();
            if (_playerAnimFrameSuffixes != null)
            {
                foreach (var suffix in _playerAnimFrameSuffixes)
                {
                    if (string.IsNullOrWhiteSpace(suffix)) continue;
                    string path = $"Demos/Danmaku/Img/player/{_characterFolderId}/{suffix.Trim()}";
                    var sp = DanmakuSpriteUtil.TryLoadSprite(path);
                    if (sp != null)
                        list.Add(sp);
                }
            }

            _shipAnimSprites = list.ToArray();
            _shipAnimTimer = 0f;
            _shipAnimFrame = 0;

            if (_shipAnimSprites.Length == 0)
            {
                var fallback = DanmakuSpriteUtil.TryLoadSprite($"Demos/Danmaku/Img/player/{_characterFolderId}/0");
                if (fallback != null)
                    _shipAnimSprites = new[] { fallback };
            }

            var sr = EnsureShipSpriteRenderer();
            if (sr == null)
            {
                Debug.LogWarning("[PlayerController] 未找到机体 SpriteRenderer（建议预制体子物体名为 Circle）。");
                return;
            }

            if (_shipAnimSprites.Length > 0)
                sr.sprite = _shipAnimSprites[0];
            else
                Debug.LogWarning($"[PlayerController] 玩家机体贴图未找到: player/{_characterFolderId}（序列帧与 0.png 均失败）");

            DanmakuSpriteUtil.BoostGameplaySorting(gameObject);
        }

        private void UpdateShipAnimation(float dt)
        {
            if (_shipAnimSprites == null || _shipAnimSprites.Length <= 1)
                return;
            var sr = EnsureShipSpriteRenderer();
            if (sr == null) return;

            _shipAnimTimer += dt;
            if (_shipAnimTimer < _playerAnimFrameSeconds)
                return;
            _shipAnimTimer -= _playerAnimFrameSeconds;
            _shipAnimFrame = (_shipAnimFrame + 1) % _shipAnimSprites.Length;
            sr.sprite = _shipAnimSprites[_shipAnimFrame];
        }

        public void TakeDamage()
        {
            if (!IsAlive || IsInvincible) return;

            // 安卓：残机已不高于开局一半时，受击自动耗 bomb 抵消本次伤害（仍要求有 bomb）
            if (Application.platform == RuntimePlatform.Android
                && Bombs > 0
                && _livesAtRunStart > 0
                && Lives <= _livesAtRunStart / 2
                && TryUseBomb())
                return;

            Lives--;
            OnDamaged?.Invoke();

            if (Lives < 0)
            {
                IsAlive = false;
                OnGameOver?.Invoke();
                return;
            }

            // 进入无敌时间
            _invincibleTimer = _invincibleSeconds;
            Bombs = _refillBombs;
        }

        public DanmakuCircle GetHitCircle()
        {
            Vector2 pos = transform.position;
            return new DanmakuCircle(pos.x, pos.y, _hitRadius);
        }

        /// <summary>拾取半径 = 受击半径 × 拾取倍率。</summary>
        public float GetPickupRadius() => _hitRadius * Mathf.Max(0.01f, _pickupRadiusMultiplier);

        public DanmakuCircle GetPickupCircle()
        {
            Vector2 pos = transform.position;
            return new DanmakuCircle(pos.x, pos.y, GetPickupRadius());
        }

        public void SetPlayArea(Rect area) => _playArea = area;

        /// <summary>当前局玩法区（世界坐标）；供掉落物等判定是否在可移动/可收集范围。</summary>
        public bool IsWorldPointInPlayArea(Vector2 worldPos) => _playArea.Contains(worldPos);

        /// <summary>重开一局时回收仍存活的自机子弹（池化实例走 <see cref="PlayerBullet.Kill"/>）。</summary>
        public void ClearActivePlayerBullets()
        {
            for (int i = 0; i < _playerBullets.Count; i++)
            {
                var b = _playerBullets[i];
                if (b != null && b.gameObject != null && b.IsAlive)
                    b.Kill();
            }
            _playerBullets.Clear();
        }

        private void ResetUpgradeFxState()
        {
            _upgradeFxPending = false;
            if (_upgradeFxRoutine != null)
            {
                StopCoroutine(_upgradeFxRoutine);
                _upgradeFxRoutine = null;
            }
        }

        private void EnsureUpgradeOptionRefs()
        {
            if (_upgradeFxRoot == null)
            {
                var t = transform.Find("Up");
                if (t != null) _upgradeFxRoot = t;
            }
            if (_optionRoot == null)
            {
                var t = transform.Find("option");
                if (t != null) _optionRoot = t;
            }
        }

        private void PrepareUpgradeAndOptionVisuals()
        {
            EnsureUpgradeOptionRefs();
            if (_upgradeFxRoot != null)
            {
                _upgradeFxBaseScale = _upgradeFxRoot.localScale;
                if (_upgradeFxBaseScale.sqrMagnitude < 0.0001f)
                    _upgradeFxBaseScale = Vector3.one;
                var sr = _upgradeFxRoot.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var sp = DanmakuSpriteUtil.TryLoadSprite(_playerUpSpritePath);
                    if (sp != null)
                        sr.sprite = sp;
                    var c = sr.color;
                    c.a = 0f;
                    sr.color = c;
                    sr.enabled = true;
                    DanmakuSpriteUtil.BoostGameplaySorting(_upgradeFxRoot.gameObject);
                }
            }

            if (_optionRoot != null)
            {
                _optionRoot.gameObject.SetActive(false);
                var sr = _optionRoot.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    string path = $"Demos/Danmaku/Img/player/{_characterFolderId}/option";
                    var sp = DanmakuSpriteUtil.TryLoadSprite(path);
                    if (sp != null)
                        sr.sprite = sp;
                    DanmakuSpriteUtil.BoostGameplaySorting(_optionRoot.gameObject);
                }
            }
        }

        private void RefreshOptionVisibility()
        {
            EnsureUpgradeOptionRefs();
            if (_optionRoot == null) return;
            int maxT = _weaponBalance != null ? _weaponBalance.MaxFireTierIndex : 4;
            bool show = _weaponTier >= maxT;
            _optionRoot.gameObject.SetActive(show);
            if (show)
            {
                var sr = _optionRoot.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    string path = $"Demos/Danmaku/Img/player/{_characterFolderId}/option";
                    var sp = DanmakuSpriteUtil.TryLoadSprite(path);
                    if (sp != null)
                        sr.sprite = sp;
                    DanmakuSpriteUtil.BoostGameplaySorting(_optionRoot.gameObject);
                }
            }
        }

        private void PlayUpgradeFxIfNeeded()
        {
            EnsureUpgradeOptionRefs();
            if (_upgradeFxRoot == null) return;
            if (_upgradeFxRoutine != null)
            {
                _upgradeFxPending = true;
                return;
            }
            _upgradeFxRoutine = StartCoroutine(CoUpgradePop());
        }

        private IEnumerator CoUpgradePop()
        {
            do
            {
                _upgradeFxPending = false;
                if (_upgradeFxRoot == null)
                    break;
                var sr = _upgradeFxRoot.GetComponent<SpriteRenderer>();
                if (sr == null)
                    break;

                Vector3 baseScale = _upgradeFxBaseScale.sqrMagnitude > 0.0001f ? _upgradeFxBaseScale : Vector3.one;
                Vector3 startScale = baseScale * 0.2f;
                _upgradeFxRoot.localScale = startScale;
                var col = sr.color;
                col.a = 0f;
                sr.color = col;

                float popIn = 0.36f;
                float t = 0f;
                while (t < popIn)
                {
                    t += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01(t / popIn);
                    float e = 1f - Mathf.Pow(1f - u, 3f);
                    col.a = e;
                    sr.color = col;
                    _upgradeFxRoot.localScale = Vector3.Lerp(startScale, baseScale * 1.12f, e);
                    yield return null;
                }

                col.a = 1f;
                sr.color = col;
                float hold = 0.1f;
                t = 0f;
                while (t < hold)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                float fadeOut = 0.38f;
                t = 0f;
                float a0 = col.a;
                while (t < fadeOut)
                {
                    t += Time.unscaledDeltaTime;
                    float u = t / fadeOut;
                    col.a = Mathf.Lerp(a0, 0f, u);
                    sr.color = col;
                    _upgradeFxRoot.localScale = Vector3.Lerp(baseScale * 1.12f, baseScale * 1.25f, u);
                    yield return null;
                }

                col.a = 0f;
                sr.color = col;
                _upgradeFxRoot.localScale = baseScale;
            } while (_upgradeFxPending);

            _upgradeFxRoutine = null;
        }

        private void OnDestroy()
        {
            ResetUpgradeFxState();
        }

        private void OnDrawGizmos()
        {
            if (_drawHitRadiusGizmo)
                DanmakuGizmoDraw.WireCircleXY(transform.position, _hitRadius, _hitRadiusGizmoColor);
            if (_drawPickupRadiusGizmo)
                DanmakuGizmoDraw.WireCircleXY(transform.position, GetPickupRadius(), _pickupRadiusGizmoColor);

            if (_drawAllPlayerBulletHitGizmos)
            {
                for (int i = 0; i < _playerBullets.Count; i++)
                {
                    var pb = _playerBullets[i];
                    if (pb == null || !pb.IsAlive) continue;
                    DanmakuGizmoDraw.WireCircleXY(pb.transform.position, pb.GetHitRadius(), _playerBulletHitGizmoColor);
                }
            }
        }
    }
}
