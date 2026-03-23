namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>主菜单选择的关卡难度（影响弹速与初始残机/炸弹；炸弹随难度 简单→无尽 为 3,2,1,0）。<c>Expand</c> 为无尽模式。</summary>
    public enum DanmakuDifficulty
    {
        Easy,
        Normal,
        Hard,
        /// <summary>无尽：关卡播完后循环同一 StageData。</summary>
        Expand
    }
}
