using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 校正データを Application.persistentDataPath のバイナリへ保存・読込します。
    /// 形式：見出し → 版 → 四隅 → 基準深度 → しきい値 → 左右反転 → （末尾）入力源 → 設置向き → 画素ごとの揺れ幅。
    /// 末尾の項目が無い古いファイルも読めます（無い項目は初期値）。
    /// </summary>
    public static class CalibrationStore
    {
        /// <summary>ファイルの見出し。</summary>
        public const string Magic = "BGCAL";

        /// <summary>現在の版。</summary>
        public const int CurrentVersion = 3;

        /// <summary>ファイル名。</summary>
        public const string FileName = "ballgame_calibration.bin";

        /// <summary>
        /// 既定の保存先を返します。
        /// </summary>
        /// <returns>保存先のフルパス</returns>
        public static string GetDefaultPath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }

        /// <summary>
        /// 校正データをストリームへ書き込みます。
        /// </summary>
        /// <param name="stream">書き込み先</param>
        /// <param name="data">校正データ</param>
        public static void Write(Stream stream, CalibrationData data)
        {
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                WriteCore(writer, data);
                WriteTail(writer, data);
            }
        }

        /// <summary>
        /// ストリームから校正データを読み込みます。末尾が無い場合は初期値のままにします。
        /// </summary>
        /// <param name="stream">読み込み元</param>
        /// <returns>校正データ</returns>
        public static CalibrationData Read(Stream stream)
        {
            CalibrationData data = new CalibrationData();
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                ReadCore(reader, data);
                ReadTail(reader, data);
            }

            return data;
        }

        /// <summary>
        /// 校正データをファイルへ保存します。
        /// </summary>
        /// <param name="path">保存先</param>
        /// <param name="data">校正データ</param>
        /// <returns>保存できたとき true</returns>
        public static bool TrySave(string path, CalibrationData data)
        {
            try
            {
                string temporaryPath = path + ".tmp";
                using (FileStream stream = File.Create(temporaryPath))
                {
                    Write(stream, data);
                }

                ReplaceFile(temporaryPath, path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("校正データを保存できませんでした: " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// ファイルから校正データを読み込みます。無い・壊れているときは初期値を返します。
        /// </summary>
        /// <param name="path">読み込み元</param>
        /// <returns>校正データ</returns>
        public static CalibrationData LoadOrDefault(string path)
        {
            if (!File.Exists(path))
            {
                return new CalibrationData();
            }

            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    return Read(stream);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("校正データを読めませんでした。初期状態で始めます: " + exception.Message);
                return new CalibrationData();
            }
        }

        /// <summary>
        /// 保存ファイルを消します。
        /// </summary>
        /// <param name="path">消すファイル</param>
        public static void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("校正データを消せませんでした: " + exception.Message);
            }
        }

        /// <summary>
        /// 古い形式（入力源・設置向きの末尾が無い形式）で書き込みます。互換性の確認用です。
        /// </summary>
        /// <param name="stream">書き込み先</param>
        /// <param name="data">校正データ</param>
        public static void WriteLegacy(Stream stream, CalibrationData data)
        {
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                WriteCore(writer, data);
            }
        }

        private static void ReplaceFile(string source, string destination)
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(source, destination);
        }

        private static void WriteCore(BinaryWriter writer, CalibrationData data)
        {
            writer.Write(Magic);
            writer.Write(CurrentVersion);
            WriteCorners(writer, data);
            WriteBaseline(writer, data);
            writer.Write(data.MaxDistanceMm);
            writer.Write(data.NoiseFloorMm);
            writer.Write(data.MirrorX);
        }

        private static void WriteTail(BinaryWriter writer, CalibrationData data)
        {
            writer.Write((int)data.InputSource);
            writer.Write((int)data.Mount);
            WriteNoise(writer, data);
        }

        private static void WriteNoise(BinaryWriter writer, CalibrationData data)
        {
            bool hasNoise = data.HasBaseline && data.BaselineNoise != null;
            writer.Write(hasNoise ? data.BaselineNoise.Length : 0);
            if (hasNoise)
            {
                byte[] bytes = new byte[data.BaselineNoise.Length * sizeof(ushort)];
                Buffer.BlockCopy(data.BaselineNoise, 0, bytes, 0, bytes.Length);
                writer.Write(bytes);
            }
        }

        private static void ReadNoise(BinaryReader reader, CalibrationData data)
        {
            int count = reader.ReadInt32();
            if (count <= 0 || !data.HasBaseline || count != data.Baseline.Length || !HasMore(reader, count * sizeof(ushort)))
            {
                return;
            }

            byte[] bytes = reader.ReadBytes(count * sizeof(ushort));
            ushort[] noise = new ushort[count];
            Buffer.BlockCopy(bytes, 0, noise, 0, bytes.Length);
            data.BaselineNoise = noise;
        }

        private static void WriteCorners(BinaryWriter writer, CalibrationData data)
        {
            writer.Write(data.Corners.Count);
            foreach (Vector2 corner in data.Corners)
            {
                writer.Write(corner.x);
                writer.Write(corner.y);
            }
        }

        private static void WriteBaseline(BinaryWriter writer, CalibrationData data)
        {
            if (!data.HasBaseline)
            {
                writer.Write(0);
                writer.Write(0);
                return;
            }

            writer.Write(data.Width);
            writer.Write(data.Height);
            byte[] bytes = new byte[data.Baseline.Length * sizeof(ushort)];
            Buffer.BlockCopy(data.Baseline, 0, bytes, 0, bytes.Length);
            writer.Write(bytes);
        }

        private static void ReadCore(BinaryReader reader, CalibrationData data)
        {
            string magic = reader.ReadString();
            if (magic != Magic)
            {
                throw new InvalidDataException("校正ファイルの見出しが違います。");
            }

            reader.ReadInt32();
            ReadCorners(reader, data);
            ReadBaseline(reader, data);
            data.MaxDistanceMm = Mathf.Clamp(reader.ReadInt32(), CalibrationData.MinMaxDistanceCm * 10, CalibrationData.MaxMaxDistanceCm * 10);
            data.NoiseFloorMm = Mathf.Max(0, reader.ReadInt32());
            data.MirrorX = reader.ReadBoolean();
        }

        private static void ReadTail(BinaryReader reader, CalibrationData data)
        {
            if (HasMore(reader, sizeof(int)))
            {
                data.InputSource = ToEnum(reader.ReadInt32(), DepthSourceKind.Kinect);
            }

            if (HasMore(reader, sizeof(int)))
            {
                data.Mount = ToEnum(reader.ReadInt32(), MountOrientation.Side);
            }

            if (HasMore(reader, sizeof(int)))
            {
                ReadNoise(reader, data);
            }
        }

        private static void ReadCorners(BinaryReader reader, CalibrationData data)
        {
            int count = Mathf.Clamp(reader.ReadInt32(), 0, CalibrationData.CornerCount);
            for (int i = 0; i < count; i++)
            {
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                data.Corners.Add(new Vector2(x, y));
            }
        }

        private static void ReadBaseline(BinaryReader reader, CalibrationData data)
        {
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            if (width <= 0 || height <= 0)
            {
                return;
            }

            int count = width * height;
            byte[] bytes = reader.ReadBytes(count * sizeof(ushort));
            if (bytes.Length != count * sizeof(ushort))
            {
                throw new EndOfStreamException("基準深度が途中で切れています。");
            }

            ushort[] baseline = new ushort[count];
            Buffer.BlockCopy(bytes, 0, baseline, 0, bytes.Length);
            data.SetBaseline(baseline, width, height);
        }

        private static bool HasMore(BinaryReader reader, int byteCount)
        {
            Stream stream = reader.BaseStream;
            return stream.CanSeek && stream.Length - stream.Position >= byteCount;
        }

        private static T ToEnum<T>(int value, T fallback) where T : struct
        {
            return Enum.IsDefined(typeof(T), value) ? (T)Enum.ToObject(typeof(T), value) : fallback;
        }
    }
}
