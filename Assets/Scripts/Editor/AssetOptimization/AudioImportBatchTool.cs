using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DemoFrameWork.EditorTools
{
    /// <summary>
    /// 批量调整 AudioImporter（Force Mono、Vorbis 质量、Load Type、采样率策略等），减小包内音频体积。
    /// 与「Audio → OGG (FFmpeg)」互补：后者改源文件格式，本工具改 Unity 导入与平台压缩参数。
    /// </summary>
    public static class AudioImportBatchTool
    {
        private const string PrefsForceMono = "DemoFrameWork.AudioBatch.ForceMono";
        private const string PrefsLoadType = "DemoFrameWork.AudioBatch.LoadType";
        private const string PrefsCompressionFormat = "DemoFrameWork.AudioBatch.CompressionFormat";
        private const string PrefsQuality = "DemoFrameWork.AudioBatch.Quality";
        private const string PrefsSampleRate = "DemoFrameWork.AudioBatch.SampleRateSetting";
        private const string PrefsPreload = "DemoFrameWork.AudioBatch.Preload";

        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".m4a", ".wma", ".mod", ".it", ".s3m", ".xm"
        };

        public static bool GetForceMonoPref() => EditorPrefs.GetBool(PrefsForceMono, false);

        public static void SetForceMonoPref(bool v) => EditorPrefs.SetBool(PrefsForceMono, v);

        public static AudioClipLoadType GetLoadTypePref()
        {
            int i = EditorPrefs.GetInt(PrefsLoadType, (int)AudioClipLoadType.CompressedInMemory);
            if (!Enum.IsDefined(typeof(AudioClipLoadType), i))
                return AudioClipLoadType.CompressedInMemory;
            return (AudioClipLoadType)i;
        }

        public static void SetLoadTypePref(AudioClipLoadType t) => EditorPrefs.SetInt(PrefsLoadType, (int)t);

        public static AudioCompressionFormat GetCompressionFormatPref()
        {
            int i = EditorPrefs.GetInt(PrefsCompressionFormat, (int)AudioCompressionFormat.Vorbis);
            if (!Enum.IsDefined(typeof(AudioCompressionFormat), i))
                return AudioCompressionFormat.Vorbis;
            return (AudioCompressionFormat)i;
        }

        public static void SetCompressionFormatPref(AudioCompressionFormat f) => EditorPrefs.SetInt(PrefsCompressionFormat, (int)f);

        public static float GetQualityPref() => Mathf.Clamp01(EditorPrefs.GetFloat(PrefsQuality, 0.7f));

        public static void SetQualityPref(float q) => EditorPrefs.SetFloat(PrefsQuality, Mathf.Clamp01(q));

        public static AudioSampleRateSetting GetSampleRateSettingPref()
        {
            int i = EditorPrefs.GetInt(PrefsSampleRate, (int)AudioSampleRateSetting.OptimizeSampleRate);
            if (!Enum.IsDefined(typeof(AudioSampleRateSetting), i))
                return AudioSampleRateSetting.OptimizeSampleRate;
            return (AudioSampleRateSetting)i;
        }

        public static void SetSampleRateSettingPref(AudioSampleRateSetting s) => EditorPrefs.SetInt(PrefsSampleRate, (int)s);

        public static bool GetPreloadPref() => EditorPrefs.GetBool(PrefsPreload, false);

        public static void SetPreloadPref(bool v) => EditorPrefs.SetBool(PrefsPreload, v);

        public static string AssetPathToFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return assetPath;
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return Path.GetFullPath(assetPath);
            string relative = assetPath.Substring("Assets/".Length);
            return Path.GetFullPath(Path.Combine(Application.dataPath, relative));
        }

        public static bool IsAudioAssetPath(string assetPath)
        {
            return AudioExtensions.Contains(Path.GetExtension(assetPath));
        }

        public static IEnumerable<string> EnumerateAudioAssetPaths(string assetPath)
        {
            string full = AssetPathToFullPath(assetPath);
            if (File.Exists(full))
            {
                if (IsAudioAssetPath(assetPath))
                    yield return assetPath;
                yield break;
            }

            if (!Directory.Exists(full)) yield break;

            foreach (string file in Directory.GetFiles(full, "*.*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                if (!AudioExtensions.Contains(Path.GetExtension(file))) continue;
                if (!file.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                string tail = file.Substring(Application.dataPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                yield return "Assets/" + tail.Replace('\\', '/');
            }
        }

        public static int ApplyToSelection(
            bool forceMono,
            AudioClipLoadType loadType,
            AudioCompressionFormat compressionFormat,
            float quality01,
            AudioSampleRateSetting sampleRateSetting,
            bool preloadAudioData,
            out List<string> messages)
        {
            messages = new List<string>();
            int ok = 0;
            quality01 = Mathf.Clamp01(quality01);

            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                messages.Add("请先在 Project 中选择音频或文件夹。");
                return 0;
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in Selection.assetGUIDs)
                {
                    string root = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(root)) continue;

                    foreach (string path in EnumerateAudioAssetPaths(root))
                    {
                        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                        if (importer == null)
                        {
                            messages.Add($"[跳过] 非 AudioImporter：{path}");
                            continue;
                        }

                        importer.forceToMono = forceMono;

                        AudioImporterSampleSettings s = importer.defaultSampleSettings;
                        s.loadType = loadType;
                        s.compressionFormat = compressionFormat;
                        s.quality = quality01;
                        s.sampleRateSetting = sampleRateSetting;
                        s.preloadAudioData = preloadAudioData;
                        importer.defaultSampleSettings = s;

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
    }

    public class AudioImportBatchToolWindow : EditorWindow
    {
        private bool _forceMono;
        private AudioClipLoadType _loadType = AudioClipLoadType.CompressedInMemory;
        private AudioCompressionFormat _compression = AudioCompressionFormat.Vorbis;
        private float _quality = 0.7f;
        private AudioSampleRateSetting _sampleRate = AudioSampleRateSetting.OptimizeSampleRate;
        private bool _preload;

        [MenuItem("Tools/DemoFrameWork/Audio/Batch Compression (Import Settings)...")]
        public static void Open()
        {
            var w = GetWindow<AudioImportBatchToolWindow>(true, "Audio Import Batch", true);
            w.minSize = new Vector2(420, 340);
            w.Show();
        }

        private void OnEnable()
        {
            _forceMono = AudioImportBatchTool.GetForceMonoPref();
            _loadType = AudioImportBatchTool.GetLoadTypePref();
            _compression = AudioImportBatchTool.GetCompressionFormatPref();
            _quality = AudioImportBatchTool.GetQualityPref();
            _sampleRate = AudioImportBatchTool.GetSampleRateSettingPref();
            _preload = AudioImportBatchTool.GetPreloadPref();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "批量修改 AudioImporter：Force Mono、压缩格式与质量等，影响打包包体大小。\n" +
                "若需把 WAV 等转成 OGG 源文件，请用 Tools → Audio → Convert To OGG (FFmpeg)。",
                MessageType.Info);

            _forceMono = EditorGUILayout.ToggleLeft("Force To Mono（双声道变单声道，常能明显减小体积）", _forceMono);
            _loadType = (AudioClipLoadType)EditorGUILayout.EnumPopup("Load Type", _loadType);
            _compression = (AudioCompressionFormat)EditorGUILayout.EnumPopup("Compression Format", _compression);

            using (new EditorGUI.DisabledScope(_compression == AudioCompressionFormat.PCM))
            {
                _quality = EditorGUILayout.Slider("Quality (Vorbis/MP3 等)", _quality, 0.01f, 1f);
            }

            _sampleRate = (AudioSampleRateSetting)EditorGUILayout.EnumPopup("Sample Rate", _sampleRate);
            EditorGUILayout.HelpBox("Optimize Sample Rate 由 Unity 按平台选择合适采样率。", MessageType.None);
            _preload = EditorGUILayout.ToggleLeft("Preload Audio Data", _preload);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("预设：短音效（Compressed，Vorbis 0.65）"))
            {
                _loadType = AudioClipLoadType.CompressedInMemory;
                _compression = AudioCompressionFormat.Vorbis;
                _quality = 0.65f;
                _sampleRate = AudioSampleRateSetting.OptimizeSampleRate;
            }
            if (GUILayout.Button("预设：音乐流式（Streaming）"))
            {
                _loadType = AudioClipLoadType.Streaming;
                _compression = AudioCompressionFormat.Vorbis;
                _quality = 0.75f;
                _sampleRate = AudioSampleRateSetting.OptimizeSampleRate;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("保存为默认选项", GUILayout.Height(22)))
            {
                AudioImportBatchTool.SetForceMonoPref(_forceMono);
                AudioImportBatchTool.SetLoadTypePref(_loadType);
                AudioImportBatchTool.SetCompressionFormatPref(_compression);
                AudioImportBatchTool.SetQualityPref(_quality);
                AudioImportBatchTool.SetSampleRateSettingPref(_sampleRate);
                AudioImportBatchTool.SetPreloadPref(_preload);
                EditorUtility.DisplayDialog("Audio Batch", "已保存。", "确定");
            }

            EditorGUILayout.Space(6);
            GUI.enabled = Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0;
            if (GUILayout.Button("应用到当前 Project 选中项", GUILayout.Height(32)))
            {
                AudioImportBatchTool.SetForceMonoPref(_forceMono);
                AudioImportBatchTool.SetLoadTypePref(_loadType);
                AudioImportBatchTool.SetCompressionFormatPref(_compression);
                AudioImportBatchTool.SetQualityPref(_quality);
                AudioImportBatchTool.SetSampleRateSettingPref(_sampleRate);
                AudioImportBatchTool.SetPreloadPref(_preload);

                int n = AudioImportBatchTool.ApplyToSelection(
                    _forceMono, _loadType, _compression, _quality, _sampleRate, _preload, out var msgs);
                var sb = new System.Text.StringBuilder();
                foreach (var m in msgs)
                    sb.AppendLine(m);
                EditorUtility.DisplayDialog("Audio Batch", $"完成。已处理：{n} 个。\n\n" + sb, "确定");
            }
            GUI.enabled = true;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("也可在 Project 中右键音频或文件夹 → Batch Audio Import Settings。", MessageType.None);
        }
    }

    public static class AudioImportBatchToolMenu
    {
        [MenuItem("Assets/Audio/Batch Compression (Import Settings)", false, 1250)]
        private static void Run()
        {
            int n = AudioImportBatchTool.ApplyToSelection(
                AudioImportBatchTool.GetForceMonoPref(),
                AudioImportBatchTool.GetLoadTypePref(),
                AudioImportBatchTool.GetCompressionFormatPref(),
                AudioImportBatchTool.GetQualityPref(),
                AudioImportBatchTool.GetSampleRateSettingPref(),
                AudioImportBatchTool.GetPreloadPref(),
                out var msgs);
            var sb = new System.Text.StringBuilder();
            foreach (var m in msgs)
                sb.AppendLine(m);
            EditorUtility.DisplayDialog("Audio Batch", $"已处理：{n} 个。\n\n" + sb, "确定");
        }

        [MenuItem("Assets/Audio/Batch Compression (Import Settings)", true)]
        private static bool Validate()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0) return false;
            foreach (string guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                string full = AudioImportBatchTool.AssetPathToFullPath(path);
                if (Directory.Exists(full)) return true;
                if (AudioImportBatchTool.IsAudioAssetPath(path)) return true;
            }
            return false;
        }
    }
}
