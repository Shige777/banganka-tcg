using NUnit.Framework;
using Banganka.Core.Game;
using Banganka.Core.Data;

namespace Banganka.Tests.EditMode
{
    [TestFixture]
    public class MechanicUnlockTests
    {
        [TearDown]
        public void TearDown()
        {
            MechanicUnlockManager.SetTotalGamesOverride(null);
        }

        [Test]
        public void ZeroGames_OnlyBasicsUnlocked()
        {
            MechanicUnlockManager.SetTotalGamesOverride(0);
            Assert.IsTrue(MechanicUnlockManager.IsUnlocked(MechanicType.Summon));
            Assert.IsTrue(MechanicUnlockManager.IsUnlocked(MechanicType.Attack));
            Assert.IsTrue(MechanicUnlockManager.IsUnlocked(MechanicType.Block));
            Assert.IsTrue(MechanicUnlockManager.IsUnlocked(MechanicType.EndTurn));
            Assert.IsFalse(MechanicUnlockManager.IsUnlocked(MechanicType.Incantation));
            Assert.IsFalse(MechanicUnlockManager.IsUnlocked(MechanicType.CpManagement));
            Assert.IsFalse(MechanicUnlockManager.IsUnlocked(MechanicType.Algorithm));
            Assert.IsFalse(MechanicUnlockManager.IsUnlocked(MechanicType.LeaderEvo));
            Assert.IsFalse(MechanicUnlockManager.IsUnlocked(MechanicType.Ambush));
            Assert.IsFalse(MechanicUnlockManager.IsUnlocked(MechanicType.AspectSynergy));
        }

        [Test]
        public void OneGame_UnlocksSpellAndCp()
        {
            MechanicUnlockManager.SetTotalGamesOverride(1);
            Assert.IsTrue(MechanicUnlockManager.IsUnlocked(MechanicType.Incantation));
            Assert.IsTrue(MechanicUnlockManager.IsUnlocked(MechanicType.CpManagement));
            Assert.IsFalse(MechanicUnlockManager.IsUnlocked(MechanicType.Algorithm));
            Assert.IsFalse(MechanicUnlockManager.IsUnlocked(MechanicType.LeaderEvo));
        }

        [Test]
        public void FourGames_UnlocksAlgorithmAndEvo()
        {
            MechanicUnlockManager.SetTotalGamesOverride(4);
            Assert.IsTrue(MechanicUnlockManager.IsUnlocked(MechanicType.Incantation));
            Assert.IsTrue(MechanicUnlockManager.IsUnlocked(MechanicType.Algorithm));
            Assert.IsTrue(MechanicUnlockManager.IsUnlocked(MechanicType.LeaderEvo));
            Assert.IsFalse(MechanicUnlockManager.IsUnlocked(MechanicType.Ambush));
            Assert.IsFalse(MechanicUnlockManager.IsUnlocked(MechanicType.AspectSynergy));
        }

        [Test]
        public void SixGames_UnlocksAdvanced()
        {
            MechanicUnlockManager.SetTotalGamesOverride(6);
            Assert.IsTrue(MechanicUnlockManager.IsUnlocked(MechanicType.Ambush));
            Assert.IsTrue(MechanicUnlockManager.IsUnlocked(MechanicType.AspectSynergy));
        }

        [Test]
        public void NineGames_AllUnlocked()
        {
            MechanicUnlockManager.SetTotalGamesOverride(9);
            Assert.IsTrue(MechanicUnlockManager.AllUnlocked);
        }

        [Test]
        public void GamesUntilNextUnlock_Correct()
        {
            MechanicUnlockManager.SetTotalGamesOverride(0);
            Assert.AreEqual(1, MechanicUnlockManager.GamesUntilNextUnlock());

            MechanicUnlockManager.SetTotalGamesOverride(2);
            Assert.AreEqual(2, MechanicUnlockManager.GamesUntilNextUnlock()); // 4-2=2

            MechanicUnlockManager.SetTotalGamesOverride(5);
            Assert.AreEqual(1, MechanicUnlockManager.GamesUntilNextUnlock()); // 6-5=1

            MechanicUnlockManager.SetTotalGamesOverride(9);
            Assert.AreEqual(0, MechanicUnlockManager.GamesUntilNextUnlock());
        }

        [Test]
        public void CardTypeLocked_SpellLockedAtZero()
        {
            MechanicUnlockManager.SetTotalGamesOverride(0);
            Assert.IsTrue(MechanicUnlockManager.IsCardTypeLocked(CardType.Spell));
            Assert.IsTrue(MechanicUnlockManager.IsCardTypeLocked(CardType.Algorithm));
            Assert.IsFalse(MechanicUnlockManager.IsCardTypeLocked(CardType.Manifest));
        }

        [Test]
        public void CardTypeLocked_SpellUnlockedAtOne()
        {
            MechanicUnlockManager.SetTotalGamesOverride(1);
            Assert.IsFalse(MechanicUnlockManager.IsCardTypeLocked(CardType.Spell));
            Assert.IsTrue(MechanicUnlockManager.IsCardTypeLocked(CardType.Algorithm));
        }

        [Test]
        public void CardTypeLocked_AlgorithmUnlockedAtFour()
        {
            MechanicUnlockManager.SetTotalGamesOverride(4);
            Assert.IsFalse(MechanicUnlockManager.IsCardTypeLocked(CardType.Spell));
            Assert.IsFalse(MechanicUnlockManager.IsCardTypeLocked(CardType.Algorithm));
        }

        [Test]
        public void NalMessage_CorrectPerStage()
        {
            MechanicUnlockManager.SetTotalGamesOverride(0);
            Assert.IsNull(MechanicUnlockManager.GetNalMessage());

            MechanicUnlockManager.SetTotalGamesOverride(2);
            Assert.AreEqual("詠術カードを使ってみよう！", MechanicUnlockManager.GetNalMessage());

            MechanicUnlockManager.SetTotalGamesOverride(5);
            Assert.AreEqual("界律を置くとフィールドが変わるよ！", MechanicUnlockManager.GetNalMessage());

            MechanicUnlockManager.SetTotalGamesOverride(7);
            Assert.AreEqual("上級テクニックを伝授するよ！", MechanicUnlockManager.GetNalMessage());

            MechanicUnlockManager.SetTotalGamesOverride(10);
            Assert.AreEqual("君はもう一人前だ！", MechanicUnlockManager.GetNalMessage());
        }
    }
}
