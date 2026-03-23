using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 在 XY 平面上绘制判定圆（与 <see cref="DanmakuCircle"/> 逻辑一致，Z 取物体位置）。
    /// 供 <see cref="OnDrawGizmos"/> 使用：Scene / Game 视图需打开 Gizmos 开关。
    /// </summary>
    public static class DanmakuGizmoDraw
    {
        /// <summary>线段数，越大越圆。</summary>
        public const int DefaultSegments = 48;

        public static void WireCircleXY(Vector3 center, float radius, Color color, int segments = DefaultSegments)
        {
            if (radius <= 0f || segments < 3) return;

            Color prev = Gizmos.color;
            Gizmos.color = color;

            float z = center.z;
            Vector3 p0 = new Vector3(center.x + radius, center.y, z);
            for (int i = 1; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                Vector3 p1 = new Vector3(
                    center.x + Mathf.Cos(a) * radius,
                    center.y + Mathf.Sin(a) * radius,
                    z);
                Gizmos.DrawLine(p0, p1);
                p0 = p1;
            }

            Gizmos.color = prev;
        }
    }
}
