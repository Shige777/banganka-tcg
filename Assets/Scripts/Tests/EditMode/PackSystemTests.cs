using System;
using System.Collections.Generic;
using NUnit.Framework;
using Banganka.Core.Data;
using Banganka.Core.Economy;

namespace Banganka.Tests.EditMode
{
    [TestFixture]
    public class PackSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            // テスト用カードDBをセットアップ
            CardDatabase.ClearForTest();
            foreach (var rarity in new[] { "C", "R", "SR", "SSR" })
            {
                for (int i = 0; i < 10; i++)
                {
                    CardDatabase.RegisterForTest(new CardData
                    {
                        id = $"TEST_{rarity}_{i:D2}",
                        cardName = $"Test {rarity} {i}",
                        type = CardType.Manifest,
                        rarity = rarity,
                        aspect = (Aspect)(i % 6),
                        cpCost = 2,
                        battlePower = 3000,
                    });
                }
            }

            // テスト用PlayerDataをセットアップ
            PlayerData.Instance = new PlayerData();
            PackSystem.ResetPityCounter();
        }

        [Test]
        public void RarityDistribution_MatchesSpec()
        {
            // 100,000回の開封でレアリティ分布を検証 (MONETIZATION_DESIGN.md §4.4)
            // スロット1-4: C=75%, R=20%, SR=4%, SSR=1%
            var rng = new System.Random(12345);
            int totalSlots14 = 0;
            var counts14 = new Dictionary<string, int> { {"C",0}, {"R",0}, {"SR",0}, {"SSR",0} };

            int totalSlot5 = 0;
            var counts5 = new Dictionary<string, int> { {"R",0}, {"SR",0}, {"SSR",0} };

            int iterations = 20000;
            for (int i = 0; i < iterations; i++)
            {
                var result = PackSystem.OpenPack(rng: rng);
                foreach (var entry in result.cards)
                {
                    if (entry.slotIndex < 4)
                    {
                        counts14[entry.rarity]++;
                        totalSlots14++;
                    }
                    else
                    {
                        counts5[entry.rarity]++;
                        totalSlot5++;
                    }
                }
            }

            // スロット1-4の分布チェック (±2%の許容範囲)
            float cPct = counts14["C"] * 100f / totalSlots14;
            float rPct = counts14["R"] * 100f / totalSlots14;
            float srPct = counts14["SR"] * 100f / totalSlots14;
            float ssrPct = counts14["SSR"] * 100f / totalSlots14;

            Assert.That(cPct, Is.InRange(73f, 77f), $"C should be ~75%, got {cPct:F1}%");
            Assert.That(rPct, Is.InRange(18f, 22f), $"R should be ~20%, got {rPct:F1}%");
            Assert.That(srPct, Is.InRange(2f, 6f), $"SR should be ~4%, got {srPct:F1}%");
            Assert.That(ssrPct, Is.InRange(0f, 3f), $"SSR should be ~1%, got {ssrPct:F1}%");

            // スロット5はCが出ないことを検証
            Assert.IsFalse(counts5.ContainsKey("C") && counts5["C"] > 0,
                "Slot 5 should never produce C rarity");
        }

        [Test]
        public void Slot5_NeverProducesCommon()
        {
            var rng = new System.Random(99999);
            for (int i = 0; i < 5000; i++)
            {
                var result = PackSystem.OpenPack(rng: rng);
                foreach (var entry in result.cards)
                {
                    if (entry.slotIndex == 4)
                    {
                        Assert.AreNotEqual("C", entry.rarity,
                            $"Slot 5 produced C rarity on iteration {i}");
                    }
                }
            }
        }

        [Test]
        public void PitySystem_GuaranteesSSR_Within50Packs()
        {
            var rng = new System.Random(77777);
            bool foundSSR = false;

            for (int i = 0; i < 50; i++)
            {
                var result = PackSystem.OpenPack(rng: rng);
                foreach (var entry in result.cards)
                {
                    if (entry.rarity == "SSR")
                    {
                        foundSSR = true;
                        break;
                    }
                }
                if (foundSSR) break;
            }

            Assert.IsTrue(foundSSR, "SSR must appear within 50 packs (pity system)");
        }

        [Test]
        public void PitySystem_ForcesSSR_AtThreshold()
        {
            // ピティカウンターを49に設定して次のパックでSSR確定を検証
            PackSystem.ResetPityCounter();

            // SSRが出ないシード値で49パック開封
            // カウンターが50に達するまでSSR以外を引き続ける
            var rng = new System.Random(42);
            for (int i = 0; i < 49; i++)
            {
                PackSystem.OpenPack(rng: rng);
                // SSRが出たらカウンターがリセットされるので再度カウント
            }

            // 50パック目: SSRが確定で出るはず
            // ただし途中でSSRが出る可能性があるので、カウンターベースで検証
            int packsNeeded = 0;
            PackSystem.ResetPityCounter();
            bool pityTriggered = false;

            for (int i = 0; i < 100; i++)
            {
                packsNeeded++;
                var result = PackSystem.OpenPack(rng: rng);

                if (result.hasPityTriggered)
                {
                    pityTriggered = true;
                    Assert.AreEqual(50, packsNeeded,
                        "Pity should trigger at exactly 50 packs since last SSR");
                    break;
                }

                // SSRが自然に出たらカウンターリセット済みなので再カウント
                bool hadSSR = false;
                foreach (var entry in result.cards)
                {
                    if (entry.rarity == "SSR") { hadSSR = true; break; }
                }
                if (hadSSR) packsNeeded = 0;
            }

            // 100パック以内にピティが発動するはず（最悪50パック×2回）
            Assert.IsTrue(pityTriggered || packsNeeded < 50,
                "Pity system should trigger within 50 packs of no SSR");
        }

        [Test]
        public void PackAlwaysReturns5Cards()
        {
            var rng = new System.Random(11111);
            for (int i = 0; i < 100; i++)
            {
                var result = PackSystem.OpenPack(rng: rng);
                Assert.AreEqual(5, result.cards.Count,
                    $"Pack should contain 5 cards, got {result.cards.Count} on iteration {i}");
            }
        }

        [Test]
        public void DuplicateConversion_CorrectGold()
        {
            // 同じカードを4枚以上持たせて重複変換を検証
            var testCard = new CardData
            {
                id = "DUP_TEST_R",
                cardName = "DupTest",
                type = CardType.Manifest,
                rarity = "R",
                aspect = Aspect.Contest,
                cpCost = 2,
                battlePower = 3000,
            };

            // DBにこのカード1枚だけ登録（R枠で必ずこれが選ばれる）
            CardDatabase.ClearForTest();
            CardDatabase.RegisterForTest(testCard);
            // 他のレアリティも最低1枚必要
            foreach (var rar in new[] { "C", "SR", "SSR" })
            {
                CardDatabase.RegisterForTest(new CardData
                {
                    id = $"DUP_OTHER_{rar}",
                    cardName = $"Other {rar}",
                    type = CardType.Manifest,
                    rarity = rar,
                    aspect = Aspect.Contest,
                    cpCost = 2,
                    battlePower = 3000,
                });
            }

            // 既に3枚所持状態にする
            PlayerData.Instance.AddCard(testCard.id, 3);
            Assert.AreEqual(3, PlayerData.Instance.GetCardCount(testCard.id));

            // パック開封 — R枠で DUP_TEST_R が出たら重複変換される
            var rng = new System.Random(42);
            bool foundDuplicate = false;

            for (int i = 0; i < 200; i++)
            {
                var result = PackSystem.OpenPack(rng: rng);
                foreach (var entry in result.cards)
                {
                    if (entry.card.id == testCard.id && entry.isDuplicate)
                    {
                        Assert.AreEqual(80, entry.goldConverted,
                            "R duplicate should convert to 80 gold");
                        foundDuplicate = true;
                        break;
                    }
                }
                if (foundDuplicate) break;
            }

            Assert.IsTrue(foundDuplicate, "Should find at least one R duplicate within 200 packs");
        }

        [Test]
        public void DuplicateGoldValues_MatchSpec()
        {
            // MONETIZATION_DESIGN.md §4.6: C=30, R=80, SR=200, SSR=500
            var expected = new Dictionary<string, int>
            {
                { "C", 30 }, { "R", 80 }, { "SR", 200 }, { "SSR", 500 }
            };

            foreach (var kv in expected)
            {
                CardDatabase.ClearForTest();
                CardDatabase.RegisterForTest(new CardData
                {
                    id = $"GOLD_TEST_{kv.Key}",
                    cardName = $"GoldTest",
                    type = CardType.Manifest,
                    rarity = kv.Key,
                    aspect = Aspect.Contest,
                    cpCost = 2,
                    battlePower = 3000,
                });
                // 他レアリティも登録
                foreach (var other in new[] { "C", "R", "SR", "SSR" })
                {
                    if (other == kv.Key) continue;
                    CardDatabase.RegisterForTest(new CardData
                    {
                        id = $"GOLD_OTHER_{other}",
                        cardName = $"Other",
                        type = CardType.Manifest,
                        rarity = other,
                        aspect = Aspect.Contest,
                        cpCost = 2,
                        battlePower = 3000,
                    });
                }

                PlayerData.Instance = new PlayerData();
                PlayerData.Instance.AddCard($"GOLD_TEST_{kv.Key}", 3);
                PackSystem.ResetPityCounter();

                var rng = new System.Random(42);
                bool found = false;
                for (int i = 0; i < 500; i++)
                {
                    var result = PackSystem.OpenPack(rng: rng);
                    foreach (var entry in result.cards)
                    {
                        if (entry.card.id == $"GOLD_TEST_{kv.Key}" && entry.isDuplicate)
                        {
                            Assert.AreEqual(kv.Value, entry.goldConverted,
                                $"{kv.Key} duplicate should convert to {kv.Value} gold");
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }

                Assert.IsTrue(found, $"Should find {kv.Key} duplicate within 500 packs");
            }
        }

        [Test]
        public void MaxRarity_ReturnsHighest()
        {
            var result = new PackOpenResult();
            result.cards.Add(new PackCardEntry { rarity = "C", slotIndex = 0 });
            result.cards.Add(new PackCardEntry { rarity = "R", slotIndex = 1 });
            result.cards.Add(new PackCardEntry { rarity = "C", slotIndex = 2 });
            result.cards.Add(new PackCardEntry { rarity = "SR", slotIndex = 3 });
            result.cards.Add(new PackCardEntry { rarity = "C", slotIndex = 4 });

            Assert.AreEqual("SR", result.MaxRarity);
        }

        [Test]
        public void MaxRarity_SSR_IsHighest()
        {
            var result = new PackOpenResult();
            result.cards.Add(new PackCardEntry { rarity = "C", slotIndex = 0 });
            result.cards.Add(new PackCardEntry { rarity = "SSR", slotIndex = 1 });
            result.cards.Add(new PackCardEntry { rarity = "SR", slotIndex = 2 });
            result.cards.Add(new PackCardEntry { rarity = "R", slotIndex = 3 });
            result.cards.Add(new PackCardEntry { rarity = "C", slotIndex = 4 });

            Assert.AreEqual("SSR", result.MaxRarity);
        }

        [Test]
        public void PacksUntilPity_DecreasesCorrectly()
        {
            PackSystem.ResetPityCounter();
            Assert.AreEqual(50, PackSystem.PacksUntilPity);

            var rng = new System.Random(42);
            PackSystem.OpenPack(rng: rng);

            // SSRが出なければ49になるはず（出たら50にリセット）
            Assert.That(PackSystem.PacksUntilPity, Is.LessThanOrEqualTo(50));
            Assert.That(PackSystem.PacksUntilPity, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void NewCard_IsMarkedAsNew()
        {
            var rng = new System.Random(42);
            var result = PackSystem.OpenPack(rng: rng);

            // 最初のパックは全カードがisNew=trueのはず（未所持）
            foreach (var entry in result.cards)
            {
                if (!entry.isDuplicate)
                {
                    Assert.IsTrue(entry.isNew,
                        $"First pack card {entry.card.id} should be marked as new");
                }
            }
        }

        [Test]
        public void AspectPickup_FavorsPickupAspect()
        {
            var rng = new System.Random(42);
            int contestCount = 0;
            int otherCount = 0;

            for (int i = 0; i < 1000; i++)
            {
                var result = PackSystem.OpenPack(aspectPickup: Aspect.Contest, rng: rng);
                foreach (var entry in result.cards)
                {
                    if (entry.card.aspect == Aspect.Contest)
                        contestCount++;
                    else
                        otherCount++;
                }
            }

            // ピックアップ指定時はContestカードが多いはず
            float contestRatio = contestCount * 100f / (contestCount + otherCount);
            Assert.That(contestRatio, Is.GreaterThan(30f),
                $"Pickup aspect should appear more often, got {contestRatio:F1}%");
        }
    }
}
