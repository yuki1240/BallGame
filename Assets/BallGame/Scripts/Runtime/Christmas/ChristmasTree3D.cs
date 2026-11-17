using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BallGame
{
    /// <summary>
    /// 3D のクリスマスの部屋（Polyart の PolyartChristmasScene）にあるツリーへ飾りを付ける係です。
    /// 始めにツリーに最初から付いている飾りと電球を見えなくし、何も飾っていないツリーにします。
    /// ボールが当たるたびに、色とりどりの飾り玉か赤い電球を、当たった場所からツリーへ飛ばして付けます。
    /// 最後に星をてっぺんに付け、そのあとで電球をぴかぴか光らせます。
    /// </summary>
    public sealed class ChristmasTree3D : MonoBehaviour
    {
        /// <summary>3D の部屋のシーン名（Build Settings に登録しておきます）。</summary>
        public const string EnvironmentSceneName = "PolyartChristmasScene";

        /// <summary>ツリーのオブジェクト名の始まり。</summary>
        public const string TreeNamePrefix = "PF_ChristmasTree";

        private const string InvisibleShaderResource = "BallGameInvisible";
        private const string InvisibleShaderName = "BallGame/Invisible";
        private const float BulbBlinkSpeed = 5f;

        /// <summary>星の中心の高さ（ツリーの最上部を 0 として、星の大きさの何倍か。マイナスなら下）。星がてっぺんに少しかかるくらいにします。</summary>
        private const float StarCenterOffset = -1.1f;

        /// <summary>星の光の強さ（まぶしすぎないよう控えめにします）。</summary>
        private const float StarEmissionScale = 0.45f;

        /// <summary>星の先が画面の上から何割までに収まっていてほしいか（上端が 1）。</summary>
        private const float MaxStarViewportY = 0.9f;

        /// <summary>画角を広げてよい上限（度）。</summary>
        private const float MaxFieldOfView = 80f;

        private static readonly Color BulbOffColor = new Color(0.35f, 0.05f, 0.06f, 1f);
        private static readonly Color BulbOnColor = new Color(1f, 0.12f, 0.1f, 1f);
        private static readonly Color StarColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color CapColor = new Color(0.85f, 0.75f, 0.4f, 1f);

        private readonly List<Bulb> bulbs = new List<Bulb>();
        private Camera viewCamera;
        private Bounds treeBounds;
        private Vector3 towardCamera;
        private Vector3[] slotPositions;
        private float ornamentSize;
        private int ornamentCount;
        private float foliageBottom;
        private float foliageTop;
        private float coneBaseRadius;
        private float starSize;
        private Transform star;
        private Quaternion starBaseRotation;
        private bool bulbsLit;
        private bool celebrating;

        private sealed class Bulb
        {
            public Material Material;
            public Light Light;
            public float Phase;
        }

        /// <summary>飾りを付けられる場所の数。</summary>
        public int SlotCount => slotPositions != null ? slotPositions.Length : 0;

        /// <summary>
        /// 3D の部屋のシーンを読み込めるか（Build Settings に入っているか）を返します。
        /// </summary>
        /// <returns>読み込めるなら true</returns>
        public static bool CanLoadEnvironment()
        {
            return Application.CanStreamedLevelBeLoaded(EnvironmentSceneName);
        }

        /// <summary>
        /// 読み込んだ部屋のシーンからカメラとツリーを探し、飾り付けの準備をします。
        /// </summary>
        /// <param name="environment">読み込んだ部屋のシーン</param>
        /// <returns>準備できた係。カメラかツリーが見つからなければ null</returns>
        public static ChristmasTree3D Create(Scene environment)
        {
            Camera roomCamera = FindInScene<Camera>(environment, null);
            Transform tree = FindTree(environment);
            if (roomCamera == null || tree == null)
            {
                Debug.LogWarning("[Christmas] 3D の部屋にカメラかツリー（" + TreeNamePrefix + "）が見つかりませんでした。");
                return null;
            }

            GameObject holder = new GameObject("ChristmasTree3D（飾り）");
            SceneManager.MoveGameObjectToScene(holder, environment);
            ChristmasTree3D view = holder.AddComponent<ChristmasTree3D>();
            view.Initialize(roomCamera, tree);
            return view;
        }

        private static T FindInScene<T>(Scene scene, string namePrefix) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>(true))
                {
                    if (namePrefix == null || component.gameObject.name.StartsWith(namePrefix, System.StringComparison.Ordinal))
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        private static Transform FindTree(Scene scene)
        {
            return FindInScene<Transform>(scene, TreeNamePrefix);
        }

        private void Initialize(Camera roomCamera, Transform tree)
        {
            viewCamera = roomCamera;
            HideOriginalDecorations(tree);
            treeBounds = GetBounds(tree);
            towardCamera = GetHorizontalDirection(treeBounds.center, roomCamera.transform.position);
            ornamentSize = treeBounds.size.y * 0.075f;
            starSize = treeBounds.size.y * 0.09f;
            EnsureStarFitsOnScreen();
            slotPositions = ComputeSlotPositions(tree);
        }

        private void EnsureStarFitsOnScreen()
        {
            // 部屋のカメラはツリーの先端が画面の上ぎりぎりなので、星が切れないよう画角を少しずつ広げる
            if (viewCamera.orthographic)
            {
                return;
            }

            Vector3 starTip = new Vector3(treeBounds.center.x, treeBounds.max.y + starSize * (StarCenterOffset + 1.1f), treeBounds.center.z);
            int guard = 0;
            while (viewCamera.WorldToViewportPoint(starTip).y > MaxStarViewportY && viewCamera.fieldOfView < MaxFieldOfView && guard++ < 100)
            {
                viewCamera.fieldOfView += 0.5f;
            }
        }

        private static void HideOriginalDecorations(Transform tree)
        {
            Material invisible = CreateInvisibleMaterial();
            if (invisible == null)
            {
                Debug.LogWarning("[Christmas] 見えなくするシェーダー（" + InvisibleShaderName + "）が見つからないため、元の飾りが残ります。");
                return;
            }

            foreach (Renderer renderer in tree.GetComponentsInChildren<Renderer>(true))
            {
                ReplaceHiddenMaterials(renderer, invisible);
            }
        }

        private static Material CreateInvisibleMaterial()
        {
            Shader shader = Resources.Load<Shader>(InvisibleShaderResource);
            if (shader == null)
            {
                shader = Shader.Find(InvisibleShaderName);
            }

            return shader != null ? new Material(shader) { name = "BallGame_Invisible" } : null;
        }

        private static void ReplaceHiddenMaterials(Renderer renderer, Material invisible)
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && ChristmasTree3DLayout.IsHiddenMaterialName(materials[i].name))
                {
                    materials[i] = invisible;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }

        private static Bounds GetBounds(Transform tree)
        {
            Renderer[] renderers = tree.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(tree.position + Vector3.up, Vector3.one * 2f);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static Vector3 GetHorizontalDirection(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
        }

        private Vector3[] ComputeSlotPositions(Transform tree)
        {
            // カメラから見えるツリーの面に置く：画面上で決めた場所からカメラの光線を飛ばし、最初に当たった葉の上に付ける
            List<ChristmasTree3DLayout.Slot> slots = ChristmasTree3DLayout.GetSlots();
            Vector3[] positions = new Vector3[slots.Count];
            float height = treeBounds.size.y;
            foliageBottom = treeBounds.min.y + height * 0.15f;
            foliageTop = treeBounds.max.y - height * 0.08f;
            coneBaseRadius = Mathf.Min(treeBounds.extents.x, treeBounds.extents.z) * 0.85f;
            Vector3 right = viewCamera.transform.right;
            right.y = 0f;
            right = right.sqrMagnitude < 0.0001f ? Vector3.right : right.normalized;
            MeshCollider probe = AddProbeCollider(tree);
            int visible = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                positions[i] = FindVisiblePoint(probe, slots[i], right);
                Vector3 view = viewCamera.WorldToViewportPoint(positions[i]);
                visible += IsInsideScreen(view) ? 1 : 0;
            }

            if (probe != null)
            {
                Destroy(probe);
            }

            Debug.Log("[Christmas] 飾りの場所 " + positions.Length + " か所のうち、画面に映るのは " + visible + " か所です。");
            return positions;
        }

        private static bool IsInsideScreen(Vector3 viewport)
        {
            return viewport.z > 0f && viewport.x > 0.02f && viewport.x < 0.98f && viewport.y > 0.02f && viewport.y < 0.98f;
        }

        private Vector3 FindVisiblePoint(MeshCollider probe, ChristmasTree3DLayout.Slot slot, Vector3 right)
        {
            float y = Mathf.Lerp(foliageBottom, foliageTop, slot.Height01);
            Vector3 axis = new Vector3(treeBounds.center.x, y, treeBounds.center.z);
            float radius = ChristmasTree3DLayout.GetConeRadius(slot.Height01, coneBaseRadius);
            Vector3 centerView = viewCamera.WorldToViewportPoint(axis);
            Vector3 edgeView = viewCamera.WorldToViewportPoint(axis + right * radius);
            float halfWidth = Mathf.Abs(edgeView.x - centerView.x);
            Vector2 target = new Vector2(centerView.x + slot.Lateral01 * halfWidth, centerView.y);
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(target.x, target.y, 0f));
            float lift = ornamentSize * 0.3f;
            if (probe != null && probe.Raycast(ray, out RaycastHit hit, 1000f))
            {
                return hit.point - ray.direction * lift;
            }

            if (TryMarchToCone(ray, out Vector3 onCone))
            {
                return onCone - ray.direction * lift;
            }

            // 見つからないときは、ツリーの真ん中のカメラ側に付ける
            return axis + towardCamera * radius;
        }

        private bool TryMarchToCone(Ray ray, out Vector3 point)
        {
            // 光線を少しずつ進め、最初に円すいの中へ入った所を表面とみなす
            float step = Mathf.Max(0.01f, ornamentSize * 0.1f);
            float limit = Vector3.Distance(ray.origin, treeBounds.center) * 2f;
            for (float distance = 0f; distance < limit; distance += step)
            {
                point = ray.GetPoint(distance);
                float height01 = Mathf.InverseLerp(foliageBottom, foliageTop, point.y);
                if (height01 <= 0f || height01 >= 1f)
                {
                    continue;
                }

                float dx = point.x - treeBounds.center.x;
                float dz = point.z - treeBounds.center.z;
                if (Mathf.Sqrt(dx * dx + dz * dz) <= ChristmasTree3DLayout.GetConeRadius(height01, coneBaseRadius))
                {
                    return true;
                }
            }

            point = Vector3.zero;
            return false;
        }

        private static MeshCollider AddProbeCollider(Transform tree)
        {
            // ツリーの表面を探すための一時的な当たり判定（いちばん細かい LOD0 の形を使う）
            MeshFilter filter = FindMainMeshFilter(tree);
            if (filter == null || filter.sharedMesh == null)
            {
                return null;
            }

            try
            {
                MeshCollider probe = filter.gameObject.AddComponent<MeshCollider>();
                probe.sharedMesh = filter.sharedMesh;
                Physics.SyncTransforms();
                return probe;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Christmas] ツリーの表面を調べられないため、円すいの形で飾りを置きます：" + exception.Message);
                return null;
            }
        }

        private static MeshFilter FindMainMeshFilter(Transform tree)
        {
            LODGroup lodGroup = tree.GetComponentInChildren<LODGroup>(true);
            if (lodGroup != null)
            {
                LOD[] lods = lodGroup.GetLODs();
                if (lods.Length > 0 && lods[0].renderers.Length > 0 && lods[0].renderers[0] != null)
                {
                    return lods[0].renderers[0].GetComponent<MeshFilter>();
                }
            }

            return tree.GetComponentInChildren<MeshFilter>(true);
        }

        /// <summary>
        /// 飾りを1つ付けます。当たった場所から飛んできて、ツリーの決まった場所に付きます。
        /// </summary>
        /// <param name="slotIndex">付ける場所の番号</param>
        /// <param name="redBulb">赤い電球にするなら true（☆が付くまで消えたまま）</param>
        /// <param name="color">飾り玉の色</param>
        /// <param name="viewportPoint">ボールが当たった場所（画面の左下 0・右上 1）</param>
        public void AddOrnament(int slotIndex, bool redBulb, Color color, Vector2 viewportPoint)
        {
            Vector3 target = slotPositions[slotIndex % slotPositions.Length];
            target += GetRepeatOffset();
            Transform ornament = redBulb ? CreateBulb(slotIndex) : CreateBall(slotIndex, color);
            ornament.rotation = Quaternion.LookRotation(towardCamera);
            ornament.gameObject.AddComponent<OrnamentFlight3D>().Fly(GetStartPoint(viewportPoint), target, Vector3.one * ornamentSize, ornamentSize * 2f);
        }

        // 固定モードで飾りが場所の数より多くなったときは、重ならないよう少しずらして付ける
        private Vector3 GetRepeatOffset()
        {
            ornamentCount++;
            if (ornamentCount <= slotPositions.Length)
            {
                return Vector3.zero;
            }

            float range = ornamentSize * 1.6f;
            Transform view = viewCamera.transform;
            return view.right * (Random.value * 2f - 1f) * range + view.up * (Random.value * 2f - 1f) * range;
        }

        /// <summary>
        /// てっぺんに星を付けます。付けたら電球を光らせます。
        /// </summary>
        /// <param name="viewportPoint">ボールが当たった場所（画面の左下 0・右上 1）</param>
        public void PlaceStar(Vector2 viewportPoint)
        {
            Vector3 top = new Vector3(treeBounds.center.x, treeBounds.max.y + starSize * StarCenterOffset, treeBounds.center.z);
            try
            {
                star = CreateStar();
                starBaseRotation = Quaternion.LookRotation(towardCamera);
                star.rotation = starBaseRotation;
                star.gameObject.AddComponent<OrnamentFlight3D>().Fly(GetStartPoint(viewportPoint), top, Vector3.one * starSize, starSize * 2f);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Christmas] 星を付けられませんでした：" + exception);
            }

            Vector3 view = viewCamera.WorldToViewportPoint(top);
            Debug.Log("[Christmas] 星を付けました。位置=" + top + " 大きさ=" + starSize + " 画面上の位置=(" + view.x.ToString("F2") + ", " + view.y.ToString("F2") + ") カメラからの距離=" + view.z.ToString("F1"));
            LightUpBulbs();
        }

        /// <summary>
        /// 赤い電球をすべて光らせます。
        /// </summary>
        public void LightUpBulbs()
        {
            bulbsLit = true;
            foreach (Bulb bulb in bulbs)
            {
                bulb.Material.SetColor("_BaseColor", BulbOnColor);
                bulb.Material.SetColor("_Color", BulbOnColor);
                bulb.Light.enabled = true;
            }
        }

        /// <summary>
        /// お祝い中（星がゆらゆら揺れる）かどうかを切り替えます。
        /// </summary>
        /// <param name="value">お祝い中なら true</param>
        public void SetCelebrating(bool value)
        {
            celebrating = value;
        }

        /// <summary>
        /// ツリーが画面のどこに映っているかを返します（画面の左下 0・右上 1）。
        /// </summary>
        /// <returns>ツリーを囲む四角</returns>
        public Rect GetViewportRect()
        {
            Vector2 min = new Vector2(1f, 1f);
            Vector2 max = new Vector2(0f, 0f);
            Vector3 c = treeBounds.center;
            Vector3 e = treeBounds.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 viewport = viewCamera.WorldToViewportPoint(corner);
                min = Vector2.Min(min, new Vector2(viewport.x, viewport.y));
                max = Vector2.Max(max, new Vector2(viewport.x, viewport.y));
            }

            min = Vector2.Max(min, Vector2.zero);
            max = Vector2.Min(max, Vector2.one);
            return Rect.MinMaxRect(min.x, min.y, Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private Vector3 GetStartPoint(Vector2 viewportPoint)
        {
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
            float distance = Vector3.Distance(viewCamera.transform.position, treeBounds.center) * 0.45f;
            return ray.GetPoint(distance);
        }

        private Transform CreateBall(int index, Color color)
        {
            Transform holder = new GameObject("Ball " + (index + 1)).transform;
            holder.SetParent(transform, false);
            AddPrimitive(holder, PrimitiveType.Sphere, Vector3.zero, Vector3.one, CreateMaterial(color, 0.85f));
            AddPrimitive(holder, PrimitiveType.Cylinder, new Vector3(0f, 0.52f, 0f), new Vector3(0.28f, 0.08f, 0.28f), CreateMaterial(CapColor, 0.7f));
            return holder;
        }

        private Transform CreateBulb(int index)
        {
            Transform holder = new GameObject("RedBulb " + (index + 1)).transform;
            holder.SetParent(transform, false);
            Material material = CreateMaterial(BulbOffColor, 0.9f);
            AddPrimitive(holder, PrimitiveType.Sphere, new Vector3(0f, -0.05f, 0f), new Vector3(0.72f, 1f, 0.72f), material);
            AddPrimitive(holder, PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.34f, 0.12f, 0.34f), CreateMaterial(new Color(0.75f, 0.75f, 0.72f), 0.6f));
            Light light = AddGlowLight(holder, BulbOnColor, ornamentSize * 6f, 1.5f);
            light.enabled = false;
            bulbs.Add(new Bulb { Material = material, Light = light, Phase = index * 1.3f });
            return holder;
        }

        private Transform CreateStar()
        {
            GameObject starObject = new GameObject("Star");
            starObject.transform.SetParent(transform, false);
            starObject.AddComponent<MeshFilter>().sharedMesh = BuildStarMesh();
            MeshRenderer renderer = starObject.AddComponent<MeshRenderer>();
            Material material = CreateMaterial(StarColor, 0.6f);
            SetEmission(material, StarColor * StarEmissionScale);
            material.SetFloat("_Cull", 0f); // 面の向きに関係なく両面とも描く
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            AddGlowLight(starObject.transform, StarColor, starSize * 4f, 0.5f);
            return starObject.transform;
        }

        private static Mesh BuildStarMesh()
        {
            // 外側と内側の点を交互につなぎ、前と後ろに少しふくらませた立体の星
            Vector3[] outline = ChristmasTree3DLayout.GetStarOutline(5, 1f, 0.45f);
            Vector3 front = new Vector3(0f, 0f, 0.3f);
            Vector3 back = new Vector3(0f, 0f, -0.3f);
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int i = 0; i < outline.Length; i++)
            {
                Vector3 a = outline[i];
                Vector3 b = outline[(i + 1) % outline.Length];
                AddTriangle(vertices, triangles, front, a, b);
                AddTriangle(vertices, triangles, back, b, a);
            }

            Mesh mesh = new Mesh { name = "BallGame_Star" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            // 角ばって見えるよう、三角ごとに頂点を分ける
            triangles.Add(vertices.Count);
            vertices.Add(a);
            triangles.Add(vertices.Count);
            vertices.Add(b);
            triangles.Add(vertices.Count);
            vertices.Add(c);
        }

        private static void AddPrimitive(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Light AddGlowLight(Transform parent, Color color, float range, float intensity)
        {
            GameObject lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(parent, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Material CreateMaterial(Color color, float smoothness)
        {
            Material material = new Material(FindLitShader());
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", 0.15f);
            return material;
        }

        private static Shader FindLitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }

            return shader != null ? shader : Shader.Find("Standard");
        }

        private static void SetEmission(Material material, Color emission)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", emission);
        }

        private void Update()
        {
            if (bulbsLit)
            {
                AnimateBulbs();
            }

            if (celebrating && star != null)
            {
                star.rotation = Quaternion.AngleAxis(25f * Mathf.Sin(Time.time * 2.2f), Vector3.up) * starBaseRotation;
            }
        }

        private void AnimateBulbs()
        {
            foreach (Bulb bulb in bulbs)
            {
                float brightness = Twinkle.EvaluateBrightness(Time.time, bulb.Phase, BulbBlinkSpeed);
                SetEmission(bulb.Material, BulbOnColor * (3f * brightness));
                bulb.Light.intensity = 1.5f * brightness;
            }
        }
    }
}
