using System;
using System.IO;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 校正をファイルに保存し、次に起動したときに読み戻します。
    /// 保存先は <c>Application.persistentDataPath/wallburst_calibration.bin</c> です（Unity のアプリ用フォルダ）。
    /// </summary>
    public static class CalibrationStore
    {
        private const string FileName = "wallburst_calibration.bin";
        private const int Magic = 0x31435357; // "WSC1"
        private const int Version = 1;

        /// <summary>保存先のパス。</summary>
        public static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        /// <summary>
        /// 保存します。失敗してもゲームは止めず、警告だけ出します。
        /// </summary>
        /// <param name="data">校正</param>
        /// <returns>保存できたとき true</returns>
        public static bool Save(CalibrationData data)
        {
            if (data == null)
            {
                return false;
            }

            try
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    Write(data, memory);
                    File.WriteAllBytes(FilePath, memory.ToArray());
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WallBurst] 校正を保存できませんでした: " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// 保存した校正を読みます。無い・壊れているときは null です。
        /// </summary>
        /// <returns>校正（無いときは null）</returns>
        public static CalibrationData Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return null;
                }

                using (FileStream stream = File.OpenRead(FilePath))
                {
                    return Read(stream);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WallBurst] 保存した校正を読めませんでした: " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// 保存した校正を消します。
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
                Debug.LogWarning("[WallBurst] 保存した校正を消せませんでした: " + exception.Message);
            }
        }

        /// <summary>
        /// 校正をストリームに書きます（テスト用にも使います）。
        /// </summary>
        /// <param name="data">校正</param>
        /// <param name="stream">書き先</param>
        public static void Write(CalibrationData data, Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(data.Width);
            writer.Write(data.Height);
            writer.Write((int)data.SourceKind);
            writer.Write(data.BallDiameterMm);
            writer.Write(data.PreviewRotation);
            writer.Write(data.PreviewMirror);
            WriteArray(writer, data.Baseline);
            WriteArray(writer, data.Noise);
            writer.Write(data.HasCorners);
            if (data.HasCorners)
            {
                for (int i = 0; i < 4; i++)
                {
                    writer.Write(data.Corners[i].x);
                    writer.Write(data.Corners[i].y);
                }
            }

            writer.Flush();
        }

        /// <summary>
        /// 校正をストリームから読みます（テスト用にも使います）。
        /// </summary>
        /// <param name="stream">読み元</param>
        /// <returns>校正（形式が違うときは null）</returns>
        public static CalibrationData Read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version)
            {
                return null;
            }

            CalibrationData data = new CalibrationData();
            data.Width = reader.ReadInt32();
            data.Height = reader.ReadInt32();
            data.SourceKind = (DepthSourceKind)reader.ReadInt32();
            data.BallDiameterMm = reader.ReadInt32();
            data.PreviewRotation = reader.ReadInt32();
            data.PreviewMirror = reader.ReadBoolean();
            data.Baseline = ReadArray(reader);
            data.Noise = ReadArray(reader);
            if (reader.ReadBoolean())
            {
                data.Corners = new Vector2[4];
                for (int i = 0; i < 4; i++)
                {
                    data.Corners[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                }
            }

            return data;
        }

        private static void WriteArray(BinaryWriter writer, ushort[] values)
        {
            writer.Write(values == null ? -1 : values.Length);
            if (values == null)
            {
                return;
            }

            byte[] bytes = new byte[values.Length * 2];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            writer.Write(bytes);
        }

        private static ushort[] ReadArray(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0)
            {
                return null;
            }

            byte[] bytes = reader.ReadBytes(length * 2);
            ushort[] values = new ushort[length];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
