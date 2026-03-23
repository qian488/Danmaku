// DemoFrameWork 核心类型：供 Runtime 辅助器实现的接口与异常

using System;

namespace DemoFrameWork
{
    /// <summary>框架异常。</summary>
    public class GameFrameworkException : Exception
    {
        public GameFrameworkException() { }
        public GameFrameworkException(string message) : base(message) { }
        public GameFrameworkException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>日志。</summary>
    public static class Log
    {
        public static void Warning(string format, params object[] args)
        {
#if UNITY_5_3_OR_NEWER
            UnityEngine.Debug.LogWarningFormat(format, args);
#endif
        }
    }

    /// <summary>参数校验。</summary>
    public static class GameFrameworkGuard
    {
        public static void NotNull(object obj, string name)
        {
            if (obj == null)
                throw new GameFrameworkException(name + " is null.");
        }
    }

    namespace Utility
    {
        /// <summary>程序集类型解析。</summary>
        public static class Assembly
        {
            public static Type GetType(string typeName)
            {
                if (string.IsNullOrEmpty(typeName)) return null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType(typeName);
                    if (t != null) return t;
                }
                return null;
            }
        }
        namespace Json
        {
            public interface IJsonHelper
            {
                string ToJson(object obj);
                T ToObject<T>(string json);
                object ToObject(Type objectType, string json);
            }
        }
        namespace Compression
        {
            public interface ICompressionHelper
            {
                bool Compress(byte[] bytes, int offset, int length, System.IO.Stream compressedStream);
                bool Decompress(byte[] bytes, int offset, int length, System.IO.Stream decompressedStream);
            }
        }
        namespace Text
        {
            public interface ITextHelper
            {
                string Format(string format, params object[] args);
            }
        }
    }

    namespace GameFrameworkLog
    {
        public enum GameFrameworkLogLevel
        {
            Debug,
            Info,
            Warning,
            Error,
            Fatal
        }
        public interface ILogHelper
        {
            void Log(GameFrameworkLogLevel level, object message);
        }
    }

    namespace Version
    {
        public interface IVersionHelper
        {
            string GameVersion { get; }
        }
    }
}
