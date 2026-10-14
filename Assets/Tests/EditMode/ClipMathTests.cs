using Hangeul.App;
using NUnit.Framework;

namespace Hangeul.Tests
{
    public class ClipMathTests
    {
        [Test]
        public void Looping_WrapsAround()
        {
            // 10fps, 4프레임: 0.0~0.1 → 0, ... 0.3~0.4 → 3, 0.4 → 다시 0
            Assert.AreEqual(0, ClipMath.FrameIndex(0f, 10f, 4, true));
            Assert.AreEqual(1, ClipMath.FrameIndex(0.11f, 10f, 4, true));
            Assert.AreEqual(3, ClipMath.FrameIndex(0.39f, 10f, 4, true));
            Assert.AreEqual(0, ClipMath.FrameIndex(0.41f, 10f, 4, true));
            Assert.AreEqual(2, ClipMath.FrameIndex(1.05f, 10f, 4, true));
        }

        [Test]
        public void NonLooping_HoldsTheLastFrame()
        {
            Assert.AreEqual(3, ClipMath.FrameIndex(0.39f, 10f, 4, false));
            Assert.AreEqual(3, ClipMath.FrameIndex(0.41f, 10f, 4, false));
            Assert.AreEqual(3, ClipMath.FrameIndex(99f, 10f, 4, false));
        }

        [Test]
        public void DegenerateInputs_AreSafe()
        {
            Assert.AreEqual(0, ClipMath.FrameIndex(1f, 10f, 0, true));          // 프레임이 없다
            Assert.AreEqual(0, ClipMath.FrameIndex(1f, 10f, 1, true));          // 한 장이면 늘 0
            Assert.AreEqual(0, ClipMath.FrameIndex(-1f, 10f, 4, false));        // 음수 시간
            Assert.AreEqual(0, ClipMath.FrameIndex(0.5f, 0f, 4, false));        // fps 0은 최소값으로 바뀐다(거의 멈춤)
        }

        [Test]
        public void FpsToFit_PlaysTheWholeClipInTheGivenTime()
        {
            float fps = ClipMath.FpsToFit(8, 0.4f);
            Assert.AreEqual(20f, fps, 1e-4f);
            // 정확히 0.4초에 마지막 프레임에 닿고(0.399초는 7번째), 넘으면 머문다
            Assert.AreEqual(7, ClipMath.FrameIndex(0.399f, fps, 8, false));
            Assert.AreEqual(0, ClipMath.FrameIndex(0f, fps, 8, false));
            Assert.AreEqual(0f, ClipMath.FpsToFit(0, 1f));
            Assert.AreEqual(0f, ClipMath.FpsToFit(8, 0f));
        }
    }
}
