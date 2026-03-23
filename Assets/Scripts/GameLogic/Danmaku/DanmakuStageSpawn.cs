using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 关卡任务用：玩法区世界矩形（由弹幕场景控制器在开局写入）。
    /// </summary>
    public static class DanmakuStageSpawn
    {
        public static Rect GameplayWorldRect { get; set; }

        public static bool TryGetRightEdgeSpawn(float spawnY, float extraRight, out Vector2 worldPos)
        {
            worldPos = default;
            if (GameplayWorldRect.width < 0.001f)
                return false;
            worldPos = new Vector2(GameplayWorldRect.xMax + Mathf.Max(0f, extraRight), spawnY);
            return true;
        }

        /// <summary>在 [玩法区右缘+minExtra, 右缘+maxExtra] 间随机 X（世界坐标）。</summary>
        public static float RandomBulletOriginX(float extraMin, float extraMax)
        {
            var r = GameplayWorldRect;
            if (r.width < 0.001f)
                return 0f;
            float lo = r.xMax + Mathf.Min(extraMin, extraMax);
            float hi = r.xMax + Mathf.Max(extraMin, extraMax);
            if (hi < lo)
            {
                float t = lo;
                lo = hi;
                hi = t;
            }
            return Random.Range(lo, hi);
        }
    }
}
