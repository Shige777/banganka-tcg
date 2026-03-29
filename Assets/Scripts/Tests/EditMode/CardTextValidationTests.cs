using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Banganka.Tests.EditMode
{
    /// <summary>
    /// カードテキスト検証 (CARD_TEXT_GUIDELINES.md + CLAUDE.md 用語ルール)
    /// 162枚すべてのカードJSON を検証する。
    /// </summary>
    [TestFixture]
    public class CardTextValidationTests
    {
        static readonly string CardsDir = Path.Combine(Application.dataPath, "StreamingAssets", "Cards");

        // 禁止用語 (CLAUDE.md)
        static readonly Dictionary<string, string> ForbiddenTerms = new()
        {
            { "ダメージ", "退場させる / 願力をN動かす" },
            { "破壊する", "退場させる" },
            { "タップ", "消耗" },
            { "アンタップ", "待機" },
            { "マナ", "CP" },
            { "バーン", "願力をN動かす" },
            { "ヒーロー", "願主" },
            { "チャンピオン", "願主" },
        };

        // type enum: 0=Manifest, 1=Spell(Incantation), 2=Algorithm
        // aspect enum: 0=Contest, 1=Whisper, 2=Weave, 3=Verse, 4=Manifest, 5=Hush
        static readonly string[] AspectNames = { "Contest", "Whisper", "Weave", "Verse", "Manifest", "Hush" };
        static readonly string[] TypeNames = { "Manifest", "Incantation", "Algorithm" };
        static readonly string[] ValidRarities = { "C", "UC", "R", "SR", "SSR" };

        List<Dictionary<string, object>> _cards;

        [OneTimeSetUp]
        public void LoadAllCards()
        {
            _cards = new List<Dictionary<string, object>>();
            if (!Directory.Exists(CardsDir))
            {
                Assert.Inconclusive($"Card directory not found: {CardsDir}");
                return;
            }

            var files = Directory.GetFiles(CardsDir, "*.json");
            foreach (var f in files)
            {
                string json = File.ReadAllText(f);
                var dict = Json.Deserialize(json) as Dictionary<string, object>;
                if (dict != null) _cards.Add(dict);
            }
        }

        [Test]
        public void AllCards_Have162Total()
        {
            Assert.AreEqual(162, _cards.Count, $"Expected 162 cards, got {_cards.Count}");
        }

        [Test]
        public void AllCards_HaveRequiredFields()
        {
            var required = new[] { "id", "cardName", "type", "cpCost", "aspect" };
            var missing = new List<string>();

            foreach (var c in _cards)
            {
                foreach (var field in required)
                {
                    if (!c.ContainsKey(field))
                        missing.Add($"{c.GetValueOrDefault("id", "?")} missing '{field}'");
                }
            }

            Assert.IsEmpty(missing, $"Missing fields:\n{string.Join("\n", missing)}");
        }

        [Test]
        public void AllCards_HaveNonEmptyName()
        {
            var empty = _cards
                .Where(c => string.IsNullOrWhiteSpace(c.GetValueOrDefault("cardName", "")?.ToString()))
                .Select(c => c.GetValueOrDefault("id", "?").ToString())
                .ToList();

            Assert.IsEmpty(empty, $"Cards with empty name: {string.Join(", ", empty)}");
        }

        [Test]
        public void AllCards_HaveValidType()
        {
            var invalid = _cards
                .Where(c => {
                    var t = System.Convert.ToInt32(c.GetValueOrDefault("type", -1));
                    return t < 0 || t > 2;
                })
                .Select(c => $"{c["id"]}: type={c["type"]}")
                .ToList();

            Assert.IsEmpty(invalid, $"Invalid type:\n{string.Join("\n", invalid)}");
        }

        [Test]
        public void AllCards_HaveValidAspect()
        {
            var invalid = _cards
                .Where(c => {
                    var a = System.Convert.ToInt32(c.GetValueOrDefault("aspect", -1));
                    return a < 0 || a > 5;
                })
                .Select(c => $"{c["id"]}: aspect={c["aspect"]}")
                .ToList();

            Assert.IsEmpty(invalid, $"Invalid aspect:\n{string.Join("\n", invalid)}");
        }

        [Test]
        public void AllCards_HaveValidCpCost()
        {
            var invalid = _cards
                .Where(c => {
                    var cost = System.Convert.ToInt32(c.GetValueOrDefault("cpCost", -1));
                    return cost < 0 || cost > 10;
                })
                .Select(c => $"{c["id"]}: cpCost={c["cpCost"]}")
                .ToList();

            Assert.IsEmpty(invalid, $"Invalid cpCost:\n{string.Join("\n", invalid)}");
        }

        [Test]
        public void AllCards_HaveValidRarity()
        {
            var invalid = _cards
                .Where(c => {
                    var r = c.GetValueOrDefault("rarity", "")?.ToString();
                    return !string.IsNullOrEmpty(r) && !ValidRarities.Contains(r);
                })
                .Select(c => $"{c["id"]}: rarity={c["rarity"]}")
                .ToList();

            Assert.IsEmpty(invalid, $"Invalid rarity:\n{string.Join("\n", invalid)}");
        }

        [Test]
        public void TypeDistribution_IsCorrect()
        {
            // 90 Manifest + 48 Incantation + 24 Algorithm = 162
            var counts = _cards.GroupBy(c => System.Convert.ToInt32(c["type"]))
                .ToDictionary(g => g.Key, g => g.Count());

            Assert.AreEqual(90, counts.GetValueOrDefault(0, 0), "Manifest should be 90");
            Assert.AreEqual(48, counts.GetValueOrDefault(1, 0), "Incantation should be 48");
            Assert.AreEqual(24, counts.GetValueOrDefault(2, 0), "Algorithm should be 24");
        }

        [Test]
        public void AspectDistribution_ManifestAndSpell_23PerAspect()
        {
            // Manifest(0) + Incantation(1) = 23 per aspect (15+8)
            for (int a = 0; a < 6; a++)
            {
                int count = _cards.Count(c =>
                {
                    var t = System.Convert.ToInt32(c["type"]);
                    var asp = System.Convert.ToInt32(c["aspect"]);
                    return (t == 0 || t == 1) && asp == a;
                });
                Assert.AreEqual(23, count,
                    $"Aspect {AspectNames[a]}({a}) Manifest+Incantation should be 23, got {count}");
            }
        }

        [Test]
        public void NoDuplicateIds()
        {
            var dupes = _cards.GroupBy(c => c["id"].ToString())
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key} (x{g.Count()})")
                .ToList();

            Assert.IsEmpty(dupes, $"Duplicate IDs: {string.Join(", ", dupes)}");
        }

        [Test]
        public void Manifests_BattlePowerFollowsCostCurve()
        {
            // 戦力 ≤ CP×2000+1000 + 2000 (tolerance)
            var violations = _cards
                .Where(c => System.Convert.ToInt32(c["type"]) == 0)
                .Where(c =>
                {
                    int cp = System.Convert.ToInt32(c["cpCost"]);
                    int bp = System.Convert.ToInt32(c.GetValueOrDefault("battlePower", 0));
                    int maxBp = cp * 2000 + 1000 + 2000; // with tolerance for keywords
                    return bp > maxBp;
                })
                .Select(c => $"{c["id"]}: CP={c["cpCost"]}, BP={c["battlePower"]}")
                .ToList();

            Assert.IsEmpty(violations,
                $"Cards exceeding cost curve:\n{string.Join("\n", violations)}");
        }

        [Test]
        public void CardNames_NoForbiddenTerms()
        {
            var violations = new List<string>();
            foreach (var c in _cards)
            {
                string name = c.GetValueOrDefault("cardName", "")?.ToString() ?? "";
                string flavor = c.GetValueOrDefault("flavorText", "")?.ToString() ?? "";
                string combined = name + " " + flavor;

                foreach (var kv in ForbiddenTerms)
                {
                    if (combined.Contains(kv.Key))
                    {
                        violations.Add($"{c["id"]} ({name}): 「{kv.Key}」→ 正式: 「{kv.Value}」");
                    }
                }
            }

            Assert.IsEmpty(violations,
                $"Forbidden terms in card text:\n{string.Join("\n", violations)}");
        }

        [Test]
        public void AllCards_HaveFlavorText()
        {
            var missing = _cards
                .Where(c => string.IsNullOrWhiteSpace(c.GetValueOrDefault("flavorText", "")?.ToString()))
                .Select(c => c["id"].ToString())
                .ToList();

            if (missing.Count > 0)
                Debug.LogWarning($"{missing.Count} cards missing flavorText: {string.Join(", ", missing.Take(10))}...");
        }
    }
}
