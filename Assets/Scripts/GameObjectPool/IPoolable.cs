namespace DemoFrameWork.Pool
{
    /// <summary>
    /// 池化对象可选接口，Spawn/Recycle 时由 GameObjectPool 调用。
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnRecycle();
    }
}
