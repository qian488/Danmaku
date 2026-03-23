using DemoFrameWork;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 弹幕难度最高分持久化：<c>persistentDataPath/saves/danmaku_high_scores.json</c>。
    /// </summary>
    public static class DanmakuScorePersistence
    {
        public const string SaveKey = "danmaku_high_scores";

        private static DanmakuHighScoreData _memoryCache;
        private static bool _cacheLoaded;

        public static int GetHighScore(DanmakuDifficulty difficulty)
        {
            return GetScore(GetCachedData(), difficulty);
        }

        /// <summary>
        /// 若 <paramref name="score"/> 高于该难度已存分数则写入存档并返回 true。
        /// </summary>
        public static bool TryUpdateHighScore(DanmakuDifficulty difficulty, int score, out int bestAfter)
        {
            var data = GetCachedData();
            int cur = GetScore(data, difficulty);
            bestAfter = cur;
            if (score <= cur) return false;

            SetScore(ref data, difficulty, score);
            SaveData(data);
            bestAfter = score;
            return true;
        }

        /// <summary>下次读写前重新从磁盘加载（例如外部修改存档后）。</summary>
        public static void InvalidateMemoryCache()
        {
            _cacheLoaded = false;
            _memoryCache = null;
        }

        private static DanmakuHighScoreData GetCachedData()
        {
            if (_cacheLoaded && _memoryCache != null)
                return _memoryCache;

            _memoryCache = LoadDataFromDisk();
            _cacheLoaded = true;
            return _memoryCache;
        }

        private static DanmakuHighScoreData LoadDataFromDisk()
        {
            if (DemoGameEntry.Save == null)
                return new DanmakuHighScoreData();
            return DemoGameEntry.Save.Load(SaveKey, new DanmakuHighScoreData()) ?? new DanmakuHighScoreData();
        }

        private static void SaveData(DanmakuHighScoreData data)
        {
            if (DemoGameEntry.Save == null) return;
            DemoGameEntry.Save.Save(SaveKey, data);
            _memoryCache = data;
            _cacheLoaded = true;
        }

        private static int GetScore(DanmakuHighScoreData d, DanmakuDifficulty difficulty)
        {
            switch (difficulty)
            {
                case DanmakuDifficulty.Easy: return d.Easy;
                case DanmakuDifficulty.Normal: return d.Normal;
                case DanmakuDifficulty.Hard: return d.Hard;
                case DanmakuDifficulty.Expand: return d.Expand;
                default: return d.Normal;
            }
        }

        private static void SetScore(ref DanmakuHighScoreData d, DanmakuDifficulty difficulty, int value)
        {
            switch (difficulty)
            {
                case DanmakuDifficulty.Easy: d.Easy = value; break;
                case DanmakuDifficulty.Normal: d.Normal = value; break;
                case DanmakuDifficulty.Hard: d.Hard = value; break;
                case DanmakuDifficulty.Expand: d.Expand = value; break;
                default: d.Normal = value; break;
            }
        }
    }
}
