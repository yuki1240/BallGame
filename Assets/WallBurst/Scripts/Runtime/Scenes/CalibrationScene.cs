using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WallBurst
{
    /// <summary>
    /// キャリブレーション（設置・調整）のシーンです。現場の職員が、画面の案内だけで進められるようにしています。
    /// <list type="number">
    /// <item>Kinect の確認：つながっているかを見る</item>
    /// <item>壁を空にする：人と物をどけて、壁の「基準の深度」を録る</item>
    /// <item>四隅をタップ：深度の見本の上で、投影範囲の四隅（壁の端）を順にタップする。深度を使って、台形を壁の上の長方形に直す</item>
    /// <item>試し投げ：ボールを投げて、当たった所に印が出るか確かめる</item>
    /// <item>（試し投げで）Kinect を置いた側（左・右）を選ぶ：Kinect に近い側が画面の同じ側になる（左右反転）</item>
    /// <item>完了：保存して、ゲームを始める</item>
    /// </list>
    /// 前回の設定があるときは、最初に「前回の設定ではじめる」を出します。
    /// </summary>
    public sealed class CalibrationScene : MonoBehaviour
    {
        private enum Step
        {
            Resume,
            Sensor,
            Baseline,
            Corners,
            TestThrow,
            Done
        }

        private const int StepCount = 5;
        private const float CountdownSeconds = 3f;
        private const float MarkerSeconds = 3f;

        private static readonly string[] CornerNames = { "①左上", "②右上", "③右下", "④左下" };

        private sealed class HitMarker
        {
            public Image Ring;
            public float Born;
        }

        private readonly List<Vector2> tapped = new List<Vector2>();
        private readonly List<HitMarker> hitMarkers = new List<HitMarker>();

        private WallBurstSession session;
        private Step step;
        private Canvas canvas;
        private Text titleText;
        private Text instructionText;
        private Text statusText;
        private Text bigText;
        private Text ballSizeText;
        private RectTransform buttonPanel;
        private CalibrationPreview preview;
        private RectifiedDepthView rectified;
        private GameObject headerObject;
        private GameObject sideObject;
        private GameObject legendObject;
        private GameObject compactPanel;
        private int lastRenderedFrame = -1;
        private BaselineRecorder recorder;
        private float countdownEnd = -1f;
        private string resultMessage = string.Empty;
        private int testHits;

        private void Start()
        {
            session = WallBurstSession.Instance;
            session.HitDetected += HandleHit;
            Ui.EnsureEventSystem();
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            bool resume = session.IsReady || session.LoadSavedCalibration();
            BuildLayout();
            GoTo(resume ? Step.Resume : Step.Sensor);
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.HitDetected -= HandleHit;
            }
        }

        private void Update()
        {
            UpdateBaseline();
            UpdatePreview();
            UpdateStatus();
            UpdateHitMarkers();
            HandlePointer();
            if (step == Step.Sensor)
            {
                RefreshSensorText();
            }
        }

        // ---------------------------------------------------------------
        // 画面の組み立て
        // ---------------------------------------------------------------
        private void BuildLayout()
        {
            canvas = Ui.CreateCanvas("キャリブレーション", 0);
            rectified = new RectifiedDepthView(canvas.transform);
            Image header = Ui.CreatePanel(canvas.transform, "見出し", Ui.PanelColor);
            headerObject = header.gameObject;
            Ui.Place(header.rectTransform, new Vector2(0f, 850f), new Vector2(1920f, 230f));
            titleText = Ui.CreateText(header.transform, string.Empty, 54, TextAnchor.UpperLeft);
            titleText.fontStyle = FontStyle.Bold;
            Ui.Place(titleText.rectTransform, new Vector2(30f, 140f), new Vector2(1860f, 80f));
            instructionText = Ui.CreateText(header.transform, string.Empty, 30, TextAnchor.UpperLeft);
            Ui.Place(instructionText.rectTransform, new Vector2(30f, 4f), new Vector2(1860f, 132f));

            preview = new CalibrationPreview(canvas.transform, new Vector2(20f, 110f), new Vector2(1160f, 730f), session.Source.Width, session.Source.Height);
            Text legend = Ui.CreateText(canvas.transform, "灰色：壁や物（明るいほど近い）　黒：深度が取れない　紫：ボールを取りにくい所\n赤：ボール　青：体・手　黄：ちらつき　緑：四隅の台形", 22, TextAnchor.UpperLeft);
            Ui.Place(legend.rectTransform, new Vector2(24f, 40f), new Vector2(1160f, 66f));
            legendObject = legend.gameObject;

            Image side = Ui.CreatePanel(canvas.transform, "操作", Ui.PanelColor);
            Ui.Place(side.rectTransform, new Vector2(1200f, 40f), new Vector2(700f, 800f));
            sideObject = side.gameObject;
            GameObject panel = new GameObject("ボタン", typeof(RectTransform));
            panel.transform.SetParent(side.transform, false);
            buttonPanel = (RectTransform)panel.transform;
            Ui.Place(buttonPanel, new Vector2(20f, 300f), new Vector2(660f, 480f));
            BuildSettings(side.transform);

            statusText = Ui.CreateText(canvas.transform, string.Empty, 22, TextAnchor.LowerLeft);
            Ui.Place(statusText.rectTransform, new Vector2(24f, 6f), new Vector2(1160f, 34f));
            statusText.color = new Color(0.8f, 0.85f, 0.9f);

            bigText = Ui.CreateText(canvas.transform, string.Empty, 200, TextAnchor.MiddleCenter);
            bigText.fontStyle = FontStyle.Bold;
            Ui.Place(bigText.rectTransform, new Vector2(20f, 110f), new Vector2(1160f, 730f));
            Outline outline = bigText.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(4f, -4f);

            BuildCompactPanel();
            CalibrationData data = session.Calibration;
            preview.SetView(data.PreviewRotation, data.PreviewMirror);
        }

        // 深度を壁に重ねて表示しているときだけ出す、小さな操作パネル（画面の下の真ん中）
        private void BuildCompactPanel()
        {
            Image panel = Ui.CreatePanel(canvas.transform, "重ねて表示中の操作", new Color(0.08f, 0.1f, 0.16f, 0.85f));
            Ui.Place(panel.rectTransform, new Vector2(360f, 50f), new Vector2(1200f, 190f));
            Text note = Ui.CreateText(panel.transform, "台形の中の深度を、壁に重ねて映しています。赤＝ボール　青＝体・手　黄＝ちらつき　黒＝深度が取れない\n手を壁に当てると、その位置に写ります。写った位置が手とずれていれば、四隅をやりなおしてください。", 24, TextAnchor.UpperLeft);
            Ui.Place(note.rectTransform, new Vector2(20f, 100f), new Vector2(1160f, 80f));
            Button back = Ui.CreateButton(panel.transform, "重ねる表示をやめる", Ui.ButtonColor, () => SetRectified(false));
            Ui.Place((RectTransform)back.transform, new Vector2(20f, 14f), new Vector2(560f, 76f));
            Button ok = Ui.CreateButton(panel.transform, "合っている（つぎへ）", Ui.PrimaryColor, () => GoTo(Step.Done));
            Ui.Place((RectTransform)ok.transform, new Vector2(620f, 14f), new Vector2(560f, 76f));
            compactPanel = panel.gameObject;
            compactPanel.SetActive(false);
        }

        // 深度を壁に重ねる表示に切り替える（ふだんの画面を隠す）
        private void SetRectified(bool on)
        {
            if (rectified == null)
            {
                return;
            }

            on = on && session.IsReady;
            rectified.Visible = on;
            headerObject.SetActive(!on);
            sideObject.SetActive(!on);
            legendObject.SetActive(!on);
            preview.SetVisible(!on);
            compactPanel.SetActive(on);
            lastRenderedFrame = -1;
        }

        private void BuildSettings(Transform parent)
        {
            Text heading = Ui.CreateText(parent, "設定（ふだんは変えなくて大丈夫です）", 26, TextAnchor.MiddleLeft);
            Ui.Place(heading.rectTransform, new Vector2(20f, 245f), new Vector2(660f, 40f));

            Text ballLabel = Ui.CreateText(parent, "ボールの大きさ", 28, TextAnchor.MiddleLeft);
            Ui.Place(ballLabel.rectTransform, new Vector2(20f, 175f), new Vector2(240f, 60f));
            Button minus = Ui.CreateButton(parent, "－", Ui.ButtonColor, () => ChangeBallSize(-10));
            Ui.Place((RectTransform)minus.transform, new Vector2(270f, 175f), new Vector2(90f, 60f));
            ballSizeText = Ui.CreateText(parent, string.Empty, 30, TextAnchor.MiddleCenter);
            Ui.Place(ballSizeText.rectTransform, new Vector2(365f, 175f), new Vector2(200f, 60f));
            Button plus = Ui.CreateButton(parent, "＋", Ui.ButtonColor, () => ChangeBallSize(10));
            Ui.Place((RectTransform)plus.transform, new Vector2(570f, 175f), new Vector2(90f, 60f));

            Button rotate = Ui.CreateButton(parent, "見本を90°回す", Ui.ButtonColor, RotatePreview);
            Ui.Place((RectTransform)rotate.transform, new Vector2(20f, 100f), new Vector2(320f, 64f));
            Button mirror = Ui.CreateButton(parent, "見本を左右反転", Ui.ButtonColor, MirrorPreview);
            Ui.Place((RectTransform)mirror.transform, new Vector2(350f, 100f), new Vector2(310f, 64f));

            Button reset = Ui.CreateButton(parent, "保存した設定を消す", Ui.WarningColor, ResetAll);
            Ui.Place((RectTransform)reset.transform, new Vector2(20f, 20f), new Vector2(640f, 64f));
            UpdateBallSizeText();
        }

        private void SetButtons(params ButtonSpec[] specs)
        {
            for (int i = buttonPanel.childCount - 1; i >= 0; i--)
            {
                Destroy(buttonPanel.GetChild(i).gameObject);
            }

            // ボタンが多いときは、少しずつ低くして、決まった高さ（480）に収める
            float step = Mathf.Min(100f, 480f / Mathf.Max(1, specs.Length));
            float y = 480f;
            foreach (ButtonSpec spec in specs)
            {
                y -= step;
                Button button = Ui.CreateButton(buttonPanel, spec.Label, spec.Color, spec.Action);
                Ui.Place((RectTransform)button.transform, new Vector2(0f, y), new Vector2(660f, step - 12f));
                button.interactable = spec.Enabled;
            }
        }

        private struct ButtonSpec
        {
            public string Label;
            public Color Color;
            public UnityEngine.Events.UnityAction Action;
            public bool Enabled;

            public ButtonSpec(string label, Color color, UnityEngine.Events.UnityAction action, bool enabled = true)
            {
                Label = label;
                Color = color;
                Action = action;
                Enabled = enabled;
            }
        }

        // ---------------------------------------------------------------
        // ステップ
        // ---------------------------------------------------------------
        private void GoTo(Step next)
        {
            SetRectified(false);
            step = next;
            bigText.text = string.Empty;
            resultMessage = string.Empty;
            session.DetectionEnabled = next == Step.TestThrow || next == Step.Corners;
            switch (next)
            {
                case Step.Resume:
                    SetTitle(0, "前回の設定があります");
                    SetInstruction("Kinect とプロジェクターを動かしていなければ、「前回の設定ではじめる」を押してください。\n動かしたときや、当たる位置がずれるときは「はじめから設定する」を押してください。");
                    SetButtons(
                        new ButtonSpec("前回の設定ではじめる", Ui.PrimaryColor, StartGame, session.IsReady),
                        new ButtonSpec("試し投げで確かめる", Ui.ButtonColor, () => GoTo(Step.TestThrow), session.IsReady),
                        new ButtonSpec("はじめから設定する", Ui.ButtonColor, () => GoTo(Step.Sensor)));
                    break;
                case Step.Sensor:
                    SetTitle(1, "Kinect の確認");
                    SetButtons(
                        new ButtonSpec("つぎへ", Ui.PrimaryColor, () => GoTo(Step.Baseline)),
                        new ButtonSpec("Kinect をもう一度さがす", Ui.ButtonColor, () => SwitchSource(DepthSourceKind.Kinect)),
                        new ButtonSpec("Kinect なし（模擬）で進める", Ui.ButtonColor, () => SwitchSource(DepthSourceKind.Simulated)));
                    RefreshSensorText();
                    break;
                case Step.Baseline:
                    SetTitle(2, "壁を空にして、基準を録る");
                    SetInstruction("壁の前から、人と物（ボールも）をどけてください。\nどけたら「基準を録る」を押してください。3・2・1 と数えたあと、約1秒で録り終わります。");
                    SetButtons(
                        new ButtonSpec("基準を録る", Ui.PrimaryColor, BeginBaseline),
                        new ButtonSpec("もどる", Ui.ButtonColor, () => GoTo(Step.Sensor)));
                    break;
                case Step.Corners:
                    tapped.Clear();
                    session.Calibration.ClearCorners();
                    session.ApplyCalibration();
                    ShowCornerStep();
                    break;
                case Step.TestThrow:
                    testHits = 0;
                    session.ResetTracking();
                    SetTitle(4, "試し投げ");
                    SetInstruction("壁にボールを何回か投げてください。当たった所に、黄色い輪が出ます。\n輪がボールの当たった所に出ていれば「合っている」を押してください。\n輪が出ないときは「深度を壁に重ねて表示」で、ボールが深度に写っているか確かめられます。" + (session.IsSimulated ? "\n（模擬：画面をクリックするとボールが飛びます。H キーを押している間、マウスの所に「壁を触る手」が出ます）" : string.Empty));
                    ShowTestThrowButtons();
                    break;
                case Step.Done:
                    session.SaveCalibration();
                    SetTitle(5, "準備ができました");
                    SetInstruction("設定を保存しました。次からは「前回の設定ではじめる」で、すぐに始められます。\n「ゲームをはじめる」を押してください。ゲーム中に Esc キーを押すと、この画面に戻ります。");
                    SetButtons(
                        new ButtonSpec("ゲームをはじめる", Ui.PrimaryColor, StartGame),
                        new ButtonSpec("試し投げにもどる", Ui.ButtonColor, () => GoTo(Step.TestThrow)));
                    break;
            }
        }

        private void SetTitle(int number, string title)
        {
            titleText.text = number <= 0 ? title : "ステップ " + number + " / " + StepCount + "　" + title;
        }

        private void SetInstruction(string text)
        {
            instructionText.text = text;
        }

        private void RefreshSensorText()
        {
            string text;
            if (session.IsSimulated)
            {
                text = "いまは Kinect なし（模擬）で動いています。Kinect を使うときは、Kinect をつないでから「Kinect をもう一度さがす」を押してください。";
            }
            else if (session.IsReceiving)
            {
                text = "Kinect がつながっています ✓　左の見本に、壁や物が写っていることを確かめて「つぎへ」を押してください。";
            }
            else
            {
                text = "Kinect から深度が届いていません。次を確かめて「Kinect をもう一度さがす」を押してください。\n① Kinect の電源ランプがついている　② USB 3.0 の差し込み口につながっている　③ Kinect for Windows Runtime 2.0 が入っている";
            }

            if (instructionText.text != text)
            {
                SetInstruction(text);
            }
        }

        private void ShowCornerStep()
        {
            SetTitle(3, "四隅をタップ（投影範囲を決める）");
            if (tapped.Count < 4)
            {
                SetInstruction("左の見本で、投影している範囲の「" + CornerNames[tapped.Count] + "」の角をタップしてください（左上→右上→右下→左下の順）。\n投影は壁の端に合わせてあります。角が見えにくいときは、角に手や箱を当てると見本に写ります（写ったら手をどけてからタップ）。");
                SetButtons(
                    new ButtonSpec("1つもどす", Ui.ButtonColor, UndoCorner, tapped.Count > 0),
                    new ButtonSpec("ぜんぶやりなおす", Ui.ButtonColor, () => GoTo(Step.Corners), tapped.Count > 0),
                    new ButtonSpec("基準を録りなおす", Ui.ButtonColor, () => GoTo(Step.Baseline)));
                return;
            }

            if (session.IsReady)
            {
                int blind = Mathf.RoundToInt(session.Detector.BlindRatio * 100f);
                SetInstruction("台形ができました。深度を使って、台形を壁の上の長方形に直しました。\n台形の中でボールを取りにくい所（紫）：約 " + blind + "%" + (blind > 30 ? "　※多いときは、Kinect の向きを少し壁側に向けて、基準から録りなおしてください。" : string.Empty) + DescribeWall());
                SetButtons(
                    new ButtonSpec("つぎへ（試し投げ）", Ui.PrimaryColor, () => GoTo(Step.TestThrow)),
                    new ButtonSpec("1つもどす", Ui.ButtonColor, UndoCorner),
                    new ButtonSpec("ぜんぶやりなおす", Ui.ButtonColor, () => GoTo(Step.Corners)));
            }
            else
            {
                SetInstruction("この四隅では使えません：" + session.ConfigureError);
                SetButtons(
                    new ButtonSpec("1つもどす", Ui.ButtonColor, UndoCorner),
                    new ButtonSpec("ぜんぶやりなおす", Ui.PrimaryColor, () => GoTo(Step.Corners)),
                    new ButtonSpec("基準を録りなおす", Ui.ButtonColor, () => GoTo(Step.Baseline)));
            }
        }

        // ---------------------------------------------------------------
        // ボタンの処理
        // ---------------------------------------------------------------
        private void StartGame()
        {
            if (!session.IsReady && !session.ApplyCalibration())
            {
                GoTo(Step.Corners);
                return;
            }

            session.SaveCalibration();
            SceneDirector.StartGame();
        }

        private void SwitchSource(DepthSourceKind kind)
        {
            session.OpenSource(kind);
            session.StartNewCalibration();
            preview = null;
            rectified = null;
            Destroy(canvas.gameObject);
            BuildLayout();
            GoTo(Step.Sensor);
        }

        private void BeginBaseline()
        {
            session.DetectionEnabled = false;
            countdownEnd = Time.unscaledTime + CountdownSeconds;
            recorder = null;
            SetButtons(new ButtonSpec("録っています…", Ui.ButtonColor, () => { }, false));
        }

        private void UpdateBaseline()
        {
            if (countdownEnd < 0f)
            {
                return;
            }

            float remaining = countdownEnd - Time.unscaledTime;
            if (remaining > 0f)
            {
                bigText.text = Mathf.CeilToInt(remaining).ToString();
                return;
            }

            if (recorder == null)
            {
                recorder = new BaselineRecorder(session.Source.Width, session.Source.Height);
                lastRenderedFrame = -1;
            }

            bigText.text = "録っています " + Mathf.RoundToInt(recorder.Progress * 100f) + "%";
            if (session.FrameCount != lastBaselineFrame)
            {
                lastBaselineFrame = session.FrameCount;
                recorder.Add(session.LatestFrame);
            }

            if (!recorder.IsDone)
            {
                return;
            }

            ushort[] baseline;
            ushort[] noise;
            recorder.Build(out baseline, out noise);
            CalibrationData data = session.Calibration;
            data.Baseline = baseline;
            data.Noise = noise;
            data.Width = session.Source.Width;
            data.Height = session.Source.Height;
            data.SourceKind = session.Source.Kind;
            countdownEnd = -1f;
            recorder = null;
            bigText.text = string.Empty;
            int valid = 0;
            foreach (ushort d in baseline)
            {
                valid += d != 0 ? 1 : 0;
            }

            int percent = Mathf.RoundToInt(valid * 100f / baseline.Length);
            SetInstruction("基準を録りました（見本の中で深度が取れた所：約 " + percent + "%）。\n「つぎへ」を押して、四隅を決めてください。");
            SetButtons(
                new ButtonSpec("つぎへ（四隅をタップ）", Ui.PrimaryColor, () => GoTo(Step.Corners)),
                new ButtonSpec("録りなおす", Ui.ButtonColor, BeginBaseline));
        }

        private int lastBaselineFrame = -1;

        private void UndoCorner()
        {
            if (tapped.Count > 0)
            {
                tapped.RemoveAt(tapped.Count - 1);
            }

            session.Calibration.ClearCorners();
            session.ApplyCalibration();
            ShowCornerStep();
        }

        private void AddCorner(Vector2 image01)
        {
            if (tapped.Count >= 4)
            {
                return;
            }

            tapped.Add(image01);
            if (tapped.Count == 4)
            {
                session.Calibration.Corners = tapped.ToArray();
                session.ApplyCalibration();
            }

            ShowCornerStep();
        }

        private void ShowTestThrowButtons()
        {
            SetButtons(
                new ButtonSpec("合っている（つぎへ）", Ui.PrimaryColor, () => GoTo(Step.Done)),
                new ButtonSpec("深度を壁に重ねて表示", Ui.ButtonColor, () => SetRectified(true)),
                new ButtonSpec(KinectSideLabel(), Ui.ButtonColor, ToggleKinectSide),
                new ButtonSpec("上下が逆になっている", Ui.ButtonColor, SwapTopBottom),
                new ButtonSpec("四隅をやりなおす", Ui.ButtonColor, () => GoTo(Step.Corners)));
        }

        private string KinectSideLabel()
        {
            return session.Calibration.KinectOnRight ? "Kinect の位置：右（押すと左に）" : "Kinect の位置：左（押すと右に）";
        }

        // Kinect を置いた側（左・右）を切り替える。Kinect に近い側が、画面の同じ側になる（左右反転）。四隅と基準はそのまま使う
        private void ToggleKinectSide()
        {
            CalibrationData data = session.Calibration;
            data.KinectOnRight = !data.KinectOnRight;
            ReapplyCorners("Kinect の位置を「" + (data.KinectOnRight ? "右" : "左") + "」にしました（左右を入れ替えました）。もう一度投げて確かめてください。");
            if (session.IsReady)
            {
                session.SaveCalibration();
            }

            ShowTestThrowButtons();
        }

        // 投影面の大きさと Kinect の位置の説明（四隅がそろったときに出す）
        private string DescribeWall()
        {
            if (!session.IsReady)
            {
                return string.Empty;
            }

            WallMapping mapping = session.Detector.Mapping;
            string side = session.Calibration.KinectOnRight ? "右" : "左";
            string size = "投影面の大きさ（深度から測った値）：幅 約 " + Mathf.RoundToInt(mapping.WidthMm / 10f) + "cm × 高さ 約 " + Mathf.RoundToInt(mapping.HeightMm / 10f) + "cm。実物と大きく違うときは、四隅をやりなおしてください。";
            string near = mapping.NearSideDetected
                ? "Kinect の位置：" + side + "（Kinect に近い側を画面の" + side + "にしました）"
                : "Kinect の位置：" + side + "（深度から近い側が分からなかったので、タップした順番どおりにしました）";
            return "\n" + size + "\n" + near;
        }

        private void SwapTopBottom()
        {
            session.Calibration.SwapTopBottom();
            ReapplyCorners("上下を入れ替えました。もう一度投げて確かめてください。");
        }

        private void ReapplyCorners(string message)
        {
            tapped.Clear();
            tapped.AddRange(session.Calibration.Corners);
            session.ApplyCalibration();
            session.ResetTracking();
            ClearHitMarkers();
            resultMessage = message;
        }

        private void ChangeBallSize(int deltaMm)
        {
            CalibrationData data = session.Calibration;
            data.BallDiameterMm = Mathf.Clamp(data.BallDiameterMm + deltaMm, CalibrationData.MinBallDiameterMm, CalibrationData.MaxBallDiameterMm);
            session.Detector.SetBallDiameter(data.BallDiameterMm);
            UpdateBallSizeText();
            if (data.IsComplete)
            {
                session.SaveCalibration();
            }
        }

        private void UpdateBallSizeText()
        {
            ballSizeText.text = "直径 " + (session.Calibration.BallDiameterMm / 10) + "cm";
        }

        private void RotatePreview()
        {
            CalibrationData data = session.Calibration;
            data.PreviewRotation = (data.PreviewRotation + 1) % 4;
            preview.SetView(data.PreviewRotation, data.PreviewMirror);
        }

        private void MirrorPreview()
        {
            CalibrationData data = session.Calibration;
            data.PreviewMirror = !data.PreviewMirror;
            preview.SetView(data.PreviewRotation, data.PreviewMirror);
        }

        private void ResetAll()
        {
            CalibrationStore.Delete();
            session.StartNewCalibration();
            tapped.Clear();
            GoTo(Step.Sensor);
        }

        // ---------------------------------------------------------------
        // 毎フレーム
        // ---------------------------------------------------------------
        private void UpdatePreview()
        {
            if (preview == null || session.FrameCount == lastRenderedFrame)
            {
                return;
            }

            lastRenderedFrame = session.FrameCount;
            if (rectified != null && rectified.Visible)
            {
                rectified.Render(session.LatestFrame, session.Detector);
                return;
            }

            IList<Vector2> corners = step == Step.Corners ? (IList<Vector2>)tapped : session.Calibration.Corners;
            preview.Render(session.LatestFrame, session.Calibration, session.IsReady ? session.Detector : null, corners);
            preview.ShowMarkers(corners);
        }

        private void UpdateStatus()
        {
            string source = session.IsSimulated ? "模擬（Kinect なし）" : (session.IsReceiving ? "Kinect（受信中）" : "Kinect（深度が届いていません）");
            string extra = step == Step.TestThrow ? "　試し投げの当たり：" + testHits + " 回" : string.Empty;
            string detection = session.IsReady ? "　ボール：" + session.Detector.LastBallCount + "　体・手：" + session.Detector.LastBodyCount : string.Empty;
            statusText.text = "入力：" + source + detection + extra + (resultMessage.Length > 0 ? "　" + resultMessage : string.Empty);
        }

        private void HandlePointer()
        {
            Keyboard escapeKeyboard = Keyboard.current;
            if (escapeKeyboard != null && escapeKeyboard.escapeKey.wasPressedThisFrame && rectified != null && rectified.Visible)
            {
                SetRectified(false);
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            Keyboard keyboard = Keyboard.current;
            if (step == Step.TestThrow && session.IsSimulated && session.IsReady)
            {
                bool hand = keyboard != null && keyboard.hKey.isPressed;
                session.SetSimulatedHand(hand, new Vector2(screen.x / Screen.width, screen.y / Screen.height));
            }

            if (!mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (step == Step.Corners)
            {
                Vector2 image01;
                if (preview.TryScreenToImage(screen, out image01))
                {
                    AddCorner(image01);
                }

                return;
            }

            if (step == Step.TestThrow && session.IsSimulated && !IsPointerOverButton())
            {
                session.ThrowSimulatedBall(new Vector2(screen.x / Screen.width, screen.y / Screen.height));
            }
        }

        private static bool IsPointerOverButton()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        private void HandleHit(WallHit hit)
        {
            if (step != Step.TestThrow)
            {
                return;
            }

            testHits++;
            Image ring = Ui.CreatePanel(canvas.transform, "当たりの印", new Color(1f, 0.9f, 0.1f, 1f));
            ring.sprite = ProceduralSprites.Ring;
            ring.raycastTarget = false;
            RectTransform rect = ring.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.sizeDelta = new Vector2(160f, 160f);
            rect.position = hit.ToScreen(Screen.width, Screen.height);
            HitMarker marker = new HitMarker();
            marker.Ring = ring;
            marker.Born = Time.unscaledTime;
            hitMarkers.Add(marker);
            Debug.Log("[WallBurst] 試し投げの当たり：壁 (" + hit.Wall.x.ToString("0.00") + ", " + hit.Wall.y.ToString("0.00") + ")　深度画像 (" + hit.Image.x.ToString("0.00") + ", " + hit.Image.y.ToString("0.00") + ")");
        }

        private void UpdateHitMarkers()
        {
            for (int i = hitMarkers.Count - 1; i >= 0; i--)
            {
                HitMarker marker = hitMarkers[i];
                float age = Time.unscaledTime - marker.Born;
                if (marker.Ring == null || age > MarkerSeconds)
                {
                    if (marker.Ring != null)
                    {
                        Destroy(marker.Ring.gameObject);
                    }

                    hitMarkers.RemoveAt(i);
                    continue;
                }

                Color color = marker.Ring.color;
                color.a = 1f - age / MarkerSeconds;
                marker.Ring.color = color;
            }
        }

        private void ClearHitMarkers()
        {
            foreach (HitMarker marker in hitMarkers)
            {
                if (marker.Ring != null)
                {
                    Destroy(marker.Ring.gameObject);
                }
            }

            hitMarkers.Clear();
        }
    }
}
