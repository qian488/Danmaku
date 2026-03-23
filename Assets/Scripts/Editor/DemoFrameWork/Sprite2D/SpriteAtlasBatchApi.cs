using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace DemoFrameWork.Editor.Sprite2D
{
    /// <summary>
    /// 单条批量任务：生成到 <see cref="AtlasAssetPath"/>，打包对象由 <see cref="PackableAssetPaths"/> 解析（文件夹或 Sprite 纹理的 Assets 路径）。
    /// </summary>
    public readonly struct SpriteAtlasBatchDefinition
    {
        public string AtlasAssetPath { get; }
        public IReadOnlyList<string> PackableAssetPaths { get; }
        /// <summary>为 null 时由 <see cref="SpriteAtlasBatchApi.Build"/> 的 <c>defaultBuildOptions</c> 或引擎侧默认补齐。</summary>
        public SpriteAtlasBuildOptions? BuildOptions { get; }

        public SpriteAtlasBatchDefinition(string atlasAssetPath, params string[] packableAssetPaths)
            : this(atlasAssetPath, (IReadOnlyList<string>)packableAssetPaths, null)
        {
        }

        public SpriteAtlasBatchDefinition(string atlasAssetPath, IReadOnlyList<string> packableAssetPaths)
            : this(atlasAssetPath, packableAssetPaths, null)
        {
        }

        /// <summary>
        /// 单个文件夹或纹理路径 + 打包选项；避免三参数调用被解析为 <c>params string[]</c> 构造函数而把第三参当成路径字符串。
        /// </summary>
        public SpriteAtlasBatchDefinition(
            string atlasAssetPath,
            string packableAssetPath,
            SpriteAtlasBuildOptions? buildOptions)
            : this(atlasAssetPath, new[] { packableAssetPath }, buildOptions)
        {
        }

        public SpriteAtlasBatchDefinition(
            string atlasAssetPath,
            IReadOnlyList<string> packableAssetPaths,
            SpriteAtlasBuildOptions? buildOptions)
        {
            AtlasAssetPath = atlasAssetPath;
            PackableAssetPaths = packableAssetPaths ?? Array.Empty<string>();
            BuildOptions = buildOptions;
        }
    }

    /// <summary>
    /// <see cref="SpriteAtlasBatchApi.Build"/> 的执行结果。
    /// </summary>
    public sealed class SpriteAtlasBatchReport
    {
        public int SuccessCount { get; internal set; }
        public int FailureCount { get; internal set; }
        public List<string> Messages { get; } = new List<string>();
        public IReadOnlyList<SpriteAtlas> PackedAtlases { get; internal set; } = Array.Empty<SpriteAtlas>();
    }

    /// <summary>
    /// 程序化批量创建/覆盖 Sprite Atlas（无需菜单点击）。单图逻辑与 <see cref="SpriteAtlasFromSelectionTool.TryCreateOrOverwriteAtlas"/> 一致，最后统一 <see cref="SpriteAtlasUtility.PackAtlases"/>。
    /// </summary>
    public static class SpriteAtlasBatchApi
    {
        /// <summary>
        /// 按定义列表批量生成图集。失败条目计入 <see cref="SpriteAtlasBatchReport.FailureCount"/> 并写入 <see cref="SpriteAtlasBatchReport.Messages"/>。
        /// </summary>
        /// <param name="onProgress">可选；参数为 (当前索引 0-based, 总数, 当前图集 asset 路径)。</param>
        /// <param name="defaultBuildOptions">
        /// 当某条 <see cref="SpriteAtlasBatchDefinition.BuildOptions"/> 为 null 时使用；整条为 null 时等价于
        /// <see cref="SpriteAtlasBuildOptions.FrameworkDefault"/>（与 <see cref="SpriteAtlasFromSelectionTool"/> 菜单一致）。
        /// </param>
        public static SpriteAtlasBatchReport Build(
            IReadOnlyList<SpriteAtlasBatchDefinition> definitions,
            Action<int, int, string> onProgress = null,
            SpriteAtlasBuildOptions? defaultBuildOptions = null)
        {
            var report = new SpriteAtlasBatchReport();
            if (definitions == null || definitions.Count == 0)
                return report;

            var built = new List<SpriteAtlas>();
            int total = definitions.Count;
            var fallback = defaultBuildOptions ?? SpriteAtlasBuildOptions.FrameworkDefault;

            for (int i = 0; i < total; i++)
            {
                var def = definitions[i];
                onProgress?.Invoke(i, total, def.AtlasAssetPath);

                if (!TryBuildPackablesFromAssetPaths(def.PackableAssetPaths, out var packables, out string resolveErr))
                {
                    report.FailureCount++;
                    report.Messages.Add($"[失败] {def.AtlasAssetPath}: {resolveErr}");
                    continue;
                }

                var opts = def.BuildOptions ?? fallback;
                if (!SpriteAtlasFromSelectionTool.TryCreateOrOverwriteAtlas(
                        def.AtlasAssetPath,
                        packables,
                        repack: false,
                        opts,
                        out string err))
                {
                    report.FailureCount++;
                    report.Messages.Add($"[失败] {def.AtlasAssetPath}: {err}");
                    continue;
                }

                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(NormalizeAssetPath(def.AtlasAssetPath));
                if (atlas != null)
                    built.Add(atlas);

                report.SuccessCount++;
                report.Messages.Add($"[OK] {def.AtlasAssetPath}");
            }

            if (built.Count > 0)
                SpriteAtlasUtility.PackAtlases(built.ToArray(), EditorUserBuildSettings.activeBuildTarget);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.PackedAtlases = built;
            return report;
        }

        /// <summary>
        /// 将 Assets 路径解析为可作为 Sprite Atlas packable 的 <see cref="UnityEngine.Object"/>（文件夹 → <see cref="DefaultAsset"/>；纹理 → Sprite 类型的 <see cref="Texture2D"/>）。
        /// </summary>
        public static bool TryBuildPackablesFromAssetPaths(
            IReadOnlyList<string> assetPaths,
            out List<UnityEngine.Object> packables,
            out string errorMessage)
        {
            packables = new List<UnityEngine.Object>();
            errorMessage = null;

            if (assetPaths == null || assetPaths.Count == 0)
            {
                errorMessage = "可打包资源路径列表为空。";
                return false;
            }

            foreach (var raw in assetPaths)
            {
                string path = NormalizeAssetPath(raw);
                if (string.IsNullOrEmpty(path))
                {
                    errorMessage = "存在空的资源路径。";
                    return false;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                    if (folder == null)
                    {
                        errorMessage = $"无法加载文件夹: {path}";
                        return false;
                    }

                    packables.Add(folder);
                    continue;
                }

                var main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main is Texture2D)
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null || importer.textureType != TextureImporterType.Sprite)
                    {
                        errorMessage = $"非 Sprite 纹理: {path}";
                        return false;
                    }

                    packables.Add(main);
                    continue;
                }

                errorMessage = $"不支持的资源路径（须为文件夹或 Sprite 纹理）: {path}";
                return false;
            }

            if (packables.Count == 0)
            {
                errorMessage = "没有有效的打包对象。";
                return false;
            }

            return true;
        }

        static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            return path.Trim().Replace('\\', '/');
        }
    }
}
