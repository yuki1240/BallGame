using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WallBurst
{
    /// <summary>
    /// ゲーム用シーンの共通の型です。新しいシーンを作るときは、これを使ったスクリプトを1つ置きます。
    /// <para>決まっているのは次の2つだけです。</para>
    /// <list type="bullet">
    /// <item>ボールが当たると <see cref="OnWallHit"/> が呼ばれる（画面の位置。左下が原点の画素）</item>
    /// <item>シーンを終えるときは <see cref="RequestNext"/> を呼ぶ（次のシーンが選ばれる）</item>
    /// </list>
    /// <para>係の人用の操作（どのシーンでも同じ）：</para>
    /// <list type="bullet">
    /// <item>Esc キー：キャリブレーションへ戻る</item>
    /// <item>スペースキー：デバッグ表示（終わるまであと何球か・シーン名など）を出す／隠す。始めは出ている</item>
    /// <item>N キー：次のシーンへ進める</item>
    /// <item>T キー：壁のランダムな位置にボールが当たったことにする（デバッグ用）</item>
    /// <item>クリック：模擬深度（Kinect なし）のときは、その位置へボールを投げる</item>
    /// </list>
    /// </summary>
    public abstract class GameSceneBase : MonoBehaviour
    {
        private WallBurstSession session;
        private bool nextRequested;
        private Text operatorText;
        private Text remainingText;
        private int hitTotal;
        private readonly System.Random debugRandom = new System.Random();

        /// <summary>このシーンで受け取った当たりの数。</summary>
        protected int HitTotal
        {
            get { return hitTotal; }
        }

        /// <summary>シーンが始まってからの秒数。</summary>
        protected float SceneSeconds { get; private set; }

        /// <summary>
        /// 当たりを受け取る準備をして、シーンを組み立てます。
        /// </summary>
        protected virtual void Start()
        {
            session = WallBurstSession.Instance;
            if (!session.IsReady && !session.LoadSavedCalibration())
            {
                // 校正が無いままゲームのシーンを開いたとき（エディタで直接再生したときなど）
                Debug.LogWarning("[WallBurst] 校正がありません。キャリブレーションへ戻ります。");
                SceneDirector.GoToCalibration();
                return;
            }

            session.DetectionEnabled = true;
            session.HitDetected += HandleHit;
            Ui.EnsureEventSystem();
            BuildOperatorOverlay();
            BuildScene();
        }

        /// <summary>
        /// シーンを組み立てます（カメラ・背景など）。
        /// </summary>
        protected abstract void BuildScene();

        /// <summary>
        /// ボールが当たったときに呼ばれます。
        /// </summary>
        /// <param name="screenPosition">画面の位置（左下が原点の画素）</param>
        protected abstract void OnWallHit(Vector2 screenPosition);

        /// <summary>
        /// 係の人用の表示に出す、このシーンの様子（数など）を返します。
        /// </summary>
        /// <returns>様子</returns>
        protected virtual string DescribeState()
        {
            return string.Empty;
        }

        /// <summary>
        /// このシーンが終わるまで、あと何球当てればよいかを返します（デバッグ表示用）。回数で終わらないシーンは -1。
        /// </summary>
        /// <returns>あと何球か</returns>
        protected virtual int RemainingBalls()
        {
            return -1;
        }

        /// <summary>
        /// このシーンを終えて、次のシーンへ進みます（何回呼んでも1回だけ）。
        /// </summary>
        protected void RequestNext()
        {
            if (nextRequested)
            {
                return;
            }

            nextRequested = true;
            SceneDirector.GoToNext(gameObject.scene.name);
        }

        /// <summary>
        /// 背景が単色のカメラを作ります。
        /// </summary>
        /// <param name="background">背景の色</param>
        /// <returns>カメラ</returns>
        protected Camera CreateCamera(Color background)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            return camera;
        }

        /// <summary>
        /// 毎フレームの処理。派生したシーンで書き足すときは、base.Update() を呼んでください。
        /// </summary>
        protected virtual void Update()
        {
            SceneSeconds += Time.deltaTime;
            HandleOperatorKeys();
            if (operatorText != null && operatorText.enabled)
            {
                operatorText.text = BuildOperatorText();
                int remaining = RemainingBalls();
                remainingText.text = remaining < 0 ? string.Empty : "終わるまで あと " + remaining + " 球";
            }
        }

        /// <summary>
        /// 後片付け。派生したシーンで書き足すときは、base.OnDestroy() を呼んでください。
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (session != null)
            {
                session.HitDetected -= HandleHit;
            }
        }

        private void HandleHit(WallHit hit)
        {
            if (nextRequested || SceneDirector.IsSwitching)
            {
                return;
            }

            hitTotal++;
            OnWallHit(hit.ToScreen(Screen.width, Screen.height));
        }

        private void HandleOperatorKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    nextRequested = true;
                    SceneDirector.GoToCalibration();
                    return;
                }

                if (keyboard.spaceKey.wasPressedThisFrame && operatorText != null)
                {
                    operatorText.enabled = !operatorText.enabled;
                    remainingText.enabled = operatorText.enabled;
                }

                if (keyboard.tKey.wasPressedThisFrame && session != null)
                {
                    // デバッグ用：壁のランダムな位置（端を少しあける）に当たったことにする
                    Vector2 random = new Vector2(0.05f + (float)debugRandom.NextDouble() * 0.9f, 0.05f + (float)debugRandom.NextDouble() * 0.9f);
                    session.RaiseHit(new WallHit(random, Vector2.zero, Time.unscaledTime));
                }

                if (keyboard.nKey.wasPressedThisFrame)
                {
                    RequestNext();
                    return;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || session == null)
            {
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            Vector2 wall = new Vector2(screen.x / Screen.width, screen.y / Screen.height);
            if (session.IsSimulated)
            {
                session.ThrowSimulatedBall(wall);
            }
        }

        private void BuildOperatorOverlay()
        {
            Canvas canvas = Ui.CreateCanvas("デバッグ表示", 900);
            operatorText = Ui.CreateText(canvas.transform, string.Empty, 26, TextAnchor.LowerLeft);
            Ui.Place(operatorText.rectTransform, new Vector2(20f, 16f), new Vector2(1400f, 200f));
            operatorText.color = new Color(1f, 1f, 1f, 0.85f);
            remainingText = Ui.CreateText(canvas.transform, string.Empty, 56, TextAnchor.UpperLeft);
            remainingText.fontStyle = FontStyle.Bold;
            Ui.Place(remainingText.rectTransform, new Vector2(30f, 960f), new Vector2(1000f, 100f));
            Outline outline = remainingText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private string BuildOperatorText()
        {
            string source = session.IsSimulated ? "模擬（クリックでボール）" : (session.IsReceiving ? "Kinect" : "Kinect（深度が届いていません）");
            return "シーン：" + gameObject.scene.name + "　入力：" + source + "　当たり：" + hitTotal + "　経過：" + Mathf.FloorToInt(SceneSeconds) + "秒\n" +
                DescribeState() + "\nEsc：キャリブレーションへ　N：次のシーンへ　T：ランダムな位置に当てる　スペース：この表示を隠す";
        }
    }
}
