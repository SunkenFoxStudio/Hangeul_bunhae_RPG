using Hangeul.App;
using NUnit.Framework;

namespace Hangeul.Tests
{
    public class HitPointsTests
    {
        [Test]
        public void StartsFull_AndClampsInitialValues()
        {
            Assert.AreEqual((6, 6), (new HitPoints(6).Current, new HitPoints(6).Max));
            Assert.AreEqual(3, new HitPoints(6, 3).Current);
            Assert.AreEqual(6, new HitPoints(6, 99).Current);
            Assert.AreEqual(0, new HitPoints(6, -5).Current);
            Assert.AreEqual(1, new HitPoints(0).Max);          // 최대 체력은 최소 1
        }

        [Test]
        public void Damage_SubtractsAndReportsTheActualAmount()
        {
            var hp = new HitPoints(6);
            Assert.AreEqual(1, hp.Damage(1));
            Assert.AreEqual(5, hp.Current);
            Assert.AreEqual(2, hp.Damage(2));
            Assert.AreEqual(3, hp.Current);
            Assert.IsFalse(hp.IsDead);
        }

        [Test]
        public void Damage_CannotGoBelowZero_AndKills()
        {
            var hp = new HitPoints(3);
            Assert.AreEqual(3, hp.Damage(10));        // 남은 체력만큼만 깎인다
            Assert.AreEqual(0, hp.Current);
            Assert.IsTrue(hp.IsDead);
            Assert.AreEqual(0, hp.Damage(1));         // 죽은 뒤에는 더 깎이지 않는다
        }

        [Test]
        public void NonPositiveDamage_DoesNothing()
        {
            var hp = new HitPoints(4);
            Assert.AreEqual(0, hp.Damage(0));
            Assert.AreEqual(0, hp.Damage(-3));
            Assert.AreEqual(4, hp.Current);
        }

        [Test]
        public void Heal_ClampsAtMax_AndNeverRevives()
        {
            var hp = new HitPoints(6, 2);
            Assert.AreEqual(3, hp.Heal(3));
            Assert.AreEqual(1, hp.Heal(5));           // 최대치까지만
            Assert.AreEqual(6, hp.Current);
            Assert.IsTrue(hp.IsFull);
            Assert.AreEqual(0, hp.Heal(1));

            var dead = new HitPoints(6, 1);
            dead.Damage(1);
            Assert.AreEqual(0, dead.Heal(3));         // 죽은 뒤 회복으로는 살아나지 않는다
            Assert.IsTrue(dead.IsDead);
        }

        [Test]
        public void Revive_RestoresFull_OrTheGivenAmount()
        {
            var hp = new HitPoints(6);
            hp.Damage(6);
            hp.Revive();
            Assert.AreEqual(6, hp.Current);

            hp.Damage(6);
            hp.Revive(2);
            Assert.AreEqual(2, hp.Current);
            Assert.IsFalse(hp.IsDead);

            hp.Damage(2);
            hp.Revive(0);                              // 되살리는데 0이면 1로 시작
            Assert.AreEqual(1, hp.Current);
        }

        [Test]
        public void SetMax_FillsOrClampsCurrent()
        {
            var hp = new HitPoints(6, 4);
            hp.SetMax(8);
            Assert.AreEqual((8, 4), (hp.Max, hp.Current));
            hp.SetMax(8, fill: true);
            Assert.AreEqual(8, hp.Current);
            hp.SetMax(3);
            Assert.AreEqual((3, 3), (hp.Max, hp.Current));   // 줄이면 현재 체력도 넘지 않게
        }
    }
}
