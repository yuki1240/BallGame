using UnityEngine;
using WallBurst.Depth;
using WallBurst.Detection;
using WallBurst.Effects;

namespace WallBurst.Session
{
    /// <summary>
    /// キャリブレーション結果と深度源を、シーンをまたいで保持する。
    /// 確定した基準は永続フォルダにも残り、次の起動で戻す。
    /// </summary>
    public sealed class InstallationSession : MonoBehaviour
    {
        private bool _useKinect = true;
        private bool _sourceLockedByUser;
        private KinectMount _mount = KinectMount.AlongWall;
        private float _probeRemaining = 3f;
        private readonly BackgroundPlaylist _backgrounds = new BackgroundPlaylist();
        private readonly EffectSelection _effects = new EffectSelection();

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

        /// <summary>Kinect の置き方。</summary>
        public KinectMount Mount
        {
            get { return _mount; }
        }

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

        /// <summary>背景の切替。シーンをまたいでも同じものを使う。</summary>
        public BackgroundPlaylist Backgrounds
        {
            get { return _backgrounds; }
        }

        /// <summary>ヒットで出す演出。複数オンなら、その中から1つ出る。</summary>
        public EffectSelection Effects
        {
            get { return _effects; }
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
            _mount = CalibrationStore.LoadMount();
            ApplyMount();
            LoadSavedCalibration();
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
            ApplyMount();
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
        /// 設置を変えて、すぐ保存する。
        /// </summary>
        /// <param name="mount">Kinect の置き方。</param>
        public void SetMount(KinectMount mount)
        {
            _mount = mount;
            ApplyMount();
            CalibrationStore.SaveMount(mount);
        }

        /// <summary>
        /// 模擬深度を、今の設置に合わせる。
        /// </summary>
        private void ApplyMount()
        {
            if (Simulator != null)
            {
                Simulator.LookingUp = _mount == KinectMount.LookingUp;
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
            Remember();
        }

        /// <summary>
        /// 今の基準をファイルへ書く。基準が無ければ何もしない。
        /// </summary>
        public void Remember()
        {
            if (Calibration == null)
            {
                return;
            }

            bool mirror = Kinect == null || Kinect.MirrorHorizontally;
            CalibrationStore.Save(Calibration, mirror, _useKinect, _effects.CopyFlags(), _mount);
        }

        /// <summary>
        /// 基準をメモリとファイルの両方から消す。
        /// </summary>
        public void ClearCalibration()
        {
            Calibration = null;
            Detector = null;
            CalibrationStore.Delete();
        }

        /// <summary>
        /// 前回の起動で残した四隅と基準を戻す。
        /// </summary>
        private void LoadSavedCalibration()
        {
            if (!CalibrationStore.TryLoad(out CalibrationData loaded, out bool mirror, out bool useKinect, out bool[] effects, out KinectMount mount))
            {
                return;
            }

            _effects.Apply(effects);
            _mount = mount;
            ApplyMount();
            CalibrationStore.SaveMount(mount);

            if (Kinect != null)
            {
                Kinect.MirrorHorizontally = mirror;
            }

            _useKinect = useKinect;
            _sourceLockedByUser = true;
            _probeRemaining = 0f;
            Calibration = loaded;
            Detector = new WallHitDetector(loaded);
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
