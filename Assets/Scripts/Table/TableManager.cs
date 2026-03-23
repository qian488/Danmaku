using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DemoFrameWork.Table
{
    /// <summary>
    /// 运行时表格管理器。
    /// 从 Resources/Tables/{ClassName}.json 加载数据并缓存，
    /// 配合 Excel2CsTool 生成的 DemoFrameWork.Table.XXX 类使用。
    /// 用法示例：
    ///   var rows = DemoGameEntry.Table.GetTable&lt;ItemConfig&gt;();
    ///   var item = DemoGameEntry.Table.GetRow&lt;ItemConfig&gt;(r => r.id == 101);
    /// </summary>
    public class TableManager : BaseManager<TableManager>
    {
        private const string ResourcesPrefix = "Tables/";
        private readonly Dictionary<Type, object> _cache = new Dictionary<Type, object>();

        /// <summary>
        /// 获取指定表格的全部数据行。
        /// 第一次调用时从 Resources 加载并缓存；后续直接返回缓存。
        /// </summary>
        public List<T> GetTable<T>() where T : class
        {
            Type type = typeof(T);
            if (_cache.TryGetValue(type, out object cached))
                return (List<T>)cached;

            string path = ResourcesPrefix + type.Name;
            TextAsset asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[TableManager] 找不到表格资源: Resources/{path}.json");
                return new List<T>();
            }

            List<T> rows;
            try
            {
                rows = JsonConvert.DeserializeObject<List<T>>(asset.text) ?? new List<T>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[TableManager] 反序列化失败 ({type.Name}): {e.Message}");
                return new List<T>();
            }

            _cache[type] = rows;
            return rows;
        }

        /// <summary>
        /// 从表格中查找第一条满足条件的行；不存在返回 null。
        /// </summary>
        public T GetRow<T>(Func<T, bool> predicate) where T : class
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            List<T> rows = GetTable<T>();
            foreach (T row in rows)
            {
                if (predicate(row)) return row;
            }
            return null;
        }

        /// <summary>
        /// 从表格中查找所有满足条件的行。
        /// </summary>
        public List<T> GetRows<T>(Func<T, bool> predicate) where T : class
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var result = new List<T>();
            foreach (T row in GetTable<T>())
            {
                if (predicate(row)) result.Add(row);
            }
            return result;
        }

        /// <summary>
        /// 清除所有已缓存的表格数据（热更新或重新加载时调用）。
        /// </summary>
        public void Clear() => _cache.Clear();

        /// <summary>
        /// 清除指定类型的表格缓存。
        /// </summary>
        public void Clear<T>() where T : class => _cache.Remove(typeof(T));
    }
}
