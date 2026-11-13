using NUnit.Framework;
using WallBurst.Depth;

namespace WallBurst.Tests
{
    /// <summary>
    /// 設置モードごとの模擬深度。
    /// </summary>
    public sealed class KinectMountTests
    {
        /// <summary>
        /// 見上げでは、画像の上が画像の下より遠い。
        /// </summary>
        [Test]
        public void Fill_LookingUpIsFartherAtTheTop()
        {
            ushort[] frame = QuietFrame(out SimulatedDepthGenerator generator);
            generator.LookingUp = true;
            generator.Fill(frame, 0);

            int top = frame[16];
            int bottom = frame[(23 * 32) + 16];
            Assert.Greater(top, bottom);
        }

        /// <summary>
        /// 壁と水平では、画像の上が画像の下より遠い。
        /// </summary>
        [Test]
        public void Fill_AlongWallIsFartherAtTheTop()
        {
            ushort[] frame = QuietFrame(out SimulatedDepthGenerator generator);
            generator.LookingUp = false;
            generator.Fill(frame, 0);

            int top = frame[16];
            int bottom = frame[(23 * 32) + 16];
            Assert.Greater(top, bottom);
        }

        /// <summary>
        /// 揺れと穴を止めた壁を用意する。
        /// </summary>
        private static ushort[] QuietFrame(out SimulatedDepthGenerator generator)
        {
            generator = new SimulatedDepthGenerator(32, 24);
            generator.NoiseAmplitudeMillimeters = 0;
            generator.EnableHoles = false;
            generator.EnableFlyingPixels = false;
            return new ushort[32 * 24];
        }
    }
}
