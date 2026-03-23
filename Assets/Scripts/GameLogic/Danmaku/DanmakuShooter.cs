using System;
using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 弹幕发射模式（在 Inspector 中选择）。
    /// </summary>
    public enum DanmakuShooterPattern
    {
        /// <summary>原版扇形 / 环形：由 Ways、Range、VecAngle、Radius 控制。</summary>
        Fan = 0,
        /// <summary>同角度一字排开；发射点沿垂直于弹道方向偏移。</summary>
        StraightLine,
        /// <summary>与 StraightLine 相同；在 Inspector 里把 Angle 调成斜向即可（如 210°、225°）。</summary>
        DiagonalLine,
        /// <summary>5 发均匀环形（72° 间隔）。</summary>
        PentagramRing,
        /// <summary>10 发双五角分布（内外各 5 发错开 36°）。</summary>
        PentagramStar,
        /// <summary>环形基础上每条弹道递增螺旋角。</summary>
        SpiralRing
    }

    /// <summary>
    /// 弹幕发射器，对应参考 Shooter.h / Shooter.cpp。
    /// 支持多-way、扇形、环形、自机狙，以及直线 / 五角星 / 螺旋等模式。
    /// </summary>
    [Serializable]
    public class DanmakuShooter
    {
        public DanmakuShooterPattern Pattern = DanmakuShooterPattern.Fan;

        /// <summary>发散数。</summary>
        public int Ways = 1;

        /// <summary>基准角度（度），0 = 正右方，逆时针为正。</summary>
        public float Angle = 270f;

        /// <summary>方向角（vecangle），控制整组弹幕朝向，通常与 Angle 相同。</summary>
        public float VecAngle = 270f;

        /// <summary>扇形范围（度），360 = 环形。</summary>
        public float Range = 360f;

        /// <summary>发射点相对原点的偏移圆半径（Unity 单位）。</summary>
        public float Radius = 0f;

        /// <summary>是否为自机狙：若为 true，则自动将 Angle 对准玩家。</summary>
        public bool Spy = false;

        [Tooltip("StraightLine：相邻两发在垂直于弹道方向上的间距（世界单位）")]
        public float LineSpacing = 0.45f;

        [Tooltip("SpiralRing：每一发在扇形角度上额外叠加的螺旋角（度）")]
        public float SpiralTwistDegrees = 14f;

        private static readonly float[] PentagramStarLocalDeg =
        {
            0f, 72f, 144f, 216f, 288f,
            36f, 108f, 180f, 252f, 324f
        };

        /// <summary>
        /// 执行发射，对应参考 Shooter::Execute。
        /// 回调参数：(angleDeg, offsetWorld) → 发射角度（度）与发射点相对原点偏移。
        /// </summary>
        /// <param name="originWorldPos">发射源世界坐标。</param>
        /// <param name="playerWorldPos">玩家世界坐标（Spy 模式用）。</param>
        /// <param name="onShoot">回调：(angleDeg, shootWorldPos)。</param>
        public void Execute(Vector2 originWorldPos, Vector2 playerWorldPos, Action<float, Vector2> onShoot)
        {
            switch (Pattern)
            {
                case DanmakuShooterPattern.StraightLine:
                case DanmakuShooterPattern.DiagonalLine:
                    ExecuteStraightLine(originWorldPos, playerWorldPos, onShoot);
                    return;
                case DanmakuShooterPattern.PentagramRing:
                    ExecuteFan(originWorldPos, playerWorldPos, onShoot, 5, 360f, 0f);
                    return;
                case DanmakuShooterPattern.PentagramStar:
                    ExecutePentagramStar(originWorldPos, playerWorldPos, onShoot);
                    return;
                case DanmakuShooterPattern.SpiralRing:
                    ExecuteFan(originWorldPos, playerWorldPos, onShoot, Mathf.Max(1, Ways), Range, SpiralTwistDegrees);
                    return;
                default:
                    ExecuteFan(originWorldPos, playerWorldPos, onShoot, Mathf.Max(1, Ways), Range, 0f);
                    return;
            }
        }

        /// <summary>
        /// 便捷方法：不使用自机狙时调用（等价于 playerWorldPos = originWorldPos）。
        /// </summary>
        public void Execute(Vector2 originWorldPos, Action<float, Vector2> onShoot)
        {
            Execute(originWorldPos, originWorldPos, onShoot);
        }

        private float GetBaseAngle(Vector2 originWorldPos, Vector2 playerWorldPos)
        {
            float baseAngle = Angle;
            if (Spy)
            {
                Vector2 origin = originWorldPos + new Vector2(Radius, 0f);
                float dx = playerWorldPos.x - origin.x;
                float dy = playerWorldPos.y - origin.y;
                baseAngle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            }

            return baseAngle;
        }

        private void ExecuteStraightLine(Vector2 originWorldPos, Vector2 playerWorldPos, Action<float, Vector2> onShoot)
        {
            float baseAngle = GetBaseAngle(originWorldPos, playerWorldPos);
            float baseRad = baseAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(baseRad), Mathf.Sin(baseRad));
            Vector2 perp = new Vector2(-dir.y, dir.x);

            int ways = Mathf.Max(1, Ways);
            float spacing = Mathf.Max(0.01f, LineSpacing);
            for (int i = 0; i < ways; i++)
            {
                float t = (i - (ways - 1) * 0.5f) * spacing;
                Vector2 shootPos = originWorldPos + perp * t;
                onShoot(baseAngle, shootPos);
            }
        }

        private void ExecuteFan(Vector2 originWorldPos, Vector2 playerWorldPos, Action<float, Vector2> onShoot,
            int ways, float range, float spiralTwistPerBullet)
        {
            float baseAngle = GetBaseAngle(originWorldPos, playerWorldPos);

            for (int i = 0; i < ways; i++)
            {
                float pointAngle = 360f / ways * i;
                pointAngle += 360f / 2f / ways;

                float vecAngleRad = (VecAngle - pointAngle * range / 360f + range / 2f) * Mathf.Deg2Rad;

                float offX = Mathf.Cos(vecAngleRad) * Radius;
                float offY = Mathf.Sin(vecAngleRad) * Radius;

                float shootAngle = (-pointAngle * range / 360f + range / 2f);
                float shootRad = shootAngle * Mathf.Deg2Rad;
                float dx2 = Mathf.Cos(shootRad);
                float dy2 = Mathf.Sin(shootRad);

                float outAngle = Mathf.Atan2(dy2, dx2) * Mathf.Rad2Deg + baseAngle + i * spiralTwistPerBullet;

                Vector2 shootPos = originWorldPos + new Vector2(offX, offY);
                onShoot(outAngle, shootPos);
            }
        }

        private void ExecutePentagramStar(Vector2 originWorldPos, Vector2 playerWorldPos, Action<float, Vector2> onShoot)
        {
            float baseAngle = GetBaseAngle(originWorldPos, playerWorldPos);

            for (int i = 0; i < PentagramStarLocalDeg.Length; i++)
            {
                float a = PentagramStarLocalDeg[i];
                float shootRad = a * Mathf.Deg2Rad;
                float dx2 = Mathf.Cos(shootRad);
                float dy2 = Mathf.Sin(shootRad);
                float outAngle = Mathf.Atan2(dy2, dx2) * Mathf.Rad2Deg + baseAngle;

                Vector2 shootPos = originWorldPos;
                onShoot(outAngle, shootPos);
            }
        }
    }
}
