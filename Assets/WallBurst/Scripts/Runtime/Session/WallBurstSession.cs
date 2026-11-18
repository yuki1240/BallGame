using System;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// シーンが替わっても動き続ける「本体」です（DontDestroyOnLoad：シーンをまたいで残す Unity のしくみ）。
    /// <list type="bullet">
    /// <item>Kinect（無ければ模擬深度）から深度を受け取る</item>
    /// <item>校正を持ち、保存・読み込みする</item>
    /// <item>当たり判定を動かし、当たりを <see cref="HitDetected"/> で各シーンへ知らせる</item>
    /// </list>
    /// </summary>
    public sealed class WallBurstSession : MonoBehaviour
    {
        private static WallBurstSession instance;

        private IDepthSource source;
        private ushort[] frame;
        private CalibrationData calibration;
        private HitDetector detector;
        private int frameCount;
        private float lastFrameTime = -1f;
        private string configureError = string.Empty;

        /// <summary>ボールが壁に当たったときの知らせ。</summary>
        public event Action<WallHit> HitDetected;

        /// <summary>
        /// 本体を返します。無ければ作ります。
        /// </summary>
        public static WallBurstSession Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject host = new GameObject("WallBurstSession（本体）");
                    DontDestroyOnLoad(host);
                    instance = host.AddComponent<WallBurstSession>();
                    instance.Initialize();
                }

                return instance;
            }
        }

        /// <summary>深度の入力源。</summary>
        public IDepthSource Source
        {
            get { return source; }
        }

        /// <summary>模擬深度を使っているか。</summary>
        public bool IsSimulated
        {
            get { return source != null && source.Kind == DepthSourceKind.Simulated; }
        }

        /// <summary>いちばん新しい深度（幅×高さ、mm）。</summary>
        public ushort[] LatestFrame
        {
            get { return frame; }
        }

        /// <summary>受け取ったフレームの数（新しいフレームが来たかを見るのに使う）。</summary>
        public int FrameCount
        {
            get { return frameCount; }
        }

        /// <summary>深度が届いているか（最後のフレームから1秒以内）。</summary>
        public bool IsReceiving
        {
            get { return lastFrameTime >= 0f && Time.unscaledTime - lastFrameTime < 1f; }
        }

        /// <summary>今の校正。</summary>
        public CalibrationData Calibration
        {
            get { return calibration; }
        }

        /// <summary>当たり判定。</summary>
        public HitDetector Detector
        {
            get { return detector; }
        }

        /// <summary>当たりを探すか（校正中の一部では止める）。</summary>
        public bool DetectionEnabled { get; set; }

        /// <summary>当たり判定が使える状態か。</summary>
        public bool IsReady
        {
            get { return detector != null && detector.IsConfigured; }
        }

        /// <summary>当たり判定を使えない理由（画面に出す文）。</summary>
        public string ConfigureError
        {
            get { return configureError; }
        }

        /// <summary>
        /// 入力源を開き直します（Kinect を探し直す・模擬に替える）。
        /// </summary>
        /// <param name="kind">使いたい入力源</param>
        /// <returns>実際に使っている入力源</returns>
        public DepthSourceKind OpenSource(DepthSourceKind kind)
        {
            if (source != null)
            {
                source.Dispose();
                source = null;
            }

            if (kind == DepthSourceKind.Kinect)
            {
                source = KinectDepthSource.TryOpen();
            }

            if (source == null)
            {
                source = new SimulatedDepthSource();
            }

            frame = new ushort[source.Width * source.Height];
            frameCount = 0;
            lastFrameTime = -1f;
            return source.Kind;
        }

        /// <summary>
        /// 新しい校正を始めます（今の校正は消します。保存したものは残ります）。
        /// </summary>
        public void StartNewCalibration()
        {
            calibration = new CalibrationData();
            calibration.Width = source.Width;
            calibration.Height = source.Height;
            calibration.SourceKind = source.Kind;
            detector.ResetTracking();
            ApplyCalibration();
        }

        /// <summary>
        /// 保存した校正を読み込みます。入力源（Kinect か模擬か）や大きさが合わないときは使いません。
        /// </summary>
        /// <returns>読み込めたとき true</returns>
        public bool LoadSavedCalibration()
        {
            CalibrationData saved = CalibrationStore.Load();
            if (saved == null || saved.Width != source.Width || saved.Height != source.Height || saved.SourceKind != source.Kind || !saved.IsComplete)
            {
                return false;
            }

            calibration = saved;
            return ApplyCalibration();
        }

        /// <summary>
        /// 今の校正を当たり判定に取り込みます。
        /// </summary>
        /// <returns>当たり判定が使える状態になったとき true</returns>
        public bool ApplyCalibration()
        {
            // 足りないときも Configure を呼んで、前の校正のままにならないようにする
            return detector.Configure(calibration, out configureError);
        }

        /// <summary>
        /// 今の校正を保存します。
        /// </summary>
        public void SaveCalibration()
        {
            CalibrationStore.Save(calibration);
        }

        /// <summary>
        /// 追いかけている物を忘れます（シーンを替えたとき）。
        /// </summary>
        public void ResetTracking()
        {
            if (detector != null)
            {
                detector.ResetTracking();
            }
        }

        /// <summary>
        /// 模擬深度のとき、壁座標の位置へボールを投げます（Kinect のときは何もしません）。
        /// </summary>
        /// <param name="wall01">壁座標</param>
        /// <returns>投げたとき true</returns>
        public bool ThrowSimulatedBall(Vector2 wall01)
        {
            SimulatedDepthSource simulated = source as SimulatedDepthSource;
            if (simulated == null || !IsReady)
            {
                return false;
            }

            Vector3 point = detector.Mapping.WallToPoint(wall01);
            if (point == Vector3.zero)
            {
                return false;
            }

            simulated.ThrowBall(point, calibration.BallDiameterMm);
            return true;
        }

        /// <summary>
        /// 模擬深度のとき、壁を触る手を出したり消したりします（見分けの確認用）。
        /// </summary>
        /// <param name="visible">出すとき true</param>
        /// <param name="wall01">触る位置</param>
        public void SetSimulatedHand(bool visible, Vector2 wall01)
        {
            SimulatedDepthSource simulated = source as SimulatedDepthSource;
            if (simulated == null || !IsReady)
            {
                return;
            }

            simulated.SetHand(visible, detector.Mapping.WallToPoint(wall01));
        }

        /// <summary>
        /// テストなどで、当たりを直接知らせます。
        /// </summary>
        /// <param name="hit">当たり</param>
        public void RaiseHit(WallHit hit)
        {
            Action<WallHit> handler = HitDetected;
            if (handler != null)
            {
                handler(hit);
            }
        }

        private void Initialize()
        {
            detector = new HitDetector();
            DetectionEnabled = true;
            OpenSource(DepthSourceKind.Kinect);
            StartNewCalibration();
        }

        private void Update()
        {
            if (source == null || !source.TryGetLatestFrame(frame))
            {
                return;
            }

            frameCount++;
            lastFrameTime = Time.unscaledTime;
            if (!DetectionEnabled || !IsReady)
            {
                return;
            }

            foreach (WallHit hit in detector.Process(frame, Time.unscaledTime))
            {
                RaiseHit(hit);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (source != null)
            {
                source.Dispose();
                source = null;
            }
        }

        private void OnApplicationQuit()
        {
            if (source != null)
            {
                source.Dispose();
                source = null;
            }
        }
    }
}
