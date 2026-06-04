using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DemoFrameWork;
using DemoFrameWork.GameLogic.Danmaku;
using DemoFrameWork.UI;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 挂在 <b>HomePanel</b> 根节点：
    /// <list type="bullet">
    /// <item><b>preBtn / nextBtn</b>：上一个 / 下一个角色（与 <see cref="_characterDisplayNames"/> 下标对应）。</item>
    /// <item><b>Select → Content 下每一行 chapter</b>：子物体 TMP 文案解析难度（Easy/Normal/Hard/Expand）；<b>第 1 行 → StageIndex 0（Stage01），第 2 行 → 1（Stage02）…</b>，与弹幕场景 <c>DanmakuGameController._stages</c> 顺序一致；最后一行为 Exit。</item>
    /// <item><b>最后一行</b>：退出游戏（Exit），不进入弹幕场景。</item>
    /// </list>
    /// </summary>
    public class DanmakuHomePanel : APanelController<DanmakuHomePanelProperties>
    {
        [Tooltip("显示角色名，默认识别子物体 playerName 上的 TMP")]
        [SerializeField] private TMP_Text _playerNameText;

        [Tooltip("可选：用于同步显示当前将使用的难度文案")]
        [SerializeField] private TMP_Text _difficultyLabel;

        [Tooltip("可选：显示当前难度（见 DanmakuRunSettings）的历史最高分")]
        [SerializeField] private TMP_Text _bestScoreForDifficultyText;

        [Tooltip("难度列表父节点，默认识别 Select/Viewport/Content；子物体顺序最后一项为 Exit")]
        [SerializeField] private RectTransform _chapterContent;

        [Tooltip(
            "true：第 1 行（非 Exit）→ StageIndex 0，第 2 行 → 1 …，与 DanmakuGameController._stages 一一对应。\n" +
            "false：始终进 StageIndex 0（旧行为）。")]
        [SerializeField] private bool _mapChapterRowToStageIndex = true;

        [Header("角色显示名（与 DanmakuGameController 自机预制体数组下标一致）")]
        [SerializeField] private string[] _characterDisplayNames = { "自机 A", "自机 B" };

        [Tooltip("主界面立绘：角色 0 显示 playerImg1，角色 1 显示 playerImg2；空则按名称查找")]
        [SerializeField] private GameObject _playerImg1;

        [SerializeField] private GameObject _playerImg2;

        protected override void AddListeners()
        {
            ConfigureMobileLayoutIfNeeded();

            if (_playerNameText == null)
            {
                var t = transform.Find("playerName");
                if (t != null) _playerNameText = t.GetComponent<TMP_Text>();
            }

            if (_chapterContent == null)
            {
                var c = transform.Find("Select/Viewport/Content");
                if (c != null) _chapterContent = c as RectTransform;
            }

            // 上一个 / 下一个角色（兼容 pteBtn 拼写）
            BindButtonByName("preBtn", OnPrevCharacter);
            BindButtonByName("pteBtn", OnPrevCharacter);
            BindButtonByName("nextBtn", OnNextCharacter);

            BindChapterButtons();

            EnsureCharacterPortraits();
            RefreshCharacterUi();
            RefreshDifficultyUi();
        }

        protected override void OnPropertiesSet()
        {
            ConfigureMobileLayoutIfNeeded();
            DanmakuAudio.PlayHomeBgm(0.5f);
            RefreshCharacterUi();
            RefreshDifficultyUi();
        }

        protected override void RemoveListeners()
        {
        }

        private void ConfigureMobileLayoutIfNeeded()
        {
            DanmakuMobileLayoutUtility.ConfigureScreen(transform);
        }

        private void BindButtonByName(string goName, UnityEngine.Events.UnityAction action)
        {
            var b = FindButton(goName);
            if (b != null) b.onClick.AddListener(action);
        }

        private Button FindButton(string goName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != goName) continue;
                var btn = t.GetComponent<Button>();
                if (btn != null) return btn;
            }
            return null;
        }

        private void BindChapterButtons()
        {
            if (_chapterContent == null) return;

            int n = _chapterContent.childCount;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var btn = _chapterContent.GetChild(i).GetComponent<Button>();
                if (btn == null) continue;
                btn.onClick.AddListener(() => OnDifficultyChapterClicked(idx));
            }
        }

        private void OnDifficultyChapterClicked(int index)
        {
            if (_chapterContent == null) return;

            var row = _chapterContent.GetChild(index);
            var label = row.GetComponentInChildren<TMP_Text>(true);
            string text = label != null ? label.text : string.Empty;

            // 最后一项：退出
            if (index == _chapterContent.childCount - 1 || IsExitLabel(text))
            {
                QuitGame();
                return;
            }

            // 该行 TMP 解析难度；行号（从 0 起，不含 Exit）映射关卡下标 → 对应 _stages 中的 Stage01、Stage02…
            DanmakuRunSettings.Difficulty = ParseDifficultyFromLabel(text);
            DanmakuRunSettings.StageIndex = _mapChapterRowToStageIndex ? index : 0;
            RefreshDifficultyUi();

            var launcher = Object.FindObjectOfType<DanmakuLauncher>();
            if (launcher == null)
            {
                Debug.LogError("[DanmakuHomePanel] 场景中未找到 DanmakuLauncher（通常挂在 Home 场景的启动对象上）。");
                return;
            }

            launcher.LaunchGame();
        }

        private static bool IsExitLabel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string s = raw.Trim().ToLowerInvariant();
            return s.Contains("exit") || s.Contains("退出") || s.Contains("quit");
        }

        /// <summary>根据 chapter 上 TMP 文案解析难度；无法识别时默认 Normal。</summary>
        public static DanmakuDifficulty ParseDifficultyFromLabel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DanmakuDifficulty.Normal;
            string s = raw.Trim().ToLowerInvariant();

            if (s.Contains("easy") || s.Contains("简单")) return DanmakuDifficulty.Easy;
            if (s.Contains("hard") || s.Contains("困难")) return DanmakuDifficulty.Hard;
            if (s.Contains("expand") || s.Contains("无尽")) return DanmakuDifficulty.Expand;
            if (s.Contains("normal") || s.Contains("普通")) return DanmakuDifficulty.Normal;

            return DanmakuDifficulty.Normal;
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnPrevCharacter()
        {
            int n = Mathf.Max(1, _characterDisplayNames != null ? _characterDisplayNames.Length : 1);
            DanmakuRunSettings.CharacterIndex = (DanmakuRunSettings.CharacterIndex - 1 + n) % n;
            DanmakuAudio.PlayUiSelectSfx();
            DanmakuAudio.SeekHomeBgmRandomTime();
            RefreshCharacterUi();
        }

        private void OnNextCharacter()
        {
            int n = Mathf.Max(1, _characterDisplayNames != null ? _characterDisplayNames.Length : 1);
            DanmakuRunSettings.CharacterIndex = (DanmakuRunSettings.CharacterIndex + 1) % n;
            DanmakuAudio.PlayUiSelectSfx();
            DanmakuAudio.SeekHomeBgmRandomTime();
            RefreshCharacterUi();
        }

        private void EnsureCharacterPortraits()
        {
            if (_playerImg1 == null)
            {
                var t = FindChildTransformByName(transform, "playerImg1");
                if (t != null) _playerImg1 = t.gameObject;
            }
            if (_playerImg2 == null)
            {
                var t = FindChildTransformByName(transform, "playerImg2");
                if (t != null) _playerImg2 = t.gameObject;
            }
        }

        private static Transform FindChildTransformByName(Transform root, string goName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == goName) return t;
            }
            return null;
        }

        private void RefreshCharacterUi()
        {
            EnsureCharacterPortraits();
            int i = DanmakuRunSettings.CharacterIndex;
            if (_playerNameText != null && _characterDisplayNames != null && i >= 0 && i < _characterDisplayNames.Length)
                _playerNameText.text = _characterDisplayNames[i];

            if (_playerImg1 != null) _playerImg1.SetActive(i == 0);
            if (_playerImg2 != null) _playerImg2.SetActive(i == 1);
        }

        private void RefreshDifficultyUi()
        {
            if (_difficultyLabel != null)
                _difficultyLabel.text = DanmakuRunSettings.GetDifficultyDisplayName();

            if (_bestScoreForDifficultyText != null)
            {
                int best = DanmakuScorePersistence.GetHighScore(DanmakuRunSettings.Difficulty);
                _bestScoreForDifficultyText.text = $"最高 {best:D8}";
            }
        }
    }
}
