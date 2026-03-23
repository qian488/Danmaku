using DemoFrameWork.UI;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 弹幕结算窗口属性：分数 + 失败 / 胜利。
    /// </summary>
    public class DanmakuGameOverWindowProperties : WindowProperties
    {
        public int FinalScore { get; }

        /// <summary>为 true 时表示通关胜利；为 false 时表示 Game Over。</summary>
        public bool IsVictory { get; }

        /// <summary>本局结算后，该难度是否刷新最高分。</summary>
        public bool IsNewHighScore { get; }

        /// <summary>结算后该难度存档中的最高分（与 <see cref="FinalScore"/> 可能相同）。</summary>
        public int BestHighScoreForDifficulty { get; }

        public DanmakuGameOverWindowProperties(int finalScore, bool isVictory, bool isNewHighScore = false,
            int bestHighScoreForDifficulty = 0) : base(suppressPrefabProperties: true)
        {
            FinalScore = finalScore;
            IsVictory = isVictory;
            IsNewHighScore = isNewHighScore;
            BestHighScoreForDifficulty = bestHighScoreForDifficulty;
            IsPopup = false;
            HideOnForegroundLost = false;
            WindowQueuePriority = WindowPriority.ForceForeground;
        }
    }
}
