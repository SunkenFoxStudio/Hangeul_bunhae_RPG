using Hangeul.App;
using NUnit.Framework;

namespace Hangeul.Tests
{
    public class HeartDisplayTests
    {
        const HeartState F = HeartState.Full, H = HeartState.Half, E = HeartState.Empty;

        [Test]
        public void HeartCount_IsHalfTheMaxHealth_RoundedUp()
        {
            Assert.AreEqual(3, HeartDisplay.Count(6));
            Assert.AreEqual(3, HeartDisplay.Count(5));
            Assert.AreEqual(1, HeartDisplay.Count(1));
            Assert.AreEqual(0, HeartDisplay.Count(0));
            Assert.AreEqual(10, HeartDisplay.Count(20));
        }

        [Test]
        public void FullHealth_AllFull()
            => CollectionAssert.AreEqual(new[] { F, F, F }, HeartDisplay.Compute(6, 6));

        [Test]
        public void EachMissingPoint_EmptiesFromTheRightInHalves()
        {
            CollectionAssert.AreEqual(new[] { F, F, H }, HeartDisplay.Compute(5, 6));
            CollectionAssert.AreEqual(new[] { F, F, E }, HeartDisplay.Compute(4, 6));
            CollectionAssert.AreEqual(new[] { F, H, E }, HeartDisplay.Compute(3, 6));
            CollectionAssert.AreEqual(new[] { F, E, E }, HeartDisplay.Compute(2, 6));
            CollectionAssert.AreEqual(new[] { H, E, E }, HeartDisplay.Compute(1, 6));
            CollectionAssert.AreEqual(new[] { E, E, E }, HeartDisplay.Compute(0, 6));
        }

        [Test]
        public void OutOfRangeCurrent_IsClamped()
        {
            CollectionAssert.AreEqual(new[] { F, F, F }, HeartDisplay.Compute(99, 6));
            CollectionAssert.AreEqual(new[] { E, E, E }, HeartDisplay.Compute(-3, 6));
        }

        [Test]
        public void OddMaxHealth_LastHeartHoldsOnePoint()
        {
            CollectionAssert.AreEqual(new[] { F, F, F }, HeartDisplay.Compute(5, 5));   // 가득 찼으면 마지막 하트도 꽉 참
            CollectionAssert.AreEqual(new[] { F, F, E }, HeartDisplay.Compute(4, 5));
            CollectionAssert.AreEqual(new[] { F, H, E }, HeartDisplay.Compute(3, 5));
        }

        [Test]
        public void NoMaxHealth_NoHearts() => Assert.IsEmpty(HeartDisplay.Compute(0, 0));

        [Test]
        public void CustomHeartSize()
            => CollectionAssert.AreEqual(new[] { F, E }, HeartDisplay.Compute(1, 2, hpPerHeart: 1));   // 하트 한 칸 = 체력 1
    }
}
