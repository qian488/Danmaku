namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 主菜单 → 弹幕场景 之间的会话数据（不进存档，仅本局进关前写入）。
    /// </summary>
    public static class DanmakuRunSettings
    {
        public static int CharacterIndex { get; set; }
        public static int StageIndex { get; set; }
        public static DanmakuDifficulty Difficulty { get; set; } = DanmakuDifficulty.Normal;

        /// <summary>是否为无尽模式（Expand）。</summary>
        public static bool IsEndlessMode => Difficulty == DanmakuDifficulty.Expand;

        public static void ResetDefaults()
        {
            CharacterIndex = 0;
            StageIndex = 0;
            Difficulty = DanmakuDifficulty.Normal;
        }

        /// <summary>敌方弹幕速度倍率（StageRunner 中 SpawnBulletWave 使用）。</summary>
        public static float GetBulletSpeedMultiplier()
        {
            switch (Difficulty)
            {
                case DanmakuDifficulty.Easy:   return 0.85f;
                case DanmakuDifficulty.Hard:   return 1.15f;
                case DanmakuDifficulty.Expand: return 1.2f;
                default:                        return 1f;
            }
        }

        /// <summary>初始残机、炸弹（由 <see cref="GameLogic.Danmaku.PlayerController.Initialize"/> 使用）。炸弹：简单 3、普通 2、困难 1、无尽 0。</summary>
        public static void GetStartingLivesAndBombs(out int lives, out int bombs)
        {
            switch (Difficulty)
            {
                case DanmakuDifficulty.Easy:
                    lives = 4; bombs = 3;
                    break;
                case DanmakuDifficulty.Hard:
                    lives = 2; bombs = 1;
                    break;
                case DanmakuDifficulty.Expand:
                    lives = 3; bombs = 0;
                    break;
                default:
                    lives = 3; bombs = 2;
                    break;
            }
        }

        public static string GetDifficultyDisplayName()
        {
            switch (Difficulty)
            {
                case DanmakuDifficulty.Easy:   return "简单";
                case DanmakuDifficulty.Hard:   return "困难";
                case DanmakuDifficulty.Expand: return "无尽";
                default:                       return "普通";
            }
        }
    }
}
