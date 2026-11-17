using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// Calibration シーンの起動用オブジェクト。実行時に 1920×1080 の Canvas で校正画面を組み立てます。
    /// 校正は現場の画面だけで完結します（設置の選択 → 四隅のタップ → 基準の録画 → ゲームへ）。
    /// </summary>
    public sealed class CalibrationBootstrap : MonoBehaviour
    {
        /// <summary>校正のあとに始める最初の場面（クリスマス）のシーン名。</summary>
        public const string GameSceneName = "Christmas";

        /// <summary>リセットの確認を待つ秒数。</summary>
        public const float ResetConfirmSeconds = 3f;

        private const float PreviewLeft = 40f;
        private const float PreviewTop = 110f;
        private const float PreviewMaxWidth = 1100f;
        private const float PreviewMaxHeight = 930f;
        private const float PreviewAspect = 1920f / 1080f;
        private const int BlobLogLimit = 800;
        private const float PanelLeft = 1180f;
        private const float PanelWidth = 700f;
        private const float CornerGrabRadius = 40f;

        private enum DragMode
        {
            None,
            Corner,
            Quad
        }

        private static readonly Color WaitingColor = new Color(0.25f, 0.25f, 0.3f, 1f);

        private static readonly string[] CornerNames = { "左上", "右上", "右下", "左下" };

        private CalibrationSession session;
        private readonly DepthPreviewRenderer previewRenderer = new DepthPreviewRenderer();
        private readonly List<string> blobLog = new List<string>();
        private string diagnosticMessage = string.Empty;
        private Vector2 lastLoggedBlob = new Vector2(-1f, -1f);
        private BaselineRecorder recorder;
        private RawImage preview;
        private RectTransform[] cornerMarkers;
        private Image[] cornerLines;
        private Button sideButton;
        private Button belowButton;
        private Button mirrorButton;
        private Button sourceButton;
        private Button recordButton;
        private Button resetButton;
        private Button gameButton;
        private Text distanceText;
        private Text noiseFloorText;
        private Text guideText;
        private Text statusText;
        private float resetRequestedAt = float.NegativeInfinity;
        private DragMode dragMode = DragMode.None;
        private int dragCornerIndex = -1;
        private Vector2 lastDragImageUv;

        private void Start()
        {
            session = CalibrationSession.EnsureExists();
            EventSystemKeeper.Ensure();
            BuildScreen();
            RefreshAll();
        }

        private void Update()
        {
            session = CalibrationSession.EnsureExists();
            if (session.HasNewFrame)
            {
                DrawPreview();
                FeedRecorder();
                RecordBlobLog();
            }

            HandleDiagnosticsKey();
            RefreshAll();
        }

        private void HandleDiagnosticsKey()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
            {
                SaveDiagnostics();
            }

            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
            {
                blobLog.Clear();
                lastLoggedBlob = new Vector2(-1f, -1f);
                diagnosticMessage = "記録を消しました";
            }
        }

        private void TrimBlobLog()
        {
            while (blobLog.Count > BlobLogLimit)
            {
                blobLog.RemoveAt(0);
            }
        }

        // 直近に見えた塊（位置・大きさ・採用できたか）を覚えておき、診断の保存に入れる
        private void RecordBlobLog()
        {
            DetectionResult result = session.LatestResult;
            if (result == null || !session.IsSavedCalibrationUsable)
            {
                return;
            }

            foreach (DetectedHit hit in result.Hits)
            {
                blobLog.Add(string.Format("t={0:F1}s ★当たりとして採用 画素=({1:F0},{2:F0}) 横={3:P0}", Time.unscaledTime, hit.Pixel.x, hit.Pixel.y, hit.ImageUv.x));
            }

            if (!result.HasLargestBlob)
            {
                return;
            }

            DepthBlob blob = result.LargestBlob;
            Vector2 position = new Vector2(blob.CentroidX, blob.CentroidY);
            if ((position - lastLoggedBlob).sqrMagnitude < 1f)
            {
                return;
            }

            lastLoggedBlob = position;
            string verdict = result.PersonDetected ? "人サイズのフレーム：" + result.PersonReason : result.LargestBlobRejectReason.Length == 0 ? "採用できる" : "不採用：" + result.LargestBlobRejectReason;
            blobLog.Add(string.Format("t={0:F1}s 画素=({1:F0},{2:F0}) 横={3:P0} 面積(2m換算)={4:F0} 強さ={5:F2} 詰まり={6:F2} → {7}", Time.unscaledTime, blob.CentroidX, blob.CentroidY, (blob.CentroidX + 0.5f) / Mathf.Max(1, session.FrameWidth), blob.NormalizedArea, blob.PeakStrength, blob.FillRatio, verdict));
            TrimBlobLog();
        }

        // 「ボールが反応しない場所」の原因を調べるため、帯ごとの表と直近の塊の記録をファイルに保存する
        private void SaveDiagnostics()
        {
            try
            {
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "_diagnostics"));
                Directory.CreateDirectory(folder);
                string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("BallGame 診断 " + stamp);
                builder.AppendLine("画像サイズ " + session.FrameWidth + "x" + session.FrameHeight + "　設置 " + MountGeometry.GetDisplayName(session.Data.Mount));
                builder.AppendLine("紫（検知できない画素）の割合 " + Mathf.RoundToInt(session.Detector.BlindRatio * 100f) + "%");
                builder.AppendLine();
                builder.AppendLine(DetectorDiagnostics.BuildBandTable(session.Detector, session.LatestDepth));
                builder.AppendLine("直近の塊（古い順）");
                foreach (string line in blobLog)
                {
                    builder.AppendLine(line);
                }

                File.WriteAllText(Path.Combine(folder, "diag_" + stamp + ".txt"), builder.ToString(), new UTF8Encoding(true));
                if (previewRenderer.Texture != null)
                {
                    File.WriteAllBytes(Path.Combine(folder, "preview_" + stamp + ".png"), previewRenderer.Texture.EncodeToPNG());
                }

                diagnosticMessage = "診断を保存しました：" + folder;
                Debug.Log("[診断] " + diagnosticMessage);
            }
            catch (System.Exception exception)
            {
                diagnosticMessage = "診断を保存できませんでした：" + exception.Message;
                Debug.LogError("[診断] " + exception);
            }
        }

        private void OnDestroy()
        {
            previewRenderer.Dispose();
        }

        private void BuildScreen()
        {
            Canvas canvas = UiFactory.CreateCanvas("CalibrationCanvas", 0);
            Image background = UiFactory.CreatePanel(canvas.transform, "Background", new Color(0.93f, 0.93f, 0.93f, 1f));
            UiFactory.Stretch(background.rectTransform);
            Text title = UiFactory.CreateText(canvas.transform, "BallGame 校正", 44, TextAnchor.MiddleLeft);
            UiFactory.PlaceTopLeft(title.rectTransform, PreviewLeft, 24f, 1000f, 70f);
            BuildPreview(canvas.transform);
            BuildPanel(canvas.transform);
        }

        private void BuildPreview(Transform parent)
        {
            Image frame = UiFactory.CreatePanel(parent, "PreviewFrame", Color.black);
            UiFactory.PlaceTopLeft(frame.rectTransform, PreviewLeft, PreviewTop, PreviewMaxWidth, PreviewMaxHeight);
            RectTransform previewRect = UiFactory.CreateRect(frame.transform, "DepthPreview");
            preview = previewRect.gameObject.AddComponent<RawImage>();
            preview.color = WaitingColor;
            PointerReceiver receiver = previewRect.gameObject.AddComponent<PointerReceiver>();
            receiver.Pressed += OnPreviewPressed;
            receiver.Dragged += OnPreviewDragged;
            receiver.Released += OnPreviewReleased;
            BuildCornerLines(previewRect);
            BuildCornerMarkers(previewRect);
        }

        private void BuildCornerMarkers(RectTransform parent)
        {
            cornerMarkers = new RectTransform[CalibrationData.CornerCount];
            for (int i = 0; i < cornerMarkers.Length; i++)
            {
                Image marker = UiFactory.CreatePanel(parent, "Corner " + CornerNames[i], new Color(0.2f, 0.9f, 0.3f, 1f));
                marker.raycastTarget = false;
                marker.rectTransform.sizeDelta = new Vector2(28f, 28f);
                SetTopLeftAnchor(marker.rectTransform, new Vector2(0.5f, 0.5f));
                Text label = UiFactory.CreateText(marker.transform, (i + 1) + " " + CornerNames[i], 26, TextAnchor.MiddleLeft);
                label.color = new Color(0.2f, 1f, 0.3f, 1f);
                UiFactory.PlaceTopLeft(label.rectTransform, 26f, -8f, 160f, 40f);
                cornerMarkers[i] = marker.rectTransform;
            }
        }

        private void BuildCornerLines(RectTransform parent)
        {
            cornerLines = new Image[CalibrationData.CornerCount];
            for (int i = 0; i < cornerLines.Length; i++)
            {
                Image line = UiFactory.CreatePanel(parent, "Line " + i, new Color(0.2f, 0.9f, 0.3f, 0.9f));
                line.raycastTarget = false;
                SetTopLeftAnchor(line.rectTransform, new Vector2(0f, 0.5f));
                cornerLines[i] = line;
            }
        }

        private static void SetTopLeftAnchor(RectTransform rect, Vector2 pivot)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = pivot;
        }

        private void BuildPanel(Transform parent)
        {
            Image panel = UiFactory.CreatePanel(parent, "Panel", UiFactory.PanelColor);
            UiFactory.PlaceTopLeft(panel.rectTransform, PanelLeft, PreviewTop, PanelWidth, PreviewMaxHeight);
            Transform p = panel.transform;
            AddLabel(p, "1. 設置を選ぶ", 20f);
            sideButton = AddButton(p, MountGeometry.GetDisplayName(MountOrientation.Side), 20f, 70f, 330f, () => OnMountPressed(MountOrientation.Side));
            belowButton = AddButton(p, MountGeometry.GetDisplayName(MountOrientation.Below), 350f, 70f, 330f, () => OnMountPressed(MountOrientation.Below));
            mirrorButton = AddButton(p, "左右反転", 20f, 150f, 330f, OnMirrorPressed);
            sourceButton = AddButton(p, "入力源", 350f, 150f, 330f, OnSourcePressed);
            AddLabel(p, "2. 左のプレビューで四隅をタップ", 230f);
            AddButton(p, "四隅をやり直す", 20f, 280f, 660f, OnClearCornersPressed);
            AddLabel(p, "3. 人がいない状態で基準を録る", 360f);
            recordButton = AddButton(p, "基準を録る（30フレーム）", 20f, 410f, 660f, OnRecordPressed);
            BuildDistanceRow(p, 490f);
            BuildNoiseRow(p, 560f);
            guideText = UiFactory.CreateText(p, string.Empty, 24, TextAnchor.UpperLeft);
            UiFactory.PlaceTopLeft(guideText.rectTransform, 20f, 632f, 660f, 104f);
            resetButton = AddButton(p, "リセット（すべて消す）", 20f, 740f, 320f, OnResetPressed);
            gameButton = AddButton(p, "4. ゲームへ", 360f, 740f, 320f, OnGamePressed);
            statusText = UiFactory.CreateText(p, string.Empty, 22, TextAnchor.UpperLeft);
            UiFactory.PlaceTopLeft(statusText.rectTransform, 20f, 830f, 660f, 90f);
        }

        private void BuildDistanceRow(Transform parent, float y)
        {
            Text label = UiFactory.CreateText(parent, "壁からの距離", 28, TextAnchor.MiddleLeft);
            UiFactory.PlaceTopLeft(label.rectTransform, 20f, y, 220f, 60f);
            AddButton(parent, "－", 250f, y, 90f, () => ChangeDistance(-1));
            distanceText = UiFactory.CreateText(parent, string.Empty, 30, TextAnchor.MiddleCenter);
            UiFactory.PlaceTopLeft(distanceText.rectTransform, 350f, y, 140f, 60f);
            AddButton(parent, "＋", 500f, y, 90f, () => ChangeDistance(1));
        }

        private void BuildNoiseRow(Transform parent, float y)
        {
            Text label = UiFactory.CreateText(parent, "ノイズ床（大きいほど厳しい）", 22, TextAnchor.MiddleLeft);
            UiFactory.PlaceTopLeft(label.rectTransform, 20f, y, 230f, 60f);
            AddButton(parent, "－", 250f, y, 90f, () => ChangeNoiseFloor(-5));
            noiseFloorText = UiFactory.CreateText(parent, string.Empty, 30, TextAnchor.MiddleCenter);
            UiFactory.PlaceTopLeft(noiseFloorText.rectTransform, 350f, y, 140f, 60f);
            AddButton(parent, "＋", 500f, y, 90f, () => ChangeNoiseFloor(5));
        }

        private static void AddLabel(Transform parent, string content, float y)
        {
            Text text = UiFactory.CreateText(parent, content, 28, TextAnchor.MiddleLeft);
            UiFactory.PlaceTopLeft(text.rectTransform, 20f, y, 660f, 44f);
        }

        private static Button AddButton(Transform parent, string label, float x, float y, float width, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiFactory.CreateButton(parent, label, 28, onClick);
            UiFactory.PlaceTopLeft((RectTransform)button.transform, x, y, width, 64f);
            return button;
        }

        private void OnPreviewPressed(Vector2 normalized)
        {
            Vector2 previewUv = ToPreviewUv(normalized);
            Vector2 imageUv = MountGeometry.PreviewToImage(previewUv, session.Data.Mount);
            if (TryBeginCornerDrag(previewUv))
            {
                return;
            }

            if (TryBeginQuadDrag(imageUv))
            {
                return;
            }

            session.AddCorner(imageUv);
        }

        private void OnPreviewDragged(Vector2 normalized)
        {
            Vector2 imageUv = MountGeometry.PreviewToImage(ToPreviewUv(normalized), session.Data.Mount);
            if (dragMode == DragMode.Corner)
            {
                session.MoveCorner(dragCornerIndex, imageUv);
            }
            else if (dragMode == DragMode.Quad)
            {
                session.MoveAllCorners(imageUv - lastDragImageUv);
                lastDragImageUv = imageUv;
            }
        }

        private void OnPreviewReleased(Vector2 normalized)
        {
            if (dragMode == DragMode.None)
            {
                return;
            }

            dragMode = DragMode.None;
            dragCornerIndex = -1;
            session.EndCornerEdit();
        }

        private static Vector2 ToPreviewUv(Vector2 normalized)
        {
            return new Vector2(normalized.x, 1f - normalized.y);
        }

        private bool TryBeginCornerDrag(Vector2 previewUv)
        {
            int index = FindCornerNear(previewUv);
            if (index < 0)
            {
                return false;
            }

            dragMode = DragMode.Corner;
            dragCornerIndex = index;
            return true;
        }

        private bool TryBeginQuadDrag(Vector2 imageUv)
        {
            CalibrationData data = session.Data;
            if (!data.HasAllCorners || !QuadMask.IsPointInPolygon(imageUv, data.Corners))
            {
                return false;
            }

            dragMode = DragMode.Quad;
            lastDragImageUv = imageUv;
            return true;
        }

        private int FindCornerNear(Vector2 previewUv)
        {
            CalibrationData data = session.Data;
            Vector2 size = preview.rectTransform.rect.size;
            int best = -1;
            float bestDistance = CornerGrabRadius;
            for (int i = 0; i < data.Corners.Count; i++)
            {
                Vector2 cornerUv = MountGeometry.ImageToPreview(data.Corners[i], data.Mount);
                Vector2 difference = new Vector2((cornerUv.x - previewUv.x) * size.x, (cornerUv.y - previewUv.y) * size.y);
                float distance = difference.magnitude;
                if (distance <= bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void OnMountPressed(MountOrientation mount)
        {
            CancelRecording();
            session.SelectMount(mount);
        }

        private void OnMirrorPressed()
        {
            session.SetMirror(!session.Data.MirrorX);
        }

        private void OnSourcePressed()
        {
            CancelRecording();
            DepthSourceKind next = session.Data.InputSource == DepthSourceKind.Kinect ? DepthSourceKind.Simulated : DepthSourceKind.Kinect;
            session.SetInputSource(next);
        }

        private void OnClearCornersPressed()
        {
            session.ClearCorners();
        }

        private void OnRecordPressed()
        {
            recorder = new BaselineRecorder(session.FrameWidth, session.FrameHeight);
            recorder.Begin();
        }

        private void OnResetPressed()
        {
            if (Time.unscaledTime - resetRequestedAt > ResetConfirmSeconds)
            {
                resetRequestedAt = Time.unscaledTime;
                return;
            }

            resetRequestedAt = float.NegativeInfinity;
            CancelRecording();
            session.ResetAll();
        }

        private void OnGamePressed()
        {
            if (CanStartGame())
            {
                SceneManager.LoadScene(GameSceneName);
            }
        }

        private void ChangeNoiseFloor(int deltaMm)
        {
            session.SetNoiseFloorMm(session.Data.NoiseFloorMm + deltaMm);
        }

        private void ChangeDistance(int deltaCm)
        {
            session.SetMaxDistanceCm(session.Data.GetMaxDistanceCm() + deltaCm);
        }

        private void CancelRecording()
        {
            if (recorder != null)
            {
                recorder.Cancel();
                recorder = null;
            }
        }

        private void FeedRecorder()
        {
            if (recorder == null || !recorder.IsRecording)
            {
                return;
            }

            if (recorder.Width != session.FrameWidth || recorder.Height != session.FrameHeight)
            {
                CancelRecording();
                return;
            }

            recorder.AddFrame(session.LatestDepth);
            if (recorder.IsComplete)
            {
                session.SetBaseline(recorder.BuildMedian(), recorder.Width, recorder.Height, recorder.BuildNoise());
            }
        }

        private bool CanStartGame()
        {
            return session.IsSavedCalibrationUsable || session.IsSimulated;
        }

        private void DrawPreview()
        {
            DetectionResult result = session.LatestResult;
            bool[] candidates = result != null && session.IsSavedCalibrationUsable ? result.CandidateMask : null;
            bool[] ball = result != null && session.IsSavedCalibrationUsable ? result.BallMask : null;
            HitDetector detector = session.IsSavedCalibrationUsable ? session.Detector : null;
            preview.color = Color.white;
            preview.texture = previewRenderer.Render(session.LatestDepth, session.FrameWidth, session.FrameHeight, session.Data.Mount, candidates, detector, ball, Time.unscaledTime, detector != null && detector.IsConfigured ? detector.BlindMask : null);
        }

        private void RefreshAll()
        {
            if (preview == null)
            {
                return;
            }

            LayoutPreview();
            RefreshCorners();
            RefreshButtons();
            RefreshTexts();
        }

        private void LayoutPreview()
        {
            Vector2 size = GetPreviewSize();
            RectTransform rect = preview.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private Vector2 GetPreviewSize()
        {
            // 深度画像（512×424 など）を、壁に映す画面と同じ 16:9（1920×1080 の比）に横へ引き伸ばして出す。
            // 見た目だけを伸ばしていて、深度のデータや四隅の位置（画像上の割合）は変わらない
            float previewWidth = PreviewMaxWidth;
            float previewHeight = previewWidth / PreviewAspect;
            if (previewHeight > PreviewMaxHeight)
            {
                previewHeight = PreviewMaxHeight;
                previewWidth = previewHeight * PreviewAspect;
            }

            return new Vector2(previewWidth, previewHeight);
        }

        private void RefreshCorners()
        {
            CalibrationData data = session.Data;
            Vector2 size = preview.rectTransform.rect.size;
            for (int i = 0; i < cornerMarkers.Length; i++)
            {
                bool placed = i < data.Corners.Count;
                cornerMarkers[i].gameObject.SetActive(placed);
                if (placed)
                {
                    cornerMarkers[i].anchoredPosition = ImageToPreviewLocal(data.Corners[i], data.Mount, size);
                }
            }

            RefreshLines(data, size);
        }

        private void RefreshLines(CalibrationData data, Vector2 size)
        {
            for (int i = 0; i < cornerLines.Length; i++)
            {
                int next = (i + 1) % CalibrationData.CornerCount;
                bool visible = next < data.Corners.Count && (next != 0 || data.HasAllCorners);
                cornerLines[i].gameObject.SetActive(visible);
                if (visible)
                {
                    PlaceLine(cornerLines[i].rectTransform, ImageToPreviewLocal(data.Corners[i], data.Mount, size), ImageToPreviewLocal(data.Corners[next], data.Mount, size));
                }
            }
        }

        private static void PlaceLine(RectTransform line, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            line.anchoredPosition = from;
            line.sizeDelta = new Vector2(delta.magnitude, 4f);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static Vector2 ImageToPreviewLocal(Vector2 imageUv, MountOrientation mount, Vector2 size)
        {
            Vector2 previewUv = MountGeometry.ImageToPreview(imageUv, mount);
            return new Vector2(previewUv.x * size.x, -previewUv.y * size.y);
        }

        private void RefreshButtons()
        {
            CalibrationData data = session.Data;
            UiFactory.SetButtonColor(sideButton, data.Mount == MountOrientation.Side ? UiFactory.SelectedColor : UiFactory.ButtonColor);
            UiFactory.SetButtonColor(belowButton, data.Mount == MountOrientation.Below ? UiFactory.SelectedColor : UiFactory.ButtonColor);
            UiFactory.SetButtonLabel(mirrorButton, "左右反転：" + (data.MirrorX ? "オン" : "オフ"));
            UiFactory.SetButtonLabel(sourceButton, "入力源：" + (data.InputSource == DepthSourceKind.Kinect ? "Kinect" : "模擬"));
            UiFactory.SetButtonLabel(recordButton, GetRecordLabel());
            UiFactory.SetButtonLabel(resetButton, IsWaitingResetConfirm() ? "もう一度押すとリセット" : "リセット（すべて消す）");
            gameButton.interactable = CanStartGame();
            distanceText.text = data.GetMaxDistanceCm() + "cm";
            noiseFloorText.text = data.NoiseFloorMm + "mm";
        }

        private string GetRecordLabel()
        {
            if (recorder != null && recorder.IsRecording)
            {
                return "録画中… " + recorder.RecordedFrames + " / " + recorder.TargetFrames;
            }

            return session.IsSavedCalibrationUsable || session.Data.MatchesResolution(session.FrameWidth, session.FrameHeight)
                ? "基準を録り直す（30フレーム）"
                : "基準を録る（30フレーム）";
        }

        private bool IsWaitingResetConfirm()
        {
            return Time.unscaledTime - resetRequestedAt <= ResetConfirmSeconds;
        }

        private void RefreshTexts()
        {
            guideText.text = BuildGuide();
            statusText.text = session.SourceStatus + "\n" + BuildDetectionStatus();
        }

        private string BuildGuide()
        {
            CalibrationData data = session.Data;
            string nearSide = MountGeometry.IsNearAtImageBottom(data.Mount) ? "画像の下が近く、上ほど遠い" : "画像の上が近く、下ほど遠い（表示は上下を入れ替えています）";
            string step;
            if (!data.HasAllCorners)
            {
                step = "次：プレビューで「" + CornerNames[data.Corners.Count] + "」をタップ（左上→右上→右下→左下）";
            }
            else if (!data.MatchesResolution(session.FrameWidth, session.FrameHeight))
            {
                step = "次：壁の前に人がいない状態で「基準を録る」\n四隅はドラッグで動かせます（内側をドラッグすると全体が動きます）。";
            }
            else
            {
                step = "校正済み。赤い点は検知の候補です。「ゲームへ」で始めます。\n四隅はドラッグで動かせます（内側をドラッグすると全体が動きます）。";
            }

            return MountGeometry.GetDisplayName(data.Mount) + "：" + nearSide + "\n" + step;
        }

        private string BuildDetectionStatus()
        {
            DetectionResult result = session.LatestResult;
            if (!session.IsSavedCalibrationUsable)
            {
                return session.IsSimulated ? "未校正のため、Game では仮の校正（模擬用）を使います。" : "未校正です。";
            }

            if (result == null)
            {
                return string.Empty;
            }

            string blind = session.Detector != null && session.Detector.IsConfigured ? "\n検知できない所（紫）：" + Mathf.RoundToInt(session.Detector.BlindRatio * 100f) + "%（基準の深度が取れていない／基準を録る間に揺れた画素）" : string.Empty;
            string diagnostic = "\nＦ９キー：診断を保存／Ｆ８キー：記録を消す" + (diagnosticMessage.Length > 0 ? "（" + diagnosticMessage + "）" : string.Empty);
            string person = result.PersonDetected ? "（人を検知中）" : result.InCooldown ? "（人が去ったあとの待ち時間）" : string.Empty;
            return "変化画素：" + result.ChangedPixelCount + "（2m換算 " + result.ChangedArea.ToString("F0") + "）" + person + "\n" + result.DescribeLargestBlob() + blind + diagnostic;
        }
    }
}
