using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WallBurst
{
    /// <summary>
    /// クリスマスツリーのシーンです。
    /// <list type="number">
    /// <item>薄暗い背景の真ん中に、Christmas_Tree_Decorator のツリー（Spruce2）を置く。
    /// 大きさは <see cref="TreeLayout"/> で、スター込みで画面に収まるように決める</item>
    /// <item>ボールが当たるたびに、当たった所から飾りの玉が飛んでいき、ツリーのランダムな場所に1つずつ付く
    /// （置き場所は <see cref="OrnamentPlacer"/> で、重ならないように先に決めておく）。飾りは丸い玉のほか、リース・プレゼントの箱・ベル・星・キャンディケーン（<see cref="OrnamentShapes"/>）。付いた飾りは <see cref="OrnamentSparkle"/> でいつもきらきら光る</item>
    /// <item>数える当たりは 0.5 秒に1回まで（<see cref="CooldownSeconds"/>）。その間の当たりは小さな光だけ出す</item>
    /// <item><see cref="TotalHits"/> 回目（30回目）で、スターがてっぺんに付いて完成</item>
    /// <item>完成したらツリー全体を輝かせ（<see cref="TreeGlow"/>）、<see cref="GlowSeconds"/> 秒たったら次のシーンへ</item>
    /// </list>
    /// </summary>
    public sealed class ChristmasTreeScene : GameSceneBase
    {
        /// <summary>完成までの当たりの数（最後の1回でスターが付く）。</summary>
        public const int TotalHits = 30;

        /// <summary>数える当たりの間隔（秒）。続けて当たりすぎないようにする。</summary>
        public const float CooldownSeconds = 0.5f;

        /// <summary>輝いた状態で待つ秒数。</summary>
        public const float GlowSeconds = 10f;

        /// <summary>カメラの縦の画角（度）。小さめにして、ツリーがゆがまないようにする。</summary>
        public const float FieldOfView = 30f;

        /// <summary>飾りの玉の直径（ツリーの高さに対する割合）。</summary>
        public const float OrnamentSizeRatio = 0.06f;

        /// <summary>飾りどうしを離す距離（飾りの直径に対する倍率）。リースなど大きめの飾りも重ならないようにする。</summary>
        public const float OrnamentSpacing = 1.45f;

        /// <summary>29個の飾りのうち、丸い玉の数。残りはリース・箱・ベル・星・キャンディケーンを3個ずつ。</summary>
        public const int BallCount = 14;

        /// <summary>スターの高さ（ツリーの高さに対する割合）。</summary>
        public const float StarSizeRatio = 0.13f;

        // 背景：夜空のような紺色（真っ暗にはしない）
        private static readonly Color Background = new Color(0.11f, 0.15f, 0.3f, 1f);

        private readonly List<Vector3> slots = new List<Vector3>();
        private readonly List<GameObject> ornaments = new List<GameObject>();
        private readonly List<int> ornamentBag = new List<int>();
        private readonly System.Random random = new System.Random();

        private WallBurstAssets assets;
        private Camera viewCamera;
        private EffectSpawner effects;
        private Transform root;
        private Bounds treeBounds;
        private Bounds fullBounds;
        private OrnamentPlacer.Cone cone;
        private GameObject star;
        private Vector3 starPosition;
        private Vector3 starScale;
        private float ornamentScale;
        private OrnamentShapes shapes;
        private int counted;
        private float lastCountedTime = float.NegativeInfinity;
        private bool completed;
        private float completedAt;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private Text errorText;

        /// <inheritdoc/>
        protected override void BuildScene()
        {
            viewCamera = CreateCamera(Background);
            viewCamera.fieldOfView = FieldOfView;
            viewCamera.nearClipPlane = 0.05f;
            viewCamera.farClipPlane = 500f;
            effects = EffectSpawner.Create(10);
            root = new GameObject("クリスマスツリー").transform;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.55f);
            Light key = new GameObject("ライト").AddComponent<Light>();
            key.transform.SetParent(root, false);
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.94f, 0.85f);
            key.intensity = 1.1f;
            key.transform.rotation = Quaternion.Euler(25f, 20f, 0f);

            assets = WallBurstAssets.Load();
            if (assets == null || assets.treePrefab == null || assets.starPrefab == null || assets.ornamentPrefabs == null || assets.ornamentPrefabs.Length == 0)
            {
                ShowError("ツリーのアセットが見つかりません。Unity のメニュー「WallBurst/セットアップ」を実行してください。");
                return;
            }

            BuildTree();
            FitCamera();
        }

        /// <inheritdoc/>
        protected override void OnWallHit(Vector2 screenPosition)
        {
            if (star == null || completed)
            {
                // 完成後や準備できていないときは、数えずに光だけ
                effects.Spawn(screenPosition, 0.6f);
                return;
            }

            if (Time.time - lastCountedTime < CooldownSeconds || counted >= TotalHits)
            {
                effects.Spawn(screenPosition, new Color(1f, 1f, 0.8f), 0.45f, 0.6f);
                return;
            }

            lastCountedTime = Time.time;
            counted++;
            effects.Spawn(screenPosition);
            Vector3 start = ScreenToWorld(screenPosition);
            if (counted < TotalHits)
            {
                AddOrnament(start);
            }
            else
            {
                PlaceStar(start);
            }
        }

        /// <inheritdoc/>
        protected override void Update()
        {
            base.Update();
            if (viewCamera != null && star != null && (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight))
            {
                FitCamera();
            }

            if (completed && Time.time - completedAt >= GlowSeconds)
            {
                RequestNext();
            }

            if (errorText != null && SceneSeconds > GlowSeconds)
            {
                RequestNext();
            }
        }

        /// <inheritdoc/>
        protected override int RemainingBalls()
        {
            return Mathf.Max(0, TotalHits - counted);
        }

        /// <inheritdoc/>
        protected override string DescribeState()
        {
            return "クリスマスツリー：数えた当たり " + counted + " / " + TotalHits + (completed ? "（完成・輝き中）" : string.Empty);
        }

        private void BuildTree()
        {
            GameObject tree = Instantiate(assets.treePrefab, root);
            tree.name = "ツリー";
            tree.transform.localPosition = Vector3.zero;
            tree.transform.localRotation = Quaternion.identity;
            treeBounds = MeasureBounds(tree);
            float height = treeBounds.size.y;

            // 飾りの大きさ：プレハブの大きさから、ツリーの高さの 6% になる倍率を求める
            GameObject sample = Instantiate(assets.ornamentPrefabs[0]);
            Bounds sampleBounds = MeasureBounds(sample);
            sample.SetActive(false);
            float sampleSize = Mathf.Max(sampleBounds.size.x, Mathf.Max(sampleBounds.size.y, sampleBounds.size.z));
            ornamentScale = sampleSize > 1e-5f ? height * OrnamentSizeRatio / sampleSize : 1f;
            Destroy(sample);

            // ツリーの形を円すいとみなす（下の 12% は幹、上の 8% はスターの下）
            float radius = Mathf.Min(treeBounds.extents.x, treeBounds.extents.z);
            cone = new OrnamentPlacer.Cone();
            cone.AxisXZ = new Vector2(treeBounds.center.x, treeBounds.center.z);
            cone.BottomY = treeBounds.min.y + height * 0.16f;
            cone.TopY = treeBounds.max.y - height * 0.12f;
            cone.BottomRadius = radius * 0.88f;
            cone.TopRadius = radius * 0.12f;
            slots.AddRange(OrnamentPlacer.Generate(cone, TotalHits - 1, height * OrnamentSizeRatio * OrnamentSpacing, random));
            FillOrnamentBag();
            Renderer sampleRenderer = assets.ornamentPrefabs[0].GetComponentInChildren<Renderer>();
            shapes = new OrnamentShapes(sampleRenderer != null ? sampleRenderer.sharedMaterial : null);

            // スター：高さの 13% にして、てっぺんに置く。はじめは隠しておく
            star = Instantiate(assets.starPrefab, root);
            star.name = "スター";
            Bounds starBounds = MeasureBounds(star);
            float starSize = Mathf.Max(0.0001f, starBounds.size.y);
            star.transform.localScale *= height * StarSizeRatio / starSize;
            starBounds = MeasureBounds(star);
            Vector3 offset = new Vector3(treeBounds.center.x, treeBounds.max.y - height * 0.02f, treeBounds.center.z) - new Vector3(starBounds.center.x, starBounds.min.y, starBounds.center.z);
            star.transform.position += offset;
            starPosition = star.transform.position;
            starScale = star.transform.localScale;
            fullBounds = treeBounds;
            fullBounds.Encapsulate(MeasureBounds(star));
            star.SetActive(false);
        }

        private void FitCamera()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            bool byWidth;
            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
            float distance = TreeLayout.ComputeCameraDistance(fullBounds.size, FieldOfView, aspect, out byWidth);
            viewCamera.transform.position = fullBounds.center + new Vector3(0f, 0f, -distance);
            viewCamera.transform.rotation = Quaternion.identity;
        }

        private void AddOrnament(Vector3 start)
        {
            int index = random.Next(slots.Count);
            Vector3 slot = slots[index];
            slots.RemoveAt(index);
            float diameter = treeBounds.size.y * OrnamentSizeRatio;
            Vector3 scale;
            GameObject ornament = CreateOrnament(diameter, out scale);
            ornament.name = "飾り " + counted;
            ornament.transform.position = start;
            ornaments.Add(ornament);
            FlyTo.Launch(ornament, slot, scale, treeBounds.size.y * 0.15f, 0.7f, () =>
            {
                if (ornament != null)
                {
                    OrnamentSparkle.Attach(ornament, diameter, -viewCamera.transform.forward, random);
                }
            });
        }

        // 丸い玉と、リース・箱・ベル・星・キャンディケーンを混ぜた袋を作り、よく混ぜる
        private void FillOrnamentBag()
        {
            ornamentBag.Clear();
            int total = TotalHits - 1;
            for (int i = 0; i < total; i++)
            {
                ornamentBag.Add(i < BallCount ? -1 : (i - BallCount) % OrnamentShapes.KindCount);
            }

            for (int i = ornamentBag.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int swap = ornamentBag[i];
                ornamentBag[i] = ornamentBag[j];
                ornamentBag[j] = swap;
            }
        }

        private GameObject CreateOrnament(float diameter, out Vector3 scale)
        {
            int kind = ornamentBag.Count > 0 ? ornamentBag[0] : -1;
            if (ornamentBag.Count > 0)
            {
                ornamentBag.RemoveAt(0);
            }

            if (kind < 0 || shapes == null)
            {
                GameObject prefab = assets.ornamentPrefabs[random.Next(assets.ornamentPrefabs.Length)];
                scale = prefab.transform.localScale * ornamentScale;
                return Instantiate(prefab, root);
            }

            OrnamentShapes.Kind shape = (OrnamentShapes.Kind)kind;
            GameObject ornament = shapes.Create(shape, random);
            ornament.transform.SetParent(root, false);
            ornament.transform.rotation = Quaternion.Euler(0f, 0f, ((float)random.NextDouble() * 2f - 1f) * 15f);
            scale = Vector3.one * diameter * ShapeSize(shape);
            return ornament;
        }

        // 丸い玉（直径 1）とくらべた、それぞれの飾りの大きさ
        private static float ShapeSize(OrnamentShapes.Kind kind)
        {
            switch (kind)
            {
                case OrnamentShapes.Kind.Wreath:
                    return 1.35f;
                case OrnamentShapes.Kind.GiftBox:
                    return 1.05f;
                case OrnamentShapes.Kind.Bell:
                    return 1.2f;
                case OrnamentShapes.Kind.Star:
                    return 1.25f;
                default:
                    return 1.35f;
            }
        }

        private void PlaceStar(Vector3 start)
        {
            star.transform.position = start;
            star.SetActive(true);
            FlyTo.Launch(star, starPosition, starScale, treeBounds.size.y * 0.2f, 1.0f, Complete);
        }

        private void Complete()
        {
            completed = true;
            completedAt = Time.time;
            TreeGlow glow = new GameObject("輝き").AddComponent<TreeGlow>();
            glow.transform.SetParent(root, false);
            glow.Begin(ornaments, star, fullBounds, cone, random);
        }

        /// <inheritdoc/>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (shapes != null)
            {
                shapes.Dispose();
            }
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            float depth = Vector3.Dot(fullBounds.center - viewCamera.transform.position, viewCamera.transform.forward) - fullBounds.extents.z;
            return viewCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, Mathf.Max(viewCamera.nearClipPlane + 0.1f, depth)));
        }

        private static Bounds MeasureBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            bool has = false;
            Bounds bounds = new Bounds(target.transform.position, Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!has)
                {
                    bounds = renderer.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private void ShowError(string message)
        {
            Canvas canvas = Ui.CreateCanvas("エラー", 50);
            errorText = Ui.CreateText(canvas.transform, message, 40, TextAnchor.MiddleCenter);
            Ui.Stretch(errorText.rectTransform);
            Debug.LogError("[WallBurst] " + message);
        }
    }
}
