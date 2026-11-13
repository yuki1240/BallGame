using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WallBurst.Depth;
using WallBurst.Detection;
using WallBurst.Session;

namespace WallBurst.Ui
{
    /// <summary>
    /// 現場用の調整画面。深度を見ながら四隅と基準を録り、しきい値をその場で変える。
    /// 結果は次の起動でも戻る。リセットで保存ごと消える。
    /// </summary>
    public sealed class CalibrationSceneController : MonoBehaviour
    {
        private static readonly string[] CornerNames = { "左上", "右上", "右下", "左下" };

        private InstallationSession _session;
        private DetectionTuning _tuning;
        private DepthPreviewWidget _preview;
        private readonly Vector2[] _corners = new Vector2[4];
        private readonly bool[] _defined = new bool[4];
        private readonly List<WallHit> _hits = new List<WallHit>(4);
        private ushort[] _depth;
        private BaselineAccumulator _accumulator;
        private Text _status;
        private Text _instruction;
        private Text _note;
        private Text _recordLabel;
        private Text _sourceLabel;
        private Image _alongMountImage;
        private Image _underMountImage;
        private Text _guide;
        private static readonly Color MountSelected = new Color(0.16f, 0.52f, 0.4f, 1f);
        private static readonly Color MountIdle = new Color(0.22f, 0.26f, 0.3f, 1f);
        private Button _recordButton;
        private Button _testButton;
        private Button _startButton;
        private LabeledSlider _noise;
        private LabeledSlider _approach;
        private LabeledSlider _area;
        private LabeledSlider _personArea;
        private LabeledSlider _personSpan;
        private LabeledSlider _personHold;
        private LabeledSlider _confirm;
        private int _pickIndex;
        private int _lastSerial = -1;
        private int _observedHits;
        private bool _recording;
        private bool _suggested;
        private bool _suppressSuggest;
        private bool _haveSeenSource;
        private bool _seenUseKinect;
        private string _noteBody = "四隅と基準は次の起動でも残ります。やり直すときはリセットを押してください。";

        /// <summary>
        /// セッションと画面を用意する。前回の調整がメモリにあれば戻す。
        /// </summary>
        private void Awake()
        {
            UiFactory.EnsureEventSystem();
            _session = InstallationSession.EnsureExists();
            if (_session.Calibration != null)
            {
                Restore(_session.Calibration);
            }
            else
            {
                _tuning = new DetectionTuning();
            }

            BuildUi();
            _haveSeenSource = true;
            _seenUseKinect = _session.UseKinect;
        }

        /// <summary>
        /// 深度を受け取り、記録中なら貯め、そうでなければ差分を塗る。
        /// </summary>
        private void Update()
        {
            WatchSourceSwitch();
            PollDepth();
            RefreshTexts();
        }

        /// <summary>
        /// メモリ上の基準から四隅としきい値を戻す。
        /// </summary>
        private void Restore(CalibrationData data)
        {
            _tuning = data.Tuning;
            _suppressSuggest = true;
            _noteBody = "前回の四隅と基準を戻しました。";
            for (int i = 0; i < 4; i++)
            {
                _defined[i] = data.IsCornerDefined(i);
                _corners[i] = data.Corners[i];
            }

            _pickIndex = data.HasAllCorners ? 4 : data.DefinedCornerCount;
        }

        /// <summary>
        /// 左に深度、右に手順を置く。
        /// </summary>
        private void BuildUi()
        {
            RectTransform canvas = UiFactory.CreateCanvas("CalibrationCanvas");
            UiFactory.CreateStretch(canvas, "Background", new Color(0.06f, 0.07f, 0.09f, 1f));
            RectTransform left = UiFactory.CreateRegion(canvas, "PreviewPane", new Vector2(0.025f, 0.05f), new Vector2(0.60f, 0.95f));
            Image frame = left.gameObject.AddComponent<Image>();
            frame.color = new Color(0.02f, 0.02f, 0.03f, 1f);
            frame.raycastTarget = false;

            GameObject rawObject = new GameObject("Depth", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter), typeof(DepthPreviewWidget));
            rawObject.transform.SetParent(left, false);
            RectTransform rawRect = rawObject.GetComponent<RectTransform>();
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = new Vector2(12f, 12f);
            rawRect.offsetMax = new Vector2(-12f, -12f);
            AspectRatioFitter fitter = rawObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 4f / 3f;
            _preview = rawObject.GetComponent<DepthPreviewWidget>();
            _preview.PixelClicked += OnPixelClicked;

            RectTransform right = UiFactory.CreateRegion(canvas, "Controls", new Vector2(0.615f, 0.04f), new Vector2(0.985f, 0.96f));
            RectTransform content = UiFactory.CreateScrollContent(right);
            _status = UiFactory.CreateBody(content, string.Empty, 24, 72f, TextAnchor.UpperLeft);
            _instruction = UiFactory.CreateBody(content, string.Empty, 26, 96f, TextAnchor.UpperLeft);
            _instruction.color = new Color(1f, 0.9f, 0.55f, 1f);
            _guide = UiFactory.CreateBody(content, MountGuide(_session.Mount), 22, 340f, TextAnchor.UpperLeft);

            Button resetCorners = UiFactory.CreateButton(content, "四隅を指定し直す", new Color(0.28f, 0.32f, 0.38f, 1f));
            resetCorners.onClick.AddListener(ResetCorners);
            Button resetAll = UiFactory.CreateButton(content, "リセット", new Color(0.55f, 0.22f, 0.18f, 1f));
            resetAll.onClick.AddListener(ResetCalibration);
            _recordButton = UiFactory.CreateButton(content, "基準の深度を記録", new Color(0.16f, 0.42f, 0.58f, 1f));
            _recordLabel = _recordButton.GetComponentInChildren<Text>();
            _recordButton.onClick.AddListener(BeginRecording);
            Button source = UiFactory.CreateButton(content, "入力を切り替え", new Color(0.33f, 0.28f, 0.18f, 1f));
            _sourceLabel = source.GetComponentInChildren<Text>();
            source.onClick.AddListener(ToggleSource);
            UiFactory.CreateBody(content, "Kinect の置き方を、どちらか押して選んでください。", 22, 56f, TextAnchor.MiddleLeft);
            Button along = UiFactory.CreateButton(content, "横に設置", MountIdle);
            _alongMountImage = along.GetComponent<Image>();
            along.onClick.AddListener(SelectAlongWall);
            Button under = UiFactory.CreateButton(content, "真下に設置", MountIdle);
            _underMountImage = under.GetComponent<Image>();
            under.onClick.AddListener(SelectLookingUp);
            Toggle mirror = HudControls.CreateCheckbox(content, "左右を反転");
            LayoutElement mirrorLayout = mirror.gameObject.AddComponent<LayoutElement>();
            mirrorLayout.preferredHeight = 56f;
            bool mirrored = _session.Kinect == null || _session.Kinect.MirrorHorizontally;
            mirror.SetIsOnWithoutNotify(mirrored);
            mirror.onValueChanged.AddListener(OnMirrorChanged);
            _testButton = UiFactory.CreateButton(content, "模擬ボールを壁の中央へ", new Color(0.4f, 0.24f, 0.42f, 1f));
            _testButton.onClick.AddListener(InjectTestBall);

            _noise = UiFactory.CreateSlider(content, "ノイズ床", 15f, 150f, _tuning.NoiseFloorMillimeters);
            _approach = UiFactory.CreateSlider(content, "壁から何cm以内", DetectionTuning.MinApproachCentimeters, DetectionTuning.MaxApproachCentimeters, _tuning.GetApproachCentimeters());
            _approach.Slider.wholeNumbers = true;
            _area = UiFactory.CreateSlider(content, "最小の塊", 8f, 160f, _tuning.MinBlobArea);
            _personArea = UiFactory.CreateSlider(content, "人の大きさ", 40f, 20000f, _tuning.MaxBlobArea);
            _personSpan = UiFactory.CreateSlider(content, "人の長さ", 12f, 320f, _tuning.MaxBlobSpan);
            _personHold = UiFactory.CreateSlider(content, "人が去ったあとの無視", 0f, 20f, Mathf.RoundToInt(_tuning.PersonHoldSeconds * 10f));
            _confirm = UiFactory.CreateSlider(content, "安定フレーム", 1f, 5f, _tuning.ConfirmFrames);
            _noise.Slider.onValueChanged.AddListener(OnNoise);
            _approach.Slider.onValueChanged.AddListener(OnApproach);
            _area.Slider.onValueChanged.AddListener(OnArea);
            _personArea.Slider.onValueChanged.AddListener(OnPersonArea);
            _personSpan.Slider.onValueChanged.AddListener(OnPersonSpan);
            _personHold.Slider.onValueChanged.AddListener(OnPersonHold);
            _confirm.Slider.onValueChanged.AddListener(OnConfirm);

            _note = UiFactory.CreateBody(content, _noteBody, 22, 90f, TextAnchor.UpperLeft);
            _note.color = new Color(1f, 0.86f, 0.45f, 1f);
            _startButton = UiFactory.CreateButton(content, "ゲームを開始", new Color(0.15f, 0.48f, 0.3f, 1f));
            _startButton.onClick.AddListener(StartGame);
        }

        /// <summary>
        /// 自動で Kinect に寄ったとき、サイズの違う基準を捨てる。
        /// </summary>
        private void WatchSourceSwitch()
        {
            if (!_haveSeenSource)
            {
                _haveSeenSource = true;
                _seenUseKinect = _session.UseKinect;
                return;
            }

            if (_seenUseKinect == _session.UseKinect)
            {
                return;
            }

            _seenUseKinect = _session.UseKinect;
            CancelRecording();
            _lastSerial = -1;
            if (_session.Calibration != null)
            {
                _session.ClearCalibration();
                _noteBody = "入力が変わったので基準を破棄しました。録り直してください。";
            }

            ClearLocalCorners();
            _suppressSuggest = _session.UseKinect;
        }

        /// <summary>
        /// 新しい深度だけを処理する。
        /// </summary>
        private void PollDepth()
        {
            IDepthSource source = _session.ActiveSource;
            if (source == null || !source.HasFrame || source.Width <= 0 || source.Height <= 0)
            {
                return;
            }

            EnsureBuffer(source.Width, source.Height);
            int serial;
            if (!source.TryCopyLatest(_depth, out serial) || serial == _lastSerial)
            {
                return;
            }

            _lastSerial = serial;
            if (_session.Calibration != null && !_session.Calibration.Matches(source.Width, source.Height))
            {
                _session.ClearCalibration();
                CancelRecording();
                _noteBody = "深度のサイズが変わったので基準を破棄しました。";
            }

            if (_recording)
            {
                RecordFrame(source);
                return;
            }

            EnsureSuggestedCorners(source.Width, source.Height);
            byte[] highlight = null;
            if (_session.Detector != null && _session.Calibration != null && _session.Calibration.HasAllCorners)
            {
                _observedHits += _session.Detector.Detect(_depth, Time.unscaledTime, _hits);
                highlight = _session.Detector.PreviewMask;
            }

            _preview.SetFrame(_depth, source.Width, source.Height, highlight);
            _preview.SetCorners(_corners, _pickIndex >= 4 ? 4 : _pickIndex);
        }

        /// <summary>
        /// 全ゼロのフレームは枚数に数えない。
        /// </summary>
        private void RecordFrame(IDepthSource source)
        {
            if (_accumulator == null || _accumulator.Width != source.Width || _accumulator.Height != source.Height)
            {
                CancelRecording();
                return;
            }

            _preview.SetFrame(_depth, source.Width, source.Height, null);
            _preview.SetCorners(_corners, _pickIndex >= 4 ? 4 : _pickIndex);
            if (!BaselineAccumulator.ContainsMeasurement(_depth))
            {
                return;
            }

            if (_accumulator.Add(_depth))
            {
                FinishRecording();
            }
        }

        /// <summary>
        /// 表示用バッファを解像度に合わせる。
        /// </summary>
        private void EnsureBuffer(int width, int height)
        {
            int length = width * height;
            if (_depth != null && _depth.Length == length)
            {
                return;
            }

            _depth = new ushort[length];
            _lastSerial = -1;
        }

        /// <summary>
        /// シミュレーションでは、投影範囲の仮の四隅を入れてすぐ試せるようにする。
        /// </summary>
        private void EnsureSuggestedCorners(int width, int height)
        {
            if (_suppressSuggest || _suggested || _pickIndex > 0 || _session.UseKinect)
            {
                return;
            }

            float insetX = width * 0.08f;
            float insetY = height * 0.08f;
            _corners[0] = new Vector2(insetX, insetY);
            _corners[1] = new Vector2(width - insetX, insetY + 2f);
            _corners[2] = new Vector2(width - insetX - 3f, height - insetY);
            _corners[3] = new Vector2(insetX + 3f, height - insetY - 2f);
            for (int i = 0; i < 4; i++)
            {
                _defined[i] = true;
            }

            _pickIndex = 4;
            _suggested = true;
            PushCorners();
        }

        /// <summary>
        /// 四隅を順に受け、4点そろったあとは近い隅だけ動かす。
        /// </summary>
        private void OnPixelClicked(Vector2 pixel)
        {
            if (_recording)
            {
                return;
            }

            int index = _pickIndex < 4 ? _pickIndex : NearestCorner(pixel);
            if (_pickIndex < 4)
            {
                _pickIndex++;
            }

            _suggested = false;
            _defined[index] = true;
            _corners[index] = pixel;
            PushCorners();
            _preview.SetCorners(_corners, _pickIndex >= 4 ? 4 : _pickIndex);
        }

        /// <summary>
        /// 一番近い指定済みの隅。
        /// </summary>
        private int NearestCorner(Vector2 pixel)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                if (!_defined[i])
                {
                    continue;
                }

                float distance = (_corners[i] - pixel).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// 確定済みの基準へ四隅を反映する。
        /// </summary>
        private void PushCorners()
        {
            if (_session.Calibration == null)
            {
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                if (_defined[i])
                {
                    _session.Calibration.SetCorner(i, _corners[i]);
                }
            }

            if (_session.Detector != null)
            {
                _session.Detector.RebuildRoi();
            }

            _session.Remember();
        }

        /// <summary>
        /// 空の壁を30フレーム貯め始める。
        /// </summary>
        private void BeginRecording()
        {
            IDepthSource source = _session.ActiveSource;
            if (_recording || source == null || !source.HasFrame)
            {
                return;
            }

            _accumulator = new BaselineAccumulator(source.Width, source.Height, 30);
            _recording = true;
            _observedHits = 0;
            _noteBody = "壁の前から離れてください。通りがかりの数フレームは中央値ではじきます。";
        }

        /// <summary>
        /// 中央値を基準にして検知を始める。
        /// </summary>
        private void FinishRecording()
        {
            _recording = false;
            ushort[] median = _accumulator.CreateMedian();
            if (_session.Calibration != null && _session.Detector != null && _session.Calibration.Matches(_accumulator.Width, _accumulator.Height))
            {
                _session.Detector.SetBaseline(median);
                PushCorners();
            }
            else
            {
                CalibrationData data = new CalibrationData(_accumulator.Width, _accumulator.Height, median, _tuning);
                for (int i = 0; i < 4; i++)
                {
                    if (_defined[i])
                    {
                        data.SetCorner(i, _corners[i]);
                    }
                }

                _session.Commit(data);
            }

            _session.Remember();
            _noteBody = "基準を記録しました。壁際がオレンジなら、ゲームへ進めます。";
        }

        /// <summary>
        /// 記録を中断する。
        /// </summary>
        private void CancelRecording()
        {
            _recording = false;
            _accumulator = null;
        }

        /// <summary>
        /// 四隅を消して、最初の隅から取り直す。
        /// </summary>
        private void ResetCorners()
        {
            _suppressSuggest = true;
            ClearLocalCorners();
            if (_session.Calibration != null)
            {
                _session.Calibration.ClearCorners();
                if (_session.Detector != null)
                {
                    _session.Detector.RebuildRoi();
                }

                _session.Remember();
            }

            _noteBody = "左上から順に、投影範囲の四隅を深度画像でタップしてください。";
        }

        /// <summary>
        /// 四隅、基準、しきい値と保存ファイルを消し、最初の状態へ戻す。
        /// </summary>
        private void ResetCalibration()
        {
            CancelRecording();
            _session.ClearCalibration();
            _session.SetMount(KinectMount.AlongWall);
            _tuning = new DetectionTuning();
            ApplyTuningToSliders();
            _suppressSuggest = false;
            ClearLocalCorners();
            _noteBody = "キャリブレーションを消しました。四隅と基準からやり直してください。";
        }

        /// <summary>
        /// スライダーを、今のしきい値へ合わせる。
        /// </summary>
        private void ApplyTuningToSliders()
        {
            _noise.Slider.SetValueWithoutNotify(_tuning.NoiseFloorMillimeters);
            _approach.Slider.SetValueWithoutNotify(_tuning.GetApproachCentimeters());
            _area.Slider.SetValueWithoutNotify(_tuning.MinBlobArea);
            _personArea.Slider.SetValueWithoutNotify(_tuning.MaxBlobArea);
            _personSpan.Slider.SetValueWithoutNotify(_tuning.MaxBlobSpan);
            _personHold.Slider.SetValueWithoutNotify(Mathf.RoundToInt(_tuning.PersonHoldSeconds * 10f));
            _confirm.Slider.SetValueWithoutNotify(_tuning.ConfirmFrames);
        }

        /// <summary>
        /// 画面側の四隅だけ消す。
        /// </summary>
        private void ClearLocalCorners()
        {
            _suggested = false;
            _pickIndex = 0;
            for (int i = 0; i < 4; i++)
            {
                _defined[i] = false;
                _corners[i] = Vector2.zero;
            }
        }

        /// <summary>
        /// Kinect の深度を左右入れ替える。向きが変わるので、録り済みの基準は捨てる。
        /// </summary>
        /// <param name="mirror">反転するなら true。</param>
        private void OnMirrorChanged(bool mirror)
        {
            if (_session.Kinect != null)
            {
                _session.Kinect.MirrorHorizontally = mirror;
            }

            if (!_session.UseKinect)
            {
                _noteBody = mirror
                    ? "Kinect の映像は左右反転します。"
                    : "Kinect の映像は反転しません。";
                return;
            }

            CancelRecording();
            _session.ClearCalibration();
            ClearLocalCorners();
            _noteBody = "左右を反転しました。四隅と基準を録り直してください。";
        }

        /// <summary>
        /// 壁沿いの水平設置を選ぶ。
        /// </summary>
        private void SelectAlongWall()
        {
            SelectMount(KinectMount.AlongWall, "横に設置を選びました。四隅と基準を録り直してください。");
        }

        /// <summary>
        /// 壁の真下から見上げる設置を選ぶ。
        /// </summary>
        private void SelectLookingUp()
        {
            SelectMount(KinectMount.LookingUp, "真下に設置を選びました。四隅と基準を録り直してください。");
        }

        /// <summary>
        /// 設置を選ぶ。同じ設置をもう一度押したときは、録り済みの基準を残す。
        /// </summary>
        /// <param name="mount">選ぶ置き方。</param>
        /// <param name="note">切り替えたときに出す一文。</param>
        private void SelectMount(KinectMount mount, string note)
        {
            if (_session.Mount == mount)
            {
                return;
            }

            _session.SetMount(mount);
            CancelRecording();
            _session.ClearCalibration();
            ClearLocalCorners();
            _suppressSuggest = _session.UseKinect;
            _noteBody = note;
        }

        /// <summary>
        /// 今の設置の説明。
        /// </summary>
        /// <param name="mount">Kinect の置き方。</param>
        /// <returns>画面の案内。</returns>
        private static string MountGuide(KinectMount mount)
        {
            string place = mount == KinectMount.LookingUp
                ? "いまは壁の真下から上向きです。深度画像の下が近く、上が遠く映ります。"
                : "いまは壁と水平です。Xbox ロゴが操作側を向きます。深度画像の下が近く、上にいくほど遠くなります。";
            return place + "投影する範囲の四隅をタップしてください。人が映らない状態で基準を録り、そのあと壁際へボールを出してオレンジになるか見てください。スポンジボールのような軽い物は「壁から何cm以内」に入るとエフェクトになります。遠くの人は、この距離を狭めてください。近くに立つ人や歩く人は「人の大きさ」と「人の長さ」を、ボールだけ検知が増える位置まで下げてください。";
        }

        /// <summary>
        /// Kinect とシミュレーションを入れ替える。基準は捨てる。
        /// </summary>
        private void ToggleSource()
        {
            bool useKinect = !_session.UseKinect;
            _session.SetUseKinect(useKinect, true);
            if (useKinect)
            {
                _session.Kinect.Reconnect();
            }

            _session.ClearCalibration();
            CancelRecording();
            _lastSerial = -1;
            ClearLocalCorners();
            _suppressSuggest = useKinect;
            _seenUseKinect = useKinect;
            _haveSeenSource = true;
            _noteBody = useKinect
                ? "Kinect に切り替えました。映ったら四隅を指定し、基準を録り直してください。"
                : "シミュレーションに切り替えました。仮の四隅で手順を確認できます。";
        }

        /// <summary>
        /// 基準と四隅がそろっていればゲームシーンへ進む。足りなければ理由を出す。
        /// </summary>
        private void StartGame()
        {
            if (!CanStart())
            {
                _noteBody = StartBlockMessage();
                return;
            }

            _session.Remember();
            SceneManager.LoadScene(SceneNames.Game);
        }

        /// <summary>
        /// 開始できない理由。
        /// </summary>
        /// <returns>画面に出す一文。</returns>
        private string StartBlockMessage()
        {
            if (_recording)
            {
                return "基準の記録が終わるまで待ってください。";
            }

            if (_pickIndex < 4)
            {
                return "先に深度画像の四隅を、左上から順にタップしてください。";
            }

            if (_session.Calibration == null)
            {
                return "先に「基準の深度を記録」を押してください。壁の前から離れて録ります。";
            }

            if (!_session.Calibration.HasAllCorners)
            {
                return "投影範囲の四隅を、深度画像で4つ指定してください。";
            }

            if (_session.UseKinect && _suggested)
            {
                return "Kinect では仮の四隅では開始できません。画像をタップして四隅を合わせてください。";
            }

            return "まだ開始できません。四隅と基準を確認してください。";
        }

        /// <summary>
        /// スライダーとしきい値を画面へ反映する。
        /// </summary>
        private void RefreshTexts()
        {
            IDepthSource source = _session.ActiveSource;
            string device = source == null ? "入力を準備しています。" : source.StatusMessage;
            if (_session.UseKinect && _session.Kinect != null && !_session.Kinect.HasFrame && _session.KinectProbeRemaining > 0f)
            {
                device = "Kinect を探しています。（あと " + Mathf.CeilToInt(_session.KinectProbeRemaining) + " 秒）";
            }

            string hits = _session.Detector == null ? string.Empty : "  検知 " + _observedHits + " 回";
            if (_session.Detector != null && _session.Detector.PersonSuppressed)
            {
                hits += "  人を無視";
            }
            _status.text = device + hits;
            _instruction.text = BuildInstruction();
            _note.text = _noteBody;
            _recordLabel.text = _recording
                ? "記録中 " + _accumulator.CollectedFrames + " / " + _accumulator.TargetFrames
                : "基準の深度を記録";
            _sourceLabel.text = _session.UseKinect ? "シミュレーションで確認" : "Kinect を使う";
            bool lookingUp = _session.Mount == KinectMount.LookingUp;
            _alongMountImage.color = lookingUp ? MountIdle : MountSelected;
            _underMountImage.color = lookingUp ? MountSelected : MountIdle;
            _guide.text = MountGuide(_session.Mount);
            _recordButton.interactable = !_recording && source != null && source.HasFrame;
            _testButton.interactable = !_recording && !_session.UseKinect && _pickIndex >= 4;
            _noise.Caption.text = "ノイズ床  " + _tuning.NoiseFloorMillimeters + " mm（小さい揺れを無視）";
            _approach.Caption.text = "壁から " + _tuning.GetApproachCentimeters() + " cm以内";
            _area.Caption.text = "最小の塊  " + _tuning.MinBlobArea + " px";
            _personArea.Caption.text = "人の大きさ  " + _tuning.MaxBlobArea + " px";
            _personSpan.Caption.text = "人の長さ  " + _tuning.MaxBlobSpan + " px";
            _personHold.Caption.text = "人が去ったあと  " + _tuning.PersonHoldSeconds.ToString("0.0") + " 秒";
            _confirm.Caption.text = "小さい塊の安定フレーム  " + _tuning.ConfirmFrames;
        }

        /// <summary>
        /// 今やることを一文にする。
        /// </summary>
        private string BuildInstruction()
        {
            if (_recording)
            {
                return "記録中です。壁の前から離れてください。";
            }

            if (_pickIndex < 4)
            {
                return "深度画像の" + CornerNames[_pickIndex] + "をタップ（" + (_pickIndex + 1) + " / 4）";
            }

            if (_session.Calibration == null)
            {
                return "四隅は揃っています。「基準の深度を記録」を押してから、ゲームを開始してください。";
            }

            if (_session.Detector != null)
            {
                string coverage = _session.Detector.CoverageWarning();
                if (coverage != null)
                {
                    return coverage.Replace("\n", string.Empty);
                }
            }

            if (_suggested)
            {
                return "仮の四隅です。壁際がオレンジなら、ゲームを開始できます。";
            }

            return "四隅は指定済みです。壁際がオレンジなら、ゲームを開始できます。";
        }

        /// <summary>
        /// ゲームへ進める状態か。
        /// </summary>
        private bool CanStart()
        {
            if (_recording || _session.Calibration == null || !_session.Calibration.HasAllCorners)
            {
                return false;
            }

            if (_session.UseKinect && _suggested)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// シミュレーションの壁中央へ、手前に出るボールを数フレーム混ぜる。
        /// </summary>
        private void InjectTestBall()
        {
            if (_session.UseKinect || _pickIndex < 4)
            {
                return;
            }

            IDepthSource source = _session.ActiveSource;
            if (source == null)
            {
                return;
            }

            Vector2 pixel = QuadMapper.WallToPixel(0.5f, 0.5f, _corners[0], _corners[1], _corners[2], _corners[3]);
            int radius = Mathf.Max(6, source.Width / 34);
            int closer = Mathf.Max(_tuning.NoiseFloorMillimeters + 10, _tuning.MaxApproachMillimeters - 30);
            _session.Simulator.Inject(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y), radius, closer, 8);
        }

        /// <summary>ノイズ床を更新する。</summary>
        private void OnNoise(float value)
        {
            _tuning.NoiseFloorMillimeters = Mathf.RoundToInt(value);
            _session.Remember();
        }

        /// <summary>壁から何cm以内でエフェクトを出すかを更新する。</summary>
        /// <param name="value">センチメートル。</param>
        private void OnApproach(float value)
        {
            _tuning.SetApproachCentimeters(Mathf.RoundToInt(value));
            _session.Remember();
        }

        /// <summary>最小面積を更新する。</summary>
        private void OnArea(float value)
        {
            _tuning.MinBlobArea = Mathf.RoundToInt(value);
            _session.Remember();
        }

        /// <summary>人として無視する変化量を更新する。</summary>
        /// <param name="value">画素の合計。</param>
        private void OnPersonArea(float value)
        {
            _tuning.MaxBlobArea = Mathf.RoundToInt(value);
            _session.Remember();
        }

        /// <summary>人として無視する縦横の長さを更新する。</summary>
        /// <param name="value">画素数。</param>
        private void OnPersonSpan(float value)
        {
            _tuning.MaxBlobSpan = Mathf.RoundToInt(value);
            _session.Remember();
        }

        /// <summary>人が去ったあとに無視する時間を更新する。</summary>
        /// <param name="value">0.1秒単位。</param>
        private void OnPersonHold(float value)
        {
            _tuning.PersonHoldSeconds = value / 10f;
            _session.Remember();
        }

        /// <summary>安定フレーム数を更新する。</summary>
        private void OnConfirm(float value)
        {
            _tuning.ConfirmFrames = Mathf.RoundToInt(value);
            _session.Remember();
        }
    }
}
