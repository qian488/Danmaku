using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DemoFrameWork.EditorTools
{
    /// <summary>
    /// 批量调整 TextureImporter（最大尺寸、压缩、Crunch 等），影响打包容积与显存占用。
    /// 不修改磁盘上的源图片文件；若需重编码 PNG/JPG，请使用外部工具（ImageMagick、TinyPNG 等）。
    /// </summary>
    public static class TextureImportBatchTool
    {
        private const string PrefsMaxSize = "DemoFrameWork.TextureBatch.MaxTextureSize";
        private const string PrefsCompression = "DemoFrameWork.TextureBatch.Compression";
        private const string PrefsCrunch = "DemoFrameWork.TextureBatch.Crunch";
        private const string PrefsCrunchQ = "DemoFrameWork.TextureBatch.CrunchQuality";
        private const string PrefsMipmaps = "DemoFrameWork.TextureBatch.Mipmaps";
        private const string PrefsSyncPlatforms = "DemoFrameWork.TextureBatch.SyncPlatforms";

        private static readonly int[] MaxSizeChoices = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

        private static readonly HashSet<string> TextureExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd", ".tif", ".tiff", ".exr", ".hdr", ".webp"
        };

        public static int GetMaxTextureSizePref()
        {
            int v = EditorPrefs.GetInt(PrefsMaxSize, 2048);
            foreach (int c in MaxSizeChoices)
            {
                if (c == v) return v;
            }
            return 2048;
        }

        public static void SetMaxTextureSizePref(int v) => EditorPrefs.SetInt(PrefsMaxSize, v);

        public static TextureImporterCompression GetCompressionPref()
        {
            int i = EditorPrefs.GetInt(PrefsCompression, (int)TextureImporterCompression.Compressed);
            if (!Enum.IsDefined(typeof(TextureImporterCompression), i))
                return TextureImporterCompression.Compressed;
            return (TextureImporterCompression)i;
        }

        public static void SetCompressionPref(TextureImporterCompression c) => EditorPrefs.SetInt(PrefsCompression, (int)c);

        public static bool GetCrunchPref() => EditorPrefs.GetBool(PrefsCrunch, false);

        public static void SetCrunchPref(bool v) => EditorPrefs.SetBool(PrefsCrunch, v);

        public static int GetCrunchQualityPref() => Mathf.Clamp(EditorPrefs.GetInt(PrefsCrunchQ, 50), 0, 100);

        public static void SetCrunchQualityPref(int q) => EditorPrefs.SetInt(PrefsCrunchQ, Mathf.Clamp(q, 0, 100));

        public static bool GetMipmapsPref() => EditorPrefs.GetBool(PrefsMipmaps, false);

        public static void SetMipmapsPref(bool v) => EditorPrefs.SetBool(PrefsMipmaps, v);

        public static bool GetSyncPlatformsPref() => EditorPrefs.GetBool(PrefsSyncPlatforms, true);

        public static void SetSyncPlatformsPref(bool v) => EditorPrefs.SetBool(PrefsSyncPlatforms, v);

        public static string AssetPathToFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return assetPath;
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return Path.GetFullPath(assetPath);
            string relative = assetPath.Substring("Assets/".Length);
            return Path.GetFullPath(Path.Combine(Application.dataPath, relative));
        }

        public static bool IsTextureAssetPath(string assetPath)
        {
            string ext = Path.GetExtension(assetPath);
            return TextureExtensions.Contains(ext);
        }

        public static IEnumerable<string> EnumerateTextureAssetPaths(string assetPath)
        {
            string full = AssetPathToFullPath(assetPath);
            if (File.Exists(full))
            {
                if (IsTextureAssetPath(assetPath))
                    yield return assetPath;
                yield break;
            }

            if (!Directory.Exists(full)) yield break;

            foreach (string file in Directory.GetFiles(full, "*.*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                if (!TextureExtensions.Contains(Path.GetExtension(file))) continue;
                if (!file.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                string tail = file.Substring(Application.dataPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                yield return "Assets/" + tail.Replace('\\', '/');
            }
        }

        /// <summary>
        /// 将设置应用到选中资源（文件或文件夹下的纹理）。
        /// </summary>
        public static int ApplyToSelection(
            int maxTextureSize,
            TextureImporterCompression compression,
            bool crunchedCompression,
            int crunchQuality0To100,
            bool mipmaps,
            bool syncStandaloneAndroidIOS,
            out List<string> messages)
        {
            messages = new List<string>();
            int ok = 0;

            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                messages.Add("请先在 Project 中选择纹理或文件夹。");
                return 0;
            }

            maxTextureSize = Mathf.Clamp(maxTextureSize, 32, 8192);
            crunchQuality0To100 = Mathf.Clamp(crunchQuality0To100, 0, 100);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in Selection.assetGUIDs)
                {
                    string root = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(root)) continue;

                    foreach (string path in EnumerateTextureAssetPaths(root))
                    {
                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer == null)
                        {
                            messages.Add($"[跳过] 非 TextureImporter：{path}");
                            continue;
                        }

                        importer.maxTextureSize = maxTextureSize;
                        importer.textureCompression = compression;
                        importer.crunchedCompression = crunchedCompression && compression != TextureImporterCompression.Uncompressed;
                        importer.compressionQuality = crunchQuality0To100;
                        importer.mipmapEnabled = mipmaps;

                        if (syncStandaloneAndroidIOS)
                            SyncBuildPlatforms(importer, maxTextureSize, compression, crunchedCompression && compression != TextureImporterCompression.Uncompressed, crunchQuality0To100);

                        EditorUtility.SetDirty(importer);
                        ok++;
                        messages.Add($"[OK] {path}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
            return ok;
        }

        private static void SyncBuildPlatforms(
            TextureImporter importer,
            int maxTextureSize,
            TextureImporterCompression compression,
            bool crunch,
            int crunchQ)
        {
            foreach (string platform in new[] { "Standalone", "Android", "iPhone" })
            {
                var ps = importer.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = maxTextureSize;
                ps.textureCompression = compression;
                ps.crunchedCompression = crunch;
                ps.compressionQuality = crunchQ;
                importer.SetPlatformTextureSettings(ps);
            }
        }
    }

    public class TextureImportBatchToolWindow : EditorWindow
    {
        private int _maxSize = 2048;
        private TextureImporterCompression _compression = TextureImporterCompression.Compressed;
        private bool _crunch;
        private int _crunchQ = 50;
        private bool _mipmaps;
        private bool _syncPlatforms = true;
        private Vector2 _scroll;
        private int _maxSizeIndex;

        [MenuItem("Tools/DemoFrameWork/Textures/Batch Compression (Import Settings)...")]
        public static void Open()
        {
            var w = GetWindow<TextureImportBatchToolWindow>(true, "Texture Import Batch", true);
            w.minSize = new Vector2(440, 360);
            w.Show();
        }

        private void OnEnable()
        {
            _maxSize = TextureImportBatchTool.GetMaxTextureSizePref();
            _compression = TextureImportBatchTool.GetCompressionPref();
            _crunch = TextureImportBatchTool.GetCrunchPref();
            _crunchQ = TextureImportBatchTool.GetCrunchQualityPref();
            _mipmaps = TextureImportBatchTool.GetMipmapsPref();
            _syncPlatforms = TextureImportBatchTool.GetSyncPlatformsPref();
            _maxSizeIndex = IndexOfMaxSize(_maxSize);
        }

        private static int IndexOfMaxSize(int size)
        {
            int[] choices = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i] == size) return i;
            }
            return 6;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "批量修改 TextureImporter：控制导入后的压缩与最大尺寸，从而影响包体与显存。\n" +
                "不会改写磁盘上的源文件。Spine/UI 图集等请按需单独检查。",
                MessageType.Info);

            EditorGUILayout.LabelField("最大尺寸", EditorStyles.boldLabel);
            _maxSizeIndex = EditorGUILayout.IntPopup(
                "Max Size",
                _maxSizeIndex,
                new[] { "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192" },
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
            _maxSize = new[] { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 }[_maxSizeIndex];

            _compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", _compression);
            EditorGUILayout.HelpBox("Uncompressed 体积最大；Compressed / HQ / LQ 为不同质量档。Crunch 仅对部分格式有效。", MessageType.None);

            using (new EditorGUI.DisabledScope(_compression == TextureImporterCompression.Uncompressed))
            {
                _crunch = EditorGUILayout.ToggleLeft("Use Crunch Compression", _crunch);
                _crunchQ = EditorGUILayout.IntSlider("Crunch Quality (0–100)", _crunchQ, 0, 100);
            }

            _mipmaps = EditorGUILayout.ToggleLeft("Generate Mip Maps", _mipmaps);
            _syncPlatforms = EditorGUILayout.ToggleLeft("同步 Standalone / Android / iOS 平台覆盖（推荐打多平台包时开启）", _syncPlatforms);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("快捷填表", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("2D UI / 精灵（无 Mip，2048，压缩）"))
            {
                _maxSizeIndex = 6;
                _maxSize = 2048;
                _compression = TextureImporterCompression.Compressed;
                _crunch = false;
                _mipmaps = false;
            }
            if (GUILayout.Button("一般场景贴图（Mip，2048，HQ）"))
            {
                _maxSizeIndex = 6;
                _maxSize = 2048;
                _compression = TextureImporterCompression.CompressedHQ;
                _crunch = false;
                _mipmaps = true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("保存为默认选项", GUILayout.Height(22)))
            {
                TextureImportBatchTool.SetMaxTextureSizePref(_maxSize);
                TextureImportBatchTool.SetCompressionPref(_compression);
                TextureImportBatchTool.SetCrunchPref(_crunch);
                TextureImportBatchTool.SetCrunchQualityPref(_crunchQ);
                TextureImportBatchTool.SetMipmapsPref(_mipmaps);
                TextureImportBatchTool.SetSyncPlatformsPref(_syncPlatforms);
                EditorUtility.DisplayDialog("Texture Batch", "已保存。", "确定");
            }

            EditorGUILayout.Space(6);
            GUI.enabled = Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0;
            if (GUILayout.Button("应用到当前 Project 选中项", GUILayout.Height(32)))
            {
                TextureImportBatchTool.SetMaxTextureSizePref(_maxSize);
                TextureImportBatchTool.SetCompressionPref(_compression);
                TextureImportBatchTool.SetCrunchPref(_crunch);
                TextureImportBatchTool.SetCrunchQualityPref(_crunchQ);
                TextureImportBatchTool.SetMipmapsPref(_mipmaps);
                TextureImportBatchTool.SetSyncPlatformsPref(_syncPlatforms);

                int n = TextureImportBatchTool.ApplyToSelection(
                    _maxSize, _compression, _crunch, _crunchQ, _mipmaps, _syncPlatforms, out var msgs);
                _scroll = Vector2.zero;
                var sb = new System.Text.StringBuilder();
                foreach (var m in msgs)
                    sb.AppendLine(m);
                EditorUtility.DisplayDialog("Texture Batch", $"完成。已处理：{n} 个。\n\n" + sb, "确定");
            }
            GUI.enabled = true;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("也可在 Project 中右键纹理或文件夹 → Batch Texture Import Settings。", MessageType.None);
        }
    }

    public static class TextureImportBatchToolMenu
    {
        [MenuItem("Assets/Textures/Batch Compression (Import Settings)", false, 1200)]
        private static void Run()
        {
            int n = TextureImportBatchTool.ApplyToSelection(
                TextureImportBatchTool.GetMaxTextureSizePref(),
                TextureImportBatchTool.GetCompressionPref(),
                TextureImportBatchTool.GetCrunchPref(),
                TextureImportBatchTool.GetCrunchQualityPref(),
                TextureImportBatchTool.GetMipmapsPref(),
                TextureImportBatchTool.GetSyncPlatformsPref(),
                out var msgs);
            var sb = new System.Text.StringBuilder();
            foreach (var m in msgs)
                sb.AppendLine(m);
            EditorUtility.DisplayDialog("Texture Batch", $"已处理：{n} 个。\n\n" + sb, "确定");
        }

        [MenuItem("Assets/Textures/Batch Compression (Import Settings)", true)]
        private static bool Validate()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0) return false;
            foreach (string guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                string full = TextureImportBatchTool.AssetPathToFullPath(path);
                if (Directory.Exists(full)) return true;
                if (TextureImportBatchTool.IsTextureAssetPath(path)) return true;
            }
            return false;
        }
    }
}
