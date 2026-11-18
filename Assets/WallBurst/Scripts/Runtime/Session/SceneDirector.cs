using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WallBurst
{
    /// <summary>
    /// シーンを切り替える係です。
    /// <list type="bullet">
    /// <item>ゲーム用シーンは <see cref="GameSceneCatalog"/>（一覧）から <see cref="SceneRotation"/> の決まりで選びます</item>
    /// <item>切り替えるときは、画面を暗くしてから替え、明るく戻します</item>
    /// <item>一覧に子ども向けの呼び名があれば「つぎは ○○！」を出します</item>
    /// </list>
    /// </summary>
    public sealed class SceneDirector : MonoBehaviour
    {
        private const float FadeSeconds = 0.8f;
        private const float TitleSeconds = 2f;

        private static SceneDirector instance;

        private GameSceneCatalog catalog;
        private Image fade;
        private Text title;
        private bool switching;

        /// <summary>切り替えの途中か。</summary>
        public static bool IsSwitching
        {
            get { return instance != null && instance.switching; }
        }

        private static SceneDirector Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject host = new GameObject("SceneDirector（シーン切り替え）");
                    DontDestroyOnLoad(host);
                    instance = host.AddComponent<SceneDirector>();
                    instance.Build();
                }

                return instance;
            }
        }

        /// <summary>
        /// ゲームを始めます（ゲーム用シーンからランダムに1つ選びます）。
        /// </summary>
        public static void StartGame()
        {
            Instance.GoTo(null);
        }

        /// <summary>
        /// 次のゲーム用シーンへ切り替えます。
        /// </summary>
        /// <param name="currentScene">今のシーン名</param>
        public static void GoToNext(string currentScene)
        {
            Instance.GoTo(currentScene);
        }

        /// <summary>
        /// キャリブレーションへ戻ります。
        /// </summary>
        public static void GoToCalibration()
        {
            Instance.Load(SceneNames.Calibration, string.Empty);
        }

        private void GoTo(string currentScene)
        {
            string next = SceneRotation.PickNext(catalog.GetPlayableScenes(), currentScene);
            if (next == null)
            {
                Debug.LogError("[WallBurst] ゲーム用シーンが1つもありません。WallBurstSceneCatalog と Build Settings を確かめてください。");
                return;
            }

            Load(next, catalog.GetKidsName(next));
        }

        private void Load(string sceneName, string kidsName)
        {
            if (switching)
            {
                return;
            }

            StartCoroutine(Switch(sceneName, kidsName));
        }

        private IEnumerator Switch(string sceneName, string kidsName)
        {
            switching = true;
            yield return Fade(0f, 1f);
            if (!string.IsNullOrEmpty(kidsName))
            {
                title.text = "つぎは " + kidsName + "！";
                title.enabled = true;
                yield return new WaitForSecondsRealtime(TitleSeconds);
            }

            WallBurstSession.Instance.ResetTracking();
            AsyncOperation loading = SceneManager.LoadSceneAsync(sceneName);
            while (loading != null && !loading.isDone)
            {
                yield return null;
            }

            title.enabled = false;
            yield return Fade(1f, 0f);
            switching = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            fade.enabled = true;
            float elapsed = 0f;
            while (elapsed < FadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, elapsed / FadeSeconds));
                yield return null;
            }

            SetAlpha(to);
            fade.enabled = to > 0f;
        }

        private void SetAlpha(float alpha)
        {
            Color color = fade.color;
            color.a = alpha;
            fade.color = color;
        }

        private void Build()
        {
            catalog = GameSceneCatalog.Load();
            Canvas canvas = Ui.CreateCanvas("切り替えの暗幕", 1000);
            canvas.transform.SetParent(transform, false);
            fade = Ui.CreatePanel(canvas.transform, "暗幕", new Color(0f, 0f, 0f, 0f));
            Ui.Stretch(fade.rectTransform);
            fade.raycastTarget = false;
            fade.enabled = false;
            title = Ui.CreateText(canvas.transform, string.Empty, 120, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            Ui.Stretch(title.rectTransform);
            title.enabled = false;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
