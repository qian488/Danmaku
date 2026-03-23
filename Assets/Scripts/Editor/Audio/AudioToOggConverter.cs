using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DemoFrameWork.EditorTools
{
    /// <summary>
    /// 将工程内音频（如导入失败的 WAV）通过外部 FFmpeg 转为 OGG (Vorbis)，便于 Unity 正常导入。
    /// 需本机安装 FFmpeg 并配置 PATH，或在窗口中指定 ffmpeg.exe 完整路径。
    /// </summary>
    public static class AudioToOggConverter
    {
        private const string PrefsFfmpegPath = "DemoFrameWork.AudioToOgg.FFmpegPath";
        private const string PrefsVorbisQ = "DemoFrameWork.AudioToOgg.VorbisQuality";
        private const string PrefsDeleteSource = "DemoFrameWork.AudioToOgg.DeleteSourceAfterConvert";

        private static readonly HashSet<string> SupportedInputExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".wma", ".m4a"
        };

        public static string GetFfmpegPathPref() => EditorPrefs.GetString(PrefsFfmpegPath, "");

        public static void SetFfmpegPathPref(string path) => EditorPrefs.SetString(PrefsFfmpegPath, path ?? "");

        public static int GetVorbisQualityPref() => Mathf.Clamp(EditorPrefs.GetInt(PrefsVorbisQ, 5), 0, 10);

        public static void SetVorbisQualityPref(int q) => EditorPrefs.SetInt(PrefsVorbisQ, Mathf.Clamp(q, 0, 10));

        public static bool GetDeleteSourcePref() => EditorPrefs.GetBool(PrefsDeleteSource, false);

        public static void SetDeleteSourcePref(bool v) => EditorPrefs.SetBool(PrefsDeleteSource, v);

        /// <summary>将 Assets 相对路径转为磁盘绝对路径。</summary>
        public static string AssetPathToFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return assetPath;
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return Path.GetFullPath(assetPath);
            string relative = assetPath.Substring("Assets/".Length);
            return Path.GetFullPath(Path.Combine(Application.dataPath, relative));
        }

        public static bool IsSupportedAudioExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            if (!extension.StartsWith(".", StringComparison.Ordinal))
                extension = "." + extension;
            return SupportedInputExtensions.Contains(extension);
        }

        /// <summary>测试 FFmpeg 是否可执行。</summary>
        public static bool TryGetFfmpegExecutable(out string ffmpegExe, out string message)
        {
            ffmpegExe = null;
            message = null;

            string pref = GetFfmpegPathPref()?.Trim();
            if (!string.IsNullOrEmpty(pref) && File.Exists(pref))
            {
                ffmpegExe = pref;
                if (TryRunFfmpegVersion(ffmpegExe, out message))
                    return true;
                message = "已配置路径但执行失败：\n" + message;
                return false;
            }

            // PATH 中的 ffmpeg
            ffmpegExe = "ffmpeg";
            if (TryRunFfmpegVersion(ffmpegExe, out message))
                return true;

            ffmpegExe = null;
            message = "未找到可用的 FFmpeg。\n" +
                      "• Windows：从 https://ffmpeg.org 下载，将 bin\\ffmpeg.exe 的完整路径填到本工具；或把 bin 加入系统 PATH。\n" +
                      "• macOS：brew install ffmpeg\n" +
                      "• 上次错误：" + message;
            return false;
        }

        private static bool TryRunFfmpegVersion(string ffmpegExe, out string combinedOutput)
        {
            combinedOutput = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit(8000);
                    combinedOutput = (stdout + "\n" + stderr).Trim();
                    return p.ExitCode == 0;
                }
            }
            catch (Exception e)
            {
                combinedOutput = e.Message;
                return false;
            }
        }

        /// <summary>转换单个文件；输出为同目录同名 .ogg（覆盖已存在）。</summary>
        public static bool ConvertFile(string inputFullPath, string ffmpegExe, int vorbisQuality0To10, out string errorLog)
        {
            errorLog = null;
            if (string.IsNullOrEmpty(inputFullPath) || !File.Exists(inputFullPath))
            {
                errorLog = "文件不存在：" + inputFullPath;
                return false;
            }

            string dir = Path.GetDirectoryName(inputFullPath);
            string nameNoExt = Path.GetFileNameWithoutExtension(inputFullPath);
            string outputFullPath = Path.Combine(dir, nameNoExt + ".ogg");

            int q = Mathf.Clamp(vorbisQuality0To10, 0, 10);

            // -y 覆盖；-loglevel error 减少噪音；libvorbis 为常见 OGG Vorbis
            string args = $"-y -hide_banner -loglevel error -i \"{inputFullPath}\" -vn -c:a libvorbis -q:a {q} \"{outputFullPath}\"";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                using (var p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        errorLog = "无法启动 FFmpeg 进程。";
                        return false;
                    }

                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit(120000);

                    if (p.ExitCode != 0)
                    {
                        errorLog = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                        if (string.IsNullOrEmpty(errorLog))
                            errorLog = "FFmpeg 退出码：" + p.ExitCode;
                        return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                errorLog = e.Message;
                return false;
            }
        }

        /// <summary>根据 Project 中选中的资源（文件或文件夹）批量转换。</summary>
        public static int ConvertSelection(bool deleteSourceIfSuccess, out List<string> messages)
        {
            messages = new List<string>();
            if (!TryGetFfmpegExecutable(out string ffmpegExe, out string ffMsg))
            {
                messages.Add(ffMsg);
                return 0;
            }

            int vq = GetVorbisQualityPref();
            int ok = 0;

            foreach (string guid in Selection.assetGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath)) continue;

                foreach (string rel in EnumerateAudioAssetPaths(assetPath))
                {
                    string full = AssetPathToFullPath(rel);
                    if (!File.Exists(full)) continue;

                    if (!IsSupportedAudioExtension(Path.GetExtension(full)))
                        continue;

                    // 已是目标且用户未要求重编码：可跳过；若需「修复」可仍转一遍
                    if (ConvertFile(full, ffmpegExe, vq, out string err))
                    {
                        ok++;
                        string oggAsset = Path.ChangeExtension(rel, ".ogg");
                        messages.Add($"[OK] {rel} → {oggAsset}");
                        if (deleteSourceIfSuccess)
                        {
                            AssetDatabase.DeleteAsset(rel);
                            messages.Add($"     已删除源：{rel}");
                        }
                        AssetDatabase.ImportAsset(oggAsset, ImportAssetOptions.ForceUpdate);
                    }
                    else
                    {
                        messages.Add($"[失败] {rel}\n{err}");
                    }
                }
            }

            AssetDatabase.Refresh();
            return ok;
        }

        private static IEnumerable<string> EnumerateAudioAssetPaths(string assetPath)
        {
            string full = AssetPathToFullPath(assetPath);
            if (File.Exists(full))
            {
                if (Path.GetExtension(full).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
                    yield break;
                yield return assetPath;
                yield break;
            }

            if (!Directory.Exists(full)) yield break;

            foreach (string file in Directory.GetFiles(full, "*.*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string ext = Path.GetExtension(file);
                if (!IsSupportedAudioExtension(ext)) continue;
                if (ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)) continue;

                if (!file.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                string tail = file.Substring(Application.dataPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string rel = "Assets/" + tail.Replace('\\', '/');
                yield return rel;
            }
        }
    }

    /// <summary>菜单与设置窗口。</summary>
    public class AudioToOggConverterWindow : EditorWindow
    {
        private string _ffmpegPath = "";
        private int _vorbisQ = 5;
        private bool _deleteSource;
        private Vector2 _scroll;
        private string _lastTestMessage = "";

        [MenuItem("Tools/DemoFrameWork/Audio/Convert To OGG (FFmpeg)...")]
        public static void Open()
        {
            var w = GetWindow<AudioToOggConverterWindow>(true, "Audio → OGG", true);
            w.minSize = new Vector2(420, 320);
            w.Show();
        }

        private void OnEnable()
        {
            _ffmpegPath = AudioToOggConverter.GetFfmpegPathPref();
            _vorbisQ = AudioToOggConverter.GetVorbisQualityPref();
            _deleteSource = AudioToOggConverter.GetDeleteSourcePref();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "将选中的音频文件（或文件夹内全部支持的音频）转为 OGG (Vorbis)，用于解决部分 WAV 无法被 Unity 导入的问题。\n" +
                "依赖本机安装 FFmpeg。",
                MessageType.Info);

            EditorGUILayout.LabelField("FFmpeg 可执行文件", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _ffmpegPath = EditorGUILayout.TextField(_ffmpegPath);
            if (GUILayout.Button("浏览…", GUILayout.Width(72)))
            {
                string p = EditorUtility.OpenFilePanel("选择 ffmpeg", "", "exe");
                if (!string.IsNullOrEmpty(p)) _ffmpegPath = p;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("留空则使用系统 PATH 中的「ffmpeg」。Windows 可填如 C:\\\\ffmpeg\\\\bin\\\\ffmpeg.exe", MessageType.None);

            _vorbisQ = EditorGUILayout.IntSlider("Vorbis 质量 (0–10)", _vorbisQ, 0, 10);
            _deleteSource = EditorGUILayout.ToggleLeft("转换成功后删除源音频（慎用）", _deleteSource);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存设置"))
            {
                AudioToOggConverter.SetFfmpegPathPref(_ffmpegPath);
                AudioToOggConverter.SetVorbisQualityPref(_vorbisQ);
                AudioToOggConverter.SetDeleteSourcePref(_deleteSource);
                EditorUtility.DisplayDialog("Audio → OGG", "已保存。", "确定");
            }
            if (GUILayout.Button("测试 FFmpeg"))
            {
                AudioToOggConverter.SetFfmpegPathPref(_ffmpegPath);
                if (AudioToOggConverter.TryGetFfmpegExecutable(out _, out _lastTestMessage))
                    _lastTestMessage = "FFmpeg 可用。\n" + _lastTestMessage;
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_lastTestMessage))
            {
                EditorGUILayout.Space(4);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(100));
                EditorGUILayout.TextArea(_lastTestMessage, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(8);
            GUI.enabled = Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0;
            if (GUILayout.Button("转换当前 Project 选中项", GUILayout.Height(32)))
            {
                AudioToOggConverter.SetFfmpegPathPref(_ffmpegPath);
                AudioToOggConverter.SetVorbisQualityPref(_vorbisQ);
                AudioToOggConverter.SetDeleteSourcePref(_deleteSource);

                int n = AudioToOggConverter.ConvertSelection(_deleteSource, out var msgs);
                var sb = new StringBuilder();
                foreach (var m in msgs)
                    sb.AppendLine(m);
                EditorUtility.DisplayDialog("Audio → OGG", $"完成。成功：{n} 个。\n\n" + sb, "确定");
            }
            GUI.enabled = true;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("也可在 Project 中右键音频 → Convert To OGG (FFmpeg)。", MessageType.None);
        }
    }

    /// <summary>Project 右键。</summary>
    public static class AudioToOggConverterMenu
    {
        [MenuItem("Assets/Audio/Convert To OGG (FFmpeg)", false, 1300)]
        private static void ConvertFromMenu()
        {
            int n = AudioToOggConverter.ConvertSelection(AudioToOggConverter.GetDeleteSourcePref(), out var msgs);
            var sb = new StringBuilder();
            foreach (var m in msgs)
                sb.AppendLine(m);
            EditorUtility.DisplayDialog("Audio → OGG", $"成功：{n} 个。\n\n" + sb, "确定");
        }

        [MenuItem("Assets/Audio/Convert To OGG (FFmpeg)", true)]
        private static bool ValidateConvertFromMenu()
        {
            foreach (string guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (Directory.Exists(AudioToOggConverter.AssetPathToFullPath(path)))
                    return true;
                if (!File.Exists(AudioToOggConverter.AssetPathToFullPath(path))) continue;
                string ext = Path.GetExtension(path);
                if (ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)) continue;
                if (AudioToOggConverter.IsSupportedAudioExtension(ext))
                    return true;
            }
            return false;
        }
    }
}
