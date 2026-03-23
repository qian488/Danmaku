using System.Collections;
using UnityEngine;

namespace DemoFrameWork.Resource
{
    /// <summary>
    /// 使用 Resources 的加载器，path 为 Resources 下相对路径（不含扩展名）。
    /// </summary>
    public class ResourcesLoader : IResourceLoader
    {
        public T Load<T>(string path) where T : UnityEngine.Object
        {
            return Resources.Load<T>(path);
        }

        public void LoadAsync<T>(string path, System.Action<T> onDone) where T : UnityEngine.Object
        {
            var req = Resources.LoadAsync<T>(path);
            if (req == null)
            {
                onDone?.Invoke(null);
                return;
            }
            // 用协程在下一帧完成时回调（Resources.LoadAsync 无 MonoBehaviour 时需全局协程）
            ResourceAsyncRunner.Run(req, () => onDone?.Invoke(req.asset as T));
        }

        public void Unload(string path)
        {
            var asset = Resources.Load(path);
            if (asset != null && !(asset is GameObject))
                Resources.UnloadAsset(asset);
        }
    }

    /// <summary>
    /// 无 MonoBehaviour 时驱动 Resources.LoadAsync 完成回调的全局运行器。
    /// </summary>
    internal static class ResourceAsyncRunner
    {
        private static CoroutineRunner _runner;

        public static void Run(ResourceRequest request, System.Action onDone)
        {
            if (request == null)
            {
                onDone?.Invoke();
                return;
            }
            if (_runner == null)
                _runner = Object.FindObjectOfType<CoroutineRunner>() ?? new GameObject("[ResourceAsyncRunner]").AddComponent<CoroutineRunner>();
            _runner.StartCoroutine(WaitThenInvoke(request, onDone));
        }

        private static IEnumerator WaitThenInvoke(ResourceRequest request, System.Action onDone)
        {
            yield return request;
            onDone?.Invoke();
        }
    }

    internal class CoroutineRunner : MonoBehaviour
    {
        private void Awake() => DontDestroyOnLoad(gameObject);
    }
}
