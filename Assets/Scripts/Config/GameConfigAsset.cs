using System;
using UnityEngine;

namespace DemoFrameWork.Config
{
    [CreateAssetMenu(menuName = "DemoFrameWork/Game Config", fileName = "GameConfig")]
    public class GameConfigAsset : ScriptableObject
    {
        public string gameVersion = "1.0.0";
        public string channel = "dev";
        public ConfigEntry[] entries;
    }

    [Serializable]
    public struct ConfigEntry
    {
        public string key;
        public string value;
    }
}
