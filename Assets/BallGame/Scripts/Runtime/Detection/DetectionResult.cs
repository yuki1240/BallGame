using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 4連結でまとめた変化画素の塊。
    /// </summary>
    public struct DepthBlob
    {
        /// <summary>面積（画素数）。</summary>
        public int Area;

        /// <summary>左端。</summary>
        public int MinX;

        /// <summary>上端。</summary>
        public int MinY;

        /// <summary>右端。</summary>
        public int MaxX;

        /// <summary>下端。</summary>
        public int MaxY;

        /// <summary>重心 x（画素）。</summary>
        public float CentroidX;

        /// <summary>重心 y（画素）。</summary>
        public float CentroidY;

        /// <summary>実際の大きさに直した面積（2m の所での画素数に直した値）。</summary>
        public float NormalizedArea;

        /// <summary>1画素の実際の長さの倍率（2m の所の1画素を 1 とした値）。</summary>
        public float LinearScale;

        /// <summary>実際の大きさに直した横幅（2m の所での画素数）。</summary>
        public float NormalizedWidth
        {
            get { return Width * LinearScale; }
        }

        /// <summary>実際の大きさに直した縦幅（2m の所での画素数）。</summary>
        public float NormalizedHeight
        {
            get { return Height * LinearScale; }
        }

        /// <summary>塊の中で一番強い点の強さ（壁からの距離÷しきい値）。</summary>
        public float PeakStrength;

        /// <summary>縦横の箱のうち塊が埋めている割合。</summary>
        public float FillRatio
        {
            get { return (float)Area / (Width * Height); }
        }

        /// <summary>横幅（画素）。</summary>
        public int Width
        {
            get { return MaxX - MinX + 1; }
        }

        /// <summary>縦幅（画素）。</summary>
        public int Height
        {
            get { return MaxY - MinY + 1; }
        }
    }

    /// <summary>
    /// 採用したヒット。
    /// </summary>
    public struct DetectedHit
    {
        /// <summary>深度画像上の重心（画素）。</summary>
        public Vector2 Pixel;

        /// <summary>深度画像上の 0〜1（上端が 0）。</summary>
        public Vector2 ImageUv;

        /// <summary>塊の面積。</summary>
        public int Area;
    }

    /// <summary>
    /// 1フレーム分の検知結果。
    /// </summary>
    public sealed class DetectionResult
    {
        /// <summary>このフレームで採用したヒット。</summary>
        public readonly List<DetectedHit> Hits = new List<DetectedHit>();

        /// <summary>このフレームで見つかった塊（孤立点を除いたあと）。</summary>
        public readonly List<DepthBlob> Blobs = new List<DepthBlob>();

        /// <summary>人とみなしたフレームか。</summary>
        public bool PersonDetected;

        /// <summary>人が去ったあとの待ち時間中か。</summary>
        public bool InCooldown;

        /// <summary>変化画素の合計（壁からの距離の上限に関係なく数える）。</summary>
        public int ChangedPixelCount;

        /// <summary>変化した面積の合計（2m の所での画素数に直した値）。</summary>
        public float ChangedArea;

        /// <summary>このフレームで一番大きい塊があるか。</summary>
        public bool HasLargestBlob;

        /// <summary>このフレームで一番大きい塊（調整用の表示に使う）。</summary>
        public DepthBlob LargestBlob;

        /// <summary>一番大きい塊を採用しない理由（採用できるときは空文字）。</summary>
        public string LargestBlobRejectReason = string.Empty;

        /// <summary>候補画素（プレビュー表示用。次のフレームで上書きされます）。</summary>
        public bool[] CandidateMask;

        /// <summary>ボールとして採用できる塊の画素（プレビューで赤く表示する。次のフレームで上書きされます）。</summary>
        public bool[] BallMask;

        /// <summary>人サイズのフレームと判断した理由（人サイズでなければ空）。</summary>
        public string PersonReason = string.Empty;

        /// <summary>BallMask に1画素でもあるか。</summary>
        public bool HasBall;

        /// <summary>ボールとして採用できる塊の重心（深度画像上の 0〜1、上端が 0）。固定モードで場所を出すのに使います。</summary>
        public readonly List<Vector2> BallImageUvs = new List<Vector2>();

        /// <summary>
        /// 中身を空にします。
        /// </summary>
        public void Clear()
        {
            Hits.Clear();
            Blobs.Clear();
            PersonDetected = false;
            InCooldown = false;
            ChangedPixelCount = 0;
            ChangedArea = 0f;
            HasLargestBlob = false;
            HasBall = false;
            PersonReason = string.Empty;
            BallImageUvs.Clear();
            LargestBlobRejectReason = string.Empty;
        }

        /// <summary>
        /// 一番大きい塊の様子を、調整用の短い文にします。
        /// </summary>
        /// <returns>説明（塊が無いときは「塊なし」）</returns>
        public string DescribeLargestBlob()
        {
            if (!HasLargestBlob)
            {
                return "塊なし";
            }

            string verdict = LargestBlobRejectReason.Length == 0 ? "ボールとして採用できる" : "不採用：" + LargestBlobRejectReason;
            return string.Format("最大の塊 面積{0:F0}（2m換算） 強さ{1:F2} 詰まり{2:F2}（{3}）", LargestBlob.NormalizedArea, LargestBlob.PeakStrength, LargestBlob.FillRatio, verdict);
        }
    }
}
