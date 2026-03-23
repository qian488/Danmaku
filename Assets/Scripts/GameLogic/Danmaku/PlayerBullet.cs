using UnityEngine;
using DemoFrameWork.Pool;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 玩家子弹：直线或追踪，越界或命中敌人后回收到对象池（若存在 <see cref="PooledObject"/>）。
    /// </summary>
    public class PlayerBullet : MonoBehaviour, IPoolable
    {
        private Vector2 _dir;
        private float _speed;
        private float _damage;
        private float _hitRadius;
        private bool _isAlive;
        private bool _homing;
        private float _homingTurnRateDeg;
        private EnemyManager _enemyManager;

        [Header("判定可视化（Scene / Game 需开 Gizmos）")]
        [SerializeField] private bool _drawHitRadiusGizmo;
        [SerializeField] private Color _hitRadiusGizmoColor = new Color(0.3f, 0.85f, 1f, 0.85f);

        public bool IsAlive => _isAlive;

        public float GetHitRadius() => _hitRadius;

        public void Init(Vector2 direction, float speed, float damage, float hitRadius, bool homing = false,
            float homingTurnRateDegPerSec = 540f, EnemyManager enemyManager = null)
        {
            _dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _speed = speed;
            _damage = damage;
            _hitRadius = hitRadius;
            _homing = homing;
            _homingTurnRateDeg = homingTurnRateDegPerSec;
            _enemyManager = enemyManager;
            _isAlive = true;
        }

        public void OnSpawn()
        {
            _isAlive = false;
        }

        public void OnRecycle()
        {
            _isAlive = false;
            _enemyManager = null;
        }

        public void LogicUpdate(float dt, Rect playArea, EnemyManager enemyManager)
        {
            if (!_isAlive) return;

            if (_homing && enemyManager != null &&
                enemyManager.TryGetNearestAliveEnemyCenter(transform.position, out Vector2 target))
            {
                Vector2 to = target - (Vector2)transform.position;
                if (to.sqrMagnitude > 0.0001f)
                {
                    Vector2 want = to.normalized;
                    float maxRad = _homingTurnRateDeg * Mathf.Deg2Rad * dt;
                    _dir = RotateTowards(_dir, want, maxRad);
                }
            }

            transform.position += (Vector3)(_dir * _speed * dt);

            Vector2 pos = transform.position;
            if (!playArea.Contains(pos))
            {
                Kill();
                return;
            }

            var mgr = enemyManager ?? _enemyManager;
            if (mgr != null)
            {
                var c = new DanmakuCircle(pos.x, pos.y, _hitRadius);
                if (mgr.TryDamageEnemy(c, _damage))
                    Kill();
            }
        }

        private static Vector2 RotateTowards(Vector2 from, Vector2 to, float maxRadians)
        {
            float maxDeg = maxRadians * Mathf.Rad2Deg;
            float signed = Vector2.SignedAngle(from, to);
            if (signed > maxDeg) signed = maxDeg;
            if (signed < -maxDeg) signed = -maxDeg;
            float rad = signed * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(from.x * c - from.y * s, from.x * s + from.y * c);
        }

        public void Kill()
        {
            if (!_isAlive) return;
            _isAlive = false;
            var po = GetComponent<PooledObject>();
            if (po != null && po.Pool != null)
                po.Recycle();
            else
                Destroy(gameObject);
        }

        private void OnDrawGizmos()
        {
            if (!_drawHitRadiusGizmo) return;
            if (!_isAlive) return;
            DanmakuGizmoDraw.WireCircleXY(transform.position, _hitRadius, _hitRadiusGizmoColor);
        }
    }
}
