using System;
using System.Collections.Generic;
using UnityEngine;
using DemoFrameWork;
using DemoFrameWork.Pool;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 敌人管理器：生成、更新、回收、与玩家子弹碰撞检测。
    /// 对应参考 EnemyManager。
    /// </summary>
    public class EnemyManager : MonoBehaviour
    {
        [Header("横屏")]
        [Tooltip("敌机进场后向左移动的目标 X（世界坐标）；开局可由 DanmakuGameController 按玩法区覆盖）")]
        [SerializeField] private float _enemyExitTargetX = -6f;

        [Tooltip(">0 时覆盖预制体移动速度（世界单位/秒），避免过快贴到左边")]
        [SerializeField] private float _enemyMoveSpeedOverride = 0.38f;

        public void SetEnemyExitTargetX(float worldX) => _enemyExitTargetX = worldX;

        private void OnEnemyDiedRelay(DanmakuEnemy e)
        {
            if (e != null)
                e.OnDied -= OnEnemyDiedRelay;
            EnemyDied?.Invoke(e);
        }

        private readonly List<DanmakuEnemy> _aliveEnemies = new List<DanmakuEnemy>(32);
        private PlayerController _player;
        private BulletManager _bulletManager;
        private GameObjectPool _pool;

        public int AliveEnemyCount => _aliveEnemies.Count;

        public IReadOnlyList<DanmakuEnemy> AliveEnemies => _aliveEnemies;

        /// <summary>敌机死亡（Die 触发 OnDied 后）。</summary>
        public event Action<DanmakuEnemy> EnemyDied;

        public void Initialize(PlayerController player, BulletManager bulletMgr, GameObjectPool pool)
        {
            _player        = player;
            _bulletManager = bulletMgr;
            _pool          = pool;
        }

        /// <summary>
        /// 生成一个敌人，对应参考 StageTaskEntry(SpawnEnemy)。
        /// </summary>
        public DanmakuEnemy SpawnEnemy(string prefabPath, Vector2 worldPos)
        {
            if (string.IsNullOrEmpty(prefabPath)) return null;

            GameObject go = _pool != null
                ? _pool.Spawn(prefabPath, worldPos, Quaternion.identity)
                : Instantiate(Resources.Load<GameObject>(prefabPath), worldPos, Quaternion.identity);

            if (go == null)
            {
                Debug.LogWarning($"[EnemyManager] 无法加载敌人预制体: {prefabPath}");
                return null;
            }

            var enemy = go.GetComponent<DanmakuEnemy>();
            if (enemy == null)
            {
                Debug.LogWarning($"[EnemyManager] 预制体上没有 DanmakuEnemy 组件: {prefabPath}");
                return null;
            }

            enemy.Initialize(_bulletManager, _player, this);
            if (_enemyMoveSpeedOverride > 0.001f)
                enemy.SetMoveSpeed(_enemyMoveSpeedOverride);

            var p = go.transform.position;
            enemy.SetTargetPosition(new Vector2(_enemyExitTargetX, p.y));
            enemy.OnDied -= OnEnemyDiedRelay;
            enemy.OnDied += OnEnemyDiedRelay;
            _aliveEnemies.Add(enemy);
            return enemy;
        }

        /// <summary>每帧由 DanmakuGameController 调用。</summary>
        public void LogicUpdate(float dt)
        {
            int n = _aliveEnemies.Count;
            for (int i = 0; i < n; i++)
            {
                var e = _aliveEnemies[i];
                if (e == null || !e.IsAlive)
                    continue;
                e.LogicUpdate(dt);
            }

            CompactAliveEnemies();
        }

        private void CompactAliveEnemies()
        {
            int w = 0;
            int c = _aliveEnemies.Count;
            for (int r = 0; r < c; r++)
            {
                var e = _aliveEnemies[r];
                if (e != null && e.IsAlive)
                    _aliveEnemies[w++] = e;
            }

            if (w < c)
                _aliveEnemies.RemoveRange(w, c - w);
        }

        /// <summary>
        /// 玩家子弹命中检测：命中第一个敌人后造成伤害并返回 true。
        /// </summary>
        public bool TryDamageEnemy(DanmakuCircle hitCircle, float damage)
        {
            for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
            {
                var e = _aliveEnemies[i];
                if (e == null || !e.IsAlive) continue;
                if (DanmakuCollision.CheckCircleToCircle(hitCircle, e.GetHitCircle()))
                {
                    e.TakeDamage(damage);
                    DanmakuAudio.PlayHitEnemySfx();
                    return true;
                }
            }
            return false;
        }

        /// <summary>供追踪弹：最近存活敌机世界坐标。</summary>
        public bool TryGetNearestAliveEnemyCenter(Vector2 from, out Vector2 center)
        {
            center = default;
            float best = float.MaxValue;
            DanmakuEnemy bestE = null;
            for (int i = 0; i < _aliveEnemies.Count; i++)
            {
                var e = _aliveEnemies[i];
                if (e == null || !e.IsAlive) continue;
                Vector2 p = e.transform.position;
                float d = (p - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    bestE = e;
                }
            }

            if (bestE == null) return false;
            center = bestE.transform.position;
            return true;
        }

        /// <summary>由 DanmakuEnemy 在死亡时回调。</summary>
        public void RecycleEnemy(DanmakuEnemy enemy)
        {
            if (_pool != null)
                _pool.Recycle(enemy.gameObject);
            else
                Destroy(enemy.gameObject);
        }

        /// <summary>清除所有敌人（如关卡结束）。</summary>
        public void ClearAll()
        {
            foreach (var e in _aliveEnemies)
            {
                if (e != null && e.IsAlive)
                    e.TakeDamage(float.MaxValue);
            }
            _aliveEnemies.Clear();
        }

        /// <summary>
        /// 游戏结束清场：不计分、不掉落，全部回收到对象池。
        /// </summary>
        public void DespawnAllSilently()
        {
            for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
            {
                var e = _aliveEnemies[i];
                if (e != null)
                    e.DespawnSilentlyToPool();
            }
            _aliveEnemies.Clear();
        }
    }
}
