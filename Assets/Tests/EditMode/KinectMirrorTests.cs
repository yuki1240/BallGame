using NUnit.Framework;
using WallBurst.Depth;

namespace WallBurst.Tests
{
    /// <summary>
    /// Kinect 深度の左右反転。
    /// </summary>
    public sealed class KinectMirrorTests
    {
        /// <summary>
        /// 行の左右が入れ替わる。
        /// </summary>
        [Test]
        public void MirrorHorizontal_SwapsLeftAndRight()
        {
            ushort[] pixels = { 1, 2, 3, 4, 5, 6 };
            KinectV2DepthSource.MirrorHorizontal(pixels, 3, 2);

            Assert.AreEqual(3, pixels[0]);
            Assert.AreEqual(2, pixels[1]);
            Assert.AreEqual(1, pixels[2]);
            Assert.AreEqual(6, pixels[3]);
            Assert.AreEqual(5, pixels[4]);
            Assert.AreEqual(4, pixels[5]);
        }
    }
}
