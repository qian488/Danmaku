using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 敌机死亡爆炸：小怪 biu0–13，Boss B0–13（Resources/Demos/Danmaku/Img/EnemyBombAnimation/）。
    /// </summary>
    public static class DanmakuEnemyDeathVfx
    {
        public const string ResourcesFolder = "Demos/Danmaku/Img/EnemyBombAnimation";

        public static void PlayAt(Vector3 worldPos, bool isBoss)
        {
            var go = new GameObject("EnemyDeathVfx");
            go.transform.position = worldPos;
            var sr = go.AddComponent<SpriteRenderer>();
            DanmakuSpriteUtil.BoostGameplaySorting(go);
            var runner = go.AddComponent<DanmakuEnemyDeathVfxRunner>();
            runner.Begin(isBoss);
        }
    }

    [DisallowMultipleComponent]
    public sealed class DanmakuEnemyDeathVfxRunner : MonoBehaviour
    {
        [SerializeField] private float _framesPerSecond = 15f;

        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _acc;
        private float _step;
        private int _idx;

        public void Begin(bool isBoss)
        {
            _sr = GetComponent<SpriteRenderer>();
            _frames = new Sprite[14];
            string prefix = isBoss ? $"{DanmakuEnemyDeathVfx.ResourcesFolder}/B" : $"{DanmakuEnemyDeathVfx.ResourcesFolder}/biu";
            for (int i = 0; i <= 13; i++)
            {
                var sp = DanmakuSpriteUtil.TryLoadSprite($"{prefix}{i}");
                _frames[i] = sp;
            }

            _step = _framesPerSecond > 0.01f ? 1f / _framesPerSecond : 0.066f;
            _idx = 0;
            if (_frames[0] != null)
                _sr.sprite = _frames[0];
            else
            {
                int first = -1;
                for (int i = 0; i < _frames.Length; i++)
                {
                    if (_frames[i] != null)
                    {
                        first = i;
                        break;
                    }
                }
                if (first >= 0)
                {
                    _idx = first;
                    _sr.sprite = _frames[first];
                }
            }
        }

        private void Update()
        {
            if (_frames == null || _sr == null) return;
            _acc += Time.deltaTime;
            if (_acc < _step) return;
            _acc -= _step;
            _idx++;
            if (_idx >= _frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            if (_frames[_idx] != null)
                _sr.sprite = _frames[_idx];
        }
    }
}
