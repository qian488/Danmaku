using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DemoFrameWork.GameLogic.Danmaku;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// Full-screen floating mobile joystick. Non-UI touches can start movement; a second non-UI touch enables slow mode.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class DanmakuTouchJoystick : MonoBehaviour
    {
        [Header("Feel")]
        [SerializeField] private float _outerRadiusPx = 112f;
        [SerializeField] private float _knobRadiusPx = 34f;
        [SerializeField] private float _maxStickPx = 72f;
        [SerializeField] private float _deadZonePx = 10f;
        [SerializeField] private float _responseExponent = 0.82f;
        [SerializeField] private float _axisRiseSpeed = 24f;
        [SerializeField] private float _axisFallSpeed = 34f;

        [Header("Visual")]
        [SerializeField] private Color _ringColor = new Color(0.45f, 0.75f, 1f, 0.36f);
        [SerializeField] private Color _ringEdgeColor = new Color(0.78f, 0.95f, 1f, 0.65f);
        [SerializeField] private Color _knobColor = new Color(1f, 1f, 1f, 0.78f);
        [SerializeField] private Color _knobCoreColor = new Color(0.38f, 0.72f, 1f, 0.95f);
        [SerializeField] private float _safeAreaPaddingPx = 12f;

        private static Sprite s_ringSprite;
        private static Sprite s_knobSprite;

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _knob;
        private CanvasGroup _canvasGroup;
        private int _activeFingerId = -1;
        private Vector2 _anchorScreen;
        private Vector2 _smoothedAxis;

        private void Awake()
        {
            if (!DanmakuMobileRuntime.IsMobileLike)
            {
                enabled = false;
                return;
            }

            EnsureEventSystem();
            BuildUi();
            DanmakuTouchInput.ClearAll();
        }

        private void OnDisable()
        {
            DanmakuTouchInput.ClearAll();
        }

        private void Update()
        {
            if (!DanmakuMobileRuntime.IsMobileLike)
                return;

            DanmakuTouchInput.IsSlowHeld = false;

            if (_activeFingerId < 0)
                TryBeginFromNewTouch();

            if (_activeFingerId >= 0)
                UpdateActiveTouch();

            DanmakuTouchInput.IsSlowHeld = HasSecondaryNonUiTouch();
        }

        private void TryBeginFromNewTouch()
        {
            for (int i = 0; i < global::UnityEngine.Input.touchCount; i++)
            {
                var t = global::UnityEngine.Input.GetTouch(i);
                if (t.phase != TouchPhase.Began)
                    continue;
                if (IsTouchOverUi(t.fingerId))
                    continue;
                BeginStick(t.fingerId, t.position);
                break;
            }
        }

        private void UpdateActiveTouch()
        {
            bool found = false;
            for (int i = 0; i < global::UnityEngine.Input.touchCount; i++)
            {
                var t = global::UnityEngine.Input.GetTouch(i);
                if (t.fingerId != _activeFingerId)
                    continue;

                found = true;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    EndStick();
                else
                    DragStick(t.position);
                break;
            }

            if (!found)
                EndStick();
        }

        private bool HasSecondaryNonUiTouch()
        {
            if (_activeFingerId < 0)
                return false;

            for (int i = 0; i < global::UnityEngine.Input.touchCount; i++)
            {
                var t = global::UnityEngine.Input.GetTouch(i);
                if (t.fingerId == _activeFingerId)
                    continue;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    continue;
                if (IsTouchOverUi(t.fingerId))
                    continue;
                return true;
            }

            return false;
        }

        private static bool IsTouchOverUi(int fingerId)
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject(fingerId);
        }

        private void BeginStick(int fingerId, Vector2 screenPos)
        {
            _activeFingerId = fingerId;
            _anchorScreen = screenPos;
            _root.gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            PlaceRootAtScreen(ClampVisualAnchorToSafeArea(screenPos));
            _smoothedAxis = Vector2.zero;
            DragStick(screenPos);
        }

        private void DragStick(Vector2 screenPos)
        {
            Vector2 desiredAxis = ComputeAxis(screenPos, out Vector2 knobOffset);
            _knob.anchoredPosition = knobOffset;

            float speed = desiredAxis.sqrMagnitude > _smoothedAxis.sqrMagnitude ? _axisRiseSpeed : _axisFallSpeed;
            _smoothedAxis = Vector2.MoveTowards(_smoothedAxis, desiredAxis, speed * Time.unscaledDeltaTime);
            DanmakuTouchInput.JoystickAxis = _smoothedAxis;
        }

        private Vector2 ComputeAxis(Vector2 screenPos, out Vector2 knobOffset)
        {
            Vector2 delta = screenPos - _anchorScreen;
            float magnitude = delta.magnitude;
            if (magnitude <= _deadZonePx)
            {
                knobOffset = Vector2.zero;
                return Vector2.zero;
            }

            Vector2 dir = delta / magnitude;
            float clamped = Mathf.Min(magnitude, _maxStickPx);
            knobOffset = dir * clamped;

            float usableRange = Mathf.Max(1f, _maxStickPx - _deadZonePx);
            float normalized = Mathf.Clamp01((clamped - _deadZonePx) / usableRange);
            normalized = Mathf.Pow(normalized, Mathf.Max(0.05f, _responseExponent));
            return dir * normalized;
        }

        private void EndStick()
        {
            _activeFingerId = -1;
            _smoothedAxis = Vector2.zero;
            if (_root != null)
                _root.gameObject.SetActive(false);
            DanmakuTouchInput.ClearJoystick();
        }

        private Vector2 ClampVisualAnchorToSafeArea(Vector2 screenPos)
        {
            Rect safe = Screen.safeArea;
            float margin = _outerRadiusPx + _safeAreaPaddingPx;
            float minX = safe.xMin + margin;
            float maxX = safe.xMax - margin;
            float minY = safe.yMin + margin;
            float maxY = safe.yMax - margin;

            if (minX > maxX)
            {
                minX = safe.center.x;
                maxX = safe.center.x;
            }

            if (minY > maxY)
            {
                minY = safe.center.y;
                maxY = safe.center.y;
            }

            return new Vector2(
                Mathf.Clamp(screenPos.x, minX, maxX),
                Mathf.Clamp(screenPos.y, minY, maxY));
        }

        private void PlaceRootAtScreen(Vector2 screenPos)
        {
            var canvasRt = _canvas.transform as RectTransform;
            if (canvasRt == null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPos, null, out var local))
                _root.anchoredPosition = local;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("DanmakuMobileJoystickCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -50;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            canvasGo.AddComponent<GraphicRaycaster>();

            _root = new GameObject("JoystickRoot", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            _root.SetParent(_canvas.transform, false);
            _root.sizeDelta = Vector2.zero;
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _canvasGroup = _root.GetComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            _root.gameObject.SetActive(false);

            var outer = NewUiImage("Outer", _root, RingSprite(), _ringColor);
            outer.rectTransform.sizeDelta = new Vector2(_outerRadiusPx * 2f, _outerRadiusPx * 2f);

            var edge = NewUiImage("OuterEdge", _root, RingSprite(), _ringEdgeColor);
            edge.rectTransform.sizeDelta = new Vector2((_outerRadiusPx + 4f) * 2f, (_outerRadiusPx + 4f) * 2f);

            var knob = NewUiImage("Knob", _root, KnobSprite(), _knobColor);
            _knob = knob.rectTransform;
            _knob.sizeDelta = new Vector2(_knobRadiusPx * 2f, _knobRadiusPx * 2f);

            var core = NewUiImage("KnobCore", _knob, KnobSprite(), _knobCoreColor);
            core.rectTransform.sizeDelta = new Vector2(_knobRadiusPx * 0.9f, _knobRadiusPx * 0.9f);
        }

        private static Image NewUiImage(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Sprite RingSprite()
        {
            if (s_ringSprite != null)
                return s_ringSprite;
            s_ringSprite = CreateCircleSprite("DanmakuJoystickRing", 128, 0.56f, 0.92f, true);
            return s_ringSprite;
        }

        private static Sprite KnobSprite()
        {
            if (s_knobSprite != null)
                return s_knobSprite;
            s_knobSprite = CreateCircleSprite("DanmakuJoystickKnob", 96, 0f, 0.92f, false);
            return s_knobSprite;
        }

        private static Sprite CreateCircleSprite(string name, int size, float innerRadius01, float outerRadius01, bool ring)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float center = (size - 1) * 0.5f;
            float outer = center * outerRadius01;
            float inner = center * innerRadius01;
            float aa = 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float outerAlpha = Mathf.Clamp01((outer - d) / aa);
                    float alpha = outerAlpha;
                    if (ring)
                    {
                        float innerAlpha = Mathf.Clamp01((d - inner) / aa);
                        alpha *= innerAlpha;
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
