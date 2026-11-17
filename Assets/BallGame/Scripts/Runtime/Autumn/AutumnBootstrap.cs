using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// Autumn シーンの起動用オブジェクト。緑の山の絵に、ボールが当たった場所から赤・だいだい・黄色の紅葉が広がります。
    /// 山ぜんぶが紅葉したら「もみじの やまが できた！」と出して、10秒のカウントダウンのあと次の場面（ランダム）へ移ります。
    /// 完成しなくても、10分たつと次の場面へ移ります（最後の1分に当たったときは、さらに1分待ちます）。
    /// 遊ぶのは幼稚園の子どもなので、文字は大きく、ひらがなを多くしています。
    /// </summary>
    public sealed class AutumnBootstrap : MonoBehaviour
    {
        /// <summary>校正のシーン名。</summary>
        public const string CalibrationSceneName = "Calibration";

        /// <summary>山の絵を描き直す間隔（秒）。</summary>
        public const float TextureRefreshInterval = 0.05f;

        /// <summary>1回の当たりで舞い上がる葉の数。</summary>
        public const int LeavesPerHit = 14;

        private static readonly Color GoldText = new Color(1f, 0.92f, 0.35f, 1f);

        private readonly System.Random random = new System.Random();
        private readonly ChristmasFlow flow = new ChristmasFlow();
        private readonly SceneTimer timer = new SceneTimer();
        private CalibrationSession session;
        private AutumnScenery scenery;
        private AutumnColoring coloring;
        private Texture2D texture;
        private Color32[] green;
        private Color32[] autumn;
        private Color32[] pixels;
        private RectTransform leafLayer;
        private EffectSpawner spawner;
        private Text instructionText;
        private Text percentText;
        private Text celebrationText;
        private Text countdownTitleText;
        private Text countdownNumberText;
        private Text operatorText;
        private GameObject overlay;
        private Button holdButton;
        private BallMarker markers;
        private bool markersShown;
        private float nextRefreshAt;
        private float nextBurstAt;
        private string nextSceneName;
        private bool sceneRequested;

        private void Start()
        {
            nextSceneName = SceneRotation.PickNext(SceneRotation.AutumnScene);
            ApplyHoldMode();
            session = CalibrationSession.EnsureExists();
            EventSystemKeeper.Ensure();
            BuildScenery();
            BuildScreen();
            RefreshTexts();
        }

        private void Update()
        {
            session = CalibrationSession.EnsureExists();
            HandleKeyboard();
            HandleHits();
            UpdateBallMarkers();
            TickColoring();
            flow.Tick(Time.deltaTime);
            TickTimer();
            AnimateCelebration();
            RefreshTexts();
            GoToNextSceneWhenFinished();
        }

        private void BuildScenery()
        {
            scenery = AutumnScenery.Generate();
            coloring = new AutumnColoring(scenery.Width, scenery.Height, scenery.Mask, scenery.EdgeNoise);
            green = ToColor32(scenery.Green);
            autumn = ToColor32(scenery.Autumn);
            pixels = (Color32[])green.Clone();
            texture = new Texture2D(scenery.Width, scenery.Height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        private static Color32[] ToColor32(Color[] colors)
        {
            Color32[] result = new Color32[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                result[i] = new Color32(
                    (byte)Mathf.RoundToInt(colors[i].r * 255f),
                    (byte)Mathf.RoundToInt(colors[i].g * 255f),
                    (byte)Mathf.RoundToInt(colors[i].b * 255f),
                    255);
            }

            return result;
        }

        private void BuildScreen()
        {
            Canvas canvas = UiFactory.CreateCanvas("AutumnCanvas", 0);
            BuildMountain(canvas.transform);
            BuildClickCatcher(canvas.transform);
            BuildLeafFall(canvas.transform);
            leafLayer = CreateFullLayer(canvas.transform, "Leaves");
            spawner = BuildEffectLayer(canvas.transform);
            markers = BallMarker.Create(canvas.transform);
            BuildKidsTexts(canvas.transform);
            BuildOperatorOverlay(canvas.transform);
        }

        private static RectTransform CreateFullLayer(Transform parent, string name)
        {
            RectTransform rect = UiFactory.CreateRect(parent, name);
            UiFactory.Stretch(rect);
            return rect;
        }

        private void BuildMountain(Transform parent)
        {
            RectTransform rect = CreateFullLayer(parent, "Mountain");
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
        }

        private void BuildClickCatcher(Transform parent)
        {
            // 模擬深度のときに画面のクリックをボールの当たりにするための、透明な受け口
            RectTransform rect = CreateFullLayer(parent, "ClickCatcher");
            Image catcher = rect.gameObject.AddComponent<Image>();
            catcher.color = new Color(1f, 1f, 1f, 0f);
            rect.gameObject.AddComponent<PointerReceiver>().Pressed += OnSkyPressed;
        }

        private static void BuildLeafFall(Transform parent)
        {
            RectTransform rect = CreateFullLayer(parent, "LeafFall");
            rect.gameObject.AddComponent<LeafFall>().Build(26);
        }

        private static EffectSpawner BuildEffectLayer(Transform parent)
        {
            RectTransform rect = CreateFullLayer(parent, "Effects");
            EffectSpawner effectSpawner = rect.gameObject.AddComponent<EffectSpawner>();
            GameEffectSettings settings = ScriptableObject.CreateInstance<GameEffectSettings>();
            settings.effectColor = new Color(1f, 0.7f, 0.2f, 1f);
            settings.effectScale = 1f;
            effectSpawner.Initialize(settings);
            return effectSpawner;
        }

        private void BuildKidsTexts(Transform parent)
        {
            instructionText = UiFactory.PlaceKidsText(parent, "ボールを かべに あてて やまを あかく そめよう！", 58, Color.white, new Vector2(960f, 1015f), new Vector2(1800f, 90f));
            percentText = UiFactory.PlaceKidsText(parent, string.Empty, 100, GoldText, new Vector2(330f, 900f), new Vector2(560f, 160f));
            celebrationText = UiFactory.PlaceKidsText(parent, "もみじの やまが できた！", 120, GoldText, new Vector2(960f, 520f), new Vector2(1900f, 200f));
            countdownTitleText = UiFactory.PlaceKidsText(parent, SceneRotation.GetKidsTitle(nextSceneName), 80, Color.white, new Vector2(960f, 760f), new Vector2(1600f, 140f));
            countdownNumberText = UiFactory.PlaceKidsText(parent, string.Empty, 340, GoldText, new Vector2(960f, 440f), new Vector2(600f, 420f));
        }

        private void BuildOperatorOverlay(Transform parent)
        {
            overlay = UiFactory.CreateRect(parent, "Overlay（スペースで隠す）").gameObject;
            UiFactory.Stretch((RectTransform)overlay.transform);
            Image panel = UiFactory.CreatePanel(overlay.transform, "OperatorPanel", UiFactory.PanelColor);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(20f, 20f);
            rect.sizeDelta = new Vector2(1420f, 250f);
            operatorText = UiFactory.CreateText(panel.transform, string.Empty, 24, TextAnchor.UpperLeft);
            UiFactory.PlaceTopLeft(operatorText.rectTransform, 16f, 10f, 820f, 230f);
            Button back = UiFactory.CreateButton(panel.transform, "校正へ戻る", 26, OnBackToCalibration);
            UiFactory.PlaceTopLeft((RectTransform)back.transform, 860f, 30f, 220f, 70f);
            holdButton = HoldButton.Create(panel.transform, 1110f, 30f, ToggleHold);
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

            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                // テスト用：Kinect がなくても、山のどこかにボールが当たったことにする
                OnWallHit(new Vector2(0.05f + 0.9f * (float)random.NextDouble(), 0.1f + 0.5f * (float)random.NextDouble()));
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

        private void HandleHits()
        {
            foreach (Vector2 wallUv in WallHitInput.CollectHits(session, "Autumn"))
            {
                OnWallHit(wallUv);
            }
        }

        private void OnWallHit(Vector2 wallUv)
        {
            if (SceneHold.IsHolding)
            {
                // 固定モード：エフェクトも葉も紅葉もさせず、ボールのいる場所だけ見せる
                markers.Mark(wallUv);
                return;
            }

            if (flow.Phase != ChristmasPhase.Playing)
            {
                return;
            }

            // 最後の1分に当たったときは、場面を替える時間が1分延びる
            timer.NotifyHit();
            coloring.AddSource(wallUv);
            spawner.SpawnEffect(EffectKind.FlashParticles, wallUv);
            Vector2 canvasPosition = new Vector2(wallUv.x * UiFactory.ReferenceResolution.x, wallUv.y * UiFactory.ReferenceResolution.y);
            LeafParticle.Burst(leafLayer, canvasPosition, random, LeavesPerHit);
        }

        private void TickColoring()
        {
            bool changed = coloring.Tick(Time.deltaTime);
            if (changed && Time.time >= nextRefreshAt)
            {
                nextRefreshAt = Time.time + TextureRefreshInterval;
                RefreshTexture();
            }

            if (coloring.IsAllColored && flow.Phase == ChristmasPhase.Playing && !SceneHold.IsHolding)
            {
                RefreshTexture();
                flow.Complete();
            }
        }

        private void RefreshTexture()
        {
            int minY;
            int maxY;
            if (!coloring.TryGetDirtyRows(out minY, out maxY))
            {
                return;
            }

            for (int y = minY; y <= maxY; y++)
            {
                BlendRow(y);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            coloring.ClearDirty();
        }

        private void BlendRow(int y)
        {
            int width = scenery.Width;
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float progress = coloring.GetProgress(x, y);
                pixels[index] = progress <= 0f ? green[index] : progress >= 1f ? autumn[index] : Color32.Lerp(green[index], autumn[index], progress);
            }
        }

        private void AnimateCelebration()
        {
            bool celebrating = flow.Phase == ChristmasPhase.Celebrating || flow.Phase == ChristmasPhase.Countdown;
            if (!celebrating || Time.time < nextBurstAt)
            {
                return;
            }

            nextBurstAt = Time.time + 0.3f;
            Vector2 wallUv = new Vector2(0.05f + 0.9f * (float)random.NextDouble(), 0.1f + 0.6f * (float)random.NextDouble());
            spawner.SpawnEffect(random.Next(3) == 0 ? EffectKind.Flash : EffectKind.FlashParticles, wallUv);
            Vector2 canvasPosition = new Vector2(wallUv.x * UiFactory.ReferenceResolution.x, wallUv.y * UiFactory.ReferenceResolution.y);
            LeafParticle.Burst(leafLayer, canvasPosition, random, 6);
        }

        private void TickTimer()
        {
            // 紅葉させている間だけ時間を数える（完成の演出とカウントダウンは別に進む）
            if (flow.Phase == ChristmasPhase.Playing && !SceneHold.IsHolding)
            {
                timer.Tick(Time.unscaledDeltaTime);
            }
        }

        private void RefreshTexts()
        {
            ChristmasPhase phase = flow.Phase;
            bool playing = phase == ChristmasPhase.Playing;
            instructionText.gameObject.SetActive(playing);
            percentText.gameObject.SetActive(playing);
            percentText.text = Mathf.FloorToInt(coloring.Coverage * 100f) + "%";
            celebrationText.gameObject.SetActive(phase == ChristmasPhase.Celebrating);
            celebrationText.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 5f));
            // 完成したあとのカウントダウン、または10分たつ前の最後の10秒のカウントダウン
            int countdownNumber = phase == ChristmasPhase.Countdown ? flow.GetCountdownNumber() : timer.GetCountdownNumber();
            countdownNumber = SceneHold.IsHolding ? 0 : countdownNumber;
            bool countdown = countdownNumber > 0;
            countdownTitleText.gameObject.SetActive(countdown);
            countdownNumberText.gameObject.SetActive(countdown);
            countdownNumberText.text = countdownNumber.ToString();
            countdownNumberText.rectTransform.localScale = Vector3.one * (phase == ChristmasPhase.Countdown ? GetCountdownPop() : GetTimerCountdownPop(countdownNumber));
            operatorText.text = BuildOperatorText();
            HoldButton.Refresh(holdButton);
        }

        private float GetTimerCountdownPop(int number)
        {
            float withinSecond = Mathf.Clamp01(number - timer.RemainingSeconds);
            return Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(withinSecond * 4f));
        }

        private float GetCountdownPop()
        {
            float withinSecond = flow.GetPhaseElapsed() % 1f;
            return Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(withinSecond * 4f));
        }

        private string BuildOperatorText()
        {
            DetectionResult result = session.LatestResult;
            string blob = result != null ? result.DescribeLargestBlob() : string.Empty;
            string state = result != null && result.PersonDetected ? "（人を検知中）" : string.Empty;
            string click = (session.IsSimulated ? "\n模擬深度：画面をクリックするとボールが当たります" : string.Empty) + "\nＴキー：テスト用に1球当たったことにする";
            return "紅葉の山　当たり " + coloring.SourceCount + " 回　紅葉 " + Mathf.FloorToInt(coloring.Coverage * 100f) + "%　場面が替わるまで " + timer.FormatRemaining() + SceneHold.GetOperatorHint() + state + "\n" + blob + click;
        }

        private void OnSkyPressed(Vector2 wallUv)
        {
            WallHitInput.InjectClick(session, wallUv);
        }

        private void OnBackToCalibration()
        {
            sceneRequested = true;
            SceneManager.LoadScene(CalibrationSceneName);
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
    }
}
