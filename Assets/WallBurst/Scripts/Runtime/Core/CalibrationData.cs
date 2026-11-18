using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 校正（キャリブレーション）で決めたことをまとめたもの。
    /// <list type="bullet">
    /// <item>基準の深度：人がいないときの深度（画素ごと、mm、0 は取れていない）</item>
    /// <item>揺れ幅：基準を録る間の深度の揺れ（mm）。<see cref="UnstableNoise"/> は深度が 0 になったことがある不安定な画素</item>
    /// <item>四隅：深度画像の上でタップした、投影範囲の四隅（左上・右上・右下・左下、0〜1、上端が 0）</item>
    /// <item>設定：ボールの大きさなど</item>
    /// </list>
    /// </summary>
    public sealed class CalibrationData
    {
        /// <summary>揺れ幅の表で「不安定な画素」を表す値。</summary>
        public const ushort UnstableNoise = ushort.MaxValue;

        /// <summary>ボールの直径の初期値（mm）。</summary>
        public const int DefaultBallDiameterMm = 70;

        /// <summary>ボールの直径の最小（mm）。</summary>
        public const int MinBallDiameterMm = 40;

        /// <summary>ボールの直径の最大（mm）。</summary>
        public const int MaxBallDiameterMm = 400;

        /// <summary>深度画像の幅。</summary>
        public int Width;

        /// <summary>深度画像の高さ。</summary>
        public int Height;

        /// <summary>入力源の種類（Kinect か模擬か）。</summary>
        public DepthSourceKind SourceKind;

        /// <summary>基準の深度（mm、幅×高さ）。</summary>
        public ushort[] Baseline;

        /// <summary>基準を録る間の揺れ幅（mm、幅×高さ）。</summary>
        public ushort[] Noise;

        /// <summary>四隅（左上・右上・右下・左下、深度画像の 0〜1、上端が 0）。null はまだ決めていない。</summary>
        public Vector2[] Corners;

        /// <summary>ボールの直径（mm）。</summary>
        public int BallDiameterMm = DefaultBallDiameterMm;

        /// <summary>プレビューの回転（0〜3、90°ずつ。表示だけで、判定には使わない）。</summary>
        public int PreviewRotation = 1;

        /// <summary>プレビューを左右反転するか（表示だけ）。</summary>
        public bool PreviewMirror;

        /// <summary>基準を録り終えたか。</summary>
        public bool HasBaseline
        {
            get { return Baseline != null && Baseline.Length == Width * Height && Width > 0; }
        }

        /// <summary>四隅を決め終えたか。</summary>
        public bool HasCorners
        {
            get { return Corners != null && Corners.Length == 4; }
        }

        /// <summary>ゲームを始められるだけそろっているか。</summary>
        public bool IsComplete
        {
            get { return HasBaseline && HasCorners; }
        }

        /// <summary>
        /// 四隅を消します（基準は残します）。
        /// </summary>
        public void ClearCorners()
        {
            Corners = null;
        }

        /// <summary>
        /// 四隅の順番を入れ替えて、左右を逆にします（試し投げで左右が逆だったとき用）。
        /// </summary>
        public void SwapLeftRight()
        {
            if (!HasCorners)
            {
                return;
            }

            Corners = new[] { Corners[1], Corners[0], Corners[3], Corners[2] };
        }

        /// <summary>
        /// 四隅の順番を入れ替えて、上下を逆にします（試し投げで上下が逆だったとき用）。
        /// </summary>
        public void SwapTopBottom()
        {
            if (!HasCorners)
            {
                return;
            }

            Corners = new[] { Corners[3], Corners[2], Corners[1], Corners[0] };
        }
    }
}
