using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// Christmas シーンの起動用オブジェクト。ボールが壁に当たるたびにクリスマスツリーへ飾り
    /// （いろいろな色のボールと赤い電球）を1つずつ付け、10回目にてっぺんの星が付いて完成します。
    /// 完成したら「クリスマスツリーの完成～！」ときらきらを出し、10秒のカウントダウンのあと次の場面（ランダム）へ移ります。
    /// 当たりは1秒に1個まで数えます。
    /// 10球そろわなくても、10分たつと草原へ移ります（最後の1分に当たったときは、さらに1分待ちます）。
    /// 遊ぶのは幼稚園の子どもなので、文字は大きく、ひらがなを多くしています。
    /// 3D の部屋（Polyart の PolyartChristmasScene）が入っていれば、その部屋の 3D のツリーを飾ります。
    /// 入っていないときは、これまでの 2D の絵のツリーで遊べます。
    /// </summary>
    public sealed class ChristmasBootstrap : MonoBehaviour
    {
        /// <summary>校正のシーン名。</summary>
        public const string CalibrationSceneName = "Calibration";

        /// <summary>「あと N こ！」の文字の大きさ。</summary>
        public const int CounterFontSize = 110;

        /// <summary>固定モード中の「デバックモード」の文字の大きさ（小さく目立たせない）。</summary>
        public const int DebugLabelFontSize = 40;

        /// <summary>お祝いの演出を出す間隔（秒）。</summary>
        public const float CelebrationBurstInterval = 0.35f;

        private static readonly Color[] OrnamentColors =
        {
            new Color(0.95f, 0.2f, 0.25f),
            new Color(1f, 0.82f, 0.2f),
            new Color(0.25f, 0.55f, 1f),
            new Color(1f, 0.45f, 0.75f),
            new Color(0.7f, 0.4f, 1f),
            new Color(1f, 0.6f, 0.15f),
            new Color(0.9f, 0.95f, 1f)
        };

        private static readonly Color StarDimColor = new Color(0.75f, 0.68f, 0.35f, 1f);
        private static readonly Color StarBrightColor = new Color(1f, 0.9f, 0.25f, 1f);
        private static readonly Color GoldText = new Color(1f, 0.92f, 0.35f, 1f);

        private static readonly Color BulbOffColor = new Color(0.45f, 0.08f, 0.1f, 1f);
        private static readonly Color BulbOnColor = new Color(1f, 0.15f, 0.12f, 1f);

        private readonly List<GameObject> bulbGlows = new List<GameObject>();
        private readonly List<Image> bulbBodies = new List<Image>();
        private readonly System.Random random = new System.Random();
        private readonly ChristmasFlow flow = new ChristmasFlow();
        private readonly SceneTimer timer = new SceneTimer();
        private CalibrationSession session;
        private List<Vector2> slots;
        private int[] slotOrder;
        private RectTransform ornamentLayer;
        private EffectSpawner spawner;
        private Image star;
        private Text instructionText;
        private Text counterText;
        private Text celebrationText;
        private Text countdownTitleText;
        private Text countdownNumberText;
        private Text operatorText;
        private GameObject overlay;
        private Button holdButton;
        private BallMarker markers;
        private bool markersShown;
        private float nextBurstAt;
        private string nextSceneName;
        private bool sceneRequested;
        private RectTransform backdrop;
        private ChristmasTree3D tree3D;
        private bool environmentReady;

        private void Start()
        {
            nextSceneName = SceneRotation.PickNext(SceneRotation.ChristmasScene);
            ApplyHoldMode();
            session = CalibrationSession.EnsureExists();
            EventSystemKeeper.Ensure();
            slots = ChristmasTreeLayout.GetOrnamentSlots();
            slotOrder = ChristmasTreeLayout.GetShuffledOrder(slots.Count, random);
            BuildScreen();
            RefreshTexts();
            if (ChristmasTree3D.CanLoadEnvironment())
            {
                StartCoroutine(LoadEnvironment());
            }
            else
            {
                FallBackTo2D();
            }
        }

        private IEnumerator LoadEnvironment()
        {
            // 3D の部屋（暖炉・雪だるま・プレゼントなど）を、このシーンに重ねて読み込む
            AsyncOperation loading = SceneManager.LoadSceneAsync(ChristmasTree3D.EnvironmentSceneName, LoadSceneMode.Additive);
            if (loading == null)
            {
                FallBackTo2D();
                yield break;
            }

            while (!loading.isDone)
            {
                yield return null;
            }

            Scene environment = SceneManager.GetSceneByName(ChristmasTree3D.EnvironmentSceneName);
            if (!environment.IsValid())
            {
                FallBackTo2D();
                yield break;
            }

            // 部屋の明かり・空の設定を使うため、部屋のシーンを「今のシーン」にする
            SceneManager.SetActiveScene(environment);
            tree3D = ChristmasTree3D.Create(environment);
            if (tree3D == null)
            {
                SceneManager.UnloadSceneAsync(environment);
                FallBackTo2D();
                yield break;
            }

            DisableOwnCamera();
            environmentReady = true;
            Debug.Log("[Christmas] 3D の部屋を読み込みました。");
        }

        private void DisableOwnCamera()
        {
            // 部屋のカメラで映すので、このシーンのカメラとリスナーは止める
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Camera ownCamera in root.GetComponentsInChildren<Camera>(true))
                {
                    ownCamera.enabled = false;
                }

                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                {
                    listener.enabled = false;
                }
            }
        }

        private void FallBackTo2D()
        {
            Debug.LogWarning("[Christmas] 3D の部屋（" + ChristmasTree3D.EnvironmentSceneName + "）を読み込めないため、2D のツリーで遊びます。");
            BuildSky(backdrop);
            BuildSnowGround(backdrop);
            BuildSnowFall(backdrop);
            BuildTree(backdrop);
            environmentReady = true;
        }

        private void Update()
        {
            session = CalibrationSession.EnsureExists();
            HandleKeyboard();
            HandleHits();
            UpdateBallMarkers();
            flow.Tick(Time.deltaTime);
            TickTimer();
            AnimateCelebration();
            RefreshTexts();
            GoToNextSceneWhenFinished();
        }

        private void BuildScreen()
        {
            Canvas canvas = UiFactory.CreateCanvas("ChristmasCanvas", 0);
            backdrop = CreateFullLayer(canvas.transform, "Backdrop（2D のときだけ使う）");
            BuildClickCatcher(canvas.transform);
            ornamentLayer = CreateFullLayer(canvas.transform, "Ornaments");
            star = BuildStar(canvas.transform);
            star.gameObject.SetActive(false);
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

        private void BuildClickCatcher(Transform parent)
        {
            // 模擬深度のときに画面のクリックをボールの当たりにするための、透明な受け口
            RectTransform rect = CreateFullLayer(parent, "ClickCatcher");
            Image catcher = rect.gameObject.AddComponent<Image>();
            catcher.color = new Color(1f, 1f, 1f, 0f);
            rect.gameObject.AddComponent<PointerReceiver>().Pressed += OnSkyPressed;
        }

        private static void BuildSky(Transform parent)
        {
            RectTransform rect = CreateFullLayer(parent, "Sky");
            RawImage sky = rect.gameObject.AddComponent<RawImage>();
            sky.texture = CreateGradientTexture(new Color(0.03f, 0.06f, 0.2f), new Color(0.12f, 0.25f, 0.5f));
            sky.raycastTarget = false;
        }

        private static Texture2D CreateGradientTexture(Color top, Color bottom)
        {
            Texture2D texture = new Texture2D(1, 256, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 256; y++)
            {
                texture.SetPixel(0, y, Color.Lerp(bottom, top, y / 255f));
            }

            texture.Apply(false, true);
            return texture;
        }

        private static void BuildSnowGround(Transform parent)
        {
            Image ground = PlaceImage(parent, "SnowGround", EffectShape.Dot, new Vector2(960f, -40f), new Vector2(2800f, 420f), new Color(0.93f, 0.96f, 1f));
            ground.raycastTarget = false;
        }

        private static void BuildSnowFall(Transform parent)
        {
            RectTransform rect = CreateFullLayer(parent, "SnowFall");
            rect.gameObject.AddComponent<SnowFall>().Build(70);
        }

        private static void BuildTree(Transform parent)
        {
            Color green = new Color(0.1f, 0.5f, 0.22f);
            Color darkGreen = new Color(0.06f, 0.38f, 0.16f);
            PlaceImage(parent, "Trunk", EffectShape.Bar, new Vector2(960f, 150f), new Vector2(120f, 150f), new Color(0.45f, 0.26f, 0.12f)).sprite = null;
            PlaceImage(parent, "TreeBottom", EffectShape.Triangle, new Vector2(960f, 400f), new Vector2(820f, 420f), darkGreen);
            PlaceImage(parent, "TreeMiddle", EffectShape.Triangle, new Vector2(960f, 590f), new Vector2(600f, 340f), green);
            PlaceImage(parent, "TreeTop", EffectShape.Triangle, new Vector2(960f, 765f), new Vector2(380f, 290f), green);
        }

        private static Image PlaceImage(Transform parent, string name, EffectShape shape, Vector2 center, Vector2 size, Color color)
        {
            RectTransform rect = UiFactory.CreateRect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = EffectSprites.Get(shape);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image BuildStar(Transform parent)
        {
            return PlaceImage(parent, "Star", EffectShape.Star, new Vector2(ChristmasTreeLayout.CenterX, ChristmasTreeLayout.ApexY + 25f), new Vector2(160f, 160f), StarDimColor);
        }

        private static EffectSpawner BuildEffectLayer(Transform parent)
        {
            RectTransform rect = CreateFullLayer(parent, "Effects");
            EffectSpawner effectSpawner = rect.gameObject.AddComponent<EffectSpawner>();
            GameEffectSettings settings = ScriptableObject.CreateInstance<GameEffectSettings>();
            settings.effectColor = new Color(1f, 0.85f, 0.3f, 1f);
            settings.effectScale = 1f;
            effectSpawner.Initialize(settings);
            return effectSpawner;
        }

        private void BuildKidsTexts(Transform parent)
        {
            instructionText = UiFactory.PlaceKidsText(parent, "ボールを かべに あてて ツリーを かざろう！", 58, Color.white, new Vector2(960f, 1015f), new Vector2(1800f, 90f));
            counterText = UiFactory.PlaceKidsText(parent, string.Empty, CounterFontSize, GoldText, new Vector2(330f, 640f), new Vector2(560f, 300f));
            celebrationText = UiFactory.PlaceKidsText(parent, "クリスマスツリーの完成～！", 120, GoldText, new Vector2(960f, 520f), new Vector2(1900f, 200f));
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
                // テスト用：Kinect がなくても、画面の中ほどにボールが当たったことにする
                OnWallHit(new Vector2(0.3f + 0.4f * (float)random.NextDouble(), 0.3f + 0.3f * (float)random.NextDouble()));
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
            flow.SetGoal(SceneHold.GetGoal(SceneHold.TreeGoal));
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
            foreach (Vector2 wallUv in WallHitInput.CollectHits(session, "Christmas"))
            {
                OnWallHit(wallUv);
            }
        }

        private void OnWallHit(Vector2 wallUv)
        {
            if (SceneHold.IsHolding)
            {
                // 固定モード：エフェクトも飾りも出さず、ボールのいる場所だけ見せる
                markers.Mark(wallUv);
                return;
            }

            if (flow.Phase == ChristmasPhase.Playing)
            {
                // 最後の1分に当たったときは、場面を替える時間が1分延びる
                timer.NotifyHit();
            }

            // 当たったことは毎回エフェクトで知らせる。
            // 飾りが付くのは、前に数えてから1秒以上たった当たりだけ（1秒以内の当たりはエフェクトのみ）
            spawner.SpawnEffect(EffectKind.FlashParticles, wallUv);
            if (!environmentReady)
            {
                return;
            }

            int ornamentIndex = flow.HitCount;
            bool isFinal = flow.IsNextFinal;
            if (!flow.RegisterHit(Time.time))
            {
                return;
            }

            if (isFinal)
            {
                PlaceStar(wallUv);
            }
            else
            {
                AddOrnament(ornamentIndex, wallUv);
            }
        }

        private void AddOrnament(int ornamentIndex, Vector2 wallUv)
        {
            int slotIndex = slotOrder[ornamentIndex % slotOrder.Length];
            bool redBulb = ChristmasTreeLayout.IsRedBulb(ornamentIndex);
            if (tree3D != null)
            {
                // 壁の位置（左下 0・右上 1）は、そのまま画面の位置として使う
                tree3D.AddOrnament(slotIndex, redBulb, OrnamentColors[random.Next(OrnamentColors.Length)], wallUv);
                return;
            }

            Vector2 slot = slots[slotIndex];
            Image ornament = redBulb ? CreateRedBulb(ornamentIndex, slot) : CreateBall(ornamentIndex, slot);
            ornament.gameObject.AddComponent<OrnamentFlight>().Fly(ToLayer(wallUv), slot);
        }

        private Vector2 ToLayer(Vector2 wallUv)
        {
            return new Vector2(wallUv.x * ornamentLayer.rect.width, wallUv.y * ornamentLayer.rect.height);
        }

        private Image CreateBall(int ornamentIndex, Vector2 slot)
        {
            Image ball = PlaceImage(ornamentLayer, "Ball " + (ornamentIndex + 1), EffectShape.Dot, slot, new Vector2(84f, 84f), OrnamentColors[random.Next(OrnamentColors.Length)]);
            AddCenteredChild(ball.transform, "Shine", EffectShape.SoftCircle, new Vector2(-16f, 16f), new Vector2(34f, 34f), new Color(1f, 1f, 1f, 0.85f));
            return ball;
        }

        private Image CreateRedBulb(int ornamentIndex, Vector2 slot)
        {
            // 透明な入れ物の中に、光（ぴかぴか）・電球・口金を重ねる
            // 電球は☆が付くまで消えたまま（暗い赤）。☆が付いたら LightUpBulbs で点ける
            Image holder = PlaceImage(ornamentLayer, "RedBulb " + (ornamentIndex + 1), EffectShape.Dot, slot, new Vector2(70f, 96f), new Color(1f, 1f, 1f, 0f));
            Image glow = AddCenteredChild(holder.transform, "Glow", EffectShape.SoftCircle, Vector2.zero, new Vector2(170f, 170f), new Color(1f, 0.25f, 0.2f, 0.9f));
            glow.gameObject.AddComponent<Twinkle>().Setup(ornamentIndex * 1.3f, 5f);
            glow.gameObject.SetActive(false);
            bulbGlows.Add(glow.gameObject);
            Image body = AddCenteredChild(holder.transform, "Bulb", EffectShape.Dot, new Vector2(0f, -6f), new Vector2(58f, 80f), BulbOffColor);
            bulbBodies.Add(body);
            AddCenteredChild(holder.transform, "Shine", EffectShape.SoftCircle, new Vector2(-10f, 6f), new Vector2(22f, 30f), new Color(1f, 0.85f, 0.85f, 0.9f));
            Image cap = AddCenteredChild(holder.transform, "Cap", EffectShape.Bar, new Vector2(0f, 40f), new Vector2(30f, 20f), new Color(0.75f, 0.75f, 0.7f));
            cap.sprite = null;
            return holder;
        }

        private static Image AddCenteredChild(Transform parent, string name, EffectShape shape, Vector2 offset, Vector2 size, Color color)
        {
            Image child = PlaceImage(parent, name, shape, Vector2.zero, size, color);
            RectTransform rect = child.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            return child;
        }

        private void PlaceStar(Vector2 wallUv)
        {
            if (tree3D != null)
            {
                tree3D.PlaceStar(wallUv);
                return;
            }

            star.gameObject.SetActive(true);
            star.gameObject.AddComponent<OrnamentFlight>().Fly(ToLayer(wallUv), ChristmasTreeLayout.StarPosition);
            LightUpBulbs();
        }

        private void LightUpBulbs()
        {
            foreach (GameObject glow in bulbGlows)
            {
                glow.SetActive(true);
            }

            foreach (Image body in bulbBodies)
            {
                body.color = BulbOnColor;
            }
        }

        private void AnimateCelebration()
        {
            bool celebrating = flow.Phase == ChristmasPhase.Celebrating || flow.Phase == ChristmasPhase.Countdown;
            if (!celebrating)
            {
                return;
            }

            if (tree3D != null)
            {
                tree3D.SetCelebrating(true);
            }
            else
            {
                star.color = StarBrightColor;
                star.transform.localRotation = Quaternion.Euler(0f, 0f, 8f * Mathf.Sin(Time.time * 3f));
            }

            if (Time.time >= nextBurstAt)
            {
                nextBurstAt = Time.time + CelebrationBurstInterval;
                SpawnCelebrationBurst();
            }
        }

        private void SpawnCelebrationBurst()
        {
            // きらきら：ツリーのまわりに光の粒を出す
            Rect area = tree3D != null ? tree3D.GetViewportRect() : Get2DTreeArea();
            Vector2 wallUv = new Vector2(
                area.xMin + (float)random.NextDouble() * area.width,
                area.yMin + (float)random.NextDouble() * area.height);
            EffectKind kind = random.Next(3) == 0 ? EffectKind.Flash : EffectKind.FlashParticles;
            spawner.SpawnEffect(kind, wallUv);
        }

        private static Rect Get2DTreeArea()
        {
            Vector2 size = UiFactory.ReferenceResolution;
            float xMin = (ChristmasTreeLayout.CenterX - 450f) / size.x;
            float yMin = ChristmasTreeLayout.BaseY / size.y;
            return new Rect(xMin, yMin, 900f / size.x, (ChristmasTreeLayout.ApexY - ChristmasTreeLayout.BaseY + 100f) / size.y);
        }

        private void TickTimer()
        {
            // 飾り付け中だけ時間を数える（完成の演出とカウントダウンは別に進む）
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
            counterText.gameObject.SetActive(playing);
            counterText.fontSize = SceneHold.IsHolding ? DebugLabelFontSize : CounterFontSize;
            counterText.text = SceneHold.IsHolding ? "デバックモード" : "あと\n" + flow.Remaining + "こ！";
            celebrationText.gameObject.SetActive(phase == ChristmasPhase.Celebrating);
            celebrationText.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 5f));
            // 10球そろったあとのカウントダウン、または10分たつ前の最後の10秒のカウントダウン
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
            string mode = tree3D != null ? "3D" : environmentReady ? "2D" : "3D 読み込み中";
            return "クリスマス（" + mode + "）　飾り " + flow.HitCount + " / " + flow.Goal + "　場面が替わるまで " + timer.FormatRemaining() + SceneHold.GetOperatorHint() + state + "\n" + blob + click;
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
