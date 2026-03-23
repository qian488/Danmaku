using System;
using DemoFrameWork.GameLogic.Danmaku.Editor;
using UnityEditor;
using UnityEngine;

namespace DemoFrameWork.Demos.Danmaku.Editor
{
    /// <summary>
    /// 供 Unity 命令行批量生成弹幕 Img 图集（无界面）。与 <c>GenerateExpandedStages.py</c> 一样可由终端触发，但图集须走 Unity API，故通过 Batch Mode 调本方法。
    /// <para>
    /// 示例：<c>Unity.exe -batchmode -quit -nographics -projectPath &lt;repo&gt; -executeMethod DemoFrameWork.Demos.Danmaku.Editor.DanmakuSpriteAtlasCli.BuildImgSpriteAtlasesFromCli</c>
    /// </para>
    /// </summary>
    public static class DanmakuSpriteAtlasCli
    {
        public static void BuildImgSpriteAtlasesFromCli()
        {
            try
            {
                if (!AssetDatabase.IsValidFolder(DanmakuImgAtlasBatchTool.ImgRoot))
                {
                    Debug.LogError($"[DanmakuSpriteAtlasCli] 未找到目录: {DanmakuImgAtlasBatchTool.ImgRoot}");
                    EditorApplication.Exit(1);
                    return;
                }

                var report = DanmakuImgAtlasBatchTool.RunBatch();
                foreach (var line in report.Messages)
                    Debug.Log($"[DanmakuSpriteAtlasCli] {line}");

                EditorApplication.Exit(report.FailureCount > 0 ? 1 : 0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
