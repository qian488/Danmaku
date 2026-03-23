using System.Collections.Generic;
using System.Text;
using DemoFrameWork.Editor.Sprite2D;
using UnityEditor;
using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku.Editor
{
    /// <summary>
    /// 弹幕 Demo：按 <c>Resources/Demos/Danmaku/Img</c> 一级子目录批量生成 Sprite Atlas（调用框架 <see cref="SpriteAtlasBatchApi"/>）。
    /// </summary>
    public static class DanmakuImgAtlasBatchTool
    {
        public const string ImgRoot = "Assets/Resources/Demos/Danmaku/Img";
        public const string AtlasOutputDir = "Assets/Resources/Demos/Danmaku/SpriteAtlases";

        /// <summary>与 Img 下子目录对应的图集基名（不含 .spriteatlas）。</summary>
        public static readonly (string atlasAssetBaseName, string imgSubFolder)[] AtlasManifest =
        {
            ("Danmaku_Img_Bullet", "bullet"),
            ("Danmaku_Img_Player", "player"),
            ("Danmaku_Img_UI", "UI"),
            ("Danmaku_Img_Pickup", "Pickup"),
            ("Danmaku_Img_Enemy", "enemy"),
            ("Danmaku_Img_Boss", "boss"),
            ("Danmaku_Img_Bg", "bg"),
            ("Danmaku_Img_EnemyBombAnimation", "EnemyBombAnimation"),
        };

        const string MenuBatch = "Tools/Demos/Danmaku/Batch Img Sprite Atlases";

        /// <summary>
        /// 构建 <see cref="SpriteAtlasBatchDefinition"/> 列表（不执行生成）。供其它 Editor 脚本或 CI 调用。
        /// </summary>
        public static List<SpriteAtlasBatchDefinition> BuildDefinitions()
        {
            var list = new List<SpriteAtlasBatchDefinition>(AtlasManifest.Length);
            foreach (var (baseName, subFolder) in AtlasManifest)
            {
                string atlasPath = $"{AtlasOutputDir}/{baseName}.spriteatlas";
                string folderPath = $"{ImgRoot}/{subFolder}";
                SpriteAtlasBuildOptions? opts = baseName == "Danmaku_Img_Bullet"
                    ? SpriteAtlasBuildOptions.DanmakuBullets
                    : SpriteAtlasBuildOptions.DanmakuStandard;
                list.Add(new SpriteAtlasBatchDefinition(atlasPath, folderPath, opts));
            }

            return list;
        }

        /// <summary>
        /// 执行批量生成并返回报告（与菜单入口相同逻辑）。无需 UI 时可由其它脚本直接调用。
        /// </summary>
        public static SpriteAtlasBatchReport RunBatch(System.Action<int, int, string> onProgress = null)
        {
            SpriteAtlasFromSelectionTool.EnsureAssetFolderExists(AtlasOutputDir);
            return SpriteAtlasBatchApi.Build(BuildDefinitions(), onProgress);
        }

        [MenuItem(MenuBatch, false, 0)]
        static void BatchBuildDanmakuImgAtlasesMenu()
        {
            if (!AssetDatabase.IsValidFolder(ImgRoot))
            {
                EditorUtility.DisplayDialog(
                    "Danmaku Img Atlases",
                    $"未找到目录：{ImgRoot}",
                    "OK");
                return;
            }

            SpriteAtlasBatchReport report;
            try
            {
                int total = AtlasManifest.Length;
                report = RunBatch((i, n, label) =>
                    EditorUtility.DisplayProgressBar("Danmaku Img Atlases", label, (i + 1f) / n));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var log = new StringBuilder();
            foreach (var line in report.Messages)
                log.AppendLine(line);

            EditorUtility.DisplayDialog(
                "Danmaku Img Atlases",
                $"完成：成功 {report.SuccessCount}，失败 {report.FailureCount}。\n输出：{AtlasOutputDir}\n\n详情见 Console。",
                "OK");

            Debug.Log($"[DanmakuImgAtlasBatch]\n{log}");
        }
    }
}
