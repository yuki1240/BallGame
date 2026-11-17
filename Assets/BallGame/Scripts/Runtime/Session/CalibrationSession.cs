using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// シーンをまたいで校正と深度の入力源を持つセッション（DontDestroyOnLoad）。
    /// 深度の取得・左右反転・検知を毎フレーム行い、Calibration と Game の両方から使います。
    /// ドメイン再読込で中身が null になっても、使うときに作り直します。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CalibrationSession : MonoBehaviour
    {
        /// <summary>Kinect がつながるのを待つ秒数。過ぎたら模擬深度に切り替えます。</summary>
        public const float KinectWaitSeconds = 3f;

        /// <summary>模擬深度の仮の校正で使う四隅の内側への余白（0〜1）。</summary>
        public const float SimulatorCornerInset = 0.06f;

        private static CalibrationSession instance;

        private CalibrationData data;
        private CalibrationData simulatorCalibration;
        private IDepthSource source;
        private ushort[] depthBuffer;
        private HitDetector detector;
        private DetectionResult latestResult;
        private Homography imageToWall;
        private Homography wallToImage;
        private bool hasHomography;
        private bool hasNewFrame;
        private int frameSerial;
        private bool waitingForKinect;
        private float kinectOpenedAt;
        private string sourceStatus = string.Empty;

        /// <summary>校正データ（保存される本体）。</summary>
        public CalibrationData Data
        {
            get
            {
                EnsureReady();
                return data;
            }
        }

        /// <summary>今使っている深度の入力源。</summary>
        public IDepthSource Source
        {
            get
            {
                EnsureReady();
                return source;
            }
        }

        /// <summary>模擬深度の生成器。模擬でないときやまだ無いときは null。</summary>
        public SimulatedDepthSource Simulator
        {
            get { return source as SimulatedDepthSource; }
        }

        /// <summary>今使っている入力源が模擬深度か。</summary>
        public bool IsSimulated
        {
            get { return Source != null && Source.Kind == DepthSourceKind.Simulated; }
        }

        /// <summary>最新の深度（左右反転済み）。</summary>
        public ushort[] LatestDepth
        {
            get { return depthBuffer; }
        }

        /// <summary>深度の幅。</summary>
        public int FrameWidth
        {
            get { return Source != null ? Source.Width : 0; }
        }

        /// <summary>深度の高さ。</summary>
        public int FrameHeight
        {
            get { return Source != null ? Source.Height : 0; }
        }

        /// <summary>このフレームで新しい深度が来たか。</summary>
        public bool HasNewFrame
        {
            get { return hasNewFrame; }
        }

        /// <summary>受け取った深度の通し番号。</summary>
        public int FrameSerial
        {
            get { return frameSerial; }
        }

        /// <summary>最新の検知結果（新しい深度が来たフレームだけ更新）。</summary>
        public DetectionResult LatestResult
        {
            get { return latestResult; }
        }

        /// <summary>検知器。</summary>
        public HitDetector Detector
        {
            get
            {
                EnsureReady();
                return detector;
            }
        }

        /// <summary>入力源の状態の説明。</summary>
        public string SourceStatus
        {
            get { return sourceStatus; }
        }

        /// <summary>Game で使う校正（保存した校正が今の入力源に合わず、模擬深度のときは仮の校正）。</summary>
        public CalibrationData ActiveCalibration
        {
            get { return SelectActiveCalibration(); }
        }

        /// <summary>保存した校正が今の入力源に合っているか。</summary>
        public bool IsSavedCalibrationUsable
        {
            get { return Data.IsComplete && Data.MatchesResolution(FrameWidth, FrameHeight); }
        }

        /// <summary>壁座標への変換があるか。</summary>
        public bool HasHomography
        {
            get { return hasHomography; }
        }

        /// <summary>深度画像（0〜1）→ 壁座標（0〜1）の変換。</summary>
        public Homography ImageToWall
        {
            get { return imageToWall; }
        }

        /// <summary>壁座標（0〜1）→ 深度画像（0〜1）の変換。</summary>
        public Homography WallToImage
        {
            get { return wallToImage; }
        }

        /// <summary>
        /// セッションを探し、無ければ作ります。
        /// </summary>
        /// <returns>セッション</returns>
        public static CalibrationSession EnsureExists()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindAnyObjectByType<CalibrationSession>();
            if (instance == null)
            {
                instance = new GameObject("CalibrationSession").AddComponent<CalibrationSession>();
            }

            return instance;
        }

        /// <summary>
        /// 校正データを保存します。
        /// </summary>
        /// <returns>保存できたとき true</returns>
        public bool Save()
        {
            return CalibrationStore.TrySave(CalibrationStore.GetDefaultPath(), Data);
        }

        /// <summary>
        /// 設置向きを選びます。同じ向きを選び直したときは四隅と基準を消しません。
        /// </summary>
        /// <param name="mount">設置向き</param>
        /// <returns>別の向きへ切り替えて四隅と基準を消したとき true</returns>
        public bool SelectMount(MountOrientation mount)
        {
            if (!Data.SelectMount(mount))
            {
                return false;
            }

            ApplyMountToSimulator();
            simulatorCalibration = null;
            ApplyCalibrationChange();
            return true;
        }

        /// <summary>
        /// 左右反転を切り替えます。四隅と基準も左右に写し替えます。
        /// </summary>
        /// <param name="mirror">左右反転するか</param>
        public void SetMirror(bool mirror)
        {
            Data.ApplyMirror(mirror);
            ApplyCalibrationChange();
        }

        /// <summary>
        /// 入力源を切り替えます。
        /// </summary>
        /// <param name="kind">入力源</param>
        public void SetInputSource(DepthSourceKind kind)
        {
            if (Data.InputSource == kind)
            {
                return;
            }

            data.InputSource = kind;
            ReopenSource();
            ApplyCalibrationChange();
        }

        /// <summary>
        /// 壁からの距離（cm、5〜80）を変えます。
        /// </summary>
        /// <param name="centimeters">cm</param>
        /// <returns>実際に設定した cm</returns>
        public int SetMaxDistanceCm(int centimeters)
        {
            int applied = Data.SetMaxDistanceCm(centimeters);
            if (simulatorCalibration != null)
            {
                simulatorCalibration.MaxDistanceMm = data.MaxDistanceMm;
            }

            Detector.Settings.MaxDistanceMm = data.MaxDistanceMm;
            Save();
            return applied;
        }

        /// <summary>
        /// ノイズ床（mm、20〜200）を変えます。大きいほど揺れに強く、小さい物に鈍くなります。
        /// </summary>
        /// <param name="millimeters">mm</param>
        /// <returns>実際に設定した mm</returns>
        public int SetNoiseFloorMm(int millimeters)
        {
            int applied = Data.SetNoiseFloorMm(millimeters);
            if (simulatorCalibration != null)
            {
                simulatorCalibration.NoiseFloorMm = applied;
            }

            Detector.Settings.NoiseFloorMm = applied;
            Save();
            return applied;
        }

        /// <summary>
        /// 四隅を1つ置きます（左上・右上・右下・左下の順）。
        /// </summary>
        /// <param name="imageUv">深度画像上の 0〜1（上端が 0）</param>
        /// <returns>置けたとき true</returns>
        public bool AddCorner(Vector2 imageUv)
        {
            DiscardMismatchedBaseline();
            if (!Data.AddCorner(imageUv))
            {
                return false;
            }

            ApplyCalibrationChange();
            return true;
        }

        /// <summary>
        /// 隅を1つ動かします。画面の表示だけ先に変え、保存と検知の作り直しは EndCornerEdit で行います。
        /// </summary>
        /// <param name="index">隅の番号</param>
        /// <param name="imageUv">新しい位置（深度画像上の 0〜1）</param>
        public void MoveCorner(int index, Vector2 imageUv)
        {
            Data.SetCorner(index, imageUv);
        }

        /// <summary>
        /// 四隅をまとめて動かします。保存と検知の作り直しは EndCornerEdit で行います。
        /// </summary>
        /// <param name="delta">動かす量（深度画像上の 0〜1）</param>
        public void MoveAllCorners(Vector2 delta)
        {
            Data.TranslateCorners(delta);
        }

        /// <summary>
        /// 四隅の移動を確定し、壁の面と検知範囲を作り直して保存します。
        /// </summary>
        public void EndCornerEdit()
        {
            ApplyCalibrationChange();
        }

        /// <summary>
        /// 四隅を消して置き直せるようにします（基準は残します）。
        /// </summary>
        public void ClearCorners()
        {
            Data.Corners.Clear();
            ApplyCalibrationChange();
        }

        /// <summary>
        /// 録った基準深度を設定します。
        /// </summary>
        /// <param name="baseline">基準深度</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="noise">画素ごとの揺れ幅（無ければ null）</param>
        public void SetBaseline(ushort[] baseline, int width, int height, ushort[] noise)
        {
            Data.SetBaseline(baseline, width, height, noise);
            ApplyCalibrationChange();
        }

        /// <summary>
        /// 四隅・基準・しきい値・左右反転・入力源・設置向きをすべて消して最初からにします。
        /// </summary>
        public void ResetAll()
        {
            Data.ResetAll();
            CalibrationStore.Delete(CalibrationStore.GetDefaultPath());
            simulatorCalibration = null;
            ReopenSource();
            Reconfigure();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EventSystemKeeper.Ensure();
            EnsureReady();
        }

        private void Update()
        {
            EnsureReady();
            PollFrame();
            CheckKinectConnection();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                CloseSource();
                instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            CloseSource();
        }

        private void EnsureReady()
        {
            if (data == null)
            {
                data = CalibrationStore.LoadOrDefault(CalibrationStore.GetDefaultPath());
            }

            if (detector == null)
            {
                detector = new HitDetector(new DetectionSettings());
            }

            if (source == null)
            {
                OpenSource();
                Reconfigure();
            }
        }

        private void OpenSource()
        {
            source = data.InputSource == DepthSourceKind.Kinect ? OpenKinect() : null;
            if (source == null)
            {
                source = new SimulatedDepthSource(data.Mount);
                if (data.InputSource == DepthSourceKind.Simulated)
                {
                    sourceStatus = "模擬深度（320×240）。Game 画面のクリックがボールになります。";
                }
            }

            depthBuffer = new ushort[source.Width * source.Height];
        }

        private IDepthSource OpenKinect()
        {
            KinectDepthSource kinect = KinectDepthSource.TryOpen();
            if (kinect == null)
            {
                sourceStatus = "Kinect が見つからないため、模擬深度（320×240）で動いています。";
                return null;
            }

            waitingForKinect = true;
            kinectOpenedAt = Time.realtimeSinceStartup;
            sourceStatus = "Kinect に接続しています…";
            return kinect;
        }

        private void CloseSource()
        {
            waitingForKinect = false;
            if (source != null)
            {
                source.Dispose();
                source = null;
            }
        }

        private void ReopenSource()
        {
            CloseSource();
            OpenSource();
        }

        private void PollFrame()
        {
            hasNewFrame = false;
            if (source == null || !TryReadFrame())
            {
                return;
            }

            if (data.MirrorX)
            {
                DepthMirror.MirrorX(depthBuffer, source.Width, source.Height);
            }

            frameSerial++;
            hasNewFrame = true;
            waitingForKinect = false;
            UpdateKinectStatus();
            latestResult = detector.Process(depthBuffer, Time.time);
        }

        private bool TryReadFrame()
        {
            try
            {
                return source.TryGetLatestFrame(depthBuffer);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Kinect から深度を読めなかったため、模擬深度に切り替えます: " + exception.Message);
                SwitchToSimulator("Kinect から深度を読めなかったため、模擬深度（320×240）で動いています。");
                return false;
            }
        }

        private void SwitchToSimulator(string status)
        {
            CloseSource();
            source = new SimulatedDepthSource(data.Mount);
            depthBuffer = new ushort[source.Width * source.Height];
            sourceStatus = status;
            Reconfigure();
        }

        private void UpdateKinectStatus()
        {
            if (source.Kind == DepthSourceKind.Kinect)
            {
                sourceStatus = "Kinect v2（512×424）で動いています。";
            }
        }

        private void CheckKinectConnection()
        {
            if (!waitingForKinect || Time.realtimeSinceStartup - kinectOpenedAt < KinectWaitSeconds)
            {
                return;
            }

            if (source != null && source.IsAvailable)
            {
                return;
            }

            Debug.LogWarning("Kinect から深度が届かないため、模擬深度に切り替えます。");
            SwitchToSimulator("Kinect がつながっていないため、模擬深度（320×240）で動いています。");
        }

        private void ApplyMountToSimulator()
        {
            SimulatedDepthSource simulator = Simulator;
            if (simulator != null)
            {
                simulator.Mount = data.Mount;
            }
        }

        private void DiscardMismatchedBaseline()
        {
            if (Data.HasBaseline && !data.MatchesResolution(FrameWidth, FrameHeight))
            {
                data.ClearBaseline();
            }
        }

        private void ApplyCalibrationChange()
        {
            Reconfigure();
            Save();
        }

        private void Reconfigure()
        {
            CalibrationData active = SelectActiveCalibration();
            if (active != null)
            {
                detector.Configure(active);
                detector.Settings.ApplyCalibration(active);
            }

            UpdateHomography(active);
        }

        private CalibrationData SelectActiveCalibration()
        {
            if (Data.IsComplete && data.MatchesResolution(FrameWidth, FrameHeight))
            {
                return data;
            }

            if (!IsSimulated)
            {
                return data;
            }

            if (simulatorCalibration == null)
            {
                simulatorCalibration = BuildSimulatorCalibration();
            }

            return simulatorCalibration;
        }

        private CalibrationData BuildSimulatorCalibration()
        {
            SimulatedDepthSource generator = new SimulatedDepthSource(data.Mount, 987);
            BaselineRecorder recorder = new BaselineRecorder(generator.Width, generator.Height);
            ushort[] frame = new ushort[generator.Width * generator.Height];
            recorder.Begin();
            while (!recorder.IsComplete)
            {
                generator.TryGetLatestFrame(frame);
                recorder.AddFrame(frame);
            }

            CalibrationData calibration = new CalibrationData();
            AddInsetCorners(calibration);
            calibration.SetBaseline(recorder.BuildMedian(), generator.Width, generator.Height, recorder.BuildNoise());
            calibration.MaxDistanceMm = data.MaxDistanceMm;
            calibration.NoiseFloorMm = data.NoiseFloorMm;
            calibration.Mount = data.Mount;
            calibration.InputSource = DepthSourceKind.Simulated;
            return calibration;
        }

        private static void AddInsetCorners(CalibrationData calibration)
        {
            float a = SimulatorCornerInset;
            float b = 1f - SimulatorCornerInset;
            calibration.AddCorner(new Vector2(a, a));
            calibration.AddCorner(new Vector2(b, a));
            calibration.AddCorner(new Vector2(b, b));
            calibration.AddCorner(new Vector2(a, b));
        }

        private void UpdateHomography(CalibrationData active)
        {
            hasHomography = false;
            if (active == null || !active.HasAllCorners)
            {
                return;
            }

            try
            {
                imageToWall = Homography.FromImageCorners(active.Corners);
                wallToImage = imageToWall.Inverse();
                hasHomography = true;
            }
            catch (System.InvalidOperationException exception)
            {
                Debug.LogWarning(exception.Message);
            }
        }
    }
}
