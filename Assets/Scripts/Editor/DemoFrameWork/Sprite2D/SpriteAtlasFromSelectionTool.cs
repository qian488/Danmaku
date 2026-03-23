using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace DemoFrameWork.Editor.Sprite2D
{
    /// <summary>
    /// Sprite Atlas 打包与纹理采样参数（可编程覆盖默认值）。
    /// </summary>
    public struct SpriteAtlasBuildOptions
    {
        public int Padding;
        public bool EnableTightPacking;
        public bool EnableRotation;
        public FilterMode FilterMode;
        public bool GenerateMipMaps;

        /// <summary>与菜单「从选中创建」一致：紧包 + 小间距，体积小但透明边易串图。</summary>
        public static SpriteAtlasBuildOptions FrameworkDefault => new SpriteAtlasBuildOptions
        {
            Padding = 4,
            EnableTightPacking = true,
            EnableRotation = false,
            FilterMode = FilterMode.Bilinear,
            GenerateMipMaps = false,
        };

        /// <summary>弹幕等透明边多的 2D：矩形包 + 更大 padding，减轻双线性采样邻图渗入。</summary>
        public static SpriteAtlasBuildOptions DanmakuStandard => new SpriteAtlasBuildOptions
        {
            Padding = 8,
            EnableTightPacking = false,
            EnableRotation = false,
            FilterMode = FilterMode.Bilinear,
            GenerateMipMaps = false,
        };

        /// <summary>小尺寸弹图：点过滤，缩放时边缘更硬（是否更好看依美术与分辨率而定）。</summary>
        public static SpriteAtlasBuildOptions DanmakuBullets => new SpriteAtlasBuildOptions
        {
            Padding = 8,
            EnableTightPacking = false,
            EnableRotation = false,
            FilterMode = FilterMode.Point,
            GenerateMipMaps = false,
        };
    }

    /// <summary>
    /// 从 Project 选中项创建 Unity Sprite Atlas（UGUI / SpriteRenderer），走引擎打包与合批管线。
    /// 支持：Sprite 纹理、文件夹（整夹入图集）。
    /// 保存使用系统另存为对话框，起始目录为 <see cref="DefaultAtlasSaveDirectory"/> 的磁盘路径；须保存在工程 Assets 下（可改到 Demo 等子目录）。
    /// </summary>
    public static class SpriteAtlasFromSelectionTool
    {
        const string MenuRoot = "Tools/DemoFrameWork/2D/Sprite Atlas/";
        const string MenuAssets = "Assets/DemoFrameWork/2D/Sprite Atlas/";

        /// <summary>框架默认图集保存目录（相对工程；不存在时会在打开对话框前创建）。</summary>
        public const string DefaultAtlasSaveDirectory = "Assets/Resources/Image";

        [MenuItem(MenuRoot + "New From Selection...", false, 0)]
        [MenuItem(MenuAssets + "New From Selection...", false, 1200)]
        static void CreateFromSelection()
        {
            if (!TryCollectPackables(out var packables, out var skippedNonSprite, out var skippedOther))
                return;

            EnsureDefaultAtlasDirectoryExists();

            string defaultFileName = DeriveDefaultAtlasFileName(DefaultAtlasSaveDirectory);
            string startDir = GetDefaultAtlasAbsoluteDirectory();
            Directory.CreateDirectory(startDir);

            // SaveFilePanelInProject 在部分环境下会错误落到系统“另存为”并沿用 OS 上次路径；改用 SaveFilePanel + Assets 绝对路径作为起始目录。
            string fullPath = EditorUtility.SaveFilePanel(
                "Save Sprite Atlas",
                startDir,
                defaultFileName + ".spriteatlas",
                "spriteatlas");
            if (string.IsNullOrEmpty(fullPath))
                return;

            if (!fullPath.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase))
                fullPath += ".spriteatlas";

            if (!TryFullPathToAssetPath(fullPath, out string savePath))
            {
                EditorUtility.DisplayDialog(
                    "Sprite Atlas",
                    "请将图集保存到本工程 Assets 目录下（例如 Assets/Resources/Image 或 Assets/Resources/Demos/...）。",
                    "OK");
                return;
            }

            savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

            if (!TryCreateOrOverwriteAtlas(savePath, packables, repack: true, out string err))
            {
                EditorUtility.DisplayDialog("Sprite Atlas", err ?? "创建失败。", "OK");
                return;
            }

            AssetDatabase.Refresh();

            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(savePath);
            if (atlas != null)
            {
                Selection.activeObject = atlas;
                EditorGUIUtility.PingObject(atlas);
            }

            var msg = $"Sprite Atlas 已创建：{savePath}\n包含 {packables.Count} 个打包对象。";
            if (skippedNonSprite.Count > 0)
                msg += $"\n已跳过非 Sprite 纹理 {skippedNonSprite.Count} 个（见 Console）。";
            if (skippedOther.Count > 0)
                msg += $"\n已跳过不支持的资源 {skippedOther.Count} 个。";
            EditorUtility.DisplayDialog("Sprite Atlas", msg, "OK");

            foreach (var p in skippedNonSprite)
                Debug.LogWarning($"[SpriteAtlas] 跳过（Texture Type 非 Sprite）: {p}", null);
        }

        [MenuItem(MenuRoot + "New From Selection...", true)]
        [MenuItem(MenuAssets + "New From Selection...", true)]
        static bool ValidateCreateFromSelection()
        {
            return TryCollectPackables(out _, out _, out _);
        }

        /// <summary>
        /// 在 <paramref name="spriteAtlasAssetPath"/> 创建或覆盖 Sprite Atlas（会先 Delete 同路径旧资源），并应用与菜单工具一致的打包参数。
        /// </summary>
        /// <param name="repack">为 true 时立即对本图集执行 <see cref="SpriteAtlasUtility.PackAtlases"/>；批量时可 false，最后统一 Pack。</param>
        public static bool TryCreateOrOverwriteAtlas(
            string spriteAtlasAssetPath,
            IList<UnityEngine.Object> packables,
            bool repack,
            out string errorMessage)
        {
            return TryCreateOrOverwriteAtlas(spriteAtlasAssetPath, packables, repack, null, out errorMessage);
        }

        /// <param name="buildOptions">为 null 时使用 <see cref="SpriteAtlasBuildOptions.FrameworkDefault"/>。</param>
        public static bool TryCreateOrOverwriteAtlas(
            string spriteAtlasAssetPath,
            IList<UnityEngine.Object> packables,
            bool repack,
            SpriteAtlasBuildOptions? buildOptions,
            out string errorMessage)
        {
            errorMessage = null;
            if (packables == null || packables.Count == 0)
            {
                errorMessage = "打包对象列表为空。";
                return false;
            }

            spriteAtlasAssetPath = ToAssetSlashes(spriteAtlasAssetPath.Trim());
            if (!spriteAtlasAssetPath.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "路径必须以 .spriteatlas 结尾。";
                return false;
            }

            foreach (var p in packables)
            {
                if (p == null)
                {
                    errorMessage = "打包列表中含 null 引用。";
                    return false;
                }
            }

            string parentDir = ToAssetSlashes(Path.GetDirectoryName(spriteAtlasAssetPath) ?? "");
            if (!string.IsNullOrEmpty(parentDir))
                EnsureAssetFolderExists(parentDir);

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(spriteAtlasAssetPath) != null)
                AssetDatabase.DeleteAsset(spriteAtlasAssetPath);

            AssetDatabase.Refresh();

            var atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, spriteAtlasAssetPath);

            SpriteAtlasExtensions.Add(atlas, new List<UnityEngine.Object>(packables).ToArray());
            ApplyPackingAndTextureSettings(atlas, buildOptions ?? SpriteAtlasBuildOptions.FrameworkDefault);

            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();

            if (repack)
                SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);

            return true;
        }

        public static void ApplyDefaultPackingAndTextureSettings(SpriteAtlas atlas)
        {
            ApplyPackingAndTextureSettings(atlas, SpriteAtlasBuildOptions.FrameworkDefault);
        }

        public static void ApplyPackingAndTextureSettings(SpriteAtlas atlas, SpriteAtlasBuildOptions options)
        {
            var packing = SpriteAtlasExtensions.GetPackingSettings(atlas);
            packing.padding = options.Padding;
            packing.enableRotation = options.EnableRotation;
            packing.enableTightPacking = options.EnableTightPacking;
            SpriteAtlasExtensions.SetPackingSettings(atlas, packing);

            var tex = SpriteAtlasExtensions.GetTextureSettings(atlas);
            tex.filterMode = options.FilterMode;
            tex.generateMipMaps = options.GenerateMipMaps;
            SpriteAtlasExtensions.SetTextureSettings(atlas, tex);
        }

        /// <summary>确保 Assets 下文件夹存在（递归创建）。</summary>
        public static void EnsureAssetFolderExists(string assetFolderPath)
        {
            assetFolderPath = ToAssetSlashes(assetFolderPath.TrimEnd('/'));
            if (string.IsNullOrEmpty(assetFolderPath) || assetFolderPath == "Assets")
                return;
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            string parent = ToAssetSlashes(Path.GetDirectoryName(assetFolderPath) ?? "");
            string name = Path.GetFileName(assetFolderPath);
            if (string.IsNullOrEmpty(name))
                return;

            if (!string.IsNullOrEmpty(parent) && parent != "Assets" && !AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolderExists(parent);

            if (!AssetDatabase.IsValidFolder(assetFolderPath))
                AssetDatabase.CreateFolder(
                    string.IsNullOrEmpty(parent) ? "Assets" : parent,
                    name);
        }

        static bool TryCollectPackables(
            out List<UnityEngine.Object> packables,
            out List<string> skippedNonSprite,
            out List<string> skippedOther)
        {
            packables = new List<UnityEngine.Object>();
            skippedNonSprite = new List<string>();
            skippedOther = new List<string>();

            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
                return false;

            var seen = new HashSet<string>();

            foreach (string guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (seen.Contains(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    seen.Add(path);
                    var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                    if (folder != null)
                        packables.Add(folder);
                    continue;
                }

                var main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main is Texture2D)
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null || importer.textureType != TextureImporterType.Sprite)
                    {
                        skippedNonSprite.Add(path);
                        continue;
                    }

                    seen.Add(path);
                    packables.Add(main);
                    continue;
                }

                skippedOther.Add(path);
            }

            return packables.Count > 0;
        }

        static void EnsureDefaultAtlasDirectoryExists()
        {
            if (AssetDatabase.IsValidFolder(DefaultAtlasSaveDirectory))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(DefaultAtlasSaveDirectory))
                AssetDatabase.CreateFolder("Assets/Resources", "Image");
        }

        static string GetDefaultAtlasAbsoluteDirectory()
        {
            string rel = DefaultAtlasSaveDirectory.Trim().Replace('\\', '/').TrimEnd('/');
            const string prefix = "Assets/";
            if (rel.StartsWith(prefix, StringComparison.Ordinal))
                rel = rel.Substring(prefix.Length);
            return Path.Combine(Application.dataPath, rel.Replace('/', Path.DirectorySeparatorChar));
        }

        static bool TryFullPathToAssetPath(string fullPath, out string assetPath)
        {
            assetPath = null;
            if (string.IsNullOrEmpty(fullPath))
                return false;

            fullPath = Path.GetFullPath(fullPath);
            string dataPath = Path.GetFullPath(Application.dataPath);
            if (!fullPath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                return false;

            string tail = fullPath.Substring(dataPath.Length).TrimStart(Path.DirectorySeparatorChar, '/');
            assetPath = "Assets/" + ToAssetSlashes(tail);
            return true;
        }

        static string DeriveDefaultAtlasFileName(string assetDirectory)
        {
            if (string.IsNullOrEmpty(assetDirectory) || assetDirectory == "Assets")
                return "NewSpriteAtlas";

            string leaf = Path.GetFileName(assetDirectory.TrimEnd('/'));
            if (string.IsNullOrEmpty(leaf))
                return "NewSpriteAtlas";

            return leaf + "Atlas";
        }

        static string ToAssetSlashes(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }
    }
}
