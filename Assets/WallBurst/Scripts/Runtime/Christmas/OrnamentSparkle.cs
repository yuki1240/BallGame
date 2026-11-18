using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// ツリーに付いた飾りの玉を、いつもきらきらさせる部品です。
    /// <list type="bullet">
    /// <item>付いた瞬間に、ぱっと光る</item>
    /// <item>玉を少し明るくし、表面のきらめき（Christmas_Tree_Decorator のシェーダーの Glint・Sparkle）を強くする</item>
    /// <item>玉の手前で、十字の光がちかちか瞬く（玉ごとに速さと間合いをずらす）</item>
    /// </list>
    /// 作った物はこの飾りの子なので、シーンが替わると一緒に消えます。
    /// </summary>
    public sealed class OrnamentSparkle : MonoBehaviour
    {
        /// <summary>玉の明るさの倍率。</summary>
        public const float Brightness = 1.35f;

        /// <summary>表面のきらめきの強さ（シェーダーの _GlintIntensity。元は 1）。</summary>
        public const float GlintIntensity = 5f;

        /// <summary>表面の細かい光の強さ（シェーダーの _SparkleStrength。元は 0.6）。</summary>
        public const float SparkleStrength = 2f;

        private const float PopSeconds = 0.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int GlintId = Shader.PropertyToID("_GlintIntensity");
        private static readonly int SparkleId = Shader.PropertyToID("_SparkleStrength");

        private static readonly Color[] Colors =
        {
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 0.92f, 0.6f, 0f),
            new Color(1f, 0.85f, 0.95f, 0f)
        };

        private SpriteRenderer twinkle;
        private SpriteRenderer pop;
        private float size;
        private float phase;
        private float speed;
        private float age;

        /// <summary>
        /// 飾りをきらきらさせ始めます。
        /// </summary>
        /// <param name="ornament">飾り</param>
        /// <param name="diameter">飾りの直径（ワールドの大きさ）</param>
        /// <param name="towardCamera">カメラの方向（長さ1）</param>
        /// <param name="random">乱数</param>
        public static void Attach(GameObject ornament, float diameter, Vector3 towardCamera, System.Random random)
        {
            OrnamentSparkle sparkle = ornament.AddComponent<OrnamentSparkle>();
            sparkle.size = diameter;
            sparkle.phase = (float)random.NextDouble() * Mathf.PI * 2f;
            sparkle.speed = 2.5f + (float)random.NextDouble() * 3.5f;
            sparkle.Brighten();
            Vector3 front = ornament.transform.position + towardCamera * diameter * 0.6f;
            sparkle.twinkle = sparkle.CreateSprite("きらめき", ProceduralSprites.Sparkle, Colors[random.Next(Colors.Length)], front);
            sparkle.pop = sparkle.CreateSprite("ぱっと光る", ProceduralSprites.Glow, new Color(1f, 0.95f, 0.75f, 0f), front);
        }

        private void Brighten()
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Material material = renderer.sharedMaterial;
                if (material == null || renderer is SpriteRenderer)
                {
                    continue;
                }

                renderer.GetPropertyBlock(block);
                if (material.HasProperty(BaseColorId))
                {
                    Color color = material.GetColor(BaseColorId) * Brightness;
                    color.a = material.GetColor(BaseColorId).a;
                    block.SetColor(BaseColorId, color);
                }

                if (material.HasProperty(GlintId))
                {
                    block.SetFloat(GlintId, GlintIntensity);
                }

                if (material.HasProperty(SparkleId))
                {
                    block.SetFloat(SparkleId, SparkleStrength);
                }

                renderer.SetPropertyBlock(block);
            }
        }

        private SpriteRenderer CreateSprite(string name, Sprite sprite, Color color, Vector3 position)
        {
            GameObject spriteObject = new GameObject(name);
            spriteObject.transform.SetParent(transform, true);
            spriteObject.transform.position = position;
            spriteObject.transform.rotation = Quaternion.identity;
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            SetWorldSize(spriteObject.transform, 0f);
            return renderer;
        }

        private void SetWorldSize(Transform target, float worldSize)
        {
            // 親（飾り）の大きさに関係なく、ワールドでの大きさをそろえる
            Vector3 parentScale = transform.lossyScale;
            float parent = Mathf.Max(1e-5f, Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Max(Mathf.Abs(parentScale.y), Mathf.Abs(parentScale.z))));
            target.localScale = Vector3.one * (worldSize / parent);
        }

        private void Update()
        {
            age += Time.deltaTime;

            // 付いた瞬間に、ぱっと光る
            if (pop != null)
            {
                float t = age / PopSeconds;
                if (t >= 1f)
                {
                    Destroy(pop.gameObject);
                    pop = null;
                }
                else
                {
                    Color color = pop.color;
                    color.a = (1f - t) * 0.9f;
                    pop.color = color;
                    SetWorldSize(pop.transform, size * (1.5f + 2.5f * t));
                }
            }

            // 十字の光が、ちかちか瞬く（ときどき強く光る）
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * speed + phase);
            float flash = Mathf.Pow(wave, 6f);
            Color twinkleColor = twinkle.color;
            twinkleColor.a = 0.25f + 0.75f * flash;
            twinkle.color = twinkleColor;
            SetWorldSize(twinkle.transform, size * (0.9f + 1.4f * flash));
            twinkle.transform.rotation = Quaternion.Euler(0f, 0f, 20f * Mathf.Sin(Time.time * 0.7f + phase));
        }
    }
}
