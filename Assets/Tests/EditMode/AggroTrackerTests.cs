using Hangeul.App;
using NUnit.Framework;

namespace Hangeul.Tests
{
    public class AggroTrackerTests
    {
        [Test]
        public void StartsIdle_AndNoticesInsideDetectRange()
        {
            var a = new AggroTracker(1.5f, 2.5f);
            Assert.IsFalse(a.Update(3f));
            Assert.IsFalse(a.Update(1.6f));
            Assert.IsTrue(a.Update(1.5f));        // 감지 거리 안(경계 포함)
        }

        [Test]
        public void KeepsChasingUntilBeyondLoseRange()
        {
            var a = new AggroTracker(1.5f, 2.5f);
            a.Update(1f);
            Assert.IsTrue(a.Update(2f));          // 감지 거리 밖이어도 포기 거리 안이면 계속 쫓는다
            Assert.IsTrue(a.Update(2.5f));
            Assert.IsFalse(a.Update(2.6f));       // 포기 거리를 넘으면 놓친다
            Assert.IsFalse(a.Update(2f));         // 놓친 뒤에는 다시 감지 거리 안으로 와야 쫓는다
            Assert.IsTrue(a.Update(1f));
        }

        [Test]
        public void DeadTarget_DropsAggroImmediately()
        {
            var a = new AggroTracker(1.5f, 2.5f);
            a.Update(0.5f);
            Assert.IsFalse(a.Update(0.5f, targetAlive: false));
            Assert.IsFalse(a.Update(0.5f, targetAlive: false));
        }

        [Test]
        public void LoseRange_CannotBeSmallerThanDetectRange()
        {
            var a = new AggroTracker(2f, 1f);
            Assert.AreEqual(2f, a.LoseRange);
            Assert.IsTrue(a.Update(2f));
            Assert.IsTrue(a.Update(2f));          // 같은 거리에서 깜빡이지 않는다
        }

        [Test]
        public void SetRanges_TakesEffectImmediately()
        {
            var a = new AggroTracker(1f, 2f);
            Assert.IsFalse(a.Update(2.5f));
            a.SetRanges(3f, 4f);                  // 감지 거리를 늘리면 바로 그 거리에서 알아챈다
            Assert.IsTrue(a.Update(2.5f));
            a.SetRanges(1f, 2f);                  // 줄이면 포기 거리 밖이 되어 놓친다
            Assert.IsFalse(a.Update(2.5f));
            a.SetRanges(2f, 1f);
            Assert.AreEqual(2f, a.LoseRange);     // 포기 거리는 감지 거리보다 작아지지 않는다
        }

        [Test]
        public void Reset_Forgets()
        {
            var a = new AggroTracker(1f, 2f);
            a.Update(0.5f);
            a.Reset();
            Assert.IsFalse(a.Active);
        }
    }
}
