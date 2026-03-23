namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>弹幕模块 EventCenter 事件名。监听方示例：DanmakuGamePanel。</summary>
    public static class DanmakuEventNames
    {
        public const string PlayerDamaged = "Danmaku.PlayerDamaged";
        public const string BombUsed = "Danmaku.BombUsed";
        public const string GameOver = "Danmaku.GameOver";
        public const string PauseChanged = "Danmaku.PauseChanged";
        /// <summary>HUD 暂停键请求暂停（由 <see cref="DanmakuGameController"/> 处理，与 Esc 行为一致）。</summary>
        public const string PauseRequested = "Danmaku.PauseRequested";
        public const string ScoreChanged = "Danmaku.ScoreChanged";
        /// <summary>Power 数值变化（如收集 P 点）；监听方：DanmakuGamePanel。</summary>
        public const string PowerChanged = "Danmaku.PowerChanged";
        /// <summary>擦弹计数变化；监听方：DanmakuGamePanel。</summary>
        public const string GrazeChanged = "Danmaku.GrazeChanged";
        public const string ResumeRequested = "Danmaku.ResumeRequested";
        public const string ReturnHomeRequested = "Danmaku.ReturnHomeRequested";
        /// <summary>失败结算点击继续：在当前场景内重开一局（不重载场景）。</summary>
        public const string RestartRunRequested = "Danmaku.RestartRunRequested";
    }
}
