using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// Resources 贴图加载与玩法层 SpriteRenderer 排序辅助。
    /// </summary>
    public static class DanmakuSpriteUtil
    {
        /// <summary>玩法层相对默认 0 的排序，避免被背景等盖住（对 Overlay UI 无效）。</summary>
        public const int GameplaySortingOrder = 50;

        /// <summary>
        /// 将 <see cref="Resources.LoadAsync"/> 等已加载的主资源转为可赋给 <see cref="SpriteRenderer"/> 的 <see cref="Sprite"/>。
        /// </summary>
        public static Sprite SpriteFromLoadedObject(UnityEngine.Object asset)
        {
            if (asset == null) return null;
            if (asset is Sprite s) return s;
            if (asset is Texture2D tex)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return null;
        }

        /// <summary>
        /// 先尝试 <see cref="Sprite"/>，再尝试 <see cref="Texture2D"/> 并动态生成 Sprite（适合导入为 Default 的测试图）。
        /// </summary>
        public static Sprite TryLoadSprite(string resourcesPathWithoutExtension)
        {
            if (string.IsNullOrEmpty(resourcesPathWithoutExtension)) return null;

            var s = Resources.Load<Sprite>(resourcesPathWithoutExtension);
            if (s != null) return s;

            var tex = Resources.Load<Texture2D>(resourcesPathWithoutExtension);
            if (tex == null) return null;

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        public static void ApplySprite(SpriteRenderer sr, string resourcesPathWithoutExtension)
        {
            if (sr == null) return;
            var sp = TryLoadSprite(resourcesPathWithoutExtension);
            if (sp != null)
                sr.sprite = sp;
        }

        public static void BoostGameplaySorting(GameObject root)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.sortingOrder = Mathf.Max(r.sortingOrder, GameplaySortingOrder);
            }
        }

        /// <summary>单组件提升排序，避免 <see cref="GetComponentsInChildren"/> 分配。</summary>
        public static void ApplyGameplaySortingToRenderer(SpriteRenderer r)
        {
            if (r == null) return;
            r.sortingOrder = Mathf.Max(r.sortingOrder, GameplaySortingOrder);
        }
    }
}
