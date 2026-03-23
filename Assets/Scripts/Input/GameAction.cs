namespace DemoFrameWork.Input
{
    /// <summary>
    /// 虚拟动作枚举，用于按键绑定与回调注册。
    /// </summary>
    public enum GameAction
    {
        Back,
        Pause,
        Confirm,
        Cancel,
        /// <summary>数独等 Demo 的作弊键（如 Q 显示答案）。</summary>
        Cheat,
    }
}
