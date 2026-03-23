using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DemoFrameWork.GameLogic.Danmaku;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 全屏触摸：在按下处显示虚拟摇杆，输出到 <see cref="DanmakuTouchInput.JoystickAxis"/>。
    /// 若触点落在其他 UI 上（如暂停键）则不接管。双指时由 <see cref="PlayerController"/> 视为低速（Shift）。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class DanmakuTouchJoystick : MonoBehaviour
    {
        [SerializeField] private float _outerRadiusPx = 88f;
        [SerializeField] private float _knobRadiusPx = 36f;
        [SerializeField] private float _maxStickPx = 52f;

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _knob;
        private int _activeFingerId = -1;
        private Vector2 _anchorScreen;

        private void Awake()
        {
            if (!Application.isMobilePlatform)
            {
                enabled = false;
                return;
            }

            EnsureEventSystem();
            BuildUi();
        }

        private void OnDisable() => DanmakuTouchInput.ClearJoystick();

        private void Update()
        {
            if (!Application.isMobilePlatform)
                return;

            DanmakuTouchInput.ClearJoystick();

            if (_activeFingerId < 0)
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
            else
            {
                bool found = false;
                for (int i = 0; i < global::UnityEngine.Input.touchCount; i++)
                {
                    var t = global::UnityEngine.Input.GetTouch(i);
                    if (t.fingerId != _activeFingerId)
                        continue;
                    found = true;
                    if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                        DragStick(t.position);
                    else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                        EndStick();
                    break;
                }

                if (!found)
                    EndStick();
            }
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
            PlaceRootAtScreen(screenPos);
            DragStick(screenPos);
        }

        private void DragStick(Vector2 screenPos)
        {
            Vector2 delta = screenPos - _anchorScreen;
            float m = delta.magnitude;
            Vector2 dir = m > 0.01f ? delta / m : Vector2.zero;
            float clamped = Mathf.Min(m, _maxStickPx);
            _knob.anchoredPosition = dir * clamped;

            if (clamped > 0.01f)
                DanmakuTouchInput.JoystickAxis = dir * (clamped / _maxStickPx);
        }

        private void EndStick()
        {
            _activeFingerId = -1;
            if (_root != null)
                _root.gameObject.SetActive(false);
            DanmakuTouchInput.ClearJoystick();
        }

        private void PlaceRootAtScreen(Vector2 screenPos)
        {
            var canvasRt = _canvas.transform as RectTransform;
            if (canvasRt == null)
                return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRt, screenPos, _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                    out var local))
            {
                _root.anchoredPosition = local;
            }
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
            // 低于 GamePanel 等 HUD，避免挡住暂停键；触摸空白区域仍可出摇杆
            _canvas.sortingOrder = -50;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            _root = new GameObject("JoystickRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(_canvas.transform, false);
            _root.sizeDelta = Vector2.zero;
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.gameObject.SetActive(false);

            var outer = NewUiImage("Outer", _root, new Color(1f, 1f, 1f, 0.28f));
            var outerRt = outer.rectTransform;
            outerRt.sizeDelta = new Vector2(_outerRadiusPx * 2f, _outerRadiusPx * 2f);

            var knobGo = NewUiImage("Knob", _root, new Color(1f, 1f, 1f, 0.72f));
            _knob = knobGo.rectTransform;
            _knob.sizeDelta = new Vector2(_knobRadiusPx * 2f, _knobRadiusPx * 2f);
        }

        private static Image NewUiImage(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = WhiteSprite();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Sprite WhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
