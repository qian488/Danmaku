using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>移动端触摸摇杆轴向（-1~1）；由弹幕场景的触摸摇杆组件写入，<see cref="PlayerController"/> 读取。</summary>
    public static class DanmakuTouchInput
    {
        public static Vector2 JoystickAxis { get; set; }

        public static void ClearJoystick() => JoystickAxis = Vector2.zero;
    }
}
