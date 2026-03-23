using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 关卡任务类型，对应参考 StageTask::CreateTaskObject 中的各种任务。
    /// </summary>
    public enum StageTaskType
    {
        SpawnEnemy,         // 生成敌人
        SpawnBulletWave,    // 直接生成一波子弹（不依赖敌人）
        ChangeBGM,          // 切换 BGM
        PhaseChange,        // 关卡阶段/符卡切换
        Message,            // （可选）显示提示文字
    }

    /// <summary>
    /// 单条关卡任务条目，对应参考 StageData::StageTaskData（帧数 + 任务描述）。
    /// </summary>
    [Serializable]
    public class StageTaskEntry
    {
        [Tooltip("触发时间（秒）")]
        public float TriggerTime;

        public StageTaskType TaskType;

        // ---- SpawnEnemy ----
        [Tooltip("[SpawnEnemy] 敌人预制体名（Resources 下的路径）")]
        public string EnemyPrefabPath;

        [Tooltip("[SpawnEnemy] 生成位置（世界坐标）；若本关开启「从右缘出」则仅用 Y")]
        public Vector2 SpawnPosition;

        [Tooltip("[SpawnEnemy] 为 true 时本条仍用手写 SpawnPosition（含 X），不受 StageData「从右缘出」影响")]
        public bool SpawnEnemyUseManualPosition;

        [Tooltip("[SpawnEnemy] 关卡内指定 Boss（无需单独 Boss 预制体）；与 BossFolderName 配合")]
        public bool IsBossEnemy;

        [Tooltip("[SpawnEnemy] Boss 时 Px 目录名，如 Alice（Resources/Demos/Danmaku/Img/boss/{名}/Px）")]
        public string BossFolderName;

        [Tooltip("[SpawnEnemy] 为 true 且本条生成 Boss 时：关卡任务时间轴暂停，直至该 Boss 被击破；弹幕/掉落仍由场上单位与逻辑照常生成")]
        public bool BlockStageTimelineUntilBossDefeated;

        [Tooltip("[SpawnEnemy] Boss：在 Balance 的 Boss 基础血量（预制体 MaxHp×bossHpMultiplierVsNormal）上再乘本系数（≥1）；同关越靠后的 Boss 或更高难度关可填更大")]
        public float BossHpStageMultiplier = 1f;

        // ---- SpawnBulletWave ----
        [Tooltip("[SpawnBulletWave] 发射源位置（世界坐标）；若开启「弹幕 X 随机」则仅用 Y")]
        public Vector2 BulletOrigin;

        [Tooltip("[SpawnBulletWave] 为 true 时本条 X 在「玩法区右缘～右缘外」随机；≤0 时用 StageData 的 min/max")]
        public bool BulletWaveRandomXBetweenPlayRightAndOffscreen;

        [Tooltip("相对玩法区右缘的额外 X 下限（世界单位）；-1 表示用 StageData")]
        public float BulletSpawnExtraRightMin = -1f;

        [Tooltip("相对玩法区右缘的额外 X 上限（世界单位）；-1 表示用 StageData")]
        public float BulletSpawnExtraRightMax = -1f;

        [Tooltip("[SpawnBulletWave] 发射器参数")]
        public DanmakuShooter Shooter;

        [Tooltip("[SpawnBulletWave] 子弹速度")]
        public float BulletSpeed = 3f;

        [Tooltip("[SpawnBulletWave] 子弹碰撞半径")]
        public float BulletHitRadius = 0.06f;

        [Tooltip("[SpawnBulletWave] 子弹图层")]
        public int BulletLayer = 0;

        [Tooltip("[SpawnBulletWave] 贴图样式目录 bullet/{样式}/…；为 -1 时交给 BulletManager（随机或默认）")]
        public int BulletStyleFolderIndex = -1;

        [Tooltip("[SpawnBulletWave] 同目录下贴图变体编号；为 -1 时由 BulletManager 随机一张存在的变体")]
        public int BulletSpriteVariantIndex = -1;

        // ---- ChangeBGM ----
        [Tooltip("[ChangeBGM] BGM 资源名称")]
        public string BGMName;

        // ---- PhaseChange ----
        [Tooltip("[PhaseChange] 阶段名（仅用于日志/UI）")]
        public string PhaseName;

        // ---- Message ----
        [Tooltip("[Message] 提示文字")]
        public string MessageText;
    }

    /// <summary>
    /// Boss 战期间由关卡驱动的「场地弹幕」单条配置（与 <see cref="StageTaskType.SpawnBulletWave"/> 字段一致，不依赖任务时间轴）。
    /// 多条按顺序循环，间隔见 <see cref="StageData.BossArenaBulletLoopIntervalSeconds"/>。
    /// </summary>
    [Serializable]
    public class BossArenaBulletWaveEntry
    {
        [Tooltip("发射源位置（世界坐标）；若开启「弹幕 X 随机」则仅用 Y")]
        public Vector2 BulletOrigin;

        [Tooltip("为 true 时本条 X 在「玩法区右缘～右缘外」随机；≤0 时用 StageData 的 min/max")]
        public bool BulletWaveRandomXBetweenPlayRightAndOffscreen;

        [Tooltip("相对玩法区右缘的额外 X 下限（世界单位）；-1 表示用 StageData")]
        public float BulletSpawnExtraRightMin = -1f;

        [Tooltip("相对玩法区右缘的额外 X 上限（世界单位）；-1 表示用 StageData")]
        public float BulletSpawnExtraRightMax = -1f;

        [Tooltip("发射器参数")]
        public DanmakuShooter Shooter;

        [Tooltip("子弹速度")]
        public float BulletSpeed = 3f;

        [Tooltip("子弹碰撞半径")]
        public float BulletHitRadius = 0.06f;

        [Tooltip("子弹图层")]
        public int BulletLayer = 0;

        [Tooltip("贴图样式目录 bullet/{样式}/…；为 -1 时交给 BulletManager（随机或默认）")]
        public int BulletStyleFolderIndex = -1;

        [Tooltip("同目录下贴图变体编号；为 -1 时由 BulletManager 随机一张存在的变体")]
        public int BulletSpriteVariantIndex = -1;
    }

    /// <summary>
    /// 关卡数据 ScriptableObject，对应参考 StageData + Main.rv + 任务 CSV。
    /// 创建：Assets > Create > Danmaku > StageData
    /// </summary>
    [CreateAssetMenu(fileName = "Stage01", menuName = "Danmaku/StageData")]
    public class StageData : ScriptableObject
    {
        [Tooltip("关卡 ID / 名称")]
        public string StageId = "Stage01";

        [Tooltip("关卡开始时 BGM：填 Resources 全路径（如 Demos/Danmaku/Audio/BGM/game）将用 PlayBGMFromResources；仅填文件名则用 AudioSettings 的 bgmPathPrefix 相对路径")]
        public string StageBGM;

        [Tooltip("背景预制体路径（Resources）")]
        public string BackgroundPrefabPath;

        [Tooltip("子弹图层数")]
        public int BulletLayerCount = 1;

        [Header("生成位置（SpawnEnemy / SpawnBulletWave）")]
        [Tooltip("为 true 时本关 SpawnEnemy 的 X 取玩法区右缘外，Y 用 SpawnPosition.y；为 false 时用 SpawnPosition 全量")]
        public bool SpawnEnemyFromRightEdgeOffscreen;

        [Tooltip("从右缘出时，在玩法区右缘外再平移（世界单位）")]
        public float EnemySpawnExtraRightWorld = 1.2f;

        [Tooltip("为 true 时 SpawnBulletWave 的 X 在「玩法区右缘～右缘外」随机；Y 仍用 BulletOrigin.y")]
        public bool BulletWaveRandomXBetweenPlayRightAndOffscreen;

        [Tooltip("弹幕随机 X：相对玩法区右缘的额外下限（世界单位）")]
        public float BulletSpawnExtraRightMin = 0f;

        [Tooltip("弹幕随机 X：相对玩法区右缘的额外上限（世界单位）")]
        public float BulletSpawnExtraRightMax = 5f;

        [Header("Boss 战：场地弹幕循环（时间轴因 Boss 暂停时仍触发）")]
        [Tooltip("进入 Boss 暂停后，首次场地弹幕前等待的秒数")]
        public float BossArenaBulletLoopFirstDelaySeconds = 0f;

        [Tooltip("场地弹幕循环：相邻两次发射的间隔（秒，含从一条切换到下一条）")]
        public float BossArenaBulletLoopIntervalSeconds = 2.5f;

        [Tooltip("多条按顺序轮流发射，发完最后一条后回到第一条。列表为空则 Boss 战无额外场地弹幕")]
        public List<BossArenaBulletWaveEntry> BossArenaBulletLoop = new List<BossArenaBulletWaveEntry>();

        [Tooltip("任务列表（按 TriggerTime 升序排列）。同秒多条时若含 Boss 且勾选 BlockStageTimelineUntilBossDefeated，建议把 Boss 放在该秒最后一条")]
        public List<StageTaskEntry> Tasks = new List<StageTaskEntry>();
    }
}
