using UnityEngine;
using UnityEngine.UI;

namespace DemoFrameWork.UI
{
    public class SafeAreaAdapter : AdapterBase
    {
        [Header("是否每一帧都计算")]
        public bool CalculateEveryFrame = false;

        [Header("指定 CanvasScaler（留空则自动查找场景中的第一个）")]
        [SerializeField] private CanvasScaler overrideScaler;

        private RectTransform rect;
        private static CanvasScaler scaler;

        public static void Init(CanvasScaler canvasScaler)
        {
            scaler = canvasScaler;
        }

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            if (overrideScaler != null)
            {
                scaler = overrideScaler;
            }
            else if (scaler == null)
            {
                scaler = FindObjectOfType<CanvasScaler>();
            }
            Adapt();
        }

        private void Update()
        {
            if (CalculateEveryFrame)
            {
                Adapt();
            }
        }

        public override void Adapt()
        {
            if (scaler == null) return;

            var safeArea = Screen.safeArea;
            int width = (int)(scaler.referenceResolution.x * (1 - scaler.matchWidthOrHeight) +
                scaler.referenceResolution.y * Screen.width / Screen.height * scaler.matchWidthOrHeight);
            int height = (int)(scaler.referenceResolution.y * scaler.matchWidthOrHeight -
                scaler.referenceResolution.x * Screen.height / Screen.width * (scaler.matchWidthOrHeight - 1));
            float ratio = scaler.referenceResolution.y * scaler.matchWidthOrHeight / Screen.height -
                scaler.referenceResolution.x * (scaler.matchWidthOrHeight - 1) / Screen.width;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(safeArea.position.x * ratio, safeArea.position.y * ratio);
            rect.offsetMax = new Vector2(safeArea.position.x * ratio + safeArea.width * ratio - width,
                -(height - safeArea.position.y * ratio - safeArea.height * ratio));
        }
    }
}
