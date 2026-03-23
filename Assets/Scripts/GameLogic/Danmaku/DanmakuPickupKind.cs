namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>道中掉落类型；拾取时在 <see cref="DanmakuPickupDrop"/> 上指定或由加权重随机。</summary>
    public enum DanmakuPickupKind
    {
        Power,
        MoveSpeed,
        FireRate,
        Heal,
        Attack,
        Xp
    }
}
