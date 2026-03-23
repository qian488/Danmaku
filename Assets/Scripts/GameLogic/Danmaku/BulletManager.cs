using System;
using System.Collections.Generic;
using UnityEngine;
using DemoFrameWork;
using DemoFrameWork.Pool;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 敌方子弹管理器：生成、逐帧更新、越界回收、与玩家碰撞检测。
    /// </summary>
    public class BulletManager : MonoBehaviour
    {
        [Header("子弹预制体（二选一）")]
        [Tooltip("优先：直接拖 BulletCircle 预制体（可不在 Resources 下）。需含 DanmakuBullet + PooledObject。")]
        [SerializeField] private GameObject _bulletPrefab;

        [Tooltip("未拖 _bulletPrefab 时使用 Resources 路径加载")]
        [SerializeField] private string _defaultBulletPrefabPath = "Demos/Danmaku/Prefabs/BulletCircle";

        [Tooltip("游戏区域（世界空间）：x/y 为左下角，width/height 为宽高")]
        [SerializeField] private Rect _playArea = new Rect(-8f, -4f, 14f, 8f);

        [Tooltip("子弹超出游戏区域后额外存活的帧数")]
        [SerializeField] private int _defaultMinLiveOutScreen = 10;

        [Header("敌弹贴图（Resources/Demos/Danmaku/Img/bullet/{样式}/{变体}）")]
        [Tooltip("为 true 且发射端未指定样式时，在下列编号区间内随机存在 bullet/{i}/0 的样式")]
        [SerializeField] private bool _randomizeBulletStyle = true;

        [Tooltip("随机样式时的最小编号（含）；仅会选用实际存在的子目录")]
        [SerializeField] private int _bulletStyleMin = 0;

        [Tooltip("随机样式时的最大编号（含）；与工程内 bullet 子文件夹一致")]
        [SerializeField] private int _bulletStyleMax = 18;

        [Tooltip("关闭随机且发射端未指定样式时使用的样式目录编号")]
        [SerializeField] private int _bulletStyleDefault = 0;

        [Tooltip("为 true 且未指定变体时，在该样式目录下随机一张存在的变体（各目录张数可不同）")]
        [SerializeField] private bool _randomizeBulletColorVariant = true;

        [Header("判定可视化（绘制所有存活敌弹的碰撞圆；Scene / Game 需开 Gizmos）")]
        [SerializeField] private bool _drawAllEnemyBulletHitGizmos = true;
        [SerializeField] private Color _enemyBulletGizmoColor = new Color(1f, 0.85f, 0.15f, 0.55f);

        private readonly List<DanmakuBullet> _aliveBullets = new List<DanmakuBullet>(256);
        private PlayerController _player;
        private GameObjectPool _pool;
        private float _grazeRadiusMultiplier = 4f;
        private Action _onPlayerGrazed;

        private Action<float, Vector2> _stageWaveSpawnHandler;
        private float _pendingStageWaveSpeed;
        private float _pendingStageWaveHitRadius;
        private int _pendingStageWaveLayer;
        private int _pendingStageWaveStyle;
        private int _pendingStageWaveVariant;

        public int AliveBulletCount => _aliveBullets.Count;

        private void Awake()
        {
            _stageWaveSpawnHandler = OnStageWaveSpawnBullet;
            DanmakuBulletResourceIndex.WarmupIfNeeded(_bulletStyleMin, _bulletStyleMax);
        }

        public void Initialize(PlayerController player, GameObjectPool pool, float grazeRadiusMultiplier = 4f,
            Action onPlayerGrazed = null)
        {
            _player = player;
            _pool = pool;
            _grazeRadiusMultiplier = Mathf.Max(1.01f, grazeRadiusMultiplier);
            _onPlayerGrazed = onPlayerGrazed;
        }

        public void SetPlayArea(Rect area) => _playArea = area;

        /// <summary>
        /// 同一波 / 一次 Execute 前调用一次：未指定样式（负数）时只随机一次，本波所有子弹共用同一样式目录。
        /// </summary>
        public int ResolveBulletStyleForWave(int explicitOrMinusOne)
        {
            return ResolveBulletStyleFolderIndex(explicitOrMinusOne);
        }

        /// <summary>
        /// 在已确定的样式目录下，未指定变体（负数）时随机一张存在的变体；否则钳到存在的编号。
        /// </summary>
        public int ResolveBulletVariantForWave(int resolvedStyleFolder, int explicitVariantOrMinusOne)
        {
            return ResolveBulletSpriteVariant(resolvedStyleFolder, explicitVariantOrMinusOne);
        }

        /// <summary>
        /// 执行关卡配置的弹幕波：<see cref="DanmakuShooter.Execute"/> 的回调使用缓存委托，避免每波分配闭包。
        /// </summary>
        public void ExecuteSpawnBulletWave(DanmakuShooter shooter, Vector2 originWorld, Vector2 playerWorld,
            float speed, float hitRadius, int layer, int resolvedStyleFolder, int resolvedVariant)
        {
            if (shooter == null) return;
            _pendingStageWaveSpeed = speed;
            _pendingStageWaveHitRadius = hitRadius;
            _pendingStageWaveLayer = layer;
            _pendingStageWaveStyle = resolvedStyleFolder;
            _pendingStageWaveVariant = resolvedVariant;
            shooter.Execute(originWorld, playerWorld, _stageWaveSpawnHandler);
        }

        private void OnStageWaveSpawnBullet(float angleDeg, Vector2 worldPos)
        {
            SpawnBullet(worldPos, angleDeg, _pendingStageWaveSpeed, _pendingStageWaveHitRadius, _pendingStageWaveLayer,
                null, _pendingStageWaveStyle, _pendingStageWaveVariant);
        }

        /// <summary>
        /// 生成一颗子弹。
        /// </summary>
        /// <param name="worldPos">生成位置。</param>
        /// <param name="angleDeg">发射角度（度），0 = 右，逆时针为正。</param>
        /// <param name="speed">速度（Unity 单位/秒）。</param>
        /// <param name="hitRadius">碰撞圆半径。</param>
        /// <param name="layer">子弹图层（用于炸弹/阶段清除）。</param>
        /// <param name="prefabPath">覆盖默认预制体路径；null = 使用默认。</param>
        /// <param name="bulletStyleFolderIndex">样式目录编号；为负数时按本组件「随机 / 默认」策略。</param>
        /// <param name="bulletSpriteVariantIndex">同目录下贴图编号；为负数时按随机变体或默认 0。</param>
        public DanmakuBullet SpawnBullet(Vector2 worldPos, float angleDeg, float speed,
            float hitRadius = 0.06f, int layer = 0, string prefabPath = null, int bulletStyleFolderIndex = -1,
            int bulletSpriteVariantIndex = -1)
        {
            GameObject go = null;

            if (!string.IsNullOrEmpty(prefabPath))
            {
                go = _pool != null ? _pool.Spawn(prefabPath, worldPos, Quaternion.identity)
                    : Instantiate(Resources.Load<GameObject>(prefabPath), worldPos, Quaternion.identity);
            }
            else if (_bulletPrefab != null)
            {
                go = _pool != null ? _pool.Spawn(_bulletPrefab, worldPos, Quaternion.identity)
                    : Instantiate(_bulletPrefab, worldPos, Quaternion.identity);
            }
            else
            {
                string path = _defaultBulletPrefabPath;
                go = _pool != null ? _pool.Spawn(path, worldPos, Quaternion.identity)
                    : Instantiate(Resources.Load<GameObject>(path), worldPos, Quaternion.identity);
            }

            if (go == null)
            {
                Debug.LogWarning("[BulletManager] 无法生成子弹：请拖 Bullet 预制体到 BulletManager，或放入 Resources 路径。");
                return null;
            }

            var bullet = go.GetComponent<DanmakuBullet>();
            if (bullet == null) bullet = go.AddComponent<DanmakuBullet>();

            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            int style = ResolveBulletStyleFolderIndex(bulletStyleFolderIndex);
            int variant = ResolveBulletSpriteVariant(style, bulletSpriteVariantIndex);
            bullet.Init(this, dir, speed, hitRadius, layer, _defaultMinLiveOutScreen, style, variant);
            _aliveBullets.Add(bullet);
            return bullet;
        }

        private int ResolveBulletStyleFolderIndex(int explicitOrMinusOne)
        {
            if (explicitOrMinusOne >= 0)
                return explicitOrMinusOne;
            if (_randomizeBulletStyle)
            {
                int lo = Mathf.Min(_bulletStyleMin, _bulletStyleMax);
                int hi = Mathf.Max(_bulletStyleMin, _bulletStyleMax);
                var folders = DanmakuBulletResourceIndex.GetStyleFoldersInRange(lo, hi);
                return folders[UnityEngine.Random.Range(0, folders.Length)];
            }

            return _bulletStyleDefault;
        }

        private int ResolveBulletSpriteVariant(int resolvedStyleFolder, int explicitOrMinusOne)
        {
            if (explicitOrMinusOne >= 0)
                return DanmakuBulletResourceIndex.ClampVariantToExisting(resolvedStyleFolder, explicitOrMinusOne);
            if (_randomizeBulletColorVariant)
            {
                var variants = DanmakuBulletResourceIndex.GetVariantIndicesForStyle(resolvedStyleFolder);
                return variants[UnityEngine.Random.Range(0, variants.Length)];
            }

            var v0 = DanmakuBulletResourceIndex.GetVariantIndicesForStyle(resolvedStyleFolder);
            return v0[0];
        }

        /// <summary>每帧由 DanmakuGameController 调用。</summary>
        public void LogicUpdate(float dt)
        {
            DanmakuCircle playerHit = _player != null ? _player.GetHitCircle() : default;
            DanmakuCircle playerGraze = default;
            bool haveGraze = false;
            if (_player != null && _player.IsAlive)
            {
                float r = playerHit.Radius * _grazeRadiusMultiplier;
                playerGraze = new DanmakuCircle(playerHit.X, playerHit.Y, r);
                haveGraze = true;
            }

            int n = _aliveBullets.Count;
            for (int i = 0; i < n; i++)
            {
                var b = _aliveBullets[i];
                if (b == null || !b.IsAlive)
                    continue;

                b.LogicUpdate(dt, _playArea);
                if (!b.IsAlive)
                    continue;

                // 与玩家碰撞（无敌帧仍允许擦弹，见计划）
                if (_player != null && _player.IsAlive)
                {
                    var bulletC = b.GetCircle();
                    if (!_player.IsInvincible &&
                        DanmakuCollision.CheckCircleToCircle(bulletC, playerHit))
                    {
                        b.Kill();
                        _player.TakeDamage();
                        if (_player == null || !_player.IsAlive)
                            break;
                        continue;
                    }

                    if (haveGraze && !b.GrazeConsumed &&
                        DanmakuCollision.CheckBulletGraze(bulletC, playerHit, playerGraze))
                    {
                        b.MarkGrazeConsumed();
                        _onPlayerGrazed?.Invoke();
                    }
                }
            }

            CompactAliveBullets();
        }

        /// <summary>单遍压实列表，避免大量 RemoveAt 导致 O(n²) 移动。</summary>
        private void CompactAliveBullets()
        {
            int w = 0;
            int c = _aliveBullets.Count;
            for (int r = 0; r < c; r++)
            {
                var b = _aliveBullets[r];
                if (b != null && b.IsAlive)
                    _aliveBullets[w++] = b;
            }

            if (w < c)
                _aliveBullets.RemoveRange(w, c - w);
        }

        /// <summary>清除指定图层（或所有）的子弹，由炸弹或关卡事件调用。</summary>
        public void ClearBullets(int layer = -1)
        {
            for (int i = 0; i < _aliveBullets.Count; i++)
            {
                var b = _aliveBullets[i];
                if (b == null || !b.IsAlive)
                    continue;
                if (layer < 0 || b.Layer == layer)
                    b.Kill();
            }

            CompactAliveBullets();
        }

        /// <summary>由 DanmakuBullet 在 Kill 时回调。</summary>
        public void RecycleBullet(DanmakuBullet bullet)
        {
            if (_pool != null)
                _pool.Recycle(bullet.gameObject);
            else
                Destroy(bullet.gameObject);
        }

        private void OnDrawGizmos()
        {
            if (!_drawAllEnemyBulletHitGizmos) return;
            for (int i = 0; i < _aliveBullets.Count; i++)
            {
                var b = _aliveBullets[i];
                if (b == null || !b.IsAlive) continue;
                DanmakuGizmoDraw.WireCircleXY(b.transform.position, b.HitRadius, _enemyBulletGizmoColor);
            }
        }
    }
}
