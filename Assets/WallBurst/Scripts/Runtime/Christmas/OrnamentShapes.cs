using System.Collections.Generic;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 丸い玉のほかの飾り（リース・プレゼントの箱・ベル・星・キャンディケーン）を、プログラムで作ります。
    /// <para>形（メッシュ）は1回だけ作って使い回します。色（マテリアル）は、Christmas_Tree_Decorator の玉と同じシェーダーで作り、
    /// シーンが終わると <see cref="Dispose"/> で消します。</para>
    /// <para>どの飾りも、直径およそ 1（-0.5〜0.5 の箱に収まる大きさ）で、正面をカメラ（-Z）に向けて作ります。</para>
    /// </summary>
    public sealed class OrnamentShapes
    {
        /// <summary>飾りの種類。</summary>
        public enum Kind
        {
            /// <summary>リース。</summary>
            Wreath,

            /// <summary>プレゼントの箱。</summary>
            GiftBox,

            /// <summary>ベル。</summary>
            Bell,

            /// <summary>星。</summary>
            Star,

            /// <summary>キャンディケーン（杖の形のあめ）。</summary>
            CandyCane
        }

        /// <summary>種類の数。</summary>
        public const int KindCount = 5;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private static readonly Color Green = new Color(0.12f, 0.5f, 0.18f);
        private static readonly Color Red = new Color(0.9f, 0.06f, 0.1f);
        private static readonly Color White = new Color(0.97f, 0.97f, 0.97f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.22f);
        private static readonly Color Silver = new Color(0.85f, 0.88f, 0.95f);

        private static readonly Color[] BoxColors =
        {
            new Color(0.85f, 0.1f, 0.15f),
            new Color(0.15f, 0.35f, 0.9f),
            new Color(0.15f, 0.65f, 0.3f),
            new Color(0.6f, 0.2f, 0.85f),
            new Color(1f, 0.45f, 0.7f)
        };

        private static Mesh torus;
        private static Mesh sphere;
        private static Mesh cube;
        private static Mesh bell;
        private static Mesh star;
        private static Mesh caneRed;
        private static Mesh caneWhite;

        private readonly Material template;
        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        /// <summary>
        /// 作る準備をします。
        /// </summary>
        /// <param name="templateMaterial">見本のマテリアル（玉のマテリアル。null なら URP の Lit）</param>
        public OrnamentShapes(Material templateMaterial)
        {
            template = templateMaterial;
        }

        /// <summary>
        /// 飾りを1つ作ります。
        /// </summary>
        /// <param name="kind">種類</param>
        /// <param name="random">乱数（色を選ぶ）</param>
        /// <returns>飾り（大きさ 1）</returns>
        public GameObject Create(Kind kind, System.Random random)
        {
            GameObject root = new GameObject("飾り（" + kind + "）");
            switch (kind)
            {
                case Kind.Wreath:
                    AddPart(root, "輪", Torus, Green, false, Vector3.zero, Vector3.one, Quaternion.identity);
                    AddPart(root, "リボン左", Sphere, Red, false, new Vector3(-0.13f, -0.36f, -0.12f), new Vector3(0.26f, 0.15f, 0.08f), Quaternion.Euler(0f, 0f, 25f));
                    AddPart(root, "リボン右", Sphere, Red, false, new Vector3(0.13f, -0.36f, -0.12f), new Vector3(0.26f, 0.15f, 0.08f), Quaternion.Euler(0f, 0f, -25f));
                    AddPart(root, "結び目", Sphere, Red, false, new Vector3(0f, -0.36f, -0.14f), Vector3.one * 0.09f, Quaternion.identity);
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = (40f + i * 70f) * Mathf.Deg2Rad;
                        Vector3 position = new Vector3(Mathf.Cos(angle) * 0.36f, Mathf.Sin(angle) * 0.36f, -0.13f);
                        AddPart(root, "実", Sphere, i % 2 == 0 ? Red : Gold, true, position, Vector3.one * 0.08f, Quaternion.identity);
                    }

                    break;
                case Kind.GiftBox:
                    Color boxColor = BoxColors[random.Next(BoxColors.Length)];
                    AddPart(root, "箱", Cube, boxColor, false, new Vector3(0f, -0.08f, 0f), Vector3.one * 0.7f, Quaternion.identity);
                    AddPart(root, "リボン縦", Cube, Gold, true, new Vector3(0f, -0.08f, 0f), new Vector3(0.13f, 0.72f, 0.72f), Quaternion.identity);
                    AddPart(root, "リボン横", Cube, Gold, true, new Vector3(0f, -0.08f, 0f), new Vector3(0.72f, 0.72f, 0.13f), Quaternion.identity);
                    AddPart(root, "ちょう結び左", Sphere, Gold, true, new Vector3(-0.12f, 0.33f, 0f), new Vector3(0.24f, 0.14f, 0.1f), Quaternion.Euler(0f, 0f, -25f));
                    AddPart(root, "ちょう結び右", Sphere, Gold, true, new Vector3(0.12f, 0.33f, 0f), new Vector3(0.24f, 0.14f, 0.1f), Quaternion.Euler(0f, 0f, 25f));
                    break;
                case Kind.Bell:
                    AddPart(root, "ベル", Bell, Gold, true, Vector3.zero, Vector3.one, Quaternion.identity);
                    AddPart(root, "舌", Sphere, Gold, true, new Vector3(0f, -0.42f, 0f), Vector3.one * 0.14f, Quaternion.identity);
                    AddPart(root, "リボン", Sphere, Red, false, new Vector3(0f, 0.38f, -0.12f), new Vector3(0.3f, 0.12f, 0.08f), Quaternion.identity);
                    break;
                case Kind.Star:
                    AddPart(root, "星", Star, random.Next(2) == 0 ? Gold : Silver, true, Vector3.zero, Vector3.one, Quaternion.identity);
                    break;
                default:
                    AddPart(root, "あめ（赤）", CaneRed, Red, false, Vector3.zero, Vector3.one, Quaternion.identity);
                    AddPart(root, "あめ（白）", CaneWhite, White, false, Vector3.zero, Vector3.one, Quaternion.identity);
                    break;
            }

            return root;
        }

        /// <summary>
        /// 作ったマテリアルを消します（シーンを終えるとき）。
        /// </summary>
        public void Dispose()
        {
            foreach (Material material in materials.Values)
            {
                if (material != null)
                {
                    Object.Destroy(material);
                }
            }

            materials.Clear();
        }

        private void AddPart(GameObject root, string name, Mesh mesh, Color color, bool metal, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial(color, metal);
        }

        private Material GetMaterial(Color color, bool metal)
        {
            Color key = new Color(color.r, color.g, color.b, metal ? 1f : 0.5f);
            Material material;
            if (materials.TryGetValue(key, out material))
            {
                return material;
            }

            if (template != null)
            {
                material = new Material(template);
            }
            else
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(lit != null ? lit : Shader.Find("Standard"));
            }

            material.name = "飾りの色";
            if (material.HasProperty(BaseMapId))
            {
                material.SetTexture(BaseMapId, null);
            }

            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }
            else
            {
                material.color = color;
            }

            if (material.HasProperty(MetallicId))
            {
                material.SetFloat(MetallicId, metal ? 1f : 0.15f);
            }

            if (material.HasProperty(SmoothnessId))
            {
                material.SetFloat(SmoothnessId, metal ? 0.9f : 0.55f);
            }

            materials[key] = material;
            return material;
        }

        // ---------------------------------------------------------------
        // 形（メッシュ）。1回だけ作って使い回す
        // ---------------------------------------------------------------
        private static Mesh Torus
        {
            get { return torus != null ? torus : (torus = BuildTorus(0.36f, 0.12f, 36, 14)); }
        }

        private static Mesh Sphere
        {
            get { return sphere != null ? sphere : (sphere = BuildSphere(0.5f, 16, 12)); }
        }

        private static Mesh Cube
        {
            get { return cube != null ? cube : (cube = BuildCube()); }
        }

        private static Mesh Bell
        {
            get
            {
                if (bell == null)
                {
                    Vector2[] profile =
                    {
                        new Vector2(0f, 0.42f), new Vector2(0.1f, 0.41f), new Vector2(0.17f, 0.34f), new Vector2(0.2f, 0.2f),
                        new Vector2(0.23f, 0.02f), new Vector2(0.3f, -0.16f), new Vector2(0.42f, -0.3f), new Vector2(0.46f, -0.36f),
                        new Vector2(0.4f, -0.37f), new Vector2(0f, -0.3f)
                    };
                    bell = BuildLathe(profile, 28);
                }

                return bell;
            }
        }

        private static Mesh Star
        {
            get { return star != null ? star : (star = BuildStar(0.5f, 0.22f, 0.13f)); }
        }

        private static Mesh CaneRed
        {
            get
            {
                if (caneRed == null)
                {
                    BuildCane(out caneRed, out caneWhite);
                }

                return caneRed;
            }
        }

        private static Mesh CaneWhite
        {
            get
            {
                if (caneWhite == null)
                {
                    BuildCane(out caneRed, out caneWhite);
                }

                return caneWhite;
            }
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<int> triangles)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildTorus(float radius, float tube, int segments, int sides)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments * Mathf.PI * 2f;
                Vector3 center = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f) * radius;
                for (int j = 0; j <= sides; j++)
                {
                    float v = j / (float)sides * Mathf.PI * 2f;
                    Vector3 outward = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
                    vertices.Add(center + (outward * Mathf.Cos(v) + new Vector3(0f, 0f, Mathf.Sin(v))) * tube);
                }
            }

            AddGrid(triangles, segments, sides);
            return Finish("WB_Torus", vertices, triangles);
        }

        private static Mesh BuildSphere(float radius, int longitude, int latitude)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int i = 0; i <= longitude; i++)
            {
                float u = i / (float)longitude * Mathf.PI * 2f;
                for (int j = 0; j <= latitude; j++)
                {
                    float v = j / (float)latitude * Mathf.PI;
                    vertices.Add(new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u)) * radius);
                }
            }

            AddGrid(triangles, longitude, latitude);
            return Finish("WB_Sphere", vertices, triangles);
        }

        private static Mesh BuildCube()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            Vector3[] normals = { Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left };
            foreach (Vector3 n in normals)
            {
                Vector3 a = new Vector3(n.y, n.z, n.x);
                Vector3 b = Vector3.Cross(n, a);
                int start = vertices.Count;
                vertices.Add((n - a - b) * 0.5f);
                vertices.Add((n - a + b) * 0.5f);
                vertices.Add((n + a + b) * 0.5f);
                vertices.Add((n + a - b) * 0.5f);
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            Mesh mesh = Finish("WB_Cube", vertices, triangles);
            FixWinding(mesh);
            return mesh;
        }

        private static Mesh BuildLathe(Vector2[] profile, int segments)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments * Mathf.PI * 2f;
                foreach (Vector2 p in profile)
                {
                    vertices.Add(new Vector3(Mathf.Cos(u) * p.x, p.y, Mathf.Sin(u) * p.x));
                }
            }

            AddGrid(triangles, segments, profile.Length - 1);
            return Finish("WB_Bell", vertices, triangles);
        }

        private static Mesh BuildStar(float outer, float inner, float thickness)
        {
            // 角ばった立体の星（面ごとに頂点を分けて、きらっと光るようにする）
            Vector3 front = new Vector3(0f, 0f, -thickness);
            Vector3 back = new Vector3(0f, 0f, thickness);
            Vector3[] rim = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                rim[i] = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
            }

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                Vector3 a = rim[i];
                Vector3 b = rim[(i + 1) % 10];
                AddTriangle(vertices, triangles, front, b, a);
                AddTriangle(vertices, triangles, back, a, b);
            }

            Mesh mesh = Finish("WB_Star", vertices, triangles);
            FixWinding(mesh);
            return mesh;
        }

        private static void BuildCane(out Mesh red, out Mesh white)
        {
            // 下からまっすぐ上がり、上で半円にまがる線（中心を左右の真ん中にそろえる）
            List<Vector3> path = new List<Vector3>();
            float hook = 0.17f;
            for (int i = 0; i <= 10; i++)
            {
                path.Add(new Vector3(-hook, Mathf.Lerp(-0.45f, 0.22f, i / 10f), 0f));
            }

            for (int i = 1; i <= 14; i++)
            {
                float angle = Mathf.PI - i / 14f * Mathf.PI;
                path.Add(new Vector3(Mathf.Cos(angle) * hook, 0.22f + Mathf.Sin(angle) * hook, 0f));
            }

            path.Add(new Vector3(hook, 0.12f, 0f));
            List<Vector3> redVertices = new List<Vector3>();
            List<int> redTriangles = new List<int>();
            List<Vector3> whiteVertices = new List<Vector3>();
            List<int> whiteTriangles = new List<int>();
            for (int i = 0; i < path.Count - 1; i++)
            {
                bool isRed = (i / 2) % 2 == 0;
                AddTubeSegment(isRed ? redVertices : whiteVertices, isRed ? redTriangles : whiteTriangles, path, i, 0.07f, 12);
            }

            red = Finish("WB_CaneRed", redVertices, redTriangles);
            white = Finish("WB_CaneWhite", whiteVertices, whiteTriangles);
        }

        private static void AddTubeSegment(List<Vector3> vertices, List<int> triangles, List<Vector3> path, int index, float radius, int sides)
        {
            int start = vertices.Count;
            for (int k = 0; k < 2; k++)
            {
                int p = index + k;
                Vector3 previous = path[Mathf.Max(0, p - 1)];
                Vector3 next = path[Mathf.Min(path.Count - 1, p + 1)];
                Vector3 tangent = (next - previous).normalized;
                Vector3 side = Vector3.Cross(tangent, Vector3.forward).normalized;
                for (int j = 0; j <= sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2f;
                    vertices.Add(path[p] + (side * Mathf.Cos(a) + Vector3.forward * Mathf.Sin(a)) * radius);
                }
            }

            for (int j = 0; j < sides; j++)
            {
                int a = start + j;
                int b = start + j + 1;
                int c = start + sides + 1 + j;
                int d = start + sides + 2 + j;
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }
        }

        private static void AddGrid(List<int> triangles, int columns, int rows)
        {
            int stride = rows + 1;
            for (int i = 0; i < columns; i++)
            {
                for (int j = 0; j < rows; j++)
                {
                    int a = i * stride + j;
                    int b = (i + 1) * stride + j;
                    triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        // 面が内側を向いていたら、外向きに直す（形の中心から見て、法線が外を向くようにそろえる）。箱と星のような、中心から外へ広がる形に使う
        private static void FixWinding(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Vector3 center = mesh.bounds.center;
            int outward = 0;
            int inward = 0;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]];
                Vector3 b = vertices[triangles[t + 1]];
                Vector3 c = vertices[triangles[t + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 middle = (a + b + c) / 3f;
                if (Vector3.Dot(normal, middle - center) >= 0f)
                {
                    outward++;
                }
                else
                {
                    inward++;
                }
            }

            if (inward <= outward)
            {
                return;
            }

            for (int t = 0; t < triangles.Length; t += 3)
            {
                int swap = triangles[t + 1];
                triangles[t + 1] = triangles[t + 2];
                triangles[t + 2] = swap;
            }

            mesh.triangles = triangles;
            mesh.RecalculateNormals();
        }
    }
}
