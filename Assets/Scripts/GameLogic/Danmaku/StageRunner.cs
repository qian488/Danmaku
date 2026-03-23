using UnityEngine;
using DemoFrameWork.Audio;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 关卡任务驱动器，对应参考 StageTaskRunner：
    /// 按当前游戏时间/帧遍历 StageData.Tasks，逐一执行到期任务。
    /// </summary>
    public class StageRunner
    {
        private StageData _data;
        private BulletManager _bulletManager;
        private EnemyManager _enemyManager;
        private AudioManager _audio;
        private PlayerController _player;
        private DanmakuGameplayBalance _gameplayBalance;

        private float _elapsedTime;
        private int _nextTaskIndex;
        private bool _isRunning;
        private float _bulletSpeedMultiplier = 1f;
        private bool _endlessLoop;

        /// <summary>Boss 战：关卡时间轴不前进、不派发新任务；场上敌弹/敌机与掉落逻辑由 GameController 照常驱动。</summary>
        private bool _timelinePausedForBoss;
        private DanmakuEnemy _bossTimelineBlocker;

        /// <summary>Boss 战期间 <see cref="StageData.BossArenaBulletLoop"/> 的循环发射。</summary>
        private float _bossArenaNextSpawnIn;
        private int _bossArenaLoopIndex;

        public float ElapsedTime => _elapsedTime;
        public bool IsFinished => _nextTaskIndex >= (_data?.Tasks?.Count ?? 0);

        /// <summary>关卡时间轴因 Boss 战暂停中（用于 UI 计时/时间分暂停，不影响场上战斗）。</summary>
        public bool IsTimelinePausedForBoss => _timelinePausedForBoss;

        /// <summary>关卡总时长估计：取最后一项任务的 <see cref="StageTaskEntry.TriggerTime"/>。</summary>
        public float EstimatedStageDurationSeconds
        {
            get
            {
                if (_data?.Tasks == null || _data.Tasks.Count == 0) return 1f;
                return Mathf.Max(0.01f, _data.Tasks[_data.Tasks.Count - 1].TriggerTime);
            }
        }

        /// <summary>阶段进度 0~1，供 UI 进度条使用（无尽模式循环时会回退）。</summary>
        public float GetProgress01()
        {
            if (_data?.Tasks == null || _data.Tasks.Count == 0) return 1f;
            return Mathf.Clamp01(_elapsedTime / EstimatedStageDurationSeconds);
        }

        public void Initialize(StageData data, BulletManager bulletMgr, EnemyManager enemyMgr, AudioManager audio,
            float bulletSpeedMultiplier = 1f, bool endlessLoop = false, PlayerController player = null,
            DanmakuGameplayBalance gameplayBalance = null)
        {
            _data                   = data;
            _bulletManager          = bulletMgr;
            _enemyManager           = enemyMgr;
            _audio                  = audio;
            _player                 = player;
            _gameplayBalance        = gameplayBalance;
            _bulletSpeedMultiplier  = bulletSpeedMultiplier;
            _endlessLoop            = endlessLoop;
            _elapsedTime            = 0f;
            _nextTaskIndex          = 0;
            _isRunning              = false;
            _timelinePausedForBoss  = false;
            _bossTimelineBlocker    = null;
            _bossArenaNextSpawnIn   = 0f;
            _bossArenaLoopIndex     = 0;
        }

        public void Start()
        {
            _isRunning = true;

            // 播放关卡 BGM（支持 Resources 全路径，如 Demos/Danmaku/Audio/BGM/game）
            // 仅「本局开始」随机起始；中途 ChangeBGM 仍从曲首播
            if (_audio != null && !string.IsNullOrEmpty(_data?.StageBGM))
                PlayStageBgm(_data.StageBGM, 0.5f, randomStart: true);
        }

        public void Pause()  => _isRunning = false;
        public void Resume() => _isRunning = true;

        /// <summary>
        /// 当前场景内重开一局：时间轴归零并继续跑任务；不重新播关卡 BGM（保持连续播放）。
        /// </summary>
        public void ResetTimelineWithoutBgm()
        {
            _elapsedTime   = 0f;
            _nextTaskIndex = 0;
            _isRunning     = true;
            _timelinePausedForBoss = false;
            _bossTimelineBlocker   = null;
            _bossArenaNextSpawnIn  = 0f;
            _bossArenaLoopIndex    = 0;
        }

        /// <summary>每帧由 DanmakuGameController 调用（在逻辑暂停时不要调用）。</summary>
        public void LogicUpdate(float dt)
        {
            if (!_isRunning || _data == null) return;

            bool bossBlocking = false;
            if (_timelinePausedForBoss)
            {
                if (_bossTimelineBlocker == null || !_bossTimelineBlocker.IsAlive)
                {
                    _timelinePausedForBoss = false;
                    _bossTimelineBlocker   = null;
                }
                else
                {
                    bossBlocking = true;
                }
            }

            // Boss 暂停期间：关卡时间轴不前进，但可按配置循环发射场地弹幕
            if (bossBlocking)
            {
                UpdateBossArenaBulletLoop(dt);
                return;
            }

            _elapsedTime += dt;

            // 执行所有已到期的任务
            while (_nextTaskIndex < _data.Tasks.Count &&
                   _data.Tasks[_nextTaskIndex].TriggerTime <= _elapsedTime)
            {
                ExecuteTask(_data.Tasks[_nextTaskIndex]);
                _nextTaskIndex++;
            }

            // 无尽：关卡任务跑完后从头循环
            if (_endlessLoop && _data.Tasks != null && _data.Tasks.Count > 0 && _nextTaskIndex >= _data.Tasks.Count)
            {
                _nextTaskIndex = 0;
                _elapsedTime   = 0f;
            }
        }

        private void ExecuteTask(StageTaskEntry task)
        {
            switch (task.TaskType)
            {
                case StageTaskType.SpawnEnemy:
                {
                    Vector2 pos = ResolveEnemySpawnPosition(task);
                    var e = _enemyManager?.SpawnEnemy(task.EnemyPrefabPath, pos);
                    if (e != null && (task.IsBossEnemy || !string.IsNullOrEmpty(task.BossFolderName)))
                        e.ApplyStageBossOverrides(task.IsBossEnemy, task.BossFolderName, _gameplayBalance,
                            task.BossHpStageMultiplier);
                    if (task.BlockStageTimelineUntilBossDefeated && e != null &&
                        (task.IsBossEnemy || !string.IsNullOrEmpty(task.BossFolderName)))
                    {
                        _bossTimelineBlocker   = e;
                        _timelinePausedForBoss = true;
                        ResetBossArenaLoopTimer();
                    }
                    break;
                }

                case StageTaskType.SpawnBulletWave:
                    ExecuteSpawnBulletWaveFromTask(task);
                    break;

                case StageTaskType.ChangeBGM:
                    if (_audio != null && !string.IsNullOrEmpty(task.BGMName))
                        PlayStageBgm(task.BGMName, 0.5f);
                    break;

                case StageTaskType.PhaseChange:
                    Debug.Log($"[StageRunner] 阶段变更: {task.PhaseName} @ {_elapsedTime:F2}s");
                    break;

                case StageTaskType.Message:
                    Debug.Log($"[StageRunner] {task.MessageText}");
                    break;
            }
        }

        private Vector2 ResolveEnemySpawnPosition(StageTaskEntry task)
        {
            Vector2 pos = task.SpawnPosition;
            bool manual = task.SpawnEnemyUseManualPosition;
            bool fromEdge = !manual && (_data != null && _data.SpawnEnemyFromRightEdgeOffscreen);
            if (!fromEdge)
                return pos;

            float extra = _data != null ? Mathf.Max(0f, _data.EnemySpawnExtraRightWorld) : 1.2f;
            if (DanmakuStageSpawn.TryGetRightEdgeSpawn(task.SpawnPosition.y, extra, out var edge))
                return edge;
            return pos;
        }

        private void ResetBossArenaLoopTimer()
        {
            _bossArenaLoopIndex = 0;
            float first = _data != null ? Mathf.Max(0f, _data.BossArenaBulletLoopFirstDelaySeconds) : 0f;
            _bossArenaNextSpawnIn = first;
        }

        private void UpdateBossArenaBulletLoop(float dt)
        {
            if (_data?.BossArenaBulletLoop == null || _data.BossArenaBulletLoop.Count == 0)
                return;

            _bossArenaNextSpawnIn -= dt;
            float interval = _data != null
                ? Mathf.Max(0.05f, _data.BossArenaBulletLoopIntervalSeconds)
                : 0.05f;

            while (_bossArenaNextSpawnIn <= 0f)
            {
                var entry = _data.BossArenaBulletLoop[_bossArenaLoopIndex % _data.BossArenaBulletLoop.Count];
                _bossArenaLoopIndex++;
                ExecuteSpawnBulletWaveFromBossArenaEntry(entry);
                _bossArenaNextSpawnIn += interval;
            }
        }

        private void ExecuteSpawnBulletWaveFromTask(StageTaskEntry task)
        {
            if (_bulletManager == null || task.Shooter == null) return;
            Vector2 origin = ResolveBulletOrigin(task);
            ExecuteSpawnBulletWaveInternal(origin, task.Shooter, task.BulletSpeed, task.BulletHitRadius,
                task.BulletLayer, task.BulletStyleFolderIndex, task.BulletSpriteVariantIndex);
        }

        private void ExecuteSpawnBulletWaveFromBossArenaEntry(BossArenaBulletWaveEntry entry)
        {
            if (_bulletManager == null || entry.Shooter == null) return;
            Vector2 origin = ResolveBulletOriginForWave(entry.BulletOrigin,
                entry.BulletWaveRandomXBetweenPlayRightAndOffscreen, entry.BulletSpawnExtraRightMin,
                entry.BulletSpawnExtraRightMax);
            ExecuteSpawnBulletWaveInternal(origin, entry.Shooter, entry.BulletSpeed, entry.BulletHitRadius,
                entry.BulletLayer, entry.BulletStyleFolderIndex, entry.BulletSpriteVariantIndex);
        }

        private void ExecuteSpawnBulletWaveInternal(Vector2 originWorld, DanmakuShooter shooter, float bulletSpeed,
            float bulletHitRadius, int bulletLayer, int bulletStyleFolderIndex, int bulletSpriteVariantIndex)
        {
            Vector2 playerPos = originWorld;
            if (_player != null && _player.IsAlive)
                playerPos = _player.transform.position;
            float spd = bulletSpeed * _bulletSpeedMultiplier;
            int waveStyle = _bulletManager.ResolveBulletStyleForWave(bulletStyleFolderIndex);
            int waveVariant =
                _bulletManager.ResolveBulletVariantForWave(waveStyle, bulletSpriteVariantIndex);
            _bulletManager.ExecuteSpawnBulletWave(shooter, originWorld, playerPos, spd, bulletHitRadius,
                bulletLayer, waveStyle, waveVariant);
            DanmakuAudio.PlaySpawnBulletWaveSfxRandom();
        }

        private Vector2 ResolveBulletOrigin(StageTaskEntry task)
        {
            return ResolveBulletOriginForWave(task.BulletOrigin, task.BulletWaveRandomXBetweenPlayRightAndOffscreen,
                task.BulletSpawnExtraRightMin, task.BulletSpawnExtraRightMax);
        }

        private Vector2 ResolveBulletOriginForWave(Vector2 bulletOrigin,
            bool bulletWaveRandomXBetweenPlayRightAndOffscreen, float bulletSpawnExtraRightMin,
            float bulletSpawnExtraRightMax)
        {
            Vector2 o = bulletOrigin;
            bool randomX = bulletWaveRandomXBetweenPlayRightAndOffscreen ||
                           (_data != null && _data.BulletWaveRandomXBetweenPlayRightAndOffscreen);
            if (!randomX)
                return o;

            float minE = bulletSpawnExtraRightMin >= 0f
                ? bulletSpawnExtraRightMin
                : (_data != null ? _data.BulletSpawnExtraRightMin : 0f);
            float maxE = bulletSpawnExtraRightMax >= 0f
                ? bulletSpawnExtraRightMax
                : (_data != null ? _data.BulletSpawnExtraRightMax : 5f);
            float x = DanmakuStageSpawn.RandomBulletOriginX(minE, maxE);
            return new Vector2(x, bulletOrigin.y);
        }

        private void PlayStageBgm(string bgmNameOrResourcePath, float fade, bool randomStart = false)
        {
            if (_audio == null || string.IsNullOrEmpty(bgmNameOrResourcePath)) return;
            if (IsFullResourcesBgmPath(bgmNameOrResourcePath))
                _audio.PlayBGMFromResources(bgmNameOrResourcePath, fade, randomStart);
            else
                _audio.PlayBGM(bgmNameOrResourcePath, fade, randomStart);
        }

        private static bool IsFullResourcesBgmPath(string s)
        {
            return s.IndexOf('/') >= 0;
        }
    }
}
