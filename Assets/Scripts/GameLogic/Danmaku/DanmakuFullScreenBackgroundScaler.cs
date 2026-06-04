using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// Stretches UI-layer SpriteRenderer backgrounds to the current mobile canvas reference size.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-250)]
    public sealed class DanmakuFullScreenBackgroundScaler : MonoBehaviour
    {
        [SerializeField] private bool _useSlicedDrawMode = true;

        private SpriteRenderer _renderer;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private float _originalLocalZ;
        private float _originalScaleZ = 1f;
        private bool _capturedOriginalTransform;

        private void Awake()
        {
            if (!DanmakuMobileRuntime.IsMobileLike)
            {
                enabled = false;
                return;
            }

            _renderer = GetComponent<SpriteRenderer>();
            CaptureOriginalTransform();
            Apply(true);
        }

        private void LateUpdate()
        {
            if (!DanmakuMobileRuntime.IsMobileLike)
                return;

            Apply(false);
        }

        public void ForceApply()
        {
            if (!DanmakuMobileRuntime.IsMobileLike)
                return;

            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
            CaptureOriginalTransform();
            Apply(true);
        }

        private void Apply(bool force)
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
                return;
            if (_renderer.sprite == null)
                return;
            CaptureOriginalTransform();

            int w = Screen.width;
            int h = Screen.height;
            if (!force && w == _lastScreenWidth && h == _lastScreenHeight)
                return;

            _lastScreenWidth = w;
            _lastScreenHeight = h;

            Vector2 targetSize = DanmakuMobileLayoutUtility.GetMobileCanvasReferenceSize();
            transform.localPosition = new Vector3(0f, 0f, _originalLocalZ);
            transform.localRotation = Quaternion.identity;

            if (_useSlicedDrawMode && _renderer.sprite.border.sqrMagnitude > 0f)
            {
                _renderer.drawMode = SpriteDrawMode.Sliced;
                _renderer.size = targetSize;
                transform.localScale = new Vector3(1f, 1f, _originalScaleZ);
            }
            else
            {
                _renderer.drawMode = SpriteDrawMode.Simple;
                Vector2 spriteSize = _renderer.sprite.bounds.size;
                float sx = spriteSize.x > 0f ? targetSize.x / spriteSize.x : 1f;
                float sy = spriteSize.y > 0f ? targetSize.y / spriteSize.y : 1f;
                transform.localScale = new Vector3(sx, sy, _originalScaleZ);
            }
        }

        private void CaptureOriginalTransform()
        {
            if (_capturedOriginalTransform)
                return;

            _originalLocalZ = transform.localPosition.z;
            _originalScaleZ = transform.localScale.z;
            _capturedOriginalTransform = true;
        }
    }
}
