using System;
using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    [Serializable]
    public struct DanmakuPickupWeightEntry
    {
        public DanmakuPickupKind kind;
        public float weight;
    }

    /// <summary>
    /// 弹幕 Demo 数值中枢：分数公式系数、时间分曲线、擦弹/XP/火力档、掉落概率与拾取效果。
    /// </summary>
    [CreateAssetMenu(menuName = "Danmaku/Gameplay Balance", fileName = "DanmakuGameplayBalance")]
    public class DanmakuGameplayBalance : ScriptableObject
    {
        [Header("Score")]
        [Tooltip("最终显示分 = (时间分累计 + 击坠×系数 + 拾取分累计 + Power×系数 + Graze×系数) × 本值")]
        public float globalScoreMultiplier = 1f;

        public int perKillScore = 100;
        public int perPowerScore = 2;
        public int perGrazeScore = 80;

        [Tooltip("横轴：本局已过秒数；纵轴：该时刻每秒增加的时间分（线性插值）")]
        public AnimationCurve scorePointsPerSecondByElapsedTime;

        [Header("Pickup → 计入「拾取分」累加器的值")]
        public int scorePowerPickup = 20;
        public int scoreMoveSpeedPickup = 15;
        public int scoreFireRatePickup = 15;
        public int scoreHealPickup = 25;
        public int scoreAttackPickup = 20;
        public int scoreXpPickup = 30;

        [Header("Power")]
        public int powerPerKillMin = 5;
        public int powerPerKillMax = 15;
        public int powerFromPowerPickup = 25;

        [Header("Graze")]
        [Tooltip("擦弹圆半径 = 受击半径 × 本值（须 >1）")]
        public float grazeRadiusMultiplier = 4f;

        public float xpPerGraze = 10f;

        [Header("XP / Fire tier")]
        [Tooltip("每升一档消耗的 XP（档 0→1 用 [0]，共 5 档火力：1~5 发，最后一档带追踪）")]
        public float[] xpThresholdPerTier = { 100f, 220f, 450f, 800f, 1300f };

        public float xpFromPickupXpKind = 50f;

        [Header("Boss（相对普通敌机的体型/受击半径倍率；可在关卡 SpawnEnemy 上配合 BlockStageTimelineUntilBossDefeated）")]
        [Tooltip("Boss 受击半径 = 普通敌预制体上的 HitRadius × 本倍率（一般 5）")]
        public float bossScaleMultiplierVsNormal = 5f;

        [Tooltip(
            "Boss 机体贴图显示缩放：在「预制体 Sprite 缩放 × 上项」之后再乘本系数；<1 则图变小、判定不变。1 = 与旧版（图与判定同倍）一致。")]
        [Range(0.05f, 2f)]
        public float bossSpriteVisualScaleFactor = 0.5f;

        [Tooltip("Boss 发射的敌弹碰撞半径 = 敌预制体上的子弹 HitRadius × 本倍率（贴图会随半径同比放大）")]
        [Range(0.5f, 6f)]
        public float bossEnemyBulletHitRadiusMultiplier = 2.25f;

        [Tooltip("击破 Boss 时额外奖励分（在 perKillScore 之外叠加）")]
        public int bonusScorePerBossKill = 8000;

        [Tooltip("击破 Boss 时额外获得的 XP")]
        public float xpPerBossKill = 250f;

        [Tooltip("Boss 最大血量 = 敌机预制体上的 MaxHp × 本倍率（相对普通敌；常设 100）；再乘关卡任务上的 BossHpStageMultiplier")]
        public float bossHpMultiplierVsNormal = 100f;

        [Tooltip(
            "Boss：首发延迟 Shoot Delay 与发射间隔 Shoot Interval 相对敌预制体上数值的倍率；<1 更快（如 0.55 ≈ 约 1.8 倍射速），>1 更慢")]
        [Range(0.05f, 3f)]
        public float bossShootTimingMultiplierVsNormal = 0.55f;

        [Header("Drop probabilities")]
        [Range(0f, 1f)] public float deathDropChance = 0.35f;

        public float ambientSpawnIntervalMin = 4f;
        public float ambientSpawnIntervalMax = 9f;

        [Range(0f, 1f)] public float ambientSpawnChance = 0.35f;

        [Tooltip("敌机死亡掉落加权（总权为 0 时均匀随机）")]
        public DanmakuPickupWeightEntry[] deathDropWeights =
        {
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.Power, weight = 3f },
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.MoveSpeed, weight = 2f },
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.FireRate, weight = 2f },
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.Heal, weight = 1f },
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.Attack, weight = 2f },
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.Xp, weight = 1.5f }
        };

        [Tooltip("场上随机刷新加权")]
        public DanmakuPickupWeightEntry[] ambientDropWeights =
        {
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.Power, weight = 2f },
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.MoveSpeed, weight = 2f },
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.FireRate, weight = 2f },
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.Heal, weight = 1f },
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.Attack, weight = 2f },
            new DanmakuPickupWeightEntry { kind = DanmakuPickupKind.Xp, weight = 2f }
        };

        [Header("Pickup stat modifiers（叠乘：移速/攻速倍率；攻击为伤害倍率）")]
        public float moveSpeedBonusPerPickup = 0.08f;
        public float fireRateBonusPerPickup = 0.1f;
        public float attackBonusPerPickup = 0.12f;
        public int healPerPickup = 1;

        [Header("自机弹幕（扇形与追踪）")]
        [Tooltip("多弹时半扇角（度），总宽 = 2×本值")]
        public float multiShotSpreadHalfAngleDeg = 8f;

        public float homingTurnRateDegPerSec = 540f;

        /// <summary>时间分：当前时刻的每秒得分（用于积分）。</summary>
        public float EvaluateScoreRate(float elapsedSeconds)
        {
            if (scorePointsPerSecondByElapsedTime == null || scorePointsPerSecondByElapsedTime.length == 0)
                return 8f;
            return Mathf.Max(0f, scorePointsPerSecondByElapsedTime.Evaluate(elapsedSeconds));
        }

        public int GetPickupScoreContribution(DanmakuPickupKind kind)
        {
            switch (kind)
            {
                case DanmakuPickupKind.Power: return scorePowerPickup;
                case DanmakuPickupKind.MoveSpeed: return scoreMoveSpeedPickup;
                case DanmakuPickupKind.FireRate: return scoreFireRatePickup;
                case DanmakuPickupKind.Heal: return scoreHealPickup;
                case DanmakuPickupKind.Attack: return scoreAttackPickup;
                case DanmakuPickupKind.Xp: return scoreXpPickup;
                default: return 0;
            }
        }

        public DanmakuPickupKind PickRandomKind(DanmakuPickupWeightEntry[] table)
        {
            if (table == null || table.Length == 0)
                return DanmakuPickupKind.Power;

            float sum = 0f;
            for (int i = 0; i < table.Length; i++)
                sum += Mathf.Max(0f, table[i].weight);

            if (sum <= 0f)
                return table[UnityEngine.Random.Range(0, table.Length)].kind;

            float r = UnityEngine.Random.value * sum;
            for (int i = 0; i < table.Length; i++)
            {
                r -= Mathf.Max(0f, table[i].weight);
                if (r <= 0f)
                    return table[i].kind;
            }

            return table[table.Length - 1].kind;
        }

        /// <summary>火力档 0=单发 … 4=五发+追踪。</summary>
        public int MaxFireTierIndex => 4;

        public int GetBulletCountForTier(int tier)
        {
            tier = Mathf.Clamp(tier, 0, MaxFireTierIndex);
            return tier + 1;
        }

        public bool TierUsesHoming(int tier) => tier >= MaxFireTierIndex;

        private void OnEnable()
        {
            if (scorePointsPerSecondByElapsedTime == null || scorePointsPerSecondByElapsedTime.length < 2)
                scorePointsPerSecondByElapsedTime = AnimationCurve.Linear(0f, 8f, 240f, 28f);
        }
    }
}
