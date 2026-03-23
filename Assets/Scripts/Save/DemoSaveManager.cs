using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DemoFrameWork.Save
{
    /// <summary>
    /// 轻量存档管理器。
    /// 每个存档 key 对应 persistentDataPath/saves/{key}.json，使用 Newtonsoft.Json 序列化。
    /// 用法示例：
    ///   DemoSaveManager.GetInstance().Save("player", new PlayerData { level = 5 });
    ///   var data = DemoSaveManager.GetInstance().Load("player", new PlayerData());
    ///   也可通过 DemoGameEntry.Save 访问。
    /// </summary>
    public class DemoSaveManager : BaseManager<DemoSaveManager>
    {
        private string SaveDir => Path.Combine(Application.persistentDataPath, "saves");

        private string GetFilePath(string key) =>
            Path.Combine(SaveDir, key + ".json");

        private void EnsureDir()
        {
            if (!Directory.Exists(SaveDir))
                Directory.CreateDirectory(SaveDir);
        }

        /// <summary>将数据序列化并写入存档文件。</summary>
        public void Save<T>(string key, T data)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            EnsureDir();
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(GetFilePath(key), json, System.Text.Encoding.UTF8);
        }

        /// <summary>读取并反序列化存档；不存在则返回 defaultValue。</summary>
        public T Load<T>(string key, T defaultValue = default)
        {
            string path = GetFilePath(key);
            if (!File.Exists(path)) return defaultValue;
            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DemoSaveManager] Load failed for key '{key}': {e.Message}");
                return defaultValue;
            }
        }

        /// <summary>判断存档是否存在。</summary>
        public bool Exists(string key) => File.Exists(GetFilePath(key));

        /// <summary>删除指定 key 的存档。</summary>
        public void Delete(string key)
        {
            string path = GetFilePath(key);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>删除全部存档文件。</summary>
        public void DeleteAll()
        {
            if (!Directory.Exists(SaveDir)) return;
            foreach (string f in Directory.GetFiles(SaveDir, "*.json"))
                File.Delete(f);
        }
    }
}
