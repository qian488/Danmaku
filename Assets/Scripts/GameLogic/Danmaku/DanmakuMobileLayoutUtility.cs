using UnityEngine;
using UnityEngine.UI;
using DemoFrameWork.UI;

namespace DemoFrameWork.GameLogic.Danmaku
{
    public static class DanmakuMobileLayoutUtility
    {
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        public static void ConfigureScreen(Transform root)
        {
            if (!DanmakuMobileRuntime.IsMobileLike || root == null)
                return;

            ConfigureCanvasScaler(root);
            EnsureFullScreenBackgrounds(root);
        }

        public static Vector2 GetMobileCanvasReferenceSize()
        {
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            return new Vector2(Mathf.Max(ReferenceResolution.x, ReferenceResolution.y * aspect), ReferenceResolution.y);
        }

        private static void ConfigureCanvasScaler(Transform root)
        {
            var canvas = root.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                return;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            SafeAreaAdapter.Init(scaler);
        }

        private static void EnsureFullScreenBackgrounds(Transform root)
        {
            Vector2 targetSize = GetMobileCanvasReferenceSize();
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image == null || image.sprite == null || image.name != "bg")
                    continue;

                RectTransform rect = image.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = targetSize;
            }

            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null || sr.sprite == null || sr.name != "bg")
                    continue;

                var scaler = sr.GetComponent<DanmakuFullScreenBackgroundScaler>();
                if (scaler == null)
                    scaler = sr.gameObject.AddComponent<DanmakuFullScreenBackgroundScaler>();
                scaler.ForceApply();
            }
        }
    }
}
