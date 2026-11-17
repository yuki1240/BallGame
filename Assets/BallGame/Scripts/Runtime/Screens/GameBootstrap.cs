using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// Game シーンの起動用オブジェクト。実行時に 1920×1080 の Canvas でゲーム画面を組み立てます。
    /// 四隅で囲った範囲を背景全体へ引き伸ばし、当たった位置に花か演出を出します。
    /// スペースキーで上部の操作・HIT 表示・校正へ戻るボタン・壁からの距離をまとめて隠します。
    /// ボールが10回当たると、または10分たつと、10秒のカウントダウンのあと次の場面（ランダム）へ移ります。
    /// 10分の最後の1分に当たったときは、さらに1分待ちます。
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>Calibration シーンの名前。</summary>
        public const string CalibrationSceneName = "Calibration";

        /// <summary>切替間隔の1回の増減（秒）。</summary>
        public const float IntervalStepSeconds = 60f;

        [SerializeField]
        [Tooltip("演出設定（ScriptableObject）。空のときは初期値で動きます。")]
        private GameEffectSettings settings;

        [SerializeField]
        [Tooltip("最初に出す背景画像の名前（空なら名前順で最初の画像）。")]
        private string startBackgroundName = string.Empty;

        [SerializeField]
        [Tooltip("オンにすると、背景を自動で切り替えない（固定）状態で始める。")]
        private bool startFixed;

        private readonly System.Random random = new System.Random();
        private CalibrationSession session;
        private List<Texture2D> backgrounds = new List<Texture2D>();
        private BackgroundRotation rotation;
        private RawImage background;
        private EffectSpawner spawner;
        private GameObject overlay;
        private Text backgroundNameText;
        private Text intervalText;
        private Text distanceText;
        private Text hitText;
        private Button holdButton;
        private BallMarker markers;
        private bool markersShown;
        private Text messageText;
        private Button randomButton;
        private Button fixedButton;
        private readonly ChristmasFlow flow = new ChristmasFlow();
        private readonly SceneTimer timer = new SceneTimer();
        private Text celebrationText;
        private Text countdownTitleText;
        private Text countdownNumberText;
        private string nextSceneName;
        private bool sceneRequested;
        private int hitCount;
        private string lastHitDescription = "まだ当たっていません";

        /// <summary>演出設定。</summary>
        public GameEffectSettings Settings
        {
            get { return settings; }
        }

        private void Start()
        {
            nextSceneName = SceneRotation.PickNext(SceneRotation.MeadowScene);
            ApplyHoldMode();
            session = CalibrationSession.EnsureExists();
            EventSystemKeeper.Ensure();
            EnsureSettings();
            LoadBackgrounds();
            BuildScreen();
            ApplyBackground();
            RefreshTexts();
        }

        private void Update()
        {
            session = CalibrationSession.EnsureExists();
            HandleKeyboard();
            if (rotation.Tick(Time.unscaledDeltaTime))
            {
                ApplyBackground();
            }

            ProcessHits();
            flow.Tick(Time.deltaTime);
            TickTimer();
            RefreshTexts();
            GoToNextSceneWhenFinished();
        }

        private void TickTimer()
        {
            // 遊んでいる間だけ時間を数える（10球そろったあとは別のカウントダウンが進む）
            if (flow.Phase == ChristmasPhase.Playing && !SceneHold.IsHolding)
            {
                timer.Tick(Time.unscaledDeltaTime);
            }
        }

        private void GoToNextSceneWhenFinished()
        {
            if (SceneHold.IsHolding)
            {
                return;
            }

            bool timeUp = flow.Phase == ChristmasPhase.Playing && timer.IsExpired;
            if ((flow.Phase != ChristmasPhase.Finished && !timeUp) || sceneRequested)
            {
                return;
            }

            sceneRequested = true;
            SceneManager.LoadScene(nextSceneName);
        }

        private void EnsureSettings()
        {
            if (settings == null)
            {
                Debug.LogWarning("GameBootstrap に演出設定が割り当てられていないため、初期値で動きます。");
                settings = ScriptableObject.CreateInstance<GameEffectSettings>();
            }
        }

        private void LoadBackgrounds()
        {
            backgrounds = BackgroundLibrary.LoadAll();
            rotation = new BackgroundRotation(backgrounds.Count);
            rotation.IsFixed = startFixed;
            SelectStartBackground();
        }

        private void SelectStartBackground()
        {
            int index = backgrounds.FindIndex(texture => string.Equals(texture.name, startBackgroundName, System.StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                rotation.Select(index);
            }
        }

        private void BuildScreen()
        {
            Canvas canvas = UiFactory.CreateCanvas("GameCanvas", 0);
            BuildBackground(canvas.transform);
            BuildEffectLayer(canvas.transform);
            markers = BallMarker.Create(canvas.transform);
            overlay = UiFactory.CreateRect(canvas.transform, "Overlay（スペースで隠す）").gameObject;
            UiFactory.Stretch((RectTransform)overlay.transform);
            BuildTopBar(overlay.transform);
            BuildHitPanel(overlay.transform);
            BuildMessage(canvas.transform);
            BuildKidsTexts(canvas.transform);
        }

        private void BuildKidsTexts(Transform parent)
        {
            Color gold = new Color(1f, 0.92f, 0.35f, 1f);
            celebrationText = UiFactory.PlaceKidsText(parent, "おはなが いっぱい！", 120, gold, new Vector2(960f, 600f), new Vector2(1900f, 200f));
            countdownTitleText = UiFactory.PlaceKidsText(parent, SceneRotation.GetKidsTitle(nextSceneName), 80, Color.white, new Vector2(960f, 760f), new Vector2(1600f, 140f));
            countdownNumberText = UiFactory.PlaceKidsText(parent, string.Empty, 340, gold, new Vector2(960f, 440f), new Vector2(600f, 420f));
        }

        private void BuildBackground(Transform parent)
        {
            RectTransform rect = UiFactory.CreateRect(parent, "Background");
            UiFactory.Stretch(rect);
            background = rect.gameObject.AddComponent<RawImage>();
            background.color = Color.white;
            rect.gameObject.AddComponent<PointerReceiver>().Pressed += OnBackgroundPressed;
        }

        private void BuildEffectLayer(Transform parent)
        {
            RectTransform rect = UiFactory.CreateRect(parent, "Effects");
            UiFactory.Stretch(rect);
            spawner = rect.gameObject.AddComponent<EffectSpawner>();
            spawner.Initialize(settings);
        }

        private void BuildTopBar(Transform parent)
        {
            Image bar = UiFactory.CreatePanel(parent, "TopBar", UiFactory.PanelColor);
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 84f);
            BuildTopBarItems(bar.transform);
        }

        private void BuildTopBarItems(Transform bar)
        {
            float x = 14f;
            x = AddBarButton(bar, "◀", x, 60f, OnPreviousBackground);
            backgroundNameText = AddBarText(bar, x, 380f, 24);
            x += 380f;
            x = AddBarButton(bar, "▶", x, 60f, OnNextBackground);
            randomButton = CreateBarButton(bar, "ランダム", ref x, 190f, OnRandomPressed);
            fixedButton = CreateBarButton(bar, "固定", ref x, 160f, OnFixedPressed);
            AddBarText(bar, x, 90f, 24).text = "切替";
            x += 90f;
            x = AddBarButton(bar, "－", x, 56f, () => ChangeInterval(-IntervalStepSeconds));
            intervalText = AddBarText(bar, x, 130f, 26);
            x += 130f;
            x = AddBarButton(bar, "＋", x, 56f, () => ChangeInterval(IntervalStepSeconds));
            AddBarText(bar, x, 170f, 24).text = "壁からの距離";
            x += 170f;
            x = AddBarButton(bar, "－", x, 56f, () => ChangeDistance(-1));
            distanceText = AddBarText(bar, x, 90f, 26);
            x += 90f;
            x = AddBarButton(bar, "＋", x, 56f, () => ChangeDistance(1));
            AddBarButton(bar, "校正へ戻る", x + 10f, 190f, OnBackToCalibration);
        }

        private static float AddBarButton(Transform bar, string label, float x, float width, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiFactory.CreateButton(bar, label, 26, onClick);
            UiFactory.PlaceTopLeft((RectTransform)button.transform, x, 12f, width, 60f);
            return x + width + 8f;
        }

        private static Button CreateBarButton(Transform bar, string label, ref float x, float width, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiFactory.CreateButton(bar, label, 26, onClick);
            UiFactory.PlaceTopLeft((RectTransform)button.transform, x, 12f, width, 60f);
            x += width + 8f;
            return button;
        }

        private static Text AddBarText(Transform bar, float x, float width, int fontSize)
        {
            Text text = UiFactory.CreateText(bar, string.Empty, fontSize, TextAnchor.MiddleCenter);
            UiFactory.PlaceTopLeft(text.rectTransform, x, 12f, width, 60f);
            return text;
        }

        private void BuildHitPanel(Transform parent)
        {
            Image panel = UiFactory.CreatePanel(parent, "HitPanel", UiFactory.PanelColor);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(20f, 20f);
            rect.sizeDelta = new Vector2(1420f, 270f);
            hitText = UiFactory.CreateText(panel.transform, string.Empty, 26, TextAnchor.UpperLeft);
            UiFactory.PlaceTopLeft(hitText.rectTransform, 16f, 10f, 1070f, 250f);
            holdButton = HoldButton.Create(panel.transform, 1110f, 20f, ToggleHold);
        }

        private void BuildMessage(Transform parent)
        {
            Image panel = UiFactory.CreatePanel(parent, "Message", UiFactory.PanelColor);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1100f, 200f);
            panel.raycastTarget = false;
            messageText = UiFactory.CreateText(panel.transform, string.Empty, 34, TextAnchor.MiddleCenter);
            UiFactory.Stretch(messageText.rectTransform);
        }

        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                // 固定モード：いまの場面をずっと続ける（場面の切り替えとカウントダウンをしない）
                ToggleHold();
            }
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                overlay.SetActive(!overlay.activeSelf);
            }
        }

        private void ToggleHold()
        {
            SceneHold.Toggle();
            ApplyHoldMode();
            HoldButton.Refresh(holdButton);
        }

        private void ApplyHoldMode()
        {
            flow.SetGoal(SceneHold.GetGoal(SceneHold.EndlessGoal));
        }

        private void UpdateBallMarkers()
        {
            if (!SceneHold.IsHolding)
            {
                if (markersShown)
                {
                    markers.Clear();
                    markersShown = false;
                }

                return;
            }

            markersShown = true;
            foreach (Vector2 position in WallHitInput.CollectBallPositions(session))
            {
                markers.Mark(position);
            }
        }

        private void ProcessHits()
        {
            UpdateBallMarkers();
            if (SceneHold.IsHolding)
            {
                // 固定モード：花もエフェクトも出さず、ボールのいる場所だけ見せる
                return;
            }

            DetectionResult result = session.LatestResult;
            if (!session.HasNewFrame || result == null || !session.HasHomography)
            {
                return;
            }

            foreach (DetectedHit hit in result.Hits)
            {
                HandleHit(hit);
            }
        }

        private void HandleHit(DetectedHit hit)
        {
            Vector2 raw = session.ImageToWall.Transform(hit.ImageUv);
            Vector2 wallUv = new Vector2(Mathf.Clamp01(raw.x), Mathf.Clamp01(raw.y));
            HitReactionKind reaction = HitReactionSelector.Classify(wallUv.y, settings.flowerBoundary);
            BackgroundTheme theme = settings.FindTheme(GetCurrentBackgroundName(), rotation.CurrentIndex);
            RectTransform placed = SpawnReaction(reaction, theme, wallUv, out string reactionName);
            hitCount++;
            RegisterForSceneChange();
            LogHit(hit, wallUv, placed, reactionName);
        }

        private void RegisterForSceneChange()
        {
            if (flow.Phase != ChristmasPhase.Playing)
            {
                return;
            }

            // 最後の1分に当たったときは、場面を替える時間が1分延びる。10球目で完成にする（1秒に1個まで数える）
            timer.NotifyHit();
            flow.RegisterHit(Time.time);
        }

        private RectTransform SpawnReaction(HitReactionKind reaction, BackgroundTheme theme, Vector2 wallUv, out string reactionName)
        {
            if (reaction == HitReactionKind.Flower)
            {
                Sprite flower = HitReactionSelector.PickFlower(theme, random);
                if (flower != null)
                {
                    reactionName = "花 " + flower.name;
                    return spawner.SpawnFlower(flower, wallUv);
                }
            }

            EffectKind kind = HitReactionSelector.PickEffect(HitReactionSelector.ResolveEffects(theme, rotation.CurrentIndex), random);
            reactionName = reaction == HitReactionKind.Flower ? "演出 " + kind + "（花が未割り当て）" : "演出 " + kind;
            return spawner.SpawnEffect(kind, wallUv);
        }

        private void LogHit(DetectedHit hit, Vector2 wallUv, RectTransform placed, string reactionName)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, placed.position);
            Vector2 canvas = placed.anchoredPosition;
            lastHitDescription = string.Format("壁({0:F2}, {1:F2})  {2}", wallUv.x, wallUv.y, reactionName);
            Debug.Log(string.Format(
                "[HIT] Kinect=({0:F3}, {1:F3}) 画素=({2:F1}, {3:F1}) 面積={4} → 壁=({5:F3}, {6:F3}) → Unity画面=({7:F1}, {8:F1}) Canvas=({9:F1}, {10:F1}) {11}",
                hit.ImageUv.x, hit.ImageUv.y, hit.Pixel.x, hit.Pixel.y, hit.Area,
                wallUv.x, wallUv.y, screen.x, screen.y, canvas.x, canvas.y, reactionName));
        }

        private void OnBackgroundPressed(Vector2 wallUv)
        {
            SimulatedDepthSource simulator = session != null ? session.Simulator : null;
            if (simulator == null || !session.HasHomography)
            {
                return;
            }

            Vector2 imageUv = session.WallToImage.Transform(wallUv);
            if (imageUv.x < 0f || imageUv.y < 0f || imageUv.x > 1f || imageUv.y > 1f)
            {
                return;
            }

            simulator.InjectBall(imageUv);
        }

        private void OnPreviousBackground()
        {
            rotation.SelectPrevious();
            ApplyBackground();
        }

        private void OnNextBackground()
        {
            rotation.SelectNext();
            ApplyBackground();
        }

        private void OnRandomPressed()
        {
            rotation.IsRandom = !rotation.IsRandom;
        }

        private void OnFixedPressed()
        {
            rotation.IsFixed = !rotation.IsFixed;
        }

        private void ChangeInterval(float deltaSeconds)
        {
            rotation.SetInterval(rotation.IntervalSeconds + deltaSeconds);
        }

        private void ChangeDistance(int deltaCm)
        {
            session.SetMaxDistanceCm(session.Data.GetMaxDistanceCm() + deltaCm);
        }

        private void OnBackToCalibration()
        {
            SceneManager.LoadScene(CalibrationSceneName);
        }

        private void ApplyBackground()
        {
            if (backgrounds.Count == 0)
            {
                background.texture = null;
                background.color = new Color(0.97f, 0.97f, 0.97f, 1f);
                return;
            }

            background.texture = backgrounds[rotation.CurrentIndex];
            background.color = Color.white;
        }

        private string GetCurrentBackgroundName()
        {
            return backgrounds.Count == 0 ? string.Empty : backgrounds[rotation.CurrentIndex].name;
        }

        private void RefreshTexts()
        {
            HoldButton.Refresh(holdButton);
            backgroundNameText.text = backgrounds.Count == 0 ? "背景画像なし" : (rotation.CurrentIndex + 1) + "/" + backgrounds.Count + " " + GetCurrentBackgroundName();
            UiFactory.SetButtonLabel(randomButton, "ランダム：" + (rotation.IsRandom ? "オン" : "オフ"));
            UiFactory.SetButtonLabel(fixedButton, "固定：" + (rotation.IsFixed ? "オン" : "オフ"));
            UiFactory.SetButtonColor(randomButton, rotation.IsRandom ? UiFactory.SelectedColor : UiFactory.ButtonColor);
            UiFactory.SetButtonColor(fixedButton, rotation.IsFixed ? UiFactory.SelectedColor : UiFactory.ButtonColor);
            intervalText.text = FormatInterval(rotation.IntervalSeconds);
            distanceText.text = session.Data.GetMaxDistanceCm() + "cm";
            hitText.text = BuildHitText();
            RefreshMessage();
            RefreshKidsTexts();
        }

        private void RefreshKidsTexts()
        {
            ChristmasPhase phase = flow.Phase;
            celebrationText.gameObject.SetActive(phase == ChristmasPhase.Celebrating);
            celebrationText.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 5f));

            // 10球そろったあとのカウントダウン、または10分たつ前の最後の10秒のカウントダウン
            int number = SceneHold.IsHolding ? 0 : phase == ChristmasPhase.Countdown ? flow.GetCountdownNumber() : timer.GetCountdownNumber();
            bool countdown = number > 0;
            countdownTitleText.gameObject.SetActive(countdown);
            countdownNumberText.gameObject.SetActive(countdown);
            countdownNumberText.text = number.ToString();
            float withinSecond = phase == ChristmasPhase.Countdown ? flow.GetPhaseElapsed() % 1f : Mathf.Clamp01(number - timer.RemainingSeconds);
            countdownNumberText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(withinSecond * 4f));
        }

        private string BuildHitText()
        {
            string state = string.Empty;
            DetectionResult result = session.LatestResult;
            if (result != null && result.PersonDetected)
            {
                state = "　人を検知中（ボールは出しません）";
            }
            else if (result != null && result.InCooldown)
            {
                state = "　人が去ったあとの待ち時間";
            }

            string clickHint = session.IsSimulated ? "\n模擬深度：画面をクリックするとボールが当たります" : string.Empty;
            string blob = result != null ? "\n" + result.DescribeLargestBlob() : string.Empty;
            string sceneChange = "　" + SceneRotation.GetOperatorName(nextSceneName) + "へ：あと" + flow.Remaining + "球 / " + timer.FormatRemaining() + SceneHold.GetOperatorHint();
            return "HIT " + hitCount + sceneChange + state + "\n最後：" + lastHitDescription + blob + clickHint;
        }

        private void RefreshMessage()
        {
            bool needsCalibration = !session.HasHomography || !session.Detector.IsConfigured;
            messageText.transform.parent.gameObject.SetActive(needsCalibration);
            messageText.text = "校正が終わっていません。\n「校正へ戻る」から四隅と基準を設定してください。";
        }

        private static string FormatInterval(float seconds)
        {
            int minutes = Mathf.RoundToInt(seconds / 60f);
            return minutes >= 60 ? "1時間" : minutes + "分";
        }
    }
}
