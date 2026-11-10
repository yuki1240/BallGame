using UnityEngine;
using WallBurst.Depth;
using WallBurst.Detection;

namespace WallBurst.Session
{
    /// <summary>
    /// キャリブレーション結果と深度源を、シーンをまたいでメモリに保持する。
    /// ファイルへは書き出さない。アプリを終了すると消える。
    /// </summary>
    public sealed class InstallationSession : MonoBehaviour
    {
        private bool _useKinect = true;
        private bool _sourceLockedByUser;
        private float _probeRemaining = 3f;

        /// <summary>今の起動に1つだけあるセッション。</summary>
        public static InstallationSession Instance { get; private set; }

        /// <summary>模擬深度。</summary>
        public SimulatedDepthSource Simulator { get; private set; }

        /// <summary>実機 Kinect。</summary>
        public KinectV2DepthSource Kinect { get; private set; }

        /// <summary>確定した基準。未実施なら null。</summary>
        public CalibrationData Calibration { get; private set; }

        /// <summary>基準に紐づく検知器。未実施なら null。</summary>
        public WallHitDetector Detector { get; private set; }

        /// <summary>Kinect を優先しているか。未接続のあいだも true のことがある。</summary>
        public bool UseKinect
        {
            get { return _useKinect; }
        }

        /// <summary>自動切替をやめるまでの残り秒。0 以下は探索済み。</summary>
        public float KinectProbeRemaining
        {
            get { return _probeRemaining; }
        }

        /// <summary>今の入力。</summary>
        public IDepthSource ActiveSource
        {
            get
            {
                if (_useKinect)
                {
                    return Kinect;
                }

                return Simulator;
            }
        }

        /// <summary>
        /// 無ければ作り、シーンをまたいで残す。
        /// </summary>
        /// <returns>セッション。</returns>
        public static InstallationSession EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameObject host = new GameObject("InstallationSession");
            return host.AddComponent<InstallationSession>();
        }

        /// <summary>
        /// 深度源をぶら下げ、破棄されないようにする。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Simulator = gameObject.AddComponent<SimulatedDepthSource>();
            Kinect = gameObject.AddComponent<KinectV2DepthSource>();
        }

        /// <summary>
        /// 静的参照が外れたときだけ付け直す。
        /// </summary>
        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        /// <summary>
        /// 基準が無いあいだは、Kinect が映り始めたらそちらへ寄せる。
        /// </summary>
        private void Update()
        {
            if (_sourceLockedByUser || Calibration != null)
            {
                return;
            }

            if (Kinect != null && Kinect.HasFrame)
            {
                _useKinect = true;
                return;
            }

            if (_probeRemaining <= 0f)
            {
                _useKinect = false;
                return;
            }

            _probeRemaining -= Time.unscaledDeltaTime;
            if (_probeRemaining <= 0f)
            {
                _useKinect = false;
            }
        }

        /// <summary>
        /// 入力を切り替える。ユーザー操作のときは自動切替を止める。
        /// </summary>
        /// <param name="useKinect">Kinect を使うなら true。</param>
        /// <param name="userOverride">画面のボタンからなら true。</param>
        public void SetUseKinect(bool useKinect, bool userOverride)
        {
            _useKinect = useKinect;
            if (userOverride)
            {
                _sourceLockedByUser = true;
            }
        }

        /// <summary>
        /// 基準を採用し、検知器を作り直す。
        /// </summary>
        /// <param name="data">四隅と基準深度。</param>
        public void Commit(CalibrationData data)
        {
            Calibration = data;
            Detector = new WallHitDetector(data);
        }

        /// <summary>
        /// 基準を捨てる。シーンのメモリからも消える。ファイルは元々無い。
        /// </summary>
        public void ClearCalibration()
        {
            Calibration = null;
            Detector = null;
        }

        /// <summary>
        /// アプリ終了で静的参照を外す。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
