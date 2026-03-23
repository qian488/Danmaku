using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 运行时探测 Resources 下 <c>Demos/Danmaku/Img/bullet/{样式}/{变体}</c> 是否存在；
    /// 各样式子目录内变体数量不一致（有的到 9、有的只有若干张）。
    /// <para>
    /// 主界面清单（如 <c>HomePreloadList.txt</c>）逐项 <c>LoadAsync</c> 后，由 <see cref="TryRegisterBulletSpriteFromManifestPath"/> 把贴图登记到 (样式, 变体) 字典；
    /// <see cref="Warmup"/> 仅根据字典重建样式/变体索引，未走清单时则按编号探测（<see cref="TryGetSprite"/>）。
    /// </para>
    /// <para>若已在主界面调用 <see cref="WarmupIfNeeded"/> 且区间覆盖本局设置，关卡内会跳过重复 Warmup。</para>
    /// </summary>
    public static class DanmakuBulletResourceIndex
    {
        public const string BasePath = "Demos/Danmaku/Img/bullet";

        private static int[] _cachedFoldersInRange;
        private static int _cachedLo = int.MinValue;
        private static int _cachedHi = int.MinValue;

        private static readonly Dictionary<int, int[]> VariantCache = new Dictionary<int, int[]>();

        /// <summary>键：(样式目录编号, 变体编号)。</summary>
        private static Dictionary<(int style, int variant), Sprite> _spriteByStyleVariant;

        /// <summary>
        /// 根据已登记的字典重建 [styleMin, styleMax] 内的样式/变体索引；该区间内若某样式字典中尚无贴图，则按编号探测（<see cref="TryGetSprite"/>）。
        /// 不清空已有登记（与主界面 <c>LoadAsync</c> + <see cref="TryRegisterBulletSpriteFromManifestPath"/> 兼容）。
        /// </summary>
        public static void Warmup(int styleMin, int styleMax)
        {
            int lo = Mathf.Min(styleMin, styleMax);
            int hi = Mathf.Max(styleMin, styleMax);
            if (_spriteByStyleVariant == null)
                _spriteByStyleVariant = new Dictionary<(int, int), Sprite>(256);

            VariantCache.Clear();
            var folderList = new List<int>();

            for (int i = lo; i <= hi; i++)
            {
                if (HasAnySpriteForStyle(i))
                {
                    folderList.Add(i);
                    continue;
                }

                if (ProbeStyleFolderIntoCache(i))
                    folderList.Add(i);
            }

            _cachedFoldersInRange = folderList.Count > 0 ? folderList.ToArray() : new[] { 0 };
            _cachedLo = lo;
            _cachedHi = hi;

            foreach (var folder in _cachedFoldersInRange)
                EnsureVariantCacheForStyle(folder);
        }

        /// <summary>
        /// 主界面清单每行 <c>LoadAsync</c> 完成后调用：路径形如 <c>Demos/Danmaku/Img/bullet/{样式}/{变体}</c> 时写入字典。
        /// </summary>
        public static void TryRegisterBulletSpriteFromManifestPath(string resourcesPath, UnityEngine.Object asset)
        {
            if (string.IsNullOrEmpty(resourcesPath) || asset == null) return;

            resourcesPath = resourcesPath.Trim().Replace('\\', '/');
            if (!resourcesPath.StartsWith(BasePath, StringComparison.Ordinal))
                return;

            string rest = resourcesPath.Substring(BasePath.Length).TrimStart('/');
            string[] parts = rest.Split('/');
            if (parts.Length != 2) return;
            if (!int.TryParse(parts[0], out int styleFolder)) return;
            if (!int.TryParse(parts[1], out int variantIndex)) return;

            var sprite = DanmakuSpriteUtil.SpriteFromLoadedObject(asset);
            if (sprite == null) return;

            if (_spriteByStyleVariant == null)
                _spriteByStyleVariant = new Dictionary<(int, int), Sprite>(256);
            _spriteByStyleVariant[(styleFolder, variantIndex)] = sprite;
        }

        /// <summary>
        /// 若已有缓存且覆盖 [styleMin, styleMax]（超集即可），则不再清空重建；否则调用 <see cref="Warmup"/>。
        /// </summary>
        public static void WarmupIfNeeded(int styleMin, int styleMax)
        {
            int lo = Mathf.Min(styleMin, styleMax);
            int hi = Mathf.Max(styleMin, styleMax);
            if (_spriteByStyleVariant != null && _spriteByStyleVariant.Count > 0 &&
                _cachedLo != int.MinValue && _cachedHi != int.MinValue &&
                _cachedLo <= lo && _cachedHi >= hi)
                return;

            Warmup(styleMin, styleMax);
        }

        /// <summary>
        /// 无清单预载时：与 <see cref="WarmupIfNeeded"/> 等效，但在探测变体时每若干次 <c>yield</c> 一帧，减轻单帧同步 IO。
        /// </summary>
        /// <param name="variantsProbeYieldInterval">每探测该数量的变体后让出一帧（建议 4～16）。</param>
        public static IEnumerator CoWarmupIfNeededAsync(int styleMin, int styleMax,
            int variantsProbeYieldInterval = 8)
        {
            int lo = Mathf.Min(styleMin, styleMax);
            int hi = Mathf.Max(styleMin, styleMax);
            if (_spriteByStyleVariant != null && _spriteByStyleVariant.Count > 0 &&
                _cachedLo != int.MinValue && _cachedHi != int.MinValue &&
                _cachedLo <= lo && _cachedHi >= hi)
                yield break;

            if (_spriteByStyleVariant == null)
                _spriteByStyleVariant = new Dictionary<(int, int), Sprite>(256);

            VariantCache.Clear();
            var folderList = new List<int>();
            int interval = Mathf.Max(1, variantsProbeYieldInterval);
            const int probeMax = 32;

            for (int i = lo; i <= hi; i++)
            {
                if (HasAnySpriteForStyle(i))
                {
                    folderList.Add(i);
                    continue;
                }

                bool any = false;
                for (int v = 0; v < probeMax; v++)
                {
                    if (TryGetSprite(i, v) != null)
                        any = true;
                    if ((v + 1) % interval == 0)
                        yield return null;
                }

                if (any)
                    folderList.Add(i);

                yield return null;
            }

            _cachedFoldersInRange = folderList.Count > 0 ? folderList.ToArray() : new[] { 0 };
            _cachedLo = lo;
            _cachedHi = hi;

            foreach (var folder in _cachedFoldersInRange)
                EnsureVariantCacheForStyle(folder);
        }

        /// <summary>取已缓存的贴图；未 Warmup 或未命中时会懒加载一次并写入缓存。</summary>
        public static Sprite TryGetSprite(int styleFolder, int variantIndex)
        {
            if (_spriteByStyleVariant != null &&
                _spriteByStyleVariant.TryGetValue((styleFolder, variantIndex), out var cached))
                return cached;

            var sprite = DanmakuSpriteUtil.TryLoadSprite($"{BasePath}/{styleFolder}/{variantIndex}");
            if (sprite == null) return null;

            if (_spriteByStyleVariant == null)
                _spriteByStyleVariant = new Dictionary<(int, int), Sprite>(256);
            _spriteByStyleVariant[(styleFolder, variantIndex)] = sprite;
            EnsureVariantCacheForStyle(styleFolder);
            return sprite;
        }

        public static void ClearCache()
        {
            _cachedFoldersInRange = null;
            _cachedLo = int.MinValue;
            _cachedHi = int.MinValue;
            VariantCache.Clear();
            _spriteByStyleVariant?.Clear();
        }

        /// <summary>在 [lo, hi] 内探测存在 <c>{i}/0</c> 的样式编号（用于随机类型）。</summary>
        public static int[] GetStyleFoldersInRange(int lo, int hi)
        {
            lo = Mathf.Min(lo, hi);
            hi = Mathf.Max(lo, hi);

            if (_cachedFoldersInRange != null && _cachedLo <= lo && _cachedHi >= hi)
            {
                var filtered = new List<int>();
                for (int i = 0; i < _cachedFoldersInRange.Length; i++)
                {
                    int f = _cachedFoldersInRange[i];
                    if (f >= lo && f <= hi)
                        filtered.Add(f);
                }

                return filtered.Count > 0 ? filtered.ToArray() : new[] { 0 };
            }

            if (_cachedFoldersInRange != null && _cachedLo == lo && _cachedHi == hi)
                return _cachedFoldersInRange;

            var list = new List<int>();
            for (int i = lo; i <= hi; i++)
            {
                if (TryGetSprite(i, 0) != null)
                    list.Add(i);
            }

            _cachedFoldersInRange = list.Count > 0 ? list.ToArray() : new[] { 0 };
            _cachedLo = lo;
            _cachedHi = hi;
            return _cachedFoldersInRange;
        }

        /// <summary>某样式目录下存在的变体文件名编号（0、1、2…），至少含 0。</summary>
        public static int[] GetVariantIndicesForStyle(int styleFolder)
        {
            if (VariantCache.TryGetValue(styleFolder, out var cached))
                return cached;

            const int probeMax = 32;
            var list = new List<int>();
            for (int v = 0; v < probeMax; v++)
            {
                if (TryGetSprite(styleFolder, v) != null)
                    list.Add(v);
            }

            cached = list.Count > 0 ? list.ToArray() : new[] { 0 };
            VariantCache[styleFolder] = cached;
            return cached;
        }

        /// <summary>若请求的变体不存在则回退到该样式第一个可用变体（通常 0）。</summary>
        public static int ClampVariantToExisting(int styleFolder, int variant)
        {
            var variants = GetVariantIndicesForStyle(styleFolder);
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] == variant)
                    return variant;
            }

            return variants[0];
        }

        private static bool HasAnySpriteForStyle(int styleFolder)
        {
            if (_spriteByStyleVariant == null) return false;
            foreach (var kv in _spriteByStyleVariant)
            {
                if (kv.Key.style == styleFolder)
                    return true;
            }

            return false;
        }

        private static bool ProbeStyleFolderIntoCache(int styleFolder)
        {
            bool any = false;
            const int probeMax = 32;
            for (int v = 0; v < probeMax; v++)
            {
                if (TryGetSprite(styleFolder, v) != null)
                    any = true;
            }

            return any;
        }

        private static void EnsureVariantCacheForStyle(int styleFolder)
        {
            if (_spriteByStyleVariant == null) return;

            var list = new List<int>();
            foreach (var kv in _spriteByStyleVariant)
            {
                if (kv.Key.style == styleFolder)
                    list.Add(kv.Key.variant);
            }

            if (list.Count == 0)
            {
                VariantCache[styleFolder] = new[] { 0 };
                return;
            }

            list.Sort();
            VariantCache[styleFolder] = list.ToArray();
        }
    }
}
