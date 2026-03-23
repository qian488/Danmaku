using UnityEngine;
using DemoFrameWork.Pool;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 道中掉落物：出现动画 → 上下浮动 → 进入拾取圆后先旋转两圈 → 飞向自机 → 拾取。
    /// </summary>
    [DisallowMultipleComponent]
    public class DanmakuPickupDrop : MonoBehaviour, IPoolable
    {
        private enum Phase
        {
            SpawnIntro,
            Floating,
            CollectSpin,
            CollectHoming
        }

        [Header("移动")]
        [Tooltip("被吸引后移向自机的速度（世界单位/秒）")]
        [SerializeField] private float _homingSpeed = 12f;

        [Tooltip("与自机距离小于此值视为拾取成功（世界单位）")]
        [SerializeField] private float _collectDistance = 0.12f;

        [Header("出现 / 待机")]
        [SerializeField] private float _spawnIntroSeconds = 0.2f;

        [Tooltip("出现：从目标缩放的该比例缩放到满")]
        [SerializeField] private float _spawnScaleFromMul = 0.35f;

        [Tooltip("上下浮动幅度（世界单位）")]
        [SerializeField] private float _bobAmplitude = 0.1f;

        [SerializeField] private float _bobFrequencyHz = 2.2f;

        [Header("收集")]
        [Tooltip("拾取时旋转两圈所用时间（秒）")]
        [SerializeField] private float _collectSpinSeconds = 0.45f;

        [Tooltip("两圈 = 720°")]
        [SerializeField] private float _collectSpinDegrees = 720f;

        [SerializeField] private DanmakuPickupKind _kind = DanmakuPickupKind.Power;

        private DanmakuPickupManager _manager;
        private PlayerController _player;
        private bool _collected;
        private Phase _phase = Phase.SpawnIntro;

        private float _spawnT;
        private float _bobClock;
        private Vector3 _floatBasePos;
        private float _visualScaleTarget = 1f;

        private float _spinT;

        /// <summary>拾取成功时触发（可加分数等）。</summary>
        public event System.Action<DanmakuPickupDrop> PickedUp;

        public DanmakuPickupKind Kind
        {
            get => _kind;
            set => _kind = value;
        }

        public void Setup(DanmakuPickupManager manager, PlayerController player)
        {
            _manager = manager;
            _player = player;
            _manager?.Register(this);

            _visualScaleTarget = Mathf.Max(0.05f, transform.localScale.x);
            transform.localScale = Vector3.one * (_visualScaleTarget * _spawnScaleFromMul);
            transform.localRotation = Quaternion.identity;

            _phase = Phase.SpawnIntro;
            _spawnT = 0f;
            _bobClock = 0f;
            _floatBasePos = transform.position;
            _spinT = 0f;
        }

        public void OnSpawn()
        {
            _collected = false;
            _phase = Phase.SpawnIntro;
            _spawnT = 0f;
            _spinT = 0f;
            transform.localRotation = Quaternion.identity;
        }

        public void OnRecycle()
        {
            _manager?.Unregister(this);
            _manager = null;
            _player = null;
        }

        internal void ForceRecycleFromManager()
        {
            _manager?.Unregister(this);
            _manager = null;
            _player = null;
            _collected = true;

            var pooled = GetComponent<PooledObject>();
            if (pooled != null && pooled.Pool != null)
                pooled.Recycle();
            else if (gameObject != null)
                Destroy(gameObject);
        }

        public void LogicUpdate(float dt, PlayerController player)
        {
            if (_collected) return;
            if (player == null || !player.IsAlive)
                return;

            _player = player;

            switch (_phase)
            {
                case Phase.SpawnIntro:
                    UpdateSpawnIntro(dt);
                    break;
                case Phase.Floating:
                    UpdateFloating(dt, player);
                    break;
                case Phase.CollectSpin:
                    UpdateCollectSpin(dt);
                    break;
                case Phase.CollectHoming:
                    UpdateCollectHoming(dt);
                    break;
            }
        }

        private void UpdateSpawnIntro(float dt)
        {
            _spawnT += dt;
            float u = _spawnIntroSeconds > 0.0001f ? Mathf.Clamp01(_spawnT / _spawnIntroSeconds) : 1f;
            float s = Mathf.SmoothStep(_spawnScaleFromMul, 1f, u);
            transform.localScale = Vector3.one * (_visualScaleTarget * s);

            if (u >= 1f)
            {
                transform.localScale = Vector3.one * _visualScaleTarget;
                _floatBasePos = transform.position;
                _bobClock = 0f;
                _phase = Phase.Floating;
            }
        }

        private void UpdateFloating(float dt, PlayerController player)
        {
            _bobClock += dt * Mathf.PI * 2f * _bobFrequencyHz;
            float y = Mathf.Sin(_bobClock) * _bobAmplitude;
            transform.position = _floatBasePos + new Vector3(0f, y, 0f);

            float pickupR = player.GetPickupRadius();
            float dist = Vector2.Distance((Vector2)transform.position, (Vector2)player.transform.position);
            if (dist <= pickupR)
            {
                _floatBasePos = transform.position;
                transform.localEulerAngles = Vector3.zero;
                _spinT = 0f;
                _phase = Phase.CollectSpin;
            }
        }

        private void UpdateCollectSpin(float dt)
        {
            _spinT += dt;
            float u = _collectSpinSeconds > 0.0001f ? Mathf.Clamp01(_spinT / _collectSpinSeconds) : 1f;
            float z = Mathf.Lerp(0f, _collectSpinDegrees, u);
            transform.localEulerAngles = new Vector3(0f, 0f, z);

            if (u >= 1f)
            {
                transform.localEulerAngles = new Vector3(0f, 0f, _collectSpinDegrees);
                _phase = Phase.CollectHoming;
            }
        }

        private void UpdateCollectHoming(float dt)
        {
            if (_player == null) return;
            Vector3 plPos = _player.transform.position;
            transform.position = Vector3.MoveTowards(transform.position, plPos, _homingSpeed * dt);

            if (Vector2.Distance((Vector2)transform.position, (Vector2)plPos) <= _collectDistance)
                Collect();
        }

        private void Collect()
        {
            if (_collected) return;
            _collected = true;
            PickedUp?.Invoke(this);
            DanmakuAudio.PlayStageItemDropSfx();
            _manager?.NotifyPickupCollected(_kind);
            _manager?.Unregister(this);
            _manager = null;
            _player = null;

            var pooled = GetComponent<PooledObject>();
            if (pooled != null && pooled.Pool != null)
                pooled.Recycle();
            else if (gameObject != null)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_manager != null)
                _manager.Unregister(this);
        }
    }
}
