using System;
using UnityEngine;

namespace DemoFrameWork.Demo.Danmaku
{
    /// <summary>
    /// 每参与一局弹幕战斗，永久攻击力（平加在单发伤害上）+0.1；按本地日历日计，每日最多登记 10 次（当日最多 +1.0）。
    /// 累计值以「十分之一单位」整数存 PlayerPrefs，避免浮点误差。
    /// </summary>
    public static class DanmakuDailyAttackBonus
    {
        private const string KeyYmd = "Danmaku.DailyAttack.LastYmd";
        private const string KeyGainsToday = "Danmaku.DailyAttack.GainsToday";
        private const string KeyTotalTenths = "Danmaku.DailyAttack.TotalTenths";
        private const string KeyLegacyTotalFlat = "Danmaku.DailyAttack.TotalFlatDamage";

        private const int MaxGainsPerDay = 10;
        private const float PerGain = 0.1f;

        private static void MigrateLegacyIfNeeded()
        {
            if (PlayerPrefs.HasKey(KeyTotalTenths) || !PlayerPrefs.HasKey(KeyLegacyTotalFlat))
                return;
            int legacy = PlayerPrefs.GetInt(KeyLegacyTotalFlat, 0);
            PlayerPrefs.SetInt(KeyTotalTenths, legacy * 10);
            PlayerPrefs.DeleteKey(KeyLegacyTotalFlat);
        }

        /// <summary>
        /// 登记本局战斗参与：若当日尚未登记满 10 次则累计 +0.1。返回当前累计平加（供自机伤害）。
        /// </summary>
        public static float RegisterBattleParticipation()
        {
            MigrateLegacyIfNeeded();
            int todayYmd = int.Parse(DateTime.Now.ToString("yyyyMMdd"), System.Globalization.CultureInfo.InvariantCulture);
            int lastYmd = PlayerPrefs.GetInt(KeyYmd, 0);
            int gainsToday = PlayerPrefs.GetInt(KeyGainsToday, 0);
            int totalTenths = PlayerPrefs.GetInt(KeyTotalTenths, 0);

            if (lastYmd != todayYmd)
            {
                gainsToday = 0;
            }

            if (gainsToday < MaxGainsPerDay)
            {
                totalTenths += 1;
                gainsToday += 1;
                PlayerPrefs.SetInt(KeyTotalTenths, totalTenths);
                PlayerPrefs.SetInt(KeyGainsToday, gainsToday);
            }

            PlayerPrefs.SetInt(KeyYmd, todayYmd);
            PlayerPrefs.Save();
            return totalTenths * PerGain;
        }

        /// <summary>当前累计平加（不触发登记，供只读展示）。</summary>
        public static float GetTotalBonusWithoutRegistering()
        {
            MigrateLegacyIfNeeded();
            return PlayerPrefs.GetInt(KeyTotalTenths, 0) * PerGain;
        }
    }
}
