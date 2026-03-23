using UnityEngine;

namespace DemoFrameWork.Audio
{
    /// <summary>
    /// 音频配置：BGM/SFX 路径前缀、SFX 池容量、默认音量。
    /// 创建资产：CreateAssetMenu → DemoFrameWork/Audio Settings
    /// </summary>
    [CreateAssetMenu(menuName = "DemoFrameWork/Audio Settings", fileName = "AudioSettings")]
    public class AudioSettings : ScriptableObject
    {
        [Header("资源路径（Resources 下相对路径）")]
        [Tooltip("BGM 路径前缀，如 Audio/BGM/")]
        public string bgmPathPrefix = "Audio/BGM/";

        [Tooltip("SFX 路径前缀，如 Audio/SFX/")]
        public string sfxPathPrefix = "Audio/SFX/";

        [Header("SFX 池")]
        [Tooltip("SFX 同时播放用的 AudioSource 数量（Round-Robin）")]
        [Min(1)]
        public int sfxPoolSize = 8;

        [Header("默认音量 0~1")]
        [Range(0f, 1f)]
        public float defaultBGMVolume = 0.8f;

        [Range(0f, 1f)]
        public float defaultSFXVolume = 0.5f;
    }
}
