using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 弹幕 Demo Resources 清单解析与异步预加载（主菜单 / 关卡共用）。
    /// </summary>
    public static class DanmakuResourcePreloadUtility
    {
        public static IEnumerable<string> EnumerateManifestLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var s = line.Trim();
                if (s.Length == 0 || s.StartsWith("#", StringComparison.Ordinal))
                    continue;
                yield return s;
            }
        }

        public static int CountValidManifestLines(string text)
        {
            int n = 0;
            foreach (var _ in EnumerateManifestLines(text))
                n++;
            return n;
        }

        /// <summary>
        /// 逐项 <see cref="Resources.LoadAsync"/>；敌弹路径登记到 <see cref="DanmakuBulletResourceIndex"/>；收尾 <see cref="DanmakuBulletResourceIndex.WarmupIfNeeded"/>。
        /// </summary>
        /// <param name="progressCap">进度回调上限（如 Loading 条 0.8）。</param>
        /// <param name="setProgress">0~1，可为 null。</param>
        public static IEnumerator CoLoadManifestAsync(string manifestText, int bulletStyleMin, int bulletStyleMax,
            float progressCap, Action<float> setProgress, bool logProgress)
        {
            int total = CountValidManifestLines(manifestText);
            if (total == 0)
            {
                DanmakuBulletResourceIndex.WarmupIfNeeded(bulletStyleMin, bulletStyleMax);
                setProgress?.Invoke(1f);
                yield break;
            }

            float cap = Mathf.Clamp01(progressCap);
            int completed = 0;
            foreach (var line in EnumerateManifestLines(manifestText))
            {
                var req = Resources.LoadAsync(line);
                while (!req.isDone)
                {
                    float raw = (completed + req.progress) / total;
                    setProgress?.Invoke(Mathf.Clamp01(raw) * cap);
                    if (logProgress)
                        Debug.Log($"[DanmakuPreload] \"{line}\" → {Mathf.Clamp01(raw) * cap:F3}");
                    yield return null;
                }

                if (req.asset == null)
                    Debug.LogWarning($"[DanmakuPreload] Resources 中未找到: {line}");
                else
                    DanmakuBulletResourceIndex.TryRegisterBulletSpriteFromManifestPath(line, req.asset);

                completed++;
                setProgress?.Invoke(Mathf.Clamp01(completed / (float)total) * cap);
                yield return null;
            }

            DanmakuBulletResourceIndex.WarmupIfNeeded(bulletStyleMin, bulletStyleMax);
            setProgress?.Invoke(1f);
        }
    }
}
