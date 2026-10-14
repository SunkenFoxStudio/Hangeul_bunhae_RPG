using Hangeul.App;
using NUnit.Framework;

namespace Hangeul.Tests
{
    public class MeleeAttackTests
    {
        [Test]
        public void Timeline_PhasesFollowTheClock()
        {
            var t = new AttackTimeline(0.1f, 0.2f, 0.3f);
            Assert.AreEqual(0.6f, t.Total, 1e-5f);
            Assert.AreEqual(AttackPhase.Windup, t.PhaseAt(0f));
            Assert.AreEqual(AttackPhase.Windup, t.PhaseAt(0.099f));
            Assert.AreEqual(AttackPhase.Active, t.PhaseAt(0.1f));
            Assert.AreEqual(AttackPhase.Active, t.PhaseAt(0.299f));
            Assert.AreEqual(AttackPhase.Recovery, t.PhaseAt(0.3f));
            Assert.AreEqual(AttackPhase.Recovery, t.PhaseAt(0.599f));
            Assert.AreEqual(AttackPhase.Done, t.PhaseAt(0.6f));
            Assert.AreEqual(AttackPhase.Done, t.PhaseAt(5f));
        }

        [Test]
        public void Timeline_ClampsBadValues()
        {
            var t = new AttackTimeline(-1f, 0f, -2f);
            Assert.AreEqual(0f, t.Windup);
            Assert.AreEqual(0.01f, t.Active, 1e-6f);       // 판정이 최소한 켜져 있어야 한다
            Assert.AreEqual(0f, t.Recovery);
            Assert.AreEqual(AttackPhase.Active, t.PhaseAt(0f));   // 준비가 없으면 바로 판정
        }

        static void AssertBox((float cx, float cy, float w, float h) actual, float cx, float cy, float w, float h)
        {
            Assert.AreEqual(cx, actual.cx, 1e-5f);
            Assert.AreEqual(cy, actual.cy, 1e-5f);
            Assert.AreEqual(w, actual.w, 1e-5f);
            Assert.AreEqual(h, actual.h, 1e-5f);
        }

        [Test]
        public void Box_ExtendsInFrontOfTheOrigin_ForEachDirection()
        {
            // 오른쪽: 가로 reach, 세로 width, 중심은 앞쪽으로 reach/2
            AssertBox(MeleeBox.Compute(1f, 0f, 1f, 2f, 0.4f, 0.3f), 1.2f, 2f, 0.4f, 0.3f);
            AssertBox(MeleeBox.Compute(-1f, 0f, 1f, 2f, 0.4f, 0.3f), 0.8f, 2f, 0.4f, 0.3f);   // 왼쪽
            AssertBox(MeleeBox.Compute(0f, 1f, 1f, 2f, 0.4f, 0.3f), 1f, 2.2f, 0.3f, 0.4f);    // 위쪽: 세로 reach, 가로 width
            AssertBox(MeleeBox.Compute(0f, -1f, 1f, 2f, 0.4f, 0.3f), 1f, 1.8f, 0.3f, 0.4f);   // 아래쪽
        }

        [Test]
        public void Box_NegativeSizesBecomeZero()
        {
            AssertBox(MeleeBox.Compute(1f, 0f, 0f, 0f, -1f, -1f), 0f, 0f, 0f, 0f);
        }
    }
}
