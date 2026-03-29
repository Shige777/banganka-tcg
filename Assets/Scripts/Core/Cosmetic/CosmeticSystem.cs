using System;
using System.Collections.Generic;
using UnityEngine;
using Banganka.Core.Data;

namespace Banganka.Core.Cosmetic
{
    public enum CosmeticType
    {
        Sleeve,     // カードスリーブ（裏面デザイン）
        FieldSkin,  // フィールドスキン（バトル背景）
        CardFrame,  // カードフレーム
    }

    /// <summary>
    /// コスメティックアイテムのマスターデータ
    /// </summary>
    [Serializable]
    public class CosmeticItem
    {
        public string id;          // e.g. "sleeve_contest_flame"
        public string displayName; // "炎のスリーブ"
        public CosmeticType type;
        public Aspect? aspect;     // アスペクト限定の場合
        public string rarity;     // "C","R","SR","SSR"
        public int goldPrice;     // 0 = 非売品（報酬のみ）
        public int premiumPrice;
        public string resourcePath; // Resources/ path
        public string unlockCondition; // "", "battlepass_10", "wins_50", etc.
    }

    /// <summary>
    /// コスメティック管理システム。
    /// 所持コスメティック・装備状態の管理。
    /// </summary>
    public static class CosmeticSystem
    {
        static Dictionary<string, CosmeticItem> _catalog;
        static bool _initialized;

        public static event Action OnEquipChanged;

        // デフォルトスリーブ・フィールド
        public const string DefaultSleeveId = "sleeve_default";
        public const string DefaultFieldId = "field_default";
        public const string DefaultFrameId = "frame_default";

        public static IReadOnlyDictionary<string, CosmeticItem> Catalog
        {
            get { if (!_initialized) Init(); return _catalog; }
        }

        /// <summary>現在装備中のスリーブID</summary>
        public static string EquippedSleeve
        {
            get => PlayerData.Instance.equippedSleeve ?? DefaultSleeveId;
            set
            {
                PlayerData.Instance.equippedSleeve = value;
                PlayerData.Save();
                OnEquipChanged?.Invoke();
            }
        }

        /// <summary>現在装備中のフィールドスキンID</summary>
        public static string EquippedField
        {
            get => PlayerData.Instance.equippedField ?? DefaultFieldId;
            set
            {
                PlayerData.Instance.equippedField = value;
                PlayerData.Save();
                OnEquipChanged?.Invoke();
            }
        }

        /// <summary>現在装備中のカードフレームID</summary>
        public static string EquippedFrame
        {
            get => PlayerData.Instance.equippedFrame ?? DefaultFrameId;
            set
            {
                PlayerData.Instance.equippedFrame = value;
                PlayerData.Save();
                OnEquipChanged?.Invoke();
            }
        }

        /// <summary>指定コスメティックを所持しているか</summary>
        public static bool Owns(string cosmeticId)
        {
            return PlayerData.Instance.ownedCosmetics.Contains(cosmeticId);
        }

        /// <summary>コスメティックを付与</summary>
        public static void Grant(string cosmeticId)
        {
            if (PlayerData.Instance.ownedCosmetics.Contains(cosmeticId)) return;
            PlayerData.Instance.ownedCosmetics.Add(cosmeticId);
            PlayerData.Save();
            Debug.Log($"[Cosmetic] Granted: {cosmeticId}");
        }

        /// <summary>ゴールドで購入</summary>
        public static bool PurchaseWithGold(string cosmeticId)
        {
            if (!_catalog.TryGetValue(cosmeticId, out var item)) return false;
            if (item.goldPrice <= 0) return false;
            if (Owns(cosmeticId)) return false;

            if (!PlayerData.Instance.TrySpend(item.goldPrice)) return false;
            Grant(cosmeticId);
            return true;
        }

        /// <summary>願晶で購入</summary>
        public static bool PurchaseWithPremium(string cosmeticId)
        {
            if (!_catalog.TryGetValue(cosmeticId, out var item)) return false;
            if (item.premiumPrice <= 0) return false;
            if (Owns(cosmeticId)) return false;

            if (!PlayerData.Instance.TrySpend(0, item.premiumPrice)) return false;
            Grant(cosmeticId);
            return true;
        }

        /// <summary>コスメティックを装備</summary>
        public static void Equip(string cosmeticId)
        {
            if (!Owns(cosmeticId) && !IsDefault(cosmeticId)) return;
            if (!_catalog.TryGetValue(cosmeticId, out var item)) return;

            switch (item.type)
            {
                case CosmeticType.Sleeve:
                    EquippedSleeve = cosmeticId;
                    break;
                case CosmeticType.FieldSkin:
                    EquippedField = cosmeticId;
                    break;
                case CosmeticType.CardFrame:
                    EquippedFrame = cosmeticId;
                    break;
            }
        }

        /// <summary>指定タイプのコスメティック一覧（所持済みフラグ付き）</summary>
        public static List<(CosmeticItem item, bool owned)> GetItemsOfType(CosmeticType type)
        {
            if (!_initialized) Init();
            var result = new List<(CosmeticItem, bool)>();
            foreach (var kv in _catalog)
            {
                if (kv.Value.type != type) continue;
                result.Add((kv.Value, Owns(kv.Key)));
            }
            return result;
        }

        static bool IsDefault(string id) =>
            id == DefaultSleeveId || id == DefaultFieldId || id == DefaultFrameId;

        static void Init()
        {
            _initialized = true;
            _catalog = new Dictionary<string, CosmeticItem>();

            // デフォルトアイテム
            RegisterDefaults();

            // アスペクト別スリーブ
            RegisterAspectSleeves();

            // フィールドスキン
            RegisterFieldSkins();

            // 初回プレイヤーにデフォルトを付与
            if (!Owns(DefaultSleeveId)) Grant(DefaultSleeveId);
            if (!Owns(DefaultFieldId)) Grant(DefaultFieldId);
            if (!Owns(DefaultFrameId)) Grant(DefaultFrameId);
        }

        static void RegisterDefaults()
        {
            _catalog[DefaultSleeveId] = new CosmeticItem
            {
                id = DefaultSleeveId, displayName = "標準スリーブ",
                type = CosmeticType.Sleeve, rarity = "C",
                resourcePath = "Cosmetics/Sleeves/default"
            };
            _catalog[DefaultFieldId] = new CosmeticItem
            {
                id = DefaultFieldId, displayName = "標準フィールド",
                type = CosmeticType.FieldSkin, rarity = "C",
                resourcePath = "Cosmetics/Fields/default"
            };
            _catalog[DefaultFrameId] = new CosmeticItem
            {
                id = DefaultFrameId, displayName = "標準フレーム",
                type = CosmeticType.CardFrame, rarity = "C",
                resourcePath = "Cosmetics/Frames/default"
            };
        }

        static void RegisterAspectSleeves()
        {
            var aspects = new[]
            {
                (Aspect.Contest, "曙のスリーブ",  2000, 20),
                (Aspect.Whisper, "空のスリーブ",  2000, 20),
                (Aspect.Weave,   "穏のスリーブ",  2000, 20),
                (Aspect.Verse,   "妖のスリーブ",  2000, 20),
                (Aspect.Manifest,"遊のスリーブ",  2000, 20),
                (Aspect.Hush,    "玄のスリーブ",  2000, 20),
            };

            foreach (var (aspect, name, gold, premium) in aspects)
            {
                string id = $"sleeve_{aspect.ToString().ToLower()}";
                _catalog[id] = new CosmeticItem
                {
                    id = id, displayName = name,
                    type = CosmeticType.Sleeve,
                    aspect = aspect, rarity = "R",
                    goldPrice = gold, premiumPrice = premium,
                    resourcePath = $"Cosmetics/Sleeves/{aspect.ToString().ToLower()}"
                };
            }
        }

        static void RegisterFieldSkins()
        {
            var fields = new[]
            {
                ("field_sakura",   "桜の庭園",   3000, 30, "R"),
                ("field_ocean",    "深海の神殿", 3000, 30, "R"),
                ("field_volcano",  "火山の闘技場", 5000, 50, "SR"),
                ("field_celestial","天界の座",   5000, 50, "SR"),
                ("field_void",     "虚無の間",   8000, 80, "SSR"),
            };

            foreach (var (id, name, gold, premium, rarity) in fields)
            {
                _catalog[id] = new CosmeticItem
                {
                    id = id, displayName = name,
                    type = CosmeticType.FieldSkin,
                    rarity = rarity,
                    goldPrice = gold, premiumPrice = premium,
                    resourcePath = $"Cosmetics/Fields/{id.Replace("field_", "")}"
                };
            }
        }
    }
}
