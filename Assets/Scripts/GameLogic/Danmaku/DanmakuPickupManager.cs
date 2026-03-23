using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DemoFrameWork;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 管理场景中 <see cref="DanmakuPickupDrop"/> 的逐帧更新与生成。
    /// </summary>
    [DisallowMultipleComponent]
    public class DanmakuPickupManager : MonoBehaviour
    {
        public static DanmakuPickupManager Instance { get; private set; }

        [Tooltip("拖入带 DanmakuPickupDrop 的预制体；空则尝试从 Resources 加载")]
        [SerializeField] private GameObject _pickupPrefab;

        [Tooltip("当 _pickupPrefab 为空时使用的 Resources 路径（相对 Resources，无扩展名）")]
        [SerializeField] private string _defaultPickupPrefabPath = "Demos/Danmaku/Prefabs/PickupItem";

        [Header("成组掉落")]
        [Tooltip("敌机死亡时一次生成的颗数下限（≥1）")]
        [SerializeField] private int _deathDropClusterMin = 2;

        [SerializeField] private int _deathDropClusterMax = 4;

        [Tooltip("场上随机刷新时一批的颗数范围")]
        [SerializeField] private int _ambientClusterMin = 3;

        [SerializeField] private int _ambientClusterMax = 6;

        [SerializeField] private float _clusterSpread = 0.22f;

        [Tooltip("掉落物根节点缩放（贴图像素较小时调大）")]
        [SerializeField] private float _pickupRootScale = 2.2f;

        private const string PickupSpritesResourcesPath = "Demos/Danmaku/Img/Pickup";

        /// <summary>战前 <see cref="PrewarmPickupSpritesCache"/> 写入，避免首颗掉落物触发同步 <c>LoadAll</c>。</summary>
        private static Sprite[] _sharedPickupSpritesPrewarmed;

        private Sprite[] _pickupSpritesCache;

        /// <summary>
        /// 在 Loading / 关前协程调用一次：同步 <c>Resources.LoadAll</c>，将尖刺固定在非战斗阶段。
        /// </summary>
        public static void PrewarmPickupSpritesCache()
        {
            if (_sharedPickupSpritesPrewarmed != null && _sharedPickupSpritesPrewarmed.Length > 0)
                return;
            _sharedPickupSpritesPrewarmed = Resources.LoadAll<Sprite>(PickupSpritesResourcesPath);
        }

        private readonly List<DanmakuPickupDrop> _drops = new List<DanmakuPickupDrop>(64);
        private PlayerController _player;

        private DanmakuGameplayBalance _balance;
        /// <summary>玩法区（兜底）</summary>
        private Rect _worldPlayRect;

        /// <summary>道具生成区：一般为「道具收集线」右侧至屏幕右缘（世界 Rect）。</summary>
        private Rect _itemDropSpawnRect;
        private Action<DanmakuPickupKind> _onPickupCollected;
        private Coroutine _ambientRoutine;
        private bool _ambientEnabled = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            StopAmbientSpawns();
            if (Instance == this)
                Instance = null;
        }

        public void Initialize(PlayerController player)
        {
            _player = player;
        }

        /// <summary>
        /// <paramref name="worldPlayRect"/>：玩法区；<paramref name="itemDropSpawnRect"/>：道具出现位置（死亡掉落 / 环境刷新均用此区）。
        /// </summary>
        public void SetBalanceAndSpawnRect(DanmakuGameplayBalance balance, Rect worldPlayRect, Rect itemDropSpawnRect)
        {
            _balance = balance;
            _worldPlayRect = worldPlayRect;
            _itemDropSpawnRect = itemDropSpawnRect.width > 0.001f && itemDropSpawnRect.height > 0.001f
                ? itemDropSpawnRect
                : worldPlayRect;
        }

        public void SetPickupCollectedCallback(Action<DanmakuPickupKind> cb) => _onPickupCollected = cb;

        internal void NotifyPickupCollected(DanmakuPickupKind kind) => _onPickupCollected?.Invoke(kind);

        public void SetAmbientSpawnsEnabled(bool on)
        {
            _ambientEnabled = on;
        }

        public void StartAmbientSpawns()
        {
            StopAmbientSpawns();
            _ambientRoutine = StartCoroutine(CoAmbientSpawns());
        }

        public void StopAmbientSpawns()
        {
            if (_ambientRoutine != null)
            {
                StopCoroutine(_ambientRoutine);
                _ambientRoutine = null;
            }
        }

        private IEnumerator CoAmbientSpawns()
        {
            while (true)
            {
                var b = _balance;
                float wait = b != null
                    ? UnityEngine.Random.Range(b.ambientSpawnIntervalMin, b.ambientSpawnIntervalMax)
                    : 5f;
                yield return new WaitForSeconds(wait);

                if (!_ambientEnabled || _player == null || !_player.IsAlive)
                    continue;
                if (b == null)
                    continue;
                if (UnityEngine.Random.value > b.ambientSpawnChance)
                    continue;

                Vector2 p = RandomPointInRect(_itemDropSpawnRect);
                var kind = b.PickRandomKind(b.ambientDropWeights);
                int count = UnityEngine.Random.Range(_ambientClusterMin, _ambientClusterMax + 1);
                SpawnPickupClusterAt(p, kind, count);
            }
        }

        private static Vector2 RandomPointInRect(Rect r)
        {
            return new Vector2(
                UnityEngine.Random.Range(r.xMin, r.xMax),
                UnityEngine.Random.Range(r.yMin, r.yMax));
        }

        /// <summary>敌机死亡时调用；尊重预制体上「允许掉落」与配置概率。</summary>
        public void TryDeathDrop(Vector2 worldPos, bool enemySpawnPickupEnabled)
        {
            _ = worldPos;
            if (!enemySpawnPickupEnabled || _player == null)
                return;
            var b = _balance;
            float p = b != null ? b.deathDropChance : 0.35f;
            if (UnityEngine.Random.value > p)
                return;

            var kind = b != null
                ? b.PickRandomKind(b.deathDropWeights)
                : DanmakuPickupKind.Power;
            int lo = Mathf.Max(1, _deathDropClusterMin);
            int hi = Mathf.Max(lo, _deathDropClusterMax);
            int count = UnityEngine.Random.Range(lo, hi + 1);
            Vector2 baseInStrip = RandomPointInRect(_itemDropSpawnRect);
            SpawnPickupClusterAt(baseInStrip, kind, count);
        }

        /// <summary>同一逻辑类型、多件随机贴图，位置微散开。</summary>
        public void SpawnPickupClusterAt(Vector2 worldPos, DanmakuPickupKind kind, int count)
        {
            count = Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                Vector2 p = worldPos + UnityEngine.Random.insideUnitCircle * _clusterSpread;
                p = ClampToItemDropRect(p);
                SpawnPickupAt(p, kind);
            }
        }

        private Vector2 ClampToItemDropRect(Vector2 p)
        {
            var r = _itemDropSpawnRect;
            if (r.width < 0.0001f || r.height < 0.0001f)
                r = _worldPlayRect;
            if (r.width < 0.0001f) return p;
            return new Vector2(
                Mathf.Clamp(p.x, r.xMin, r.xMax),
                Mathf.Clamp(p.y, r.yMin, r.yMax));
        }

        public void Register(DanmakuPickupDrop drop)
        {
            if (drop != null && !_drops.Contains(drop))
                _drops.Add(drop);
        }

        public void Unregister(DanmakuPickupDrop drop)
        {
            if (drop != null)
                _drops.Remove(drop);
        }

        /// <summary>重开一局或结算清场：池化实例回池，否则销毁。</summary>
        public void ClearAllDrops()
        {
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                if (d != null)
                    d.ForceRecycleFromManager();
            }
            _drops.Clear();
        }

        public void LogicUpdate(float dt)
        {
            if (_player == null || !_player.IsAlive)
                return;

            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                if (d == null)
                {
                    _drops.RemoveAt(i);
                    continue;
                }
                d.LogicUpdate(dt, _player);
            }
        }

        public DanmakuPickupDrop SpawnPickupAt(Vector2 worldPos, DanmakuPickupKind kind = DanmakuPickupKind.Power)
        {
            if (_player == null)
            {
                Debug.LogWarning("[DanmakuPickupManager] 未 Initialize，无法生成掉落物。");
                return null;
            }

            GameObject go = null;
            var pool = DemoGameEntry.Pool;
            if (pool != null)
            {
                if (_pickupPrefab != null)
                    go = pool.Spawn(_pickupPrefab, worldPos, Quaternion.identity);
                else if (!string.IsNullOrEmpty(_defaultPickupPrefabPath))
                    go = pool.Spawn(_defaultPickupPrefabPath, worldPos, Quaternion.identity);
            }

            DanmakuPickupDrop drop;
            if (go != null)
            {
                drop = go.GetComponent<DanmakuPickupDrop>();
                if (drop == null)
                    drop = go.AddComponent<DanmakuPickupDrop>();
            }
            else
            {
                GameObject prefab = _pickupPrefab;
                if (prefab == null && !string.IsNullOrEmpty(_defaultPickupPrefabPath))
                    prefab = Resources.Load<GameObject>(_defaultPickupPrefabPath);

                if (prefab != null)
                {
                    go = Instantiate(prefab, worldPos, Quaternion.identity);
                    drop = go.GetComponent<DanmakuPickupDrop>();
                    if (drop == null)
                        drop = go.AddComponent<DanmakuPickupDrop>();
                }
                else
                    drop = CreateRuntimePickup(worldPos);
            }

            drop.Kind = kind;
            ApplyRandomPickupVisual(drop);
            if (drop != null && _pickupRootScale > 0.001f)
                drop.transform.localScale = Vector3.one * _pickupRootScale;
            drop.Setup(this, _player);
            return drop;
        }

        private Sprite[] PickupSprites
        {
            get
            {
                if (_pickupSpritesCache == null || _pickupSpritesCache.Length == 0)
                {
                    if (_sharedPickupSpritesPrewarmed != null && _sharedPickupSpritesPrewarmed.Length > 0)
                        _pickupSpritesCache = _sharedPickupSpritesPrewarmed;
                    else
                        _pickupSpritesCache = Resources.LoadAll<Sprite>(PickupSpritesResourcesPath);
                }

                return _pickupSpritesCache;
            }
        }

        private void ApplyRandomPickupVisual(DanmakuPickupDrop drop)
        {
            if (drop == null) return;
            var sprites = PickupSprites;
            if (sprites == null || sprites.Length == 0) return;

            var sp = sprites[UnityEngine.Random.Range(0, sprites.Length)];
            var sr = drop.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null) sr = drop.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            DanmakuSpriteUtil.BoostGameplaySorting(drop.gameObject);
        }

        /// <summary>兼容旧调用：无类型时使用预制体默认 Kind。</summary>
        public DanmakuPickupDrop SpawnPickupAt(Vector2 worldPos)
        {
            return SpawnPickupAt(worldPos, DanmakuPickupKind.Power);
        }

        private DanmakuPickupDrop CreateRuntimePickup(Vector2 worldPos)
        {
            var go = new GameObject("PickupItem");
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * (0.55f * Mathf.Max(0.2f, _pickupRootScale));
            go.AddComponent<SpriteRenderer>();
            DanmakuSpriteUtil.BoostGameplaySorting(go);
            var drop = go.AddComponent<DanmakuPickupDrop>();
            ApplyRandomPickupVisual(drop);
            return drop;
        }
    }
}
