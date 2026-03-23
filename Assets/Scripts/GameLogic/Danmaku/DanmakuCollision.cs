using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 轻量圆碰撞结构，对应参考 Collision.h 中的 Circle / CheckC2C。
    /// 不依赖 Unity 物理引擎，逐帧在逻辑层检测。
    /// </summary>
    public struct DanmakuCircle
    {
        public float X, Y, Radius;

        public DanmakuCircle(float x, float y, float radius)
        {
            X = x; Y = y; Radius = radius;
        }

        public DanmakuCircle(Vector2 center, float radius)
        {
            X = center.x; Y = center.y; Radius = radius;
        }

        public Vector2 Center => new Vector2(X, Y);
    }

    /// <summary>
    /// 圆-圆碰撞检测工具，对应参考 CheckC2C。
    /// </summary>
    public static class DanmakuCollision
    {
        public static bool CheckCircleToCircle(in DanmakuCircle a, in DanmakuCircle b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float r  = a.Radius + b.Radius;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>
        /// 擦弹：敌弹与擦弹圆相交，且不与受击圆相交（避免贴脸算擦弹）。
        /// </summary>
        public static bool CheckBulletGraze(in DanmakuCircle bullet, in DanmakuCircle playerHit, in DanmakuCircle playerGraze)
        {
            if (CheckCircleToCircle(bullet, playerHit))
                return false;
            return CheckCircleToCircle(bullet, playerGraze);
        }

        public static bool CheckCircleToCircle(Vector2 posA, float rA, Vector2 posB, float rB)
        {
            float dx = posA.x - posB.x;
            float dy = posA.y - posB.y;
            float r  = rA + rB;
            return dx * dx + dy * dy <= r * r;
        }
    }
}
