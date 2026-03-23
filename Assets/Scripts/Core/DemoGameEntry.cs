using UnityEngine;
using UnityEngine.EventSystems;
using DemoFrameWork.Audio;
using AudioSettings = DemoFrameWork.Audio.AudioSettings;
using DemoFrameWork.Config;
using DemoFrameWork.Input;
using DemoFrameWork.Pool;
using DemoFrameWork.Save;
using DemoFrameWork.Resource;
using DemoFrameWork.Scene;
using DemoFrameWork.Table;
using DemoFrameWork.UI;

namespace DemoFrameWork
{
    /// <summary>
    /// 框架统一入口。
    /// UI 初始化：可指定 UISettings（从模板实例化 UIFrame，或用「场景里已摆好的 UIFrame」+ 扫描注册）；或仅拖场景中的 UIFrame（不配 UISettings）。
    /// </summary>
    public class DemoGameEntry : MonoBehaviour
    {
        [Tooltip("UI 配置：模板预制体、Screens To Register 等。")]
        [SerializeField] private UISettings _uiSettings;

        [Tooltip(
            "可选。若未指定 UISettings：必须使用场景中已存在的 UIFrame（拖场景里的实例）。\n" +
            "若指定了 UISettings：可拖「当前场景里」已摆好的 UIFrame → 先扫描子级注册，再按 UISettings 的 Screens To Register 补充尚未注册的预制体；留空 UI Frame 则从模板新建整套 UIFrame。")]
        [SerializeField] private UIFrame _uiFrame;

        [SerializeField] private AudioSettings _audioSettings;
        [SerializeField] private SceneSettings _sceneSettings;
        [SerializeField] private GameConfigAsset _gameConfigAsset;

        public static EventCenter Event => EventCenter.GetInstance();
        public static DemoSaveManager Save => DemoSaveManager.GetInstance();
        public static TableManager Table => TableManager.GetInstance();
        public static UIFrame UI { get; private set; }
        public static AudioManager Audio { get; private set; }
        public static GameSceneManager Scene { get; private set; }
        public static GameResourceManager Resource { get; private set; }
        public static GameConfig Config { get; private set; }
        public static InputManager Input { get; private set; }
        public static GameObjectPool Pool { get; private set; }

        private static DemoGameEntry _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (_uiSettings != null)
            {
                bool useSceneUiFrame = _uiFrame != null && _uiFrame.gameObject.scene.IsValid();
                if (useSceneUiFrame)
                {
                    UI = _uiFrame;
                    DontDestroyOnLoad(UI.gameObject);
                    UI.Initialize();
                    UI.RegisterScreensDiscoveredInHierarchy();
                    _uiSettings.RegisterMissingScreensFromPrefabList(UI);
                }
                else
                {
                    if (_uiFrame == null)
                    {
                        var orphan = FindObjectOfType<UIFrame>(true);
                        if (orphan != null && orphan.gameObject.scene.IsValid())
                        {
                            Debug.LogWarning(
                                "[DemoGameEntry] 场景里已有 UIFrame，但未拖入「UI Frame」字段；将按 UISettings 模板再生成一套 UIFrame，易出现重复。若要用场景内摆好的 UIFrame，请把该实例拖入此字段。");
                        }
                    }

                    UI = _uiSettings.CreateUIInstance(true);
                    if (UI != null)
                    {
                        DontDestroyOnLoad(UI.gameObject);
                        UI.Initialize();
                    }
                }
            }
            else if (_uiFrame != null)
            {
                if (!_uiFrame.gameObject.scene.IsValid())
                {
                    _uiFrame = Instantiate(_uiFrame);
                    DontDestroyOnLoad(_uiFrame.gameObject);
                }
                UI = _uiFrame;
                if (UI != null)
                    UI.Initialize();
            }

            EnsureSingleEventSystem();

            Audio = AudioManager.Create(gameObject, _audioSettings);
            Scene = GameSceneManager.Create(gameObject, _sceneSettings);
            var loadingOverlay = gameObject.AddComponent<SceneLoadingOverlay>();
            loadingOverlay.Initialize(Scene);
            Resource = GameResourceManager.GetInstance();
            Resource.SetLoader(new ResourcesLoader());
            Config = GameConfig.GetInstance();
            Config.Initialize(
                _gameConfigAsset != null ? _gameConfigAsset : Resources.Load<GameConfigAsset>("Config/GameConfig")
            );
            Input = gameObject.AddComponent<InputManager>();
            Input.Initialize();
            Pool = gameObject.AddComponent<GameObjectPool>();
            Pool.Initialize();
        }

        /// <summary>场景中只保留一个 EventSystem，避免 "There are 2 event systems" 警告。优先保留 UIFrame 下的。</summary>
        private void EnsureSingleEventSystem()
        {
            var all = FindObjectsOfType<EventSystem>(true);
            if (all.Length <= 1) return;
            EventSystem keep = null;
            if (UI != null)
                keep = UI.GetComponentInChildren<EventSystem>(true);
            if (keep == null)
                keep = all[0];
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != keep)
                    all[i].gameObject.SetActive(false);
            }
        }
    }
}
