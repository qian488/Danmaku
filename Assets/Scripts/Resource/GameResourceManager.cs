using System;
using UnityEngine;
using DemoFrameWork;
using DemoFrameWork.UI;

namespace DemoFrameWork.Resource
{
    /// <summary>
    /// 资源统一入口，默认 ResourcesLoader，可替换为 ABLoader。
    /// 通过 DemoGameEntry.Resource 访问。
    /// </summary>
    public class GameResourceManager : BaseManager<GameResourceManager>
    {
        private IResourceLoader _loader;

        public void SetLoader(IResourceLoader loader)
        {
            _loader = loader ?? new ResourcesLoader();
        }

        private IResourceLoader GetLoader()
        {
            if (_loader == null)
                _loader = new ResourcesLoader();
            return _loader;
        }

        public T Load<T>(string path) where T : UnityEngine.Object
        {
            return GetLoader().Load<T>(path);
        }

        public void LoadAsync<T>(string path, Action<T> onDone) where T : UnityEngine.Object
        {
            GetLoader().LoadAsync(path, onDone);
        }

        public void Unload(string path)
        {
            GetLoader().Unload(path);
        }

        /// <summary>
        /// 按需加载 UI 预制体并注册到 UIFrame。prefabPath 默认 Prefabs/{screenId}。
        /// </summary>
        public void LoadAndRegisterScreen(string screenId, string prefabPath = null, Action onDone = null)
        {
            string path = prefabPath ?? UIPrefabPath(screenId);
            var prefab = Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameResourceManager] Prefab not found: {path}");
                onDone?.Invoke();
                return;
            }
            var instance = UnityEngine.Object.Instantiate(prefab);
            var controller = instance.GetComponent<IScreenController>();
            if (controller == null)
            {
                Debug.LogWarning($"[GameResourceManager] No IScreenController on: {path}");
                onDone?.Invoke();
                return;
            }
            if (DemoGameEntry.UI == null)
            {
                Debug.LogWarning("[GameResourceManager] DemoGameEntry.UI is null, cannot register.");
                onDone?.Invoke();
                return;
            }
            DemoGameEntry.UI.RegisterScreen(screenId, controller, instance.transform);
            onDone?.Invoke();
        }

        public static string UIPrefabPath(string screenId) => "Prefabs/" + screenId;
        public static string AudioBGMPath(string name) => "Audio/BGM/" + name;
        public static string AudioSFXPath(string name) => "Audio/SFX/" + name;
        public static string TablePath(string typeName) => "Tables/" + typeName;
    }
}
