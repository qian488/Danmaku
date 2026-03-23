using System;
using System.Collections.Generic;
using UnityEngine;
using DemoFrameWork.GameFrameworkLog;

namespace DemoFrameWork.Config
{
    /// <summary>
    /// 全局配置与版本/日志入口，通过 DemoGameEntry.Config 访问。
    /// </summary>
    public class GameConfig : BaseManager<GameConfig>
    {
        private GameConfigAsset _asset;
        private ILogHelper _logHelper;
        private Dictionary<string, string> _kv;

        public string Version => _asset != null ? _asset.gameVersion : Application.version;
        public string Channel => _asset != null ? _asset.channel : "dev";
        public bool IsDev => Channel == "dev";

        public void Initialize(GameConfigAsset asset, ILogHelper logHelper = null)
        {
            _asset = asset;
            _logHelper = logHelper;
            _kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (_asset?.entries != null)
            {
                foreach (var e in _asset.entries)
                {
                    if (!string.IsNullOrEmpty(e.key))
                        _kv[e.key] = e.value ?? "";
                }
            }
        }

        public void SetLogHelper(ILogHelper helper)
        {
            _logHelper = helper;
        }

        public string Get(string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            return _kv != null && _kv.TryGetValue(key, out var v) ? v : defaultValue;
        }

        public T Get<T>(string key, T defaultValue = default) where T : IConvertible
        {
            var s = Get(key, null);
            if (s == null) return defaultValue;
            try
            {
                return (T)Convert.ChangeType(s, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public bool GetFlag(string key, bool defaultValue = false)
        {
            var s = Get(key, null);
            if (s == null) return defaultValue;
            return s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1";
        }

        public void Log(GameFrameworkLogLevel level, object message)
        {
            if (_logHelper != null)
                _logHelper.Log(level, message);
            else
            {
                switch (level)
                {
                    case GameFrameworkLogLevel.Warning:
                        Debug.LogWarning(message);
                        break;
                    case GameFrameworkLogLevel.Error:
                    case GameFrameworkLogLevel.Fatal:
                        Debug.LogError(message);
                        break;
                    default:
                        Debug.Log(message);
                        break;
                }
            }
        }

        /// <summary>
        /// 运行时覆盖（P2 扩展，初期留空实现）。
        /// </summary>
        public void LoadRuntimeOverride(string json) { }
    }
}
