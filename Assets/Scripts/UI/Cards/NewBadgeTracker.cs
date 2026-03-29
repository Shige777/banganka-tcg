using System;
using System.Collections.Generic;
using UnityEngine;

namespace Banganka.UI.Cards
{
    /// <summary>
    /// Tracks which cards have been viewed after acquisition.
    /// Shows "NEW" badge on unviewed cards in collection.
    /// Uses PlayerPrefs to persist viewed state.
    /// </summary>
    public static class NewBadgeTracker
    {
        const string KeyPrefix = "card_viewed_";
        const string AllCardsKey = "card_viewed_all_ids";

        static HashSet<string> _viewedCards;
        static HashSet<string> _ownedCardIds;

        public static event Action OnNewCountChanged;

        /// <summary>
        /// Load all viewed states from PlayerPrefs.
        /// Must be called once at startup before other methods.
        /// </summary>
        public static void Initialize()
        {
            _viewedCards = new HashSet<string>();
            _ownedCardIds = new HashSet<string>();

            string allIds = PlayerPrefs.GetString(AllCardsKey, "");
            if (!string.IsNullOrEmpty(allIds))
            {
                string[] ids = allIds.Split(',');
                foreach (string id in ids)
                {
                    if (string.IsNullOrEmpty(id))
                        continue;

                    if (PlayerPrefs.GetInt(KeyPrefix + id, 0) == 1)
                        _viewedCards.Add(id);
                }
            }
        }

        /// <summary>
        /// Register a card as owned. Call this when the player acquires a card.
        /// </summary>
        public static void RegisterOwnedCard(string cardId)
        {
            if (_ownedCardIds == null)
                Initialize();

            if (_ownedCardIds.Add(cardId))
            {
                SaveOwnedIds();
                OnNewCountChanged?.Invoke();
            }
        }

        /// <summary>
        /// Register multiple owned cards at once (e.g. on collection load).
        /// </summary>
        public static void RegisterOwnedCards(IEnumerable<string> cardIds)
        {
            if (_ownedCardIds == null)
                Initialize();

            bool changed = false;
            foreach (string id in cardIds)
            {
                if (_ownedCardIds.Add(id))
                    changed = true;
            }

            if (changed)
            {
                SaveOwnedIds();
                OnNewCountChanged?.Invoke();
            }
        }

        /// <summary>
        /// Mark a card as seen so the NEW badge no longer appears.
        /// </summary>
        public static void MarkAsViewed(string cardId)
        {
            if (_viewedCards == null)
                Initialize();

            if (_viewedCards.Add(cardId))
            {
                PlayerPrefs.SetInt(KeyPrefix + cardId, 1);
                PlayerPrefs.Save();
                OnNewCountChanged?.Invoke();
            }
        }

        /// <summary>
        /// Returns true if the card is owned but has not yet been viewed.
        /// </summary>
        public static bool IsNew(string cardId)
        {
            if (_viewedCards == null)
                Initialize();

            return _ownedCardIds.Contains(cardId) && !_viewedCards.Contains(cardId);
        }

        /// <summary>
        /// Total number of owned cards that have not been viewed.
        /// </summary>
        public static int GetNewCount()
        {
            if (_viewedCards == null)
                Initialize();

            int count = 0;
            foreach (string id in _ownedCardIds)
            {
                if (!_viewedCards.Contains(id))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Mark all currently owned cards as viewed.
        /// </summary>
        public static void MarkAllViewed()
        {
            if (_viewedCards == null)
                Initialize();

            bool changed = false;
            foreach (string id in _ownedCardIds)
            {
                if (_viewedCards.Add(id))
                {
                    PlayerPrefs.SetInt(KeyPrefix + id, 1);
                    changed = true;
                }
            }

            if (changed)
            {
                PlayerPrefs.Save();
                OnNewCountChanged?.Invoke();
            }
        }

        static void SaveOwnedIds()
        {
            string joined = string.Join(",", _ownedCardIds);
            PlayerPrefs.SetString(AllCardsKey, joined);
            PlayerPrefs.Save();
        }
    }
}
