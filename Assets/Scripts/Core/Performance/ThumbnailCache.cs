using System.Collections.Generic;
using UnityEngine;

namespace Banganka.Core.Performance
{
    /// <summary>
    /// カードサムネイル用 LRU メモリキャッシュ (COLLECTION_UX_SPEC §1.3)
    /// メモリ上限 50MB、超過時は最も古いエントリから破棄する
    /// </summary>
    public class ThumbnailCache
    {
        // ====================================================================
        // Singleton
        // ====================================================================

        static ThumbnailCache _instance;
        public static ThumbnailCache Instance => _instance ??= new ThumbnailCache();

        // ====================================================================
        // Limits
        // ====================================================================

        const long MemoryLimitBytes = 50L * 1024 * 1024; // 50MB
        const int MaxEntries = 500;

        // ====================================================================
        // Internal state
        // ====================================================================

        struct CacheEntry
        {
            public string cardId;
            public Sprite sprite;
            public long sizeBytes;
        }

        readonly LinkedList<CacheEntry> _lruList = new();
        readonly Dictionary<string, LinkedListNode<CacheEntry>> _lookup = new();
        long _currentMemoryBytes;

        // ====================================================================
        // Properties
        // ====================================================================

        public int Count => _lookup.Count;
        public long CurrentMemoryBytes => _currentMemoryBytes;

        // ====================================================================
        // Public API
        // ====================================================================

        /// <summary>
        /// キャッシュからサムネイルを取得する。ヒット時は LRU の先頭に移動。
        /// </summary>
        public Sprite Get(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;

            if (!_lookup.TryGetValue(cardId, out var node))
                return null;

            // Move to front (most recently used)
            _lruList.Remove(node);
            _lruList.AddFirst(node);
            return node.Value.sprite;
        }

        /// <summary>
        /// サムネイルをキャッシュに追加する。メモリ上限を超える場合は古いものから破棄。
        /// </summary>
        public void Put(string cardId, Sprite sprite)
        {
            if (string.IsNullOrEmpty(cardId) || sprite == null) return;

            long size = EstimateSpriteSize(sprite);

            // Already cached — update and move to front
            if (_lookup.TryGetValue(cardId, out var existing))
            {
                _currentMemoryBytes -= existing.Value.sizeBytes;
                _lruList.Remove(existing);
                _lookup.Remove(cardId);
            }

            // Evict until we have room
            while (_currentMemoryBytes + size > MemoryLimitBytes || _lookup.Count >= MaxEntries)
            {
                if (_lruList.Count == 0) break;
                Evict();
            }

            var entry = new CacheEntry
            {
                cardId = cardId,
                sprite = sprite,
                sizeBytes = size
            };

            var node = _lruList.AddFirst(entry);
            _lookup[cardId] = node;
            _currentMemoryBytes += size;
        }

        /// <summary>
        /// キャッシュを全クリアする
        /// </summary>
        public void Clear()
        {
            _lruList.Clear();
            _lookup.Clear();
            _currentMemoryBytes = 0;
        }

        // ====================================================================
        // Internal
        // ====================================================================

        /// <summary>
        /// LRU の末尾（最も古い）エントリを破棄する
        /// </summary>
        void Evict()
        {
            var last = _lruList.Last;
            if (last == null) return;

            _lookup.Remove(last.Value.cardId);
            _currentMemoryBytes -= last.Value.sizeBytes;
            _lruList.RemoveLast();
        }

        /// <summary>
        /// Sprite のメモリ使用量を概算する (width × height × 4 bytes per pixel)
        /// </summary>
        public static long EstimateSpriteSize(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return 0;
            var tex = sprite.texture;
            return (long)tex.width * tex.height * 4;
        }
    }
}
