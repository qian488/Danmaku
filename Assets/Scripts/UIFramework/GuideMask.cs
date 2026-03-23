namespace DemoFrameWork.UI
{
    using UnityEngine;
    using UnityEngine.UI;
    
    public class GuideMask : MaskableGraphic, ICanvasRaycastFilter
    {
        public static GuideMask Self;
    
        [Tooltip("UI 相机；留空时自动使用 Camera.main")]
        [SerializeField] private Camera uiCamera;
    
        private RectTransform _target;
        private Vector2 _targetMin;
        private Vector2 _targetMax;
        private RectTransform _targetArea;
    
        private Camera ActiveCamera => uiCamera != null ? uiCamera : Camera.main;
    
        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (_targetArea == null) return true;
            return !RectTransformUtility.RectangleContainsScreenPoint(_targetArea, sp, eventCamera);
        }
    
        public void Close()
        {
            gameObject.SetActive(false);
        }
    
        public void Play(RectTransform target)
        {
            if (_targetArea == null)
            {
                Debug.LogError("[GuideMask] TargetArea 未找到，请确保存在名为 \"TargetArea\" 的子节点。");
                return;
            }
    
            gameObject.SetActive(true);
    
            var cam = ActiveCamera;
            var screenPoint = RectTransformUtility.WorldToScreenPoint(cam, target.position);
    
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, cam, out localPoint))
            {
                Close();
                return;
            }
    
            _targetArea.anchorMax = target.anchorMax;
            _targetArea.anchorMin = target.anchorMin;
            _targetArea.anchoredPosition = target.anchoredPosition;
            _targetArea.anchoredPosition3D = target.anchoredPosition3D;
            _targetArea.offsetMax = target.offsetMax;
            _targetArea.offsetMin = target.offsetMin;
            _targetArea.pivot = target.pivot;
            _targetArea.sizeDelta = target.sizeDelta;
            _targetArea.localPosition = localPoint;
    
            _targetArea.ForceUpdateRectTransforms();
            _target = _targetArea;
            _target.ForceUpdateRectTransforms();
            LateUpdate();
        }
    
        public void Init()
        {
            _targetArea = transform.Find("TargetArea") as RectTransform;
            if (_targetArea == null)
            {
                Debug.LogError("[GuideMask] 初始化失败：未找到名为 \"TargetArea\" 的子节点。");
            }
            Self = this;
            Close();
        }
    
        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            toFill.Clear();
    
            if (_targetArea == null) return;
    
            var maskRect = rectTransform.rect;
    
            var maskRectLeftTop     = new Vector2(-maskRect.width / 2,  maskRect.height / 2);
            var maskRectLeftBottom  = new Vector2(-maskRect.width / 2, -maskRect.height / 2);
            var maskRectRightTop    = new Vector2( maskRect.width / 2,  maskRect.height / 2);
            var maskRectRightBottom = new Vector2( maskRect.width / 2, -maskRect.height / 2);
    
            var targetRectLeftTop     = new Vector2(_targetMin.x, _targetMax.y);
            var targetRectLeftBottom  = _targetMin;
            var targetRectRightTop    = _targetMax;
            var targetRectRightBottom = new Vector2(_targetMax.x, _targetMin.y);
    
            toFill.AddVert(maskRectLeftBottom,  color, Vector2.zero);
            toFill.AddVert(targetRectLeftBottom, color, Vector2.zero);
            toFill.AddVert(targetRectRightBottom, color, Vector2.zero);
            toFill.AddVert(maskRectRightBottom, color, Vector2.zero);
            toFill.AddVert(targetRectRightTop,  color, Vector2.zero);
            toFill.AddVert(maskRectRightTop,    color, Vector2.zero);
            toFill.AddVert(targetRectLeftTop,   color, Vector2.zero);
            toFill.AddVert(maskRectLeftTop,     color, Vector2.zero);
    
            toFill.AddTriangle(0, 1, 2);
            toFill.AddTriangle(2, 3, 0);
            toFill.AddTriangle(3, 2, 4);
            toFill.AddTriangle(4, 5, 3);
            toFill.AddTriangle(6, 7, 5);
            toFill.AddTriangle(5, 4, 6);
            toFill.AddTriangle(7, 6, 1);
            toFill.AddTriangle(1, 0, 7);
        }
    
        void LateUpdate()
        {
            RefreshView();
        }
    
        private void RefreshView()
        {
            if (_targetArea == null) return;
    
            Vector2 newMin;
            Vector2 newMax;
            if (_target != null && _target.gameObject.activeSelf)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(transform, _target);
                newMin = bounds.min;
                newMax = bounds.max;
            }
            else
            {
                newMin = Vector2.zero;
                newMax = Vector2.zero;
            }
    
            if (_targetMin != newMin || _targetMax != newMax)
            {
                _targetMin = newMin;
                _targetMax = newMax;
                SetAllDirty();
            }
        }
    }
}
