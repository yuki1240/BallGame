using System;
using System.IO;
using UnityEngine;
using WallBurst.Depth;
using WallBurst.Detection;
using WallBurst.Effects;

namespace WallBurst.Session
{
    /// <summary>
    /// 四隅、基準深度、しきい値を起動のあいだ残す。
    /// 保存先はアプリの永続フォルダで、現場の手作業では触らない。
    /// </summary>
    public static class CalibrationStore
    {
        private const int Version = 1;

        /// <summary>
        /// 今の基準を書く。失敗しても遊びは止めない。
        /// </summary>
        /// <param name="data">四隅と基準深度。</param>
        /// <param name="mirrorHorizontally">Kinect を左右反転しているか。</param>
        /// <param name="useKinect">Kinect を入力にしているか。</param>
        public static void Save(CalibrationData data, bool mirrorHorizontally, bool useKinect, bool[] effects, KinectMount mount)
        {
            try
            {
                SaveTo(FilePath, data, mirrorHorizontally, useKinect, effects, mount);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("キャリブレーションを保存できませんでした。 " + exception.Message);
            }
        }

        /// <summary>
        /// 保存があれば読み、壊れていれば捨てる。
        /// </summary>
        /// <param name="data">復元した基準。失敗時は null。</param>
        /// <param name="mirrorHorizontally">保存されていた左右反転。</param>
        /// <param name="useKinect">保存されていた入力。</param>
        /// <returns>復元できたとき true。</returns>
        public static bool TryLoad(out CalibrationData data, out bool mirrorHorizontally, out bool useKinect, out bool[] effects, out KinectMount mount)
        {
            if (!TryLoadFrom(FilePath, out data, out mirrorHorizontally, out useKinect, out effects, out mount))
            {
                if (File.Exists(FilePath))
                {
                    Delete();
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// 設置モードをすぐ残す。基準が無くても書く。
        /// </summary>
        /// <param name="mount">Kinect の置き方。</param>
        public static void SaveMount(KinectMount mount)
        {
            try
            {
                byte flag = mount == KinectMount.AlongWall ? (byte)1 : (byte)0;
                File.WriteAllBytes(MountPath, new[] { flag });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("設置モードを保存できませんでした。 " + exception.Message);
            }
        }

        /// <summary>
        /// 保存した設置モード。無ければ見上げ。
        /// </summary>
        /// <returns>Kinect の置き方。</returns>
        public static KinectMount LoadMount()
        {
            try
            {
                if (!File.Exists(MountPath))
                {
                    return KinectMount.AlongWall;
                }

                byte[] bytes = File.ReadAllBytes(MountPath);
                if (bytes.Length < 1 || bytes[0] == 0)
                {
                    return KinectMount.LookingUp;
                }

                return KinectMount.AlongWall;
            }
            catch (IOException)
            {
                return KinectMount.AlongWall;
            }
        }

        /// <summary>
        /// 保存ファイルを消す。無いときは何もしない。
        /// </summary>
        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("キャリブレーションを削除できませんでした。 " + exception.Message);
            }
        }

        /// <summary>
        /// 指定パスへ書く。テストから使う。
        /// </summary>
        /// <param name="path">書き出し先。</param>
        /// <param name="data">四隅と基準深度。</param>
        /// <param name="mirrorHorizontally">左右反転。</param>
        /// <param name="useKinect">Kinect を使うか。</param>
        public static void SaveTo(string path, CalibrationData data, bool mirrorHorizontally, bool useKinect)
        {
            SaveTo(path, data, mirrorHorizontally, useKinect, null);
        }

        /// <summary>
        /// 指定パスへ書く。演出のオンオフも末尾に残す。
        /// </summary>
        /// <param name="path">書き出し先。</param>
        /// <param name="data">四隅と基準深度。</param>
        /// <param name="mirrorHorizontally">左右反転。</param>
        /// <param name="useKinect">Kinect を使うか。</param>
        /// <param name="effects">長さ 4。null なら全部オン。</param>
        public static void SaveTo(string path, CalibrationData data, bool mirrorHorizontally, bool useKinect, bool[] effects, KinectMount mount)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(data.Width);
                writer.Write(data.Height);
                writer.Write(mirrorHorizontally);
                writer.Write(useKinect);
                writer.Write(data.Baseline.Length);
                for (int i = 0; i < data.Baseline.Length; i++)
                {
                    writer.Write(data.Baseline[i]);
                }

                for (int i = 0; i < 4; i++)
                {
                    writer.Write(data.Corners[i].x);
                    writer.Write(data.Corners[i].y);
                    writer.Write(data.IsCornerDefined(i));
                }

                WriteTuning(writer, data.Tuning);
                WriteEffects(writer, effects);
                writer.Write(mount == KinectMount.AlongWall);
            }
        }

        /// <summary>
        /// 指定パスへ書く。設置は壁と水平として残す。
        /// </summary>
        public static void SaveTo(string path, CalibrationData data, bool mirrorHorizontally, bool useKinect, bool[] effects)
        {
            SaveTo(path, data, mirrorHorizontally, useKinect, effects, KinectMount.AlongWall);
        }

        /// <summary>
        /// 指定パスから読む。無い、または壊れているときは false。
        /// </summary>
        /// <param name="path">読み出し元。</param>
        /// <param name="data">復元した基準。</param>
        /// <param name="mirrorHorizontally">左右反転。</param>
        /// <param name="useKinect">Kinect を使うか。</param>
        /// <returns>復元できたとき true。</returns>
        public static bool TryLoadFrom(string path, out CalibrationData data, out bool mirrorHorizontally, out bool useKinect)
        {
            return TryLoadFrom(path, out data, out mirrorHorizontally, out useKinect, out _);
        }

        /// <summary>
        /// 指定パスから読む。演出フラグが無い古いファイルは、全部オンとして返す。
        /// </summary>
        /// <param name="path">読み出し元。</param>
        /// <param name="data">復元した基準。</param>
        /// <param name="mirrorHorizontally">左右反転。</param>
        /// <param name="useKinect">Kinect を使うか。</param>
        /// <param name="effects">長さ 4。古いファイルなら全部 true。</param>
        /// <returns>復元できたとき true。</returns>
        public static bool TryLoadFrom(string path, out CalibrationData data, out bool mirrorHorizontally, out bool useKinect, out bool[] effects)
        {
            return TryLoadFrom(path, out data, out mirrorHorizontally, out useKinect, out effects, out _);
        }

        /// <summary>
        /// 指定パスから読む。設置の記録が無い古いファイルは、壁と水平として返す。
        /// </summary>
        public static bool TryLoadFrom(string path, out CalibrationData data, out bool mirrorHorizontally, out bool useKinect, out bool[] effects, out KinectMount mount)
        {
            data = null;
            mirrorHorizontally = true;
            useKinect = true;
            effects = null;
            mount = KinectMount.AlongWall;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != Version)
                    {
                        return false;
                    }

                    int width = reader.ReadInt32();
                    int height = reader.ReadInt32();
                    mirrorHorizontally = reader.ReadBoolean();
                    useKinect = reader.ReadBoolean();
                    int count = reader.ReadInt32();
                    if (width <= 0 || height <= 0 || count != width * height)
                    {
                        return false;
                    }

                    ushort[] baseline = new ushort[count];
                    for (int i = 0; i < count; i++)
                    {
                        baseline[i] = reader.ReadUInt16();
                    }

                    DetectionTuning tuning = new DetectionTuning();
                    Vector2[] corners = new Vector2[4];
                    bool[] defined = new bool[4];
                    for (int i = 0; i < 4; i++)
                    {
                        corners[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                        defined[i] = reader.ReadBoolean();
                    }

                    ReadTuning(reader, tuning);
                    data = new CalibrationData(width, height, baseline, tuning);
                    for (int i = 0; i < 4; i++)
                    {
                        if (defined[i])
                        {
                            data.SetCorner(i, corners[i]);
                        }
                    }

                    effects = ReadEffects(reader, stream);
                    mount = ReadMount(reader, stream);
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                data = null;
                effects = null;
                mount = KinectMount.AlongWall;
                return false;
            }
            catch (IOException)
            {
                data = null;
                effects = null;
                mount = KinectMount.AlongWall;
                return false;
            }
        }

        /// <summary>
        /// 残りがあれば設置を読む。無ければ壁と水平。
        /// </summary>
        private static KinectMount ReadMount(BinaryReader reader, Stream stream)
        {
            if (stream.Length - stream.Position < 1)
            {
                return KinectMount.AlongWall;
            }

            return reader.ReadBoolean() ? KinectMount.AlongWall : KinectMount.LookingUp;
        }

        /// <summary>
        /// 演出のオンオフを4つ書く。
        /// </summary>
        private static void WriteEffects(BinaryWriter writer, bool[] effects)
        {
            for (int i = 0; i < EffectSelection.Count; i++)
            {
                bool enabled = effects == null || i >= effects.Length || effects[i];
                writer.Write(enabled);
            }
        }

        /// <summary>
        /// 残りがあれば演出フラグを読む。無ければ全部オン。
        /// </summary>
        private static bool[] ReadEffects(BinaryReader reader, Stream stream)
        {
            bool[] effects = new bool[EffectSelection.Count];
            if (stream.Length - stream.Position < EffectSelection.Count)
            {
                for (int i = 0; i < effects.Length; i++)
                {
                    effects[i] = true;
                }

                return effects;
            }

            for (int i = 0; i < effects.Length; i++)
            {
                effects[i] = reader.ReadBoolean();
            }

            return effects;
        }

        /// <summary>永続フォルダ上の保存ファイル。</summary>
        private static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, "wall-burst-calibration.bin"); }
        }

        /// <summary>設置モードだけの保存ファイル。</summary>
        private static string MountPath
        {
            get { return Path.Combine(Application.persistentDataPath, "wall-burst-mount.bin"); }
        }

        /// <summary>
        /// しきい値を固定順で書く。
        /// </summary>
        private static void WriteTuning(BinaryWriter writer, DetectionTuning tuning)
        {
            tuning.Clamp();
            writer.Write(tuning.NoiseFloorMillimeters);
            writer.Write(tuning.MaxApproachMillimeters);
            writer.Write(tuning.MinBlobArea);
            writer.Write(tuning.StrongBlobMultiplier);
            writer.Write(tuning.MaxBlobArea);
            writer.Write(tuning.MaxBlobSpan);
            writer.Write(tuning.PersonHoldSeconds);
            writer.Write(tuning.ConfirmFrames);
            writer.Write(tuning.SpatialMinNeighbors);
            writer.Write(tuning.CooldownSeconds);
            writer.Write(tuning.GridColumns);
            writer.Write(tuning.GridRows);
            writer.Write(tuning.FreshFrameMargin);
            writer.Write(tuning.EnableDriftCompensation);
            writer.Write(tuning.DriftBlend);
            writer.Write(tuning.DriftIntervalFrames);
        }

        /// <summary>
        /// しきい値を固定順で戻す。
        /// </summary>
        private static void ReadTuning(BinaryReader reader, DetectionTuning tuning)
        {
            tuning.NoiseFloorMillimeters = reader.ReadInt32();
            tuning.MaxApproachMillimeters = reader.ReadInt32();
            tuning.MinBlobArea = reader.ReadInt32();
            tuning.StrongBlobMultiplier = reader.ReadInt32();
            tuning.MaxBlobArea = reader.ReadInt32();
            tuning.MaxBlobSpan = reader.ReadInt32();
            tuning.PersonHoldSeconds = reader.ReadSingle();
            tuning.ConfirmFrames = reader.ReadInt32();
            tuning.SpatialMinNeighbors = reader.ReadInt32();
            tuning.CooldownSeconds = reader.ReadSingle();
            tuning.GridColumns = reader.ReadInt32();
            tuning.GridRows = reader.ReadInt32();
            tuning.FreshFrameMargin = reader.ReadInt32();
            tuning.EnableDriftCompensation = reader.ReadBoolean();
            tuning.DriftBlend = reader.ReadSingle();
            tuning.DriftIntervalFrames = reader.ReadInt32();
            tuning.Clamp();
        }
    }
}
