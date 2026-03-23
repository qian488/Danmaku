using UnityEngine;

namespace DemoFrameWork.Resource
{
    /// <summary>
    /// 资源加载器抽象，可替换为 Resources 或 AB 实现。
    /// </summary>
    public interface IResourceLoader
    {
        T Load<T>(string path) where T : UnityEngine.Object;
        void LoadAsync<T>(string path, System.Action<T> onDone) where T : UnityEngine.Object;
        void Unload(string path);
    }
}
