using UnityEngine;
using UnityEngine.Serialization;

namespace DemoFrameWork.Scene
{
    /// <summary>
    /// 场景配置：加载面板 ScreenId、Bootstrap 首个跳转场景等。
    /// </summary>
    [CreateAssetMenu(menuName = "DemoFrameWork/Scene Settings", fileName = "SceneSettings")]
    public class SceneSettings : ScriptableObject
    {
        [Tooltip("场景加载面板在 UIFrame 中的 ScreenId（预制体名须一致，默认 LoadingPanel）")]
        [FormerlySerializedAs("loadingWindowId")]
        public string loadingPanelId = "LoadingPanel";

        [Tooltip("Bootstrap 启动后首个跳转场景名（如 MainMenu）")]
        public string bootstrapFirstScene = "MainMenu";

        [Tooltip(
            "若为 true：每次场景加载完成后调用对象池 ClearAll，销毁各桶中「已回池、未借出」的空闲实例；不回收仍在外面的对象（需场景内自行 Despawn/Recycle）。默认可关，以保留预热与跨场景复用。")]
        public bool clearIdlePoolAfterSceneLoad;

        [Tooltip(
            "填入场景名（与 Build Settings / LoadScene 一致）。这些场景在异步激活后仍保持 Loading 显示，由场景内逻辑预载完成后再调用 GameSceneManager.CompleteDeferredLoadingHide()。\n" +
            "留空或未配置：不延后，行为与旧版一致（激活后即关 Loading）。")]
        public string[] deferLoadingHideUntilScriptSceneNames;
    }
}
