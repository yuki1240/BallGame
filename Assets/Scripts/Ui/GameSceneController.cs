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
        private InstallationSession _session;
        private WallEffectDirector _director;
        private ScreenClickRelay _click;
        private readonly List<WallHit> _hits = new List<WallHit>(4);
        private ushort[] _depth;
        private Text _counter;
        private Text _idle;
        private GameObject _hitPlate;
        private GameObject _backButton;
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
            BuildUi();
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
            _backButton.SetActive(playing && _hudVisible);
            _idle.gameObject.SetActive(playing && _hitCount == 0);
            if (playing && _hitCount == 0)
            {
                _idle.text = _session.ActiveSource is SimulatedDepthSource
                    ? "ここが投影面です。\n画面をクリックすると、その位置に演出が出ます。\nスペースキーで HIT と調整を隠せます。"
                    : "ここが投影面です。\nボールが壁に当たると、その位置に演出が出ます。\nスペースキーで HIT と調整を隠せます。";
            }

            if (_click != null)
            {
                _click.gameObject.SetActive(playing && _session.ActiveSource is SimulatedDepthSource);
            }

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
            _counter.text = "HIT  " + _hitCount;
        }

        /// <summary>
        /// スペースキーで HIT と調整ボタンの表示を入れ替える。
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
            BuildWall(canvas);
            BuildClickCatcher(canvas);
            BuildEffects(canvas);
            BuildHud(canvas);

            _gate = new GameObject("Gate", typeof(RectTransform), typeof(Image));
            _gate.transform.SetParent(canvas, false);
            RectTransform gateRect = _gate.GetComponent<RectTransform>();
            Anchor(gateRect, new Vector2(0.18f, 0.28f), new Vector2(0.82f, 0.72f));
            _gate.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.13f, 0.96f);
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
            Button open = UiFactory.CreateButton(_gate.transform, "キャリブレーションへ", new Color(0.16f, 0.42f, 0.58f, 1f));
            open.onClick.AddListener(ReturnToCalibration);
        }

        /// <summary>
        /// 投影する壁の範囲。真っ黒だとシーンが切り替わったことが分からない。
        /// </summary>
        /// <param name="canvas">親キャンバス。</param>
        private static void BuildWall(RectTransform canvas)
        {
            RectTransform wall = UiFactory.CreateRegion(canvas, "Wall", new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.86f));
            Image image = wall.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.16f, 0.2f, 1f);
            image.raycastTarget = false;
            Outline outline = wall.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.86f, 0.95f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);
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
            _hitPlate = UiFactory.CreateRegion(canvas, "HitPlate", new Vector2(0.03f, 0.89f), new Vector2(0.28f, 0.97f)).gameObject;
            Image plateImage = _hitPlate.AddComponent<Image>();
            plateImage.color = new Color(0.08f, 0.1f, 0.14f, 0.94f);
            plateImage.raycastTarget = false;
            _counter = UiFactory.CreateText(_hitPlate.transform, "HIT  0", 42, TextAnchor.MiddleCenter);
            Anchor(_counter.rectTransform, Vector2.zero, Vector2.one);

            _idle = UiFactory.CreateText(canvas, string.Empty, 32, TextAnchor.MiddleCenter);
            _idle.color = new Color(0.9f, 0.95f, 0.98f, 1f);
            Anchor(_idle.rectTransform, new Vector2(0.16f, 0.34f), new Vector2(0.84f, 0.66f));

            Button back = UiFactory.CreateButton(canvas, "調整", new Color(0.16f, 0.48f, 0.62f, 1f));
            _backButton = back.gameObject;
            Anchor(back.GetComponent<RectTransform>(), new Vector2(0.84f, 0.89f), new Vector2(0.97f, 0.97f));
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
                WallHit hit = _hits[i];
                _director.Play(hit.WallU, hit.WallV);
                _hitCount++;
            }
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
            _session.Simulator.Inject(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y), radius, 240, 8);
        }

        /// <summary>
        /// 調整シーンへ戻る。基準はメモリに残る。
        /// </summary>
        private void ReturnToCalibration()
        {
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
