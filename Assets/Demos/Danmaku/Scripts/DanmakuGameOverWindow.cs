using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DemoFrameWork;
using DemoFrameWork.Generated;
using DemoFrameWork.UI;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 弹幕结算窗口。点击整块面板（默认 <c>bg</c>）：
    /// <list type="bullet">
    /// <item><b>失败</b>：在当前场景内重开一局（不重载场景，见 <see cref="DanmakuEventNames.RestartRunRequested"/>）。</item>
    /// <item><b>胜利</b>：回到 <c>Home</c> 主菜单。</item>
    /// </list>
    /// 预制体根节点名须为 <c>GameOverWindow</c>。
    /// </summary>
    public class DanmakuGameOverWindow : WindowController<DanmakuGameOverWindowProperties>
    {
        private const string HomeSceneName = "Home";

        [Header("结算图（胜利显示 win、失败显示 lose）")]
        [Tooltip("胜利图节点；空则按子物体名 win 查找")]
        [SerializeField] private GameObject _win;

        [Tooltip("失败图节点；空则按子物体名 lose 查找")]
        [SerializeField] private GameObject _lose;

        [Header("分数与提示")]
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _tipsText;

        [Tooltip("可选：显示「新纪录」等；空则合并进 Tips")]
        [SerializeField] private TMP_Text _newRecordText;

        [Header("点击任意处")]
        [Tooltip("整块可点区域，通常拖 bg；留空则自动在名为 bg 的物体上补 Button+透明 Image")]
        [SerializeField] private Button _tapAnywhereButton;

        [SerializeField] private string _tipsContinue = "Tap to Continue";

        protected override void AddListeners()
        {
            ResolveOptionalReferences();
            // 避免 Score / Tips 的 TMP 挡住 bg 上的全屏点击
            foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
                tmp.raycastTarget = false;

            EnsureTapAnywhereButton();

            if (_tapAnywhereButton != null)
                _tapAnywhereButton.onClick.AddListener(OnTapAnywhere);
        }

        protected override void RemoveListeners()
        {
            if (_tapAnywhereButton != null)
                _tapAnywhereButton.onClick.RemoveListener(OnTapAnywhere);
        }

        protected override void OnPropertiesSet()
        {
            ResolveOptionalReferences();
            ApplyTexts();
        }

        private void ResolveOptionalReferences()
        {
            if (_win == null)  _win  = FindChildGoByName("win");
            if (_lose == null) _lose = FindChildGoByName("lose");
            if (_scoreText == null) _scoreText = FindTmpByChildName("Score");
            if (_tipsText == null)  _tipsText  = FindTmpByChildName("Tips");
        }

        private GameObject FindChildGoByName(string objectName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == objectName)
                    return t.gameObject;
            }
            return null;
        }

        private TMP_Text FindTmpByChildName(string objectName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != objectName) continue;
                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null) return tmp;
            }
            return null;
        }

        private void EnsureTapAnywhereButton()
        {
            if (_tapAnywhereButton != null) return;

            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "bg") continue;

                var btn = t.GetComponent<Button>();
                if (btn == null)
                    btn = t.gameObject.AddComponent<Button>();

                var graphic = t.GetComponent<Graphic>();
                if (graphic == null)
                {
                    var img = t.gameObject.AddComponent<Image>();
                    img.color = new Color(0f, 0f, 0f, 0.01f);
                    img.raycastTarget = true;
                    graphic = img;
                }
                else
                    graphic.raycastTarget = true;

                btn.targetGraphic = graphic;
                _tapAnywhereButton = btn;
                return;
            }

            Debug.LogWarning("[DanmakuGameOverWindow] 未找到可点击区域：请拖入 Tap Anywhere Button，或保证子物体中有名为 bg 的对象。");
        }

        private void ApplyTexts()
        {
            if (Properties == null) return;

            bool victory = Properties.IsVictory;
            if (_win != null)  _win.SetActive(victory);
            if (_lose != null) _lose.SetActive(!victory);

            if (_scoreText != null)
                _scoreText.text = $"{Properties.FinalScore:D8}";

            if (_newRecordText != null)
            {
                _newRecordText.gameObject.SetActive(Properties.IsNewHighScore);
                _newRecordText.text = Properties.IsNewHighScore ? "新纪录！" : string.Empty;
            }

            if (_tipsText != null)
            {
                string extra = Properties.IsNewHighScore
                    ? $"\n本难度最高 {Properties.BestHighScoreForDifficulty:D8}"
                    : string.Empty;
                _tipsText.text = _tipsContinue + extra;
            }
        }

        private void OnTapAnywhere()
        {
            bool isVictory = Properties != null && Properties.IsVictory;
            UI_Close();

            if (isVictory)
                DemoGameEntry.Scene?.LoadScene(HomeSceneName);
            else
                DemoGameEntry.Event?.EventTrigger(DanmakuEventNames.RestartRunRequested);
        }

        /// <summary>失败：Game Over。</summary>
        public static void OpenLoss(int finalScore)
        {
            var diff = DanmakuRunSettings.Difficulty;
            int prevBest = DanmakuScorePersistence.GetHighScore(diff);
            DanmakuScorePersistence.TryUpdateHighScore(diff, finalScore, out int bestAfter);
            bool isNew = finalScore > prevBest;
            DemoGameEntry.UI?.OpenWindow(ScreenIds.GameOverWindow,
                new DanmakuGameOverWindowProperties(finalScore, isVictory: false, isNew, bestAfter));
        }

        /// <summary>胜利：通关。</summary>
        public static void OpenVictory(int finalScore)
        {
            var diff = DanmakuRunSettings.Difficulty;
            int prevBest = DanmakuScorePersistence.GetHighScore(diff);
            DanmakuScorePersistence.TryUpdateHighScore(diff, finalScore, out int bestAfter);
            bool isNew = finalScore > prevBest;
            DemoGameEntry.UI?.OpenWindow(ScreenIds.GameOverWindow,
                new DanmakuGameOverWindowProperties(finalScore, isVictory: true, isNew, bestAfter));
        }
    }
}
