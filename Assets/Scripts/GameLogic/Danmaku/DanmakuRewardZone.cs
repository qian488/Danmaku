using System;
using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 可拾取区域（道中 Reward 栏）的世界坐标检测；由弹幕 GamePanel 在显示时注册。
    /// </summary>
    public static class DanmakuRewardZone
    {
        /// <summary>
        /// 世界坐标是否在可拾取 UI 区域内；未注册时视为不在区域内。
        /// </summary>
        public static Func<Vector3, bool> IsWorldPointInZone { get; set; }
    }
}
