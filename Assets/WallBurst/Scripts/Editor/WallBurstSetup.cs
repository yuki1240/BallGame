using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WallBurst.EditorTools
{
    /// <summary>
    /// WallBurst のシーンと設定ファイルを用意するエディタ用の処理です。
    /// <list type="bullet">
    /// <item>プロジェクトを開いたとき、足りないものがあれば自動で作ります（1回だけ）</item>
    /// <item>メニュー「WallBurst/セットアップ」で、いつでも作り直せます（今あるシーンは上書きしません）</item>
    /// </list>
    /// 作るもの：
    /// <list type="number">
    /// <item>シーン：Assets/WallBurst/Scenes/ の WB_Calibration・WB_EffectOnly・WB_ChristmasTree</item>
    /// <item>ゲーム用シーンの一覧：Assets/WallBurst/Resources/WallBurstSceneCatalog.asset</item>
    /// <item>アセットの参照：Assets/WallBurst/Resources/WallBurstAssets.asset（ツリー・飾り・スター）</item>
    /// <item>Build Settings：WallBurst の3つのシーンを先頭に入れる（前からあるシーンは消さずに、チェックを外して残す）</item>
    /// </list>
    /// </summary>
    [InitializeOnLoad]
    public static class WallBurstSetup
    {
        private const string Root = "Assets/WallBurst";
        private const string SceneFolder = Root + "/Scenes";
        private const string ResourceFolder = Root + "/Resources";
        private const string CatalogPath = ResourceFolder + "/" + GameSceneCatalog.ResourceName + ".asset";
        private const string AssetsPath = ResourceFolder + "/" + WallBurstAssets.ResourceName + ".asset";
        private const string DecoratorRoot = "Assets/Assets/Christmas_Tree_Decorator";
        private const string TreePrefabPath = DecoratorRoot + "/Tree/Spruce2/Spruce2.prefab";
        private const string StarPrefabPath = DecoratorRoot + "/Models/TreeTopStar.prefab";
        private const string AutoSetupKey = "WallBurst.AutoSetupDone";

        private static readonly string[] OrnamentPrefabPaths =
        {
            DecoratorRoot + "/Toys/ToyRed.prefab",
            DecoratorRoot + "/Toys/ToyBlue.prefab",
            DecoratorRoot + "/Toys/ToyGold.prefab"
        };

        static WallBurstSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        /// <summary>
        /// メニューから、足りないものを作り直します。
        /// </summary>
        [MenuItem("WallBurst/セットアップ（シーンと設定を用意する）")]
        public static void RunFromMenu()
        {
            Run(true);
            EditorUtility.DisplayDialog("WallBurst", "セットアップが終わりました。\nAssets/WallBurst/Scenes/WB_Calibration を開いて再生してください。", "OK");
        }

        /// <summary>
        /// キャリブレーションのシーンを開きます。
        /// </summary>
        [MenuItem("WallBurst/キャリブレーションのシーンを開く")]
        public static void OpenCalibration()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath(SceneNames.Calibration));
            }
        }

        private static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(AutoSetupKey, false))
            {
                return;
            }

            SessionState.SetBool(AutoSetupKey, true);
            bool missing = !File.Exists(ScenePath(SceneNames.Calibration)) || !File.Exists(ScenePath(SceneNames.EffectOnly)) ||
                           !File.Exists(ScenePath(SceneNames.ChristmasTree)) || !File.Exists(CatalogPath) || !File.Exists(AssetsPath);
            if (missing)
            {
                Run(false);
            }
        }

        private static void Run(bool fromMenu)
        {
            try
            {
                EnsureFolder(Root, "Scenes");
                EnsureFolder(Root, "Resources");
                CreateSceneIfMissing(SceneNames.Calibration, typeof(CalibrationScene));
                CreateSceneIfMissing(SceneNames.EffectOnly, typeof(EffectOnlyScene));
                CreateSceneIfMissing(SceneNames.ChristmasTree, typeof(ChristmasTreeScene));
                CreateCatalogIfMissing();
                UpdateAssetReferences();
                UpdateBuildSettings();
                AssetDatabase.SaveAssets();
                Debug.Log("[WallBurst] セットアップしました（シーン・設定ファイル・Build Settings）。");
            }
            catch (Exception exception)
            {
                Debug.LogError("[WallBurst] セットアップできませんでした。メニュー「WallBurst/セットアップ」をもう一度実行してください。\n" + exception);
                if (fromMenu)
                {
                    throw;
                }
            }
        }

        private static string ScenePath(string sceneName)
        {
            return SceneFolder + "/" + sceneName + ".unity";
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void CreateSceneIfMissing(string sceneName, Type component)
        {
            string path = ScenePath(sceneName);
            if (File.Exists(path))
            {
                return;
            }

            // 今開いているシーンはそのままにして、別に作って保存して閉じる
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject host = new GameObject(component.Name);
            host.AddComponent(component);
            SceneManager.MoveGameObjectToScene(host, scene);
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void CreateCatalogIfMissing()
        {
            if (File.Exists(CatalogPath))
            {
                return;
            }

            GameSceneCatalog catalog = ScriptableObject.CreateInstance<GameSceneCatalog>();
            catalog.entries.Add(new GameSceneCatalog.Entry { sceneName = SceneNames.EffectOnly, kidsName = string.Empty, enabled = true });
            catalog.entries.Add(new GameSceneCatalog.Entry { sceneName = SceneNames.ChristmasTree, kidsName = string.Empty, enabled = true });
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        private static void UpdateAssetReferences()
        {
            WallBurstAssets assets = AssetDatabase.LoadAssetAtPath<WallBurstAssets>(AssetsPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<WallBurstAssets>();
                AssetDatabase.CreateAsset(assets, AssetsPath);
            }

            if (assets.treePrefab == null)
            {
                assets.treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            }

            if (assets.starPrefab == null)
            {
                assets.starPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StarPrefabPath);
            }

            if (assets.ornamentPrefabs == null || assets.ornamentPrefabs.Length == 0 || Array.IndexOf(assets.ornamentPrefabs, null) >= 0)
            {
                List<GameObject> ornaments = new List<GameObject>();
                foreach (string path in OrnamentPrefabPaths)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        ornaments.Add(prefab);
                    }
                }

                assets.ornamentPrefabs = ornaments.ToArray();
            }

            if (assets.treePrefab == null || assets.starPrefab == null || assets.ornamentPrefabs.Length == 0)
            {
                Debug.LogWarning("[WallBurst] Christmas_Tree_Decorator のツリー・飾り・スターが見つかりません。" + DecoratorRoot + " にアセットがあるか確かめてください。");
            }

            EditorUtility.SetDirty(assets);
        }

        private static void UpdateBuildSettings()
        {
            string[] ours = { ScenePath(SceneNames.Calibration), ScenePath(SceneNames.EffectOnly), ScenePath(SceneNames.ChristmasTree) };
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in ours)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            // 前からあるシーンは消さずに、チェックを外して後ろに残す
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (Array.IndexOf(ours, existing.path) >= 0)
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(existing.path, false));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
