using System;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 弹幕各难度最高分（JSON 存档，由 <see cref="DanmakuScorePersistence"/> 读写）。
    /// </summary>
    [Serializable]
    public class DanmakuHighScoreData
    {
        public int Easy;
        public int Normal;
        public int Hard;
        public int Expand;
    }
}
