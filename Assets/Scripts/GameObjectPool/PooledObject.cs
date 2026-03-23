using UnityEngine;
using DemoFrameWork;

namespace DemoFrameWork.Pool
{
    /// <summary>
    /// 挂在池化 GameObject 上，记录所属桶与池引用，支持 Recycle()。
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        public string BucketKey { get; internal set; }
        internal GameObjectPool Pool { get; set; }

        public void Recycle()
        {
            if (Pool != null)
                Pool.Recycle(gameObject);
        }
    }
}
