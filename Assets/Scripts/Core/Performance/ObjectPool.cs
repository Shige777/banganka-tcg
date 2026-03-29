using System;
using System.Collections.Generic;
using UnityEngine;

namespace Banganka.Core.Performance
{
    /// <summary>
    /// 汎用オブジェクトプール (PERFORMANCE_SPEC.md)
    /// カードUI / エフェクト / ダメージテキスト等の再利用
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        [Serializable]
        public class PoolEntry
        {
            public string key;
            public GameObject prefab;
            public int initialSize = 10;
            public int maxSize = 50;
        }

        [SerializeField] PoolEntry[] pools;

        readonly Dictionary<string, Queue<GameObject>> _available = new();
        readonly Dictionary<string, List<GameObject>> _active = new();
        readonly Dictionary<string, PoolEntry> _entries = new();

        static ObjectPool _instance;
        public static ObjectPool Instance => _instance;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            if (pools == null) return;
            foreach (var entry in pools)
            {
                if (string.IsNullOrEmpty(entry.key) || entry.prefab == null) continue;
                _entries[entry.key] = entry;
                _available[entry.key] = new Queue<GameObject>();
                _active[entry.key] = new List<GameObject>();
                Prewarm(entry);
            }
        }

        void Prewarm(PoolEntry entry)
        {
            for (int i = 0; i < entry.initialSize; i++)
            {
                var obj = Instantiate(entry.prefab, transform);
                obj.SetActive(false);
                obj.name = $"{entry.key}_pooled_{i}";
                _available[entry.key].Enqueue(obj);
            }
        }

        // ====================================================================
        // Public API
        // ====================================================================

        public GameObject Get(string key, Transform parent = null)
        {
            if (!_available.ContainsKey(key))
            {
                Debug.LogWarning($"[ObjectPool] Unknown pool key: {key}");
                return null;
            }

            GameObject obj;
            if (_available[key].Count > 0)
            {
                obj = _available[key].Dequeue();
            }
            else if (_entries.TryGetValue(key, out var entry))
            {
                int totalCount = _available[key].Count + _active[key].Count;
                if (totalCount >= entry.maxSize)
                {
                    Debug.LogWarning($"[ObjectPool] Pool '{key}' at max capacity ({entry.maxSize})");
                    return null;
                }
                obj = Instantiate(entry.prefab, transform);
                obj.name = $"{key}_pooled_{totalCount}";
            }
            else
            {
                return null;
            }

            obj.SetActive(true);
            if (parent != null) obj.transform.SetParent(parent, false);
            _active[key].Add(obj);
            return obj;
        }

        public void Return(string key, GameObject obj)
        {
            if (obj == null) return;
            if (!_available.ContainsKey(key))
            {
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            obj.transform.SetParent(transform, false);
            _active[key].Remove(obj);
            _available[key].Enqueue(obj);
        }

        public void ReturnAll(string key)
        {
            if (!_active.ContainsKey(key)) return;
            var list = new List<GameObject>(_active[key]);
            foreach (var obj in list)
                Return(key, obj);
        }

        // ====================================================================
        // Stats
        // ====================================================================

        public int GetActiveCount(string key)
            => _active.TryGetValue(key, out var list) ? list.Count : 0;

        public int GetAvailableCount(string key)
            => _available.TryGetValue(key, out var queue) ? queue.Count : 0;

        public int GetTotalCount(string key)
            => GetActiveCount(key) + GetAvailableCount(key);
    }
}
