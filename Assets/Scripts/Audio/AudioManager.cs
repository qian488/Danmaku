using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DemoFrameWork.Audio
{
    /// <summary>
    /// 音频管理：BGM 单轨 + SFX 多轨池，通过 DemoGameEntry.Audio 访问。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private const string PrefsBGMVolume = "audio_bgm_vol";
        private const string PrefsSFXVolume = "audio_sfx_vol";

        private AudioSource _bgmSource;
        private AudioSource[] _sfxSources;
        private int _sfxPoolIndex;
        private Coroutine _seekBgmRandomCoroutine;
        private bool _initialized;
        private float _masterVolume = 1f;
        private bool _muteAll;

        private string _bgmPathPrefix = "Audio/BGM/";
        private string _sfxPathPrefix = "Audio/SFX/";
        private int _sfxPoolSize = 8;
        private float _defaultBGMVol = 0.8f;
        private float _defaultSFXVol = 0.5f;

        private readonly Dictionary<string, AudioClip> _audioClipByResourcePath = new Dictionary<string, AudioClip>(64);

        // BGM
        /// <summary>
        /// 使用 Resources 完整路径（不含扩展名）播放 BGM，例如 Demos/Danmaku/Audio/BGM/game
        /// </summary>
        /// <param name="randomStartTime">为 true 时在曲长范围内随机起始位置（适合每次进入关卡）；为 false 则从开头播放。</param>
        public void PlayBGMFromResources(string resourcePath, float fadeTime = 0f, bool randomStartTime = false)
        {
            if (!_initialized || string.IsNullOrEmpty(resourcePath)) return;
            var clip = GetOrLoadAudioClip(resourcePath);
            if (clip == null) return;
            PlayBGMClipInternal(clip, fadeTime, randomStartTime);
        }

        /// <summary>
        /// 使用 Resources 完整路径（不含扩展名）播放 SFX。
        /// </summary>
        public void PlaySFXFromResources(string resourcePath)
        {
            if (!_initialized || string.IsNullOrEmpty(resourcePath)) return;
            var clip = GetOrLoadAudioClip(resourcePath);
            if (clip == null) return;
            PlaySFX(clip);
        }

        /// <summary>
        /// 将当前 BGM 跳转到随机时间点（用于主菜单切角等）。
        /// </summary>
        /// <param name="fadeTime">淡出与淡入各持续的秒数；≤0 时立即跳转（无淡变）。</param>
        public void SeekBGMRandomTime(float fadeTime = 0.35f)
        {
            if (!_initialized || _bgmSource == null || _bgmSource.clip == null) return;
            if (!_bgmSource.isPlaying) return;
            float len = _bgmSource.clip.length;
            if (len <= 0.15f) return;

            if (_seekBgmRandomCoroutine != null)
            {
                StopCoroutine(_seekBgmRandomCoroutine);
                _seekBgmRandomCoroutine = null;
            }

            if (fadeTime <= 0f)
            {
                _bgmSource.time = UnityEngine.Random.Range(0f, len);
                return;
            }

            _seekBgmRandomCoroutine = StartCoroutine(FadeSeekBGMRandomCoroutine(fadeTime));
        }

        /// <param name="randomStartTime">为 true 时在曲长范围内随机起始位置；为 false 则从开头播放。</param>
        public void PlayBGM(string name, float fadeTime = 0f, bool randomStartTime = false)
        {
            if (!_initialized) return;
            var clip = LoadClip(_bgmPathPrefix + name);
            if (clip == null) return;
            PlayBGMClipInternal(clip, fadeTime, randomStartTime);
        }

        private void PlayBGMClipInternal(AudioClip clip, float fadeTime, bool randomStartTime = false)
        {
            if (clip == null) return;
            if (fadeTime > 0f && _bgmSource.isPlaying)
            {
                StartCoroutine(CrossFadeBGM(clip, fadeTime, randomStartTime));
                return;
            }
            _bgmSource.clip = clip;
            _bgmSource.loop = true;
            ApplyBgmStartTime(clip, randomStartTime);
            if (fadeTime > 0f)
            {
                _bgmSource.volume = 0f;
                _bgmSource.Play();
                StartCoroutine(FadeBGMVolume(GetBGMVolumeStored(), fadeTime));
            }
            else
            {
                _bgmSource.volume = GetBGMVolumeStored() * _masterVolume;
                _bgmSource.Play();
            }
        }

        /// <summary>在 <see cref="Play"/> 前设置 <see cref="AudioSource.time"/>。</summary>
        private void ApplyBgmStartTime(AudioClip clip, bool randomStartTime)
        {
            if (clip == null) return;
            if (randomStartTime && clip.length > 0.15f)
                _bgmSource.time = UnityEngine.Random.Range(0f, clip.length);
            else
                _bgmSource.time = 0f;
        }

        public void StopBGM(float fadeTime = 0f)
        {
            if (!_initialized) return;
            if (fadeTime > 0f && _bgmSource.isPlaying)
            {
                StartCoroutine(FadeOutAndStopBGM(fadeTime));
                return;
            }
            _bgmSource.Stop();
            _bgmSource.clip = null;
        }

        public void SetBGMVolume(float volume)
        {
            if (!_initialized) return;
            float v = Mathf.Clamp01(volume);
            if (!_bgmSource.mute)
                _bgmSource.volume = v * _masterVolume;
            PlayerPrefs.SetFloat(PrefsBGMVolume, v);
            PlayerPrefs.Save();
        }

        public void PauseBGM()
        {
            if (!_initialized) return;
            _bgmSource.Pause();
        }

        public void ResumeBGM()
        {
            if (!_initialized) return;
            _bgmSource.UnPause();
        }

        public void MuteBGM(bool mute)
        {
            if (!_initialized) return;
            _bgmSource.mute = mute;
        }

        // SFX
        public void PlaySFX(string name)
        {
            if (!_initialized) return;
            var clip = LoadClip(_sfxPathPrefix + name);
            if (clip != null)
                PlaySFX(clip);
        }

        public void PlaySFX(AudioClip clip)
        {
            if (!_initialized || clip == null || _sfxSources == null) return;
            var source = _sfxSources[_sfxPoolIndex];
            _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxSources.Length;
            source.clip = clip;
            source.volume = (_muteAll ? 0f : GetSFXVolumeStored() * _masterVolume);
            source.mute = _muteAll;
            source.PlayOneShot(clip);
        }

        public void SetSFXVolume(float volume)
        {
            if (!_initialized) return;
            float v = Mathf.Clamp01(volume);
            if (_sfxSources != null)
            {
                foreach (var s in _sfxSources)
                    s.volume = s.mute ? s.volume : v * _masterVolume;
            }
            PlayerPrefs.SetFloat(PrefsSFXVolume, v);
            PlayerPrefs.Save();
        }

        public void MuteSFX(bool mute)
        {
            if (!_initialized) return;
            if (_sfxSources != null)
            {
                foreach (var s in _sfxSources)
                    s.mute = mute;
            }
        }

        // 全局
        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            if (!_initialized) return;
            _bgmSource.volume = _bgmSource.mute ? _bgmSource.volume : GetBGMVolumeStored() * _masterVolume;
            if (_sfxSources != null)
            {
                foreach (var s in _sfxSources)
                    s.volume = s.mute ? s.volume : GetSFXVolumeStored() * _masterVolume;
            }
        }

        public void MuteAll(bool mute)
        {
            _muteAll = mute;
            if (!_initialized) return;
            _bgmSource.mute = mute;
            if (_sfxSources != null)
            {
                foreach (var s in _sfxSources)
                    s.mute = mute;
            }
        }

        private float GetBGMVolumeStored()
        {
            return PlayerPrefs.GetFloat(PrefsBGMVolume, _defaultBGMVol);
        }

        private float GetSFXVolumeStored()
        {
            return PlayerPrefs.GetFloat(PrefsSFXVolume, _defaultSFXVol);
        }

        private static string NormalizeAudioResourceKey(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path.Trim().Replace('\\', '/');
        }

        /// <summary>
        /// 在 Loading 等阶段调用，将 <see cref="AudioClip"/> 载入 <see cref="_audioClipByResourcePath"/>。
        /// 避免首帧播放时 Unity 音频管线同步解码（Profiler 中常显示为 <c>SoundManager.LoadFMODSound</c>）造成卡顿。
        /// </summary>
        public void PrewarmClipFromResources(string resourcePath)
        {
            if (!_initialized || string.IsNullOrEmpty(resourcePath)) return;
            GetOrLoadAudioClip(resourcePath);
        }

        /// <summary>路径归一化后字典缓存，减少重复 <see cref="Resources.Load"/>。</summary>
        private AudioClip GetOrLoadAudioClip(string resourcePath)
        {
            string key = NormalizeAudioResourceKey(resourcePath);
            if (key.Length == 0) return null;
            if (_audioClipByResourcePath.TryGetValue(key, out var cached) && cached != null)
                return cached;
            var clip = Resources.Load<AudioClip>(key);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] Clip not found: {key}");
                return null;
            }

            _audioClipByResourcePath[key] = clip;
            return clip;
        }

        private AudioClip LoadClip(string path) => GetOrLoadAudioClip(path);

        private IEnumerator FadeSeekBGMRandomCoroutine(float fadeTime)
        {
            float targetVol = GetBGMVolumeStored() * _masterVolume;
            float start = _bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(start, 0f, elapsed / fadeTime);
                yield return null;
            }

            _bgmSource.volume = 0f;
            float len = _bgmSource.clip.length;
            if (len > 0.15f)
                _bgmSource.time = UnityEngine.Random.Range(0f, len);

            elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(0f, targetVol, elapsed / fadeTime);
                yield return null;
            }

            _bgmSource.volume = targetVol;
            _seekBgmRandomCoroutine = null;
        }

        private IEnumerator CrossFadeBGM(AudioClip nextClip, float fadeTime, bool randomStartTime = false)
        {
            float start = _bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(start, 0f, elapsed / fadeTime);
                yield return null;
            }
            _bgmSource.Stop();
            _bgmSource.clip = nextClip;
            _bgmSource.loop = true;
            _bgmSource.volume = 0f;
            ApplyBgmStartTime(nextClip, randomStartTime);
            _bgmSource.Play();
            elapsed = 0f;
            float target = GetBGMVolumeStored() * _masterVolume;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(0f, target, elapsed / fadeTime);
                yield return null;
            }
            _bgmSource.volume = target;
        }

        private IEnumerator FadeBGMVolume(float target, float fadeTime)
        {
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(0f, target * _masterVolume, elapsed / fadeTime);
                yield return null;
            }
            _bgmSource.volume = target * _masterVolume;
        }

        private IEnumerator FadeOutAndStopBGM(float fadeTime)
        {
            float start = _bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(start, 0f, elapsed / fadeTime);
                yield return null;
            }
            _bgmSource.Stop();
            _bgmSource.clip = null;
            _bgmSource.volume = GetBGMVolumeStored() * _masterVolume;
        }

        public void Initialize(AudioSettings settings = null)
        {
            if (_initialized) return;

            if (settings != null)
            {
                _bgmPathPrefix = settings.bgmPathPrefix;
                _sfxPathPrefix = settings.sfxPathPrefix;
                _sfxPoolSize = settings.sfxPoolSize;
                _defaultBGMVol = settings.defaultBGMVolume;
                _defaultSFXVol = settings.defaultSFXVolume;
            }

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.volume = GetBGMVolumeStored() * _masterVolume;

            _sfxSources = new AudioSource[_sfxPoolSize];
            for (int i = 0; i < _sfxPoolSize; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.volume = GetSFXVolumeStored() * _masterVolume;
                _sfxSources[i] = s;
            }
            _sfxPoolIndex = 0;
            _initialized = true;
        }

        public static AudioManager Create(GameObject host, AudioSettings settings = null)
        {
            var am = host.AddComponent<AudioManager>();
            am.Initialize(settings);
            return am;
        }
    }
}
