using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// Keeps the gameplay camera framed for the mobile reference aspect without letterboxing the viewport.
    /// Background renderers can then cover the full physical screen.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-300)]
    public sealed class DanmakuGameplayAspectAdapter : MonoBehaviour
    {
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private bool _lockAspect = true;

        public static Rect CurrentGameplayViewport { get; private set; } = new Rect(0f, 0f, 1f, 1f);

        private Camera _camera;
        private float _baseOrthographicSize = -1f;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private float _lastOrthographicSize = -1f;

        private void Awake()
        {
            if (!DanmakuMobileRuntime.IsMobileLike)
            {
                enabled = false;
                return;
            }

            _camera = GetComponent<Camera>();
            if (_camera != null && _camera.orthographic)
                _baseOrthographicSize = _camera.orthographicSize;
            ApplyIfNeeded(true);
        }

        private void OnEnable()
        {
            if (!DanmakuMobileRuntime.IsMobileLike)
                return;

            ApplyIfNeeded(true);
        }

        private void LateUpdate()
        {
            if (!DanmakuMobileRuntime.IsMobileLike)
                return;

            ApplyIfNeeded(false);
        }

        private void OnDisable()
        {
            if (_camera != null)
            {
                _camera.rect = new Rect(0f, 0f, 1f, 1f);
                if (_baseOrthographicSize > 0f && _camera.orthographic)
                    _camera.orthographicSize = _baseOrthographicSize;
            }

            CurrentGameplayViewport = new Rect(0f, 0f, 1f, 1f);
        }

        private void ApplyIfNeeded(bool force)
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();
            if (_camera == null)
                return;
            if (_camera.rect != new Rect(0f, 0f, 1f, 1f))
                _camera.rect = new Rect(0f, 0f, 1f, 1f);
            if (_baseOrthographicSize <= 0f && _camera.orthographic)
                _baseOrthographicSize = _camera.orthographicSize;

            int w = Screen.width;
            int h = Screen.height;
            if (w <= 0 || h <= 0)
                return;

            float targetOrthographicSize = ComputeOrthographicSize(w, h);
            if (!force
                && w == _lastScreenWidth
                && h == _lastScreenHeight
                && Mathf.Approximately(targetOrthographicSize, _lastOrthographicSize))
                return;

            _lastScreenWidth = w;
            _lastScreenHeight = h;
            _lastOrthographicSize = targetOrthographicSize;
            CurrentGameplayViewport = new Rect(0f, 0f, 1f, 1f);
            if (_camera.orthographic)
                _camera.orthographicSize = targetOrthographicSize;
        }

        private float ComputeOrthographicSize(float screenWidth, float screenHeight)
        {
            if (!_lockAspect || !_camera.orthographic || _baseOrthographicSize <= 0f)
                return _camera != null && _camera.orthographic ? _camera.orthographicSize : 0f;

            float referenceAspect = ReferenceAspect;
            float screenAspect = screenWidth / screenHeight;

            if (screenAspect >= referenceAspect)
                return _baseOrthographicSize;

            return _baseOrthographicSize * (referenceAspect / Mathf.Max(0.01f, screenAspect));
        }

        public float ReferenceAspect =>
            Mathf.Max(0.01f, _referenceResolution.x) / Mathf.Max(0.01f, _referenceResolution.y);
    }
}
