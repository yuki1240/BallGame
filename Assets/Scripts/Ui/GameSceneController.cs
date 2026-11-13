using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WallBurst.Depth;
using WallBurst.Detection;
using WallBurst.Effects;
using WallBurst.Session;

namespace WallBurst.Ui
{
    /// <summary>
    /// 投影面。基準との差分が壁際に出たら、その位置へ大きい演出を出す。
    /// </summary>
    public sealed class GameSceneController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("背景ごとの花と演出。Game シーンのこのコンポーネントへ割り当てる。")]
        private WallShowSettings _showSettings;

        private InstallationSession _session;
        private WallEffectDirector _director;
        private ScreenClickRelay _click;
        private readonly List<WallHit> _hits = new List<WallHit>(4);
        private ushort[] _depth;
        private Text _counter;
        private Text _idle;
        private GameObject _hitPlate;
        private GameObject _backButton;
        private GameObject _backgroundSettings;
        private RawImage _background;
        private Image _wallImage;
        private Color _wallColor;
        private Text _intervalLabel;
        private Text _approachLabel;
        private Dropdown _imageList;
        private Toggle _randomToggle;
        private Toggle _holdToggle;
        private Texture2D[] _textures = System.Array.Empty<Texture2D>();
        private GameObject _gate;
        private Text _gateBody;
        private int _lastSerial = -1;
        private int _hitCount;
        private int _injectFrame = -1;
        private bool _clockReady;
        private bool _hudVisible = true;

        /// <summary>
        /// 画面を組み、調整済みなら検知の時間情報だけリセットする。
        /// </summary>
        private void Awake()
        {
            UiFactory.EnsureEventSystem();
            _session = InstallationSession.EnsureExists();
            _textures = BackgroundImageLibrary.Load();
            _session.Backgrounds.Arm(Time.unscaledTime, _textures.Length);
            BuildUi();
            ApplyBackground();
            ShowCurrentImageOnList();
        }

        /// <summary>
        /// 深度が新しくなるたびにヒットを演出へ渡す。
        /// </summary>
        private void Update()
        {
            bool playing = CanPlay();
            PollHudToggle();
            _gate.SetActive(!playing);
            _hitPlate.SetActive(playing && _hudVisible);
            _backButton.SetActive(_hudVisible);
            _backgroundSettings.SetActive(_hudVisible);
            _idle.gameObject.SetActive(playing && _hitCount == 0);

            if (_click != null)
            {
                _click.gameObject.SetActive(playing && _session.ActiveSource is SimulatedDepthSource);
            }

            TickBackground();
            if (!playing)
            {
                _gateBody.text = GateMessage();
                return;
            }

            if (!_clockReady && _session.Detector != null)
            {
                _session.Detector.ResetTemporal();
                _clockReady = true;
            }

            PollPointerThrow();
            Poll();
            if (_hitCount == 0)
            {
                _idle.text = DescribeIdle();
            }

            _counter.text = "HIT  " + _hitCount;
        }

        /// <summary>
        /// 間隔が来ていれば次の背景にする。
        /// </summary>
        private void TickBackground()
        {
            if (!_session.Backgrounds.TryAdvance(Time.unscaledTime, _textures.Length, RollIndex))
            {
                return;
            }

            ApplyBackground();
            ShowCurrentImageOnList();
        }

        /// <summary>
        /// 0 以上 count 未満の乱数。
        /// </summary>
        /// <param name="count">枚数。</param>
        /// <returns>番号。</returns>
        private static int RollIndex(int count)
        {
            return UnityEngine.Random.Range(0, count);
        }

        /// <summary>
        /// スペースキーで HIT、背景の設定、キャリブレーションボタンの表示を入れ替える。
        /// </summary>
        private void PollHudToggle()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame)
            {
                return;
            }

            _hudVisible = !_hudVisible;
        }

        /// <summary>
        /// UI のクリックが届かないときも、シミュレーションでは押した位置へボールを流す。
        /// </summary>
        private void PollPointerThrow()
        {
            if (!(_session.ActiveSource is SimulatedDepthSource))
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            float wallU = Mathf.Clamp01(screen.x / Screen.width);
            float wallV = Mathf.Clamp01(screen.y / Screen.height);
            OnWallClicked(wallU, wallV);
        }

        /// <summary>
        /// 演出用の全面キャンバスと、調整へ戻るボタンを作る。
        /// </summary>
        private void BuildUi()
        {
            RectTransform canvas = UiFactory.CreateCanvas("GameCanvas");
            UiFactory.CreateStretch(canvas, "Background", new Color(0.04f, 0.05f, 0.07f, 1f));
            _background = CreateBackground(canvas);
            BuildWall(canvas);
            BuildClickCatcher(canvas);
            BuildEffects(canvas);
            BuildHud(canvas);

            _gate = new GameObject("Gate", typeof(RectTransform), typeof(Image));
            _gate.transform.SetParent(canvas, false);
            RectTransform gateRect = _gate.GetComponent<RectTransform>();
            Anchor(gateRect, new Vector2(0.18f, 0.28f), new Vector2(0.82f, 0.72f));
            _gate.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);
            VerticalLayoutGroup layout = _gate.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 36, 36);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            Text title = UiFactory.CreateText(_gate.transform, "キャリブレーションが必要です", 36, TextAnchor.MiddleCenter);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
            _gateBody = UiFactory.CreateText(_gate.transform, string.Empty, 24, TextAnchor.UpperCenter);
            _gateBody.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;
            Button open = UiFactory.CreateButton(_gate.transform, "キャリブレーションへ", new Color(0.9f, 0.93f, 0.95f, 1f));
            open.onClick.AddListener(ReturnToCalibration);
            BuildBackgroundSettings(canvas);
            _backButton.transform.SetAsLastSibling();
            PaintBlackText(canvas);
        }

        /// <summary>
        /// ゲーム画面の文字を黒にする。
        /// </summary>
        /// <param name="root">キャンバス。</param>
        private static void PaintBlackText(Transform root)
        {
            Text[] labels = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].color = Color.black;
            }
        }

        /// <summary>
        /// 全面の背景。画像が無いあいだは暗い色のまま。
        /// </summary>
        /// <param name="canvas">親キャンバス。</param>
        /// <returns>背景。</returns>
        private static RawImage CreateBackground(RectTransform canvas)
        {
            GameObject item = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
            item.transform.SetParent(canvas, false);
            Anchor(item.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            RawImage image = item.GetComponent<RawImage>();
            image.color = new Color(0.04f, 0.05f, 0.07f, 1f);
            image.raycastTarget = false;
            image.texture = null;
            return image;
        }

        /// <summary>
        /// 投影する壁の範囲。画像が無いときだけ色を置く。
        /// </summary>
        /// <param name="canvas">親キャンバス。</param>
        private void BuildWall(RectTransform canvas)
        {
            RectTransform wall = UiFactory.CreateRegion(canvas, "Wall", new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.86f));
            _wallImage = wall.gameObject.AddComponent<Image>();
            _wallColor = new Color(0.12f, 0.16f, 0.2f, 1f);
            _wallImage.color = _wallColor;
            _wallImage.raycastTarget = false;
        }

        /// <summary>
        /// 画面クリックを壁座標にする透明な受け皿。
        /// </summary>
        /// <param name="canvas">親キャンバス。</param>
        private void BuildClickCatcher(RectTransform canvas)
        {
            GameObject clickObject = new GameObject("WallClick", typeof(RectTransform), typeof(Image), typeof(ScreenClickRelay));
            clickObject.transform.SetParent(canvas, false);
            Anchor(clickObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            Image clickImage = clickObject.GetComponent<Image>();
            clickImage.color = new Color(0f, 0f, 0f, 0.004f);
            _click = clickObject.GetComponent<ScreenClickRelay>();
            _click.Clicked += OnWallClicked;
        }

        /// <summary>
        /// 演出を壁の上に重ねる。
        /// </summary>
        /// <param name="canvas">親キャンバス。</param>
        private void BuildEffects(RectTransform canvas)
        {
            GameObject effectObject = new GameObject("Effects", typeof(RectTransform), typeof(WallEffectDirector));
            effectObject.transform.SetParent(canvas, false);
            Anchor(effectObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            _director = effectObject.GetComponent<WallEffectDirector>();
        }

        /// <summary>
        /// ヒット数、待ち文、調整へ戻るボタン。
        /// </summary>
        /// <param name="canvas">親キャンバス。</param>
        private void BuildHud(RectTransform canvas)
        {
            _hitPlate = UiFactory.CreateRegion(canvas, "HitPlate", new Vector2(0.012f, 0.815f), new Vector2(0.16f, 0.90f)).gameObject;
            Image plateImage = _hitPlate.AddComponent<Image>();
            plateImage.color = new Color(1f, 1f, 1f, 0.88f);
            plateImage.raycastTarget = false;
            _counter = UiFactory.CreateText(_hitPlate.transform, "HIT  0", 42, TextAnchor.MiddleCenter);
            Anchor(_counter.rectTransform, Vector2.zero, Vector2.one);

            _idle = UiFactory.CreateText(canvas, string.Empty, 32, TextAnchor.MiddleCenter);
            _idle.color = Color.black;
            Anchor(_idle.rectTransform, new Vector2(0.16f, 0.34f), new Vector2(0.84f, 0.66f));

            Button back = UiFactory.CreateButton(canvas, "キャリブレーション", new Color(1f, 1f, 1f, 0.88f));
            _backButton = back.gameObject;
            Anchor(back.GetComponent<RectTransform>(), new Vector2(0.012f, 0.02f), new Vector2(0.24f, 0.09f));
            back.onClick.AddListener(ReturnToCalibration);
            _director.transform.SetSiblingIndex(_idle.transform.GetSiblingIndex() + 1);
        }

        /// <summary>
        /// 基準があり、今の深度と同じサイズで、四隅がある。
        /// </summary>
        private bool CanPlay()
        {
            IDepthSource source = _session.ActiveSource;
            CalibrationData data = _session.Calibration;
            if (source == null || data == null || _session.Detector == null || !data.HasAllCorners)
            {
                return false;
            }

            if (!source.HasFrame)
            {
                return false;
            }

            return data.Matches(source.Width, source.Height);
        }

        /// <summary>
        /// 画像リスト、ランダム、固定、間隔。上部にまとめ、スペースキーで隠れる。
        /// </summary>
        /// <param name="canvas">親キャンバス。</param>
        private void BuildBackgroundSettings(RectTransform canvas)
        {
            _backgroundSettings = new GameObject("BackgroundSettings", typeof(RectTransform));
            _backgroundSettings.transform.SetParent(canvas, false);
            Anchor(_backgroundSettings.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

            _imageList = HudControls.CreateImageList(_backgroundSettings.transform);
            Anchor(_imageList.GetComponent<RectTransform>(), new Vector2(0.012f, 0.915f), new Vector2(0.36f, 0.985f));
            FillImageList();
            _imageList.onValueChanged.AddListener(OnImageSelected);

            _randomToggle = HudControls.CreateCheckbox(_backgroundSettings.transform, "ランダム");
            Anchor(_randomToggle.GetComponent<RectTransform>(), new Vector2(0.37f, 0.915f), new Vector2(0.52f, 0.985f));
            _randomToggle.SetIsOnWithoutNotify(_session.Backgrounds.RandomOrder);
            _randomToggle.onValueChanged.AddListener(OnRandomChanged);

            _holdToggle = HudControls.CreateCheckbox(_backgroundSettings.transform, "固定");
            Anchor(_holdToggle.GetComponent<RectTransform>(), new Vector2(0.53f, 0.915f), new Vector2(0.66f, 0.985f));
            _holdToggle.SetIsOnWithoutNotify(_session.Backgrounds.HoldImage);
            _holdToggle.onValueChanged.AddListener(OnHoldChanged);

            _intervalLabel = UiFactory.CreateText(_backgroundSettings.transform, "5分", 26, TextAnchor.MiddleCenter);
            Anchor(_intervalLabel.rectTransform, new Vector2(0.67f, 0.915f), new Vector2(0.74f, 0.985f));

            Button shorter = UiFactory.CreateButton(_backgroundSettings.transform, "−", new Color(1f, 1f, 1f, 0.88f));
            Anchor(shorter.GetComponent<RectTransform>(), new Vector2(0.745f, 0.925f), new Vector2(0.80f, 0.975f));
            shorter.onClick.AddListener(ShortenInterval);

            Button longer = UiFactory.CreateButton(_backgroundSettings.transform, "＋", new Color(1f, 1f, 1f, 0.88f));
            Anchor(longer.GetComponent<RectTransform>(), new Vector2(0.805f, 0.925f), new Vector2(0.86f, 0.975f));
            longer.onClick.AddListener(LengthenInterval);

            _approachLabel = UiFactory.CreateText(_backgroundSettings.transform, "壁から\n15cm以内", 22, TextAnchor.MiddleCenter);
            Anchor(_approachLabel.rectTransform, new Vector2(0.865f, 0.915f), new Vector2(0.93f, 0.985f));

            Button nearer = UiFactory.CreateButton(_backgroundSettings.transform, "−", new Color(1f, 1f, 1f, 0.88f));
            Anchor(nearer.GetComponent<RectTransform>(), new Vector2(0.932f, 0.925f), new Vector2(0.958f, 0.975f));
            nearer.onClick.AddListener(NarrowApproach);

            Button farther = UiFactory.CreateButton(_backgroundSettings.transform, "＋", new Color(1f, 1f, 1f, 0.88f));
            Anchor(farther.GetComponent<RectTransform>(), new Vector2(0.962f, 0.925f), new Vector2(0.988f, 0.975f));
            farther.onClick.AddListener(WidenApproach);
            RefreshBackgroundControls();
        }

        /// <summary>
        /// エフェクトが出る距離を1cm狭くする。
        /// </summary>
        private void NarrowApproach()
        {
            ChangeApproach(-1);
        }

        /// <summary>
        /// エフェクトが出る距離を1cm広くする。
        /// </summary>
        private void WidenApproach()
        {
            ChangeApproach(1);
        }

        /// <summary>
        /// 壁から何cm以内でエフェクトを出すかを変える。
        /// </summary>
        /// <param name="stepCentimeters">足すセンチメートル。負なら狭くする。</param>
        private void ChangeApproach(int stepCentimeters)
        {
            if (_session.Calibration == null)
            {
                return;
            }

            DetectionTuning tuning = _session.Calibration.Tuning;
            tuning.SetApproachCentimeters(tuning.GetApproachCentimeters() + stepCentimeters);
            _session.Remember();
            RefreshApproachLabel();
        }

        /// <summary>
        /// 壁からの距離の表示を今の設定に合わせる。
        /// </summary>
        private void RefreshApproachLabel()
        {
            if (_approachLabel == null)
            {
                return;
            }

            if (_session.Calibration == null)
            {
                _approachLabel.text = "壁から\n—";
                return;
            }

            _approachLabel.text = "壁から\n" + _session.Calibration.Tuning.GetApproachCentimeters() + "cm以内";
        }

        /// <summary>
        /// 切替を1分短くする。
        /// </summary>
        private void ShortenInterval()
        {
            BackgroundPlaylist playlist = _session.Backgrounds;
            playlist.SetInterval(playlist.IntervalSeconds - 60f, Time.unscaledTime);
            RefreshBackgroundControls();
        }

        /// <summary>
        /// 切替を1分長くする。
        /// </summary>
        private void LengthenInterval()
        {
            BackgroundPlaylist playlist = _session.Backgrounds;
            playlist.SetInterval(playlist.IntervalSeconds + 60f, Time.unscaledTime);
            RefreshBackgroundControls();
        }

        /// <summary>
        /// ランダムのチェックを反映する。
        /// </summary>
        /// <param name="enabled">オンなら true。</param>
        private void OnRandomChanged(bool enabled)
        {
            _session.Backgrounds.SetRandomOrder(enabled);
        }

        /// <summary>
        /// 固定のチェックを反映する。オンのあいだは自動で切り替わらない。
        /// </summary>
        /// <param name="held">固定するなら true。</param>
        private void OnHoldChanged(bool held)
        {
            _session.Backgrounds.SetHoldImage(held, Time.unscaledTime);
        }

        /// <summary>
        /// リストで選んだ画像をすぐに出す。
        /// </summary>
        /// <param name="index">リストの番号。</param>
        private void OnImageSelected(int index)
        {
            if (_textures.Length == 0)
            {
                return;
            }

            _session.Backgrounds.Select(index, _textures.Length, Time.unscaledTime);
            ApplyBackground();
        }

        /// <summary>
        /// 読み込んだ画像名をリストへ入れ、いまの画像を選択状態にする。
        /// </summary>
        private void FillImageList()
        {
            _imageList.ClearOptions();
            List<string> names = new List<string>();
            if (_textures.Length == 0)
            {
                names.Add("画像なし");
                _imageList.AddOptions(names);
                _imageList.interactable = false;
                return;
            }

            for (int i = 0; i < _textures.Length; i++)
            {
                string imageName = _textures[i] == null ? "画像" : _textures[i].name;
                names.Add(imageName);
            }

            _imageList.AddOptions(names);
            _imageList.interactable = true;
            ShowCurrentImageOnList();
        }

        /// <summary>
        /// リストの選択を、いま出している画像に合わせる。
        /// </summary>
        private void ShowCurrentImageOnList()
        {
            if (_imageList == null || _textures.Length == 0)
            {
                return;
            }

            int index = _session.Backgrounds.CurrentIndex;
            if (index < 0 || index >= _textures.Length)
            {
                index = 0;
            }

            _imageList.SetValueWithoutNotify(index);
            _imageList.RefreshShownValue();
        }

        /// <summary>
        /// 間隔の表示を今の設定に合わせる。
        /// </summary>
        private void RefreshBackgroundControls()
        {
            int minutes = Mathf.RoundToInt(_session.Backgrounds.IntervalSeconds / 60f);
            _intervalLabel.text = minutes + "分";
            ShowCurrentImageOnList();
            RefreshApproachLabel();
        }

        /// <summary>
        /// いまの番号の画像を全面に出す。無ければ暗い色に戻す。
        /// </summary>
        private void ApplyBackground()
        {
            if (_background == null)
            {
                return;
            }

            if (_textures.Length == 0)
            {
                _background.texture = null;
                _background.color = new Color(0.04f, 0.05f, 0.07f, 1f);
                _wallImage.color = _wallColor;
                return;
            }

            int index = _session.Backgrounds.CurrentIndex;
            if (index < 0 || index >= _textures.Length)
            {
                index = 0;
            }

            _background.texture = _textures[index];
            _background.color = Color.white;
            _wallImage.color = new Color(_wallColor.r, _wallColor.g, _wallColor.b, 0f);
        }

        /// <summary>
        /// ゲートに出す理由。
        /// </summary>
        private string GateMessage()
        {
            if (_session.Calibration == null)
            {
                return "壁の基準深度と四隅を、調整画面で録ってから始めてください。";
            }

            IDepthSource source = _session.ActiveSource;
            if (source != null && source.HasFrame && !_session.Calibration.Matches(source.Width, source.Height))
            {
                return "深度のサイズが記録と違います。調整画面で録り直してください。";
            }

            if (!_session.Calibration.HasAllCorners)
            {
                return "投影範囲の四隅が未指定です。調整画面でタップしてください。";
            }

            return "深度を待っています。" + (_session.ActiveSource == null ? string.Empty : _session.ActiveSource.StatusMessage);
        }

        /// <summary>
        /// まだ一度も当たっていないときの案内。
        /// </summary>
        /// <returns>画面中央の文。</returns>
        private string DescribeIdle()
        {
            if (_session.Detector != null && _session.Detector.PersonSuppressed)
            {
                return "大きな変化を人として無視しています。\nボールだけなのに止まるときは、調整画面の「人の長さ」を上げてください。";
            }

            string coverage = _session.Detector == null ? null : _session.Detector.CoverageWarning();
            if (coverage != null)
            {
                return coverage;
            }

            if (_session.ActiveSource is SimulatedDepthSource)
            {
                return "ここが投影面です。\n画面をクリックすると、その位置に演出が出ます。\nスペースキーで HIT、背景、壁からの距離、キャリブレーションを隠せます。";
            }

            return "ここが投影面です。\nボールが壁に当たると、その位置に演出が出ます。\nスペースキーで HIT、背景、壁からの距離、キャリブレーションを隠せます。";
        }

        /// <summary>
        /// 新しいフレームだけ検知する。
        /// </summary>
        private void Poll()
        {
            IDepthSource source = _session.ActiveSource;
            EnsureBuffer(source.Width, source.Height);
            int serial;
            if (!source.TryCopyLatest(_depth, out serial) || serial == _lastSerial)
            {
                return;
            }

            _lastSerial = serial;
            int count = _session.Detector.Detect(_depth, Time.unscaledTime, _hits);
            for (int i = 0; i < count; i++)
            {
                ShowHit(_hits[i]);
                _hitCount++;
            }
        }

        /// <summary>
        /// 壁の下なら花、上ならその背景用の演出を出す。
        /// </summary>
        private void ShowHit(WallHit hit)
        {
            int imageIndex = _textures.Length == 0 ? 0 : _session.Backgrounds.CurrentIndex;
            Texture2D photo = ImageAt(imageIndex);
            RectTransform placed;
            if (!TrySproutFlower(hit, imageIndex, photo, out placed))
            {
                int kind = ChooseKind(imageIndex, photo);
                placed = _director.Play(hit.WallU, hit.WallV, kind);
            }

            LogHit(hit, placed);
        }

        /// <summary>
        /// 下側で、花が割り当ててあればそれを出す。
        /// </summary>
        /// <param name="hit">検知したボール。</param>
        /// <param name="imageIndex">名前順の背景番号。</param>
        /// <param name="photo">いまの背景。</param>
        /// <param name="placed">花を置いた矩形。</param>
        /// <returns>花を出したら true。</returns>
        private bool TrySproutFlower(WallHit hit, int imageIndex, Texture2D photo, out RectTransform placed)
        {
            placed = null;
            if (_showSettings == null || !_showSettings.IsLower(hit.WallV))
            {
                return false;
            }

            Sprite flower = _showSettings.PickFlower(imageIndex, photo, RollIndex);
            if (flower == null)
            {
                return false;
            }

            placed = _director.PlayFlower(hit.WallU, hit.WallV, flower);
            return placed != null;
        }

        /// <summary>
        /// 背景に指定した演出。設定が無いときは画像番号の既定3種。
        /// </summary>
        private int ChooseKind(int imageIndex, Texture2D photo)
        {
            if (_showSettings == null)
            {
                return EffectSets.Pick(imageIndex, RollIndex);
            }

            return (int)_showSettings.PickEffect(imageIndex, photo, RollIndex);
        }

        /// <summary>
        /// いまの背景画像。範囲外は null。
        /// </summary>
        private Texture2D ImageAt(int imageIndex)
        {
            if (imageIndex < 0 || imageIndex >= _textures.Length)
            {
                return null;
            }

            return _textures[imageIndex];
        }

        /// <summary>
        /// Kinect 上の 0〜1 と、画面へ置いた座標を出す。
        /// </summary>
        /// <param name="hit">検知したボール。</param>
        /// <param name="placed">画面に置いた矩形。</param>
        private void LogHit(WallHit hit, RectTransform placed)
        {
            IDepthSource source = _session.ActiveSource;
            int width = source == null ? 0 : source.Width;
            int height = source == null ? 0 : source.Height;
            float kinectU = width > 0 ? hit.PixelX / width : 0f;
            float kinectV = height > 0 ? hit.PixelY / height : 0f;
            Vector2 expected = WallToScreen(hit.WallU, hit.WallV);
            Vector2 shown = ShownPoint(placed);
            float gap = Vector2.Distance(expected, shown);
            string verdict = placed == null ? "表示なし" : (gap <= 2f ? "画面変換=一致" : "画面変換=ずれ " + gap.ToString("F1") + "px");

            Debug.Log(
                "ボール Kinect画素=(" + hit.PixelX.ToString("F1") + ", " + hit.PixelY.ToString("F1") + ")"
                + " " + width + "x" + height
                + " 画像内0-1 横=" + kinectU.ToString("F3")
                + " 縦=" + kinectV.ToString("F3") + "(上0)"
                + " 壁0-1=(" + hit.WallU.ToString("F3") + ", " + hit.WallV.ToString("F3") + ")"
                + " Unity画面=(" + expected.x.ToString("F1") + ", " + expected.y.ToString("F1") + ")"
                + " " + Screen.width + "x" + Screen.height
                + " 表示=(" + shown.x.ToString("F1") + ", " + shown.y.ToString("F1") + ")"
                + " " + verdict);
        }

        /// <summary>
        /// 壁の 0〜1 を、左下原点の画面ピクセルにする。
        /// </summary>
        /// <param name="wallU">横 0〜1。</param>
        /// <param name="wallV">縦 0〜1。下が 0。</param>
        /// <returns>画面座標。</returns>
        private static Vector2 WallToScreen(float wallU, float wallV)
        {
            return new Vector2(wallU * Screen.width, wallV * Screen.height);
        }

        /// <summary>
        /// 置いた矩形の中心、花なら根元の画面座標。
        /// </summary>
        /// <param name="placed">演出の矩形。</param>
        /// <returns>画面座標。無いときはゼロ。</returns>
        private static Vector2 ShownPoint(RectTransform placed)
        {
            if (placed == null)
            {
                return Vector2.zero;
            }

            Canvas.ForceUpdateCanvases();
            return placed.position;
        }

        /// <summary>
        /// 深度バッファを解像度に合わせる。
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
        /// クリック位置を壁座標とみなし、模擬のボールを深度へ入れる。
        /// </summary>
        private void OnWallClicked(float wallU, float wallV)
        {
            if (!(_session.ActiveSource is SimulatedDepthSource))
            {
                return;
            }

            CalibrationData data = _session.Calibration;
            if (data == null || !data.HasAllCorners)
            {
                return;
            }

            if (_injectFrame == Time.frameCount)
            {
                return;
            }

            _injectFrame = Time.frameCount;
            Vector2 pixel = QuadMapper.WallToPixel(wallU, wallV, data.Corners[0], data.Corners[1], data.Corners[2], data.Corners[3]);
            int radius = Mathf.Max(12, data.Width / 22);
            int closer = Mathf.Max(data.Tuning.NoiseFloorMillimeters + 10, data.Tuning.MaxApproachMillimeters - 30);
            _session.Simulator.Inject(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y), radius, closer, 8);
        }

        /// <summary>
        /// 四隅と基準を残して、調整シーンへ戻る。
        /// </summary>
        private void ReturnToCalibration()
        {
            _session.Remember();
            SceneManager.LoadScene(SceneNames.Calibration);
        }

        /// <summary>
        /// アンカーいっぱいに広げる。
        /// </summary>
        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    /// <summary>
    /// 全面のクリックを壁の正規化座標で渡す。
    /// </summary>
    public sealed class ScreenClickRelay : MonoBehaviour, IPointerDownHandler
    {
        /// <summary>横、縦（下が 0）。</summary>
        public event System.Action<float, float> Clicked;

        /// <summary>
        /// 押した位置を 0〜1 にする。離す前に届くので、速いクリックでも取りこぼさない。
        /// </summary>
        /// <param name="eventData">ポインタ。</param>
        public void OnPointerDown(PointerEventData eventData)
        {
            RectTransform rect = (RectTransform)transform;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out local))
            {
                return;
            }

            Rect area = rect.rect;
            float u = Mathf.InverseLerp(area.xMin, area.xMax, local.x);
            float v = Mathf.InverseLerp(area.yMin, area.yMax, local.y);
            if (Clicked != null)
            {
                Clicked(u, v);
            }
        }
    }
}
