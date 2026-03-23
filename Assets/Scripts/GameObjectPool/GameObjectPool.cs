using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DemoFrameWork;

namespace DemoFrameWork.Pool
{
    /// <summary>
    /// GameObject 池：按预制体 Spawn/Recycle，通过 DemoGameEntry.Pool 访问。
    /// </summary>
    public class GameObjectPool : MonoBehaviour
    {
        private class PrefabBucket
        {
            public GameObject Prefab;
            public Transform Container;
            public Stack<GameObject> Available = new Stack<GameObject>();
        }

        private Dictionary<string, PrefabBucket> _buckets = new Dictionary<string, PrefabBucket>();
        private Transform _poolRoot;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized) return;
            _poolRoot = new GameObject("PoolRoot").transform;
            _poolRoot.SetParent(transform);
            DontDestroyOnLoad(gameObject);
            _initialized = true;
        }

        public GameObject Spawn(string prefabPath)
        {
            return Spawn(prefabPath, Vector3.zero, Quaternion.identity);
        }

        public GameObject Spawn(string prefabPath, Vector3 position, Quaternion rotation)
        {
            var bucket = GetOrCreateBucketByPath(prefabPath);
            if (bucket == null) return null;
            return SpawnFromBucket(bucket, position, rotation);
        }

        public GameObject Spawn(GameObject prefab)
        {
            return Spawn(prefab, Vector3.zero, Quaternion.identity);
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            var bucket = GetOrCreateBucketByPrefab(prefab);
            return SpawnFromBucket(bucket, position, rotation);
        }

        public T Spawn<T>(string prefabPath) where T : Component
        {
            var go = Spawn(prefabPath);
            return go != null ? go.GetComponent<T>() : null;
        }

        public T Spawn<T>(GameObject prefab) where T : Component
        {
            var go = Spawn(prefab);
            return go != null ? go.GetComponent<T>() : null;
        }

        public void Recycle(GameObject go)
        {
            if (go == null) return;
            var po = go.GetComponent<PooledObject>();
            if (po == null || string.IsNullOrEmpty(po.BucketKey))
            {
                Object.Destroy(go);
                return;
            }
            if (!_buckets.TryGetValue(po.BucketKey, out var bucket))
            {
                Object.Destroy(go);
                return;
            }

            // 已在该桶下且未激活：视为重复 Recycle，避免同一实例两次 Push 导致 Pop 出「已借出」对象并触发 Transform 断言
            // 上文 if (go == null) 已含 Unity 已销毁实例，此处再访问 .activeSelf 才安全
            if (!go.activeSelf && go.transform.parent == bucket.Container)
                return;

            foreach (var c in go.GetComponentsInChildren<IPoolable>(true))
                c.OnRecycle();
            go.SetActive(false);
            go.transform.SetParent(bucket.Container, true);
            bucket.Available.Push(go);
        }

        public void RecycleDelay(GameObject go, float delay)
        {
            if (go == null) return;
            StartCoroutine(RecycleDelayCoroutine(go, delay));
        }

        private IEnumerator RecycleDelayCoroutine(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            Recycle(go);
        }

        public void Prewarm(string prefabPath, int count)
        {
            var bucket = GetOrCreateBucketByPath(prefabPath);
            if (bucket == null) return;
            for (int i = 0; i < count; i++)
            {
                var go = Object.Instantiate(bucket.Prefab, bucket.Container);
                var po = go.GetComponent<PooledObject>() ?? go.AddComponent<PooledObject>();
                po.BucketKey = prefabPath;
                po.Pool = this;
                go.SetActive(false);
                bucket.Available.Push(go);
            }
        }

        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            var bucket = GetOrCreateBucketByPrefab(prefab);
            for (int i = 0; i < count; i++)
            {
                var go = Object.Instantiate(prefab, bucket.Container);
                var key = prefab.name;
                var po = go.GetComponent<PooledObject>() ?? go.AddComponent<PooledObject>();
                po.BucketKey = key;
                po.Pool = this;
                go.SetActive(false);
                bucket.Available.Push(go);
            }
        }

        public void Clear(string prefabPath)
        {
            if (_buckets.TryGetValue(prefabPath, out var bucket))
            {
                while (bucket.Available.Count > 0)
                    Object.Destroy(bucket.Available.Pop());
            }
        }

        public void Clear(GameObject prefab)
        {
            if (prefab != null)
                Clear(prefab.name);
        }

        public void ClearAll()
        {
            foreach (var bucket in _buckets.Values)
            {
                while (bucket.Available.Count > 0)
                    Object.Destroy(bucket.Available.Pop());
            }
        }

        /// <summary>
        /// 缩减各桶中「仅堆在池里、未借出」的实例数量，释放内存；适合暂停等安全时机调用。
        /// </summary>
        /// <param name="maxIdlePerBucket">每桶最多保留的空闲实例；≤0 则不处理。</param>
        public void TrimExcessIdle(int maxIdlePerBucket)
        {
            if (maxIdlePerBucket <= 0) return;
            foreach (var bucket in _buckets.Values)
            {
                if (bucket == null || bucket.Available == null) continue;
                while (bucket.Available.Count > maxIdlePerBucket)
                {
                    var go = bucket.Available.Pop();
                    if (go != null)
                        Object.Destroy(go);
                }
            }
        }

        private PrefabBucket GetOrCreateBucketByPath(string path)
        {
            if (_buckets.TryGetValue(path, out var b))
                return b;
            GameObject prefab = null;
            if (DemoGameEntry.Resource != null)
                prefab = DemoGameEntry.Resource.Load<GameObject>(path);
            if (prefab == null)
                prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameObjectPool] Prefab not found: {path}");
                return null;
            }
            return GetOrCreateBucketByPrefab(prefab, path);
        }

        private PrefabBucket GetOrCreateBucketByPrefab(GameObject prefab, string key = null)
        {
            key = key ?? prefab.name;
            if (_buckets.TryGetValue(key, out var b))
                return b;
            var container = new GameObject($"Bucket_{key}").transform;
            container.SetParent(_poolRoot);
            var bucket = new PrefabBucket { Prefab = prefab, Container = container };
            _buckets[key] = bucket;
            return bucket;
        }

        private GameObject SpawnFromBucket(PrefabBucket bucket, Vector3 position, Quaternion rotation)
        {
            // 栈里可能有「已销毁」引用（切场景等导致）；Pop 后须用 == null 判断（Unity 重载含已销毁），再访问 .activeSelf
            GameObject go = null;
            while (bucket.Available.Count > 0)
            {
                var candidate = bucket.Available.Pop();
                if (candidate != null)
                {
                    go = candidate;
                    break;
                }
            }

            if (go == null)
            {
                go = Object.Instantiate(bucket.Prefab, position, rotation);
                var po = go.GetComponent<PooledObject>() ?? go.AddComponent<PooledObject>();
                po.BucketKey = GetKeyForBucket(bucket);
                po.Pool = this;
            }
            else
            {
                // 栈损坏或重复 Recycle 时可能仍激活或已不在桶下：丢弃并新实例化，避免 SetParent/MoveGameObjectToScene 触发引擎断言
                if (go.activeSelf || go.transform.parent != bucket.Container)
                {
                    Debug.LogWarning(
                        $"[GameObjectPool] Pooled instance in bad state (active={go.activeSelf}, parentOk={go.transform.parent == bucket.Container}); destroying and creating new. Prefab={bucket.Prefab?.name}",
                        go);
                    Object.Destroy(go);
                    go = Object.Instantiate(bucket.Prefab, position, rotation);
                    var poNew = go.GetComponent<PooledObject>() ?? go.AddComponent<PooledObject>();
                    poNew.BucketKey = GetKeyForBucket(bucket);
                    poNew.Pool = this;
                }
                else
                {
                    if (go.transform.parent != null)
                        go.transform.SetParent(null, true);
                    go.transform.position = position;
                    go.transform.rotation = rotation;
                    // 从 PoolRoot（DontDestroyOnLoad）下取出后若不设场景，根物体会留在 DDOL，切场景也不会被卸载。
                    var active = SceneManager.GetActiveScene();
                    if (active.IsValid() && go.scene != active)
                        SceneManager.MoveGameObjectToScene(go, active);
                }
            }

            go.SetActive(true);
            foreach (var c in go.GetComponentsInChildren<IPoolable>(true))
                c.OnSpawn();
            return go;
        }

        private string GetKeyForBucket(PrefabBucket bucket)
        {
            foreach (var kv in _buckets)
                if (kv.Value == bucket) return kv.Key;
            return bucket.Prefab.name;
        }
    }
}
