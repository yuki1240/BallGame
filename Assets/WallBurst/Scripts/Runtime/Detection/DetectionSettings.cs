using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// ボールの当たりを見つけるときの決まりごと（しきい値）。
    /// 現場で動かすのは「ボールの直径」だけで、ほかはボールの直径から決まるか、決まった値です。
    /// </summary>
    public sealed class DetectionSettings
    {
        /// <summary>ボールの直径（mm）。</summary>
        public float BallDiameterMm = CalibrationData.DefaultBallDiameterMm;

        /// <summary>これより小さい深度の変化（mm）は、センサーの揺れとして捨てる。</summary>
        public float NoiseFloorMm = 15f;

        /// <summary>画素ごとの揺れ幅の何倍までを揺れとみなすか。</summary>
        public float NoiseMultiplier = 3f;

        /// <summary>壁から手前へ、これより出ていない画素は「壁」とみなす（mm）。</summary>
        public float WallToleranceMm = 15f;

        /// <summary>となりの画素と同じ物とみなす、深度の差の上限（mm）。これより差があれば別の物。</summary>
        public float DepthJumpMm = 90f;

        /// <summary>塊として扱う最小の画素数（これより小さいのはちらつき）。</summary>
        public int MinPixels = 5;

        /// <summary>同じボールとみなす、前のフレームからの移動の上限（mm）。</summary>
        public float TrackMatchMm = 450f;

        /// <summary>見えなくなってから、このフレーム数で追いかけるのをやめる。</summary>
        public int TrackLostFrames = 5;

        /// <summary>壁に近づく（または離れる）動きとみなす距離（mm）。</summary>
        public float ApproachMm = 80f;

        /// <summary>壁に触れてから、離れるのを待つフレーム数。</summary>
        public int LeaveWaitFrames = 8;

        /// <summary>当たりのあと、跳ね返りとみなす時間（秒）。</summary>
        public float BounceSeconds = 0.7f;

        /// <summary>ボールの半径の面積（mm²）。</summary>
        public float BallAreaMm2
        {
            get
            {
                float r = BallDiameterMm * 0.5f;
                return Mathf.PI * r * r;
            }
        }

        /// <summary>壁に触れたとみなす距離（mm）。ボールのいちばん壁に近い所が、これより近ければ触れている。</summary>
        public float ContactMm
        {
            get { return Mathf.Clamp(BallDiameterMm * 0.35f, 50f, 110f); }
        }

        /// <summary>ボールとみなす大きさ（見えている面積）の下限（mm²）。</summary>
        public float MinBallAreaMm2
        {
            get { return BallAreaMm2 * 0.15f; }
        }

        /// <summary>ボールとみなす大きさ（見えている面積）の上限（mm²）。これより大きいのは体や手。</summary>
        public float MaxBallAreaMm2
        {
            get { return BallAreaMm2 * 2.2f; }
        }

        /// <summary>ボールとみなす広がり（3Dの箱の対角線、mm）の上限。これより長いのは腕や体。</summary>
        public float MaxBallExtentMm
        {
            get { return BallDiameterMm * 2.0f + 60f; }
        }

        /// <summary>跳ね返りとみなす距離（mm）。</summary>
        public float BounceRadiusMm
        {
            get { return Mathf.Max(200f, BallDiameterMm * 1.5f); }
        }
    }
}
