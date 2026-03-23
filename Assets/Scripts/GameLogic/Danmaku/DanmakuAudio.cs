using System.Collections.Generic;
using UnityEngine;
using DemoFrameWork;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 弹幕 Demo 音频 Resources 路径（不含扩展名）与便捷播放；依赖 <see cref="DemoGameEntry.Audio"/>。
    /// </summary>
    public static class DanmakuAudio
    {
        public static class Paths
        {
            public const string BgmGame = "Demos/Danmaku/Audio/BGM/game";
            public const string BgmHome = "Demos/Danmaku/Audio/BGM/home";

            /// <summary>敌机射击（固定「弹幕嗡声」）。</summary>
            public const string SfxEnemyShoot = "Demos/Danmaku/Audio/SFX/弹幕嗡声";

            /// <summary>自机射击。</summary>
            public const string SfxPlayerShoot = "Demos/Danmaku/Audio/SFX/擦弹";

            /// <summary>擦弹成功（<c>Resources/Demos/Danmaku/Audio/SFX/灵击.wav</c>）。</summary>
            public const string SfxGraze = "Demos/Danmaku/Audio/SFX/灵击";

            public const string SfxHitEnemy = "Demos/Danmaku/Audio/SFX/命中";
            public const string SfxEnemyExplode = "Demos/Danmaku/Audio/SFX/炸裂";
            public const string SfxBossBreak = "Demos/Danmaku/Audio/SFX/击破boss";

            public const string SfxPowerUp0 = "Demos/Danmaku/Audio/SFX/up";
            public const string SfxPowerUp1 = "Demos/Danmaku/Audio/SFX/up1";
            public const string SfxPowerUp2 = "Demos/Danmaku/Audio/SFX/up2";

            public const string SfxStageItemDrop = "Demos/Danmaku/Audio/SFX/道中碎片掉落";

            public const string SfxPauseMenu = "Demos/Danmaku/Audio/SFX/暂停菜单";
            public const string SfxUiSelect = "Demos/Danmaku/Audio/SFX/选择";
            public const string SfxUiConfirm = "Demos/Danmaku/Audio/SFX/确定";
            public const string SfxUiCancel = "Demos/Danmaku/Audio/SFX/取消";
        }

        private static readonly string[] PowerUpClips =
        {
            Paths.SfxPowerUp0,
            Paths.SfxPowerUp1,
            Paths.SfxPowerUp2,
        };

        /// <summary>SpawnBulletWave 用：文件名含「弹幕」且不含敌机射击专用「弹幕嗡声」的音效随机。</summary>
        private static readonly string[] SpawnBulletWaveSfxPaths =
        {
            "Demos/Danmaku/Audio/SFX/弹幕展开kira",
            "Demos/Danmaku/Audio/SFX/弹幕蓄力2",
            "Demos/Danmaku/Audio/SFX/弹幕展开woooo",
            "Demos/Danmaku/Audio/SFX/弹幕展开tan",
            "Demos/Danmaku/Audio/SFX/弹幕蓄力",
            "Demos/Danmaku/Audio/SFX/弹幕展开tan2",
            "Demos/Danmaku/Audio/SFX/弹幕展开tan3",
            "Demos/Danmaku/Audio/SFX/激光弹幕",
        };

        /// <summary>
        /// 关内可能用到的全部 Resources 音频路径（与 <see cref="Paths"/>、弹幕波 SFX 列表一致），供 Loading 协程分帧预热。
        /// </summary>
        public static IEnumerable<string> EnumerateCombatAudioResourcePaths()
        {
            var set = new HashSet<string>(System.StringComparer.Ordinal);
            void Add(string p)
            {
                if (!string.IsNullOrEmpty(p)) set.Add(p.Trim().Replace('\\', '/'));
            }

            Add(Paths.BgmGame);
            Add(Paths.SfxEnemyShoot);
            Add(Paths.SfxPlayerShoot);
            Add(Paths.SfxGraze);
            Add(Paths.SfxHitEnemy);
            Add(Paths.SfxEnemyExplode);
            Add(Paths.SfxBossBreak);
            Add(Paths.SfxPowerUp0);
            Add(Paths.SfxPowerUp1);
            Add(Paths.SfxPowerUp2);
            Add(Paths.SfxStageItemDrop);
            Add(Paths.SfxPauseMenu);
            Add(Paths.SfxUiSelect);
            Add(Paths.SfxUiConfirm);
            Add(Paths.SfxUiCancel);
            if (SpawnBulletWaveSfxPaths != null)
            {
                for (int i = 0; i < SpawnBulletWaveSfxPaths.Length; i++)
                    Add(SpawnBulletWaveSfxPaths[i]);
            }

            foreach (var s in set)
                yield return s;
        }

        /// <param name="randomStartTime">进入关卡时通常为 true；与 <see cref="AudioManager.PlayBGMFromResources"/> 一致。</param>
        public static void PlayGameBgm(float fadeSeconds = 0.5f, bool randomStartTime = true)
        {
            DemoGameEntry.Audio?.PlayBGMFromResources(Paths.BgmGame, fadeSeconds, randomStartTime);
        }

        public static void PlayHomeBgm(float fadeSeconds = 0.5f)
        {
            DemoGameEntry.Audio?.PlayBGMFromResources(Paths.BgmHome, fadeSeconds);
        }

        /// <param name="fadeSeconds">淡出、淡入各持续的秒数；0 为立即跳转。</param>
        public static void SeekHomeBgmRandomTime(float fadeSeconds = 0.35f)
        {
            DemoGameEntry.Audio?.SeekBGMRandomTime(fadeSeconds);
        }

        /// <summary>敌机每轮射击（<see cref="DanmakuEnemy.Shoot"/>）。</summary>
        public static void PlayEnemyShootSfx()
        {
            DemoGameEntry.Audio?.PlaySFXFromResources(Paths.SfxEnemyShoot);
        }

        /// <summary>关卡任务 SpawnBulletWave：从含「弹幕」的音效中随机（不含敌机专用「弹幕嗡声」）。</summary>
        public static void PlaySpawnBulletWaveSfxRandom()
        {
            if (SpawnBulletWaveSfxPaths == null || SpawnBulletWaveSfxPaths.Length == 0) return;
            string p = SpawnBulletWaveSfxPaths[Random.Range(0, SpawnBulletWaveSfxPaths.Length)];
            DemoGameEntry.Audio?.PlaySFXFromResources(p);
        }

        public static void PlayPlayerShootSfx()
        {
            DemoGameEntry.Audio?.PlaySFXFromResources(Paths.SfxPlayerShoot);
        }

        /// <summary>擦弹成功。</summary>
        public static void PlayGrazeSfx()
        {
            DemoGameEntry.Audio?.PlaySFXFromResources(Paths.SfxGraze);
        }

        public static void PlayHitEnemySfx()
        {
            DemoGameEntry.Audio?.PlaySFXFromResources(Paths.SfxHitEnemy);
        }

        public static void PlayEnemyDeathSfx(bool isBoss)
        {
            DemoGameEntry.Audio?.PlaySFXFromResources(isBoss ? Paths.SfxBossBreak : Paths.SfxEnemyExplode);
        }

        public static void PlayPowerUpSfxRandom()
        {
            string p = PowerUpClips[Random.Range(0, PowerUpClips.Length)];
            DemoGameEntry.Audio?.PlaySFXFromResources(p);
        }

        /// <summary>道中掉落物进栏等可调用。</summary>
        public static void PlayStageItemDropSfx()
        {
            DemoGameEntry.Audio?.PlaySFXFromResources(Paths.SfxStageItemDrop);
        }

        public static void PlayPauseMenuSfx()
        {
            DemoGameEntry.Audio?.PlaySFXFromResources(Paths.SfxPauseMenu);
        }

        public static void PlayUiSelectSfx()
        {
            DemoGameEntry.Audio?.PlaySFXFromResources(Paths.SfxUiSelect);
        }

        public static void PlayUiConfirmSfx()
        {
            DemoGameEntry.Audio?.PlaySFXFromResources(Paths.SfxUiConfirm);
        }

        public static void PlayUiCancelSfx()
        {
            DemoGameEntry.Audio?.PlaySFXFromResources(Paths.SfxUiCancel);
        }
    }
}
