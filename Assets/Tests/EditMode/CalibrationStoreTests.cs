using System.IO;
using NUnit.Framework;
using UnityEngine;
using WallBurst.Depth;
using WallBurst.Detection;
using WallBurst.Session;

namespace WallBurst.Tests
{
    /// <summary>
    /// キャリブレーションの保存と復元。
    /// </summary>
    public sealed class CalibrationStoreTests
    {
        /// <summary>
        /// 四隅、基準、しきい値、左右反転、入力が同じ値で戻る。
        /// </summary>
        [Test]
        public void SaveTo_RoundTripsCornersBaselineAndTuning()
        {
            string path = Path.Combine(Path.GetTempPath(), "wall-burst-calibration-test.bin");
            DetectionTuning tuning = new DetectionTuning();
            tuning.NoiseFloorMillimeters = 60;
            tuning.MaxApproachMillimeters = 500;
            tuning.MaxBlobArea = 3200;
            ushort[] baseline = { 1000, 1100, 1200, 1300 };
            CalibrationData source = new CalibrationData(2, 2, baseline, tuning);
            source.SetCorner(0, new Vector2(3f, 4f));
            source.SetCorner(1, new Vector2(18f, 5f));
            source.SetCorner(2, new Vector2(17f, 20f));
            source.SetCorner(3, new Vector2(2f, 19f));

            CalibrationStore.SaveTo(path, source, false, true);
            bool loaded = CalibrationStore.TryLoadFrom(path, out CalibrationData restored, out bool mirror, out bool useKinect);

            Assert.IsTrue(loaded);
            Assert.IsFalse(mirror);
            Assert.IsTrue(useKinect);
            Assert.AreEqual(2, restored.Width);
            Assert.AreEqual(2, restored.Height);
            Assert.AreEqual(1000, restored.Baseline[0]);
            Assert.AreEqual(1300, restored.Baseline[3]);
            Assert.IsTrue(restored.HasAllCorners);
            Assert.AreEqual(18f, restored.Corners[1].x);
            Assert.AreEqual(19f, restored.Corners[3].y);
            Assert.AreEqual(60, restored.Tuning.NoiseFloorMillimeters);
            Assert.AreEqual(500, restored.Tuning.MaxApproachMillimeters);
            Assert.AreEqual(3200, restored.Tuning.MaxBlobArea);

            bool[] effects = { true, false, true, false };
            CalibrationStore.SaveTo(path, source, false, true, effects, KinectMount.LookingUp);
            loaded = CalibrationStore.TryLoadFrom(path, out restored, out mirror, out useKinect, out bool[] restoredEffects, out KinectMount restoredMount);
            Assert.IsTrue(loaded);
            Assert.IsTrue(restoredEffects[0]);
            Assert.IsFalse(restoredEffects[1]);
            Assert.IsTrue(restoredEffects[2]);
            Assert.IsFalse(restoredEffects[3]);
            Assert.AreEqual(KinectMount.LookingUp, restoredMount);
            File.Delete(path);
        }

        /// <summary>
        /// ファイルが無いときは復元しない。
        /// </summary>
        [Test]
        public void TryLoadFrom_MissingFileReturnsFalse()
        {
            string path = Path.Combine(Path.GetTempPath(), "wall-burst-calibration-missing.bin");
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            bool loaded = CalibrationStore.TryLoadFrom(path, out CalibrationData restored, out bool mirror, out bool useKinect);

            Assert.IsFalse(loaded);
            Assert.IsNull(restored);
            Assert.IsTrue(mirror);
            Assert.IsTrue(useKinect);
        }
    }
}
