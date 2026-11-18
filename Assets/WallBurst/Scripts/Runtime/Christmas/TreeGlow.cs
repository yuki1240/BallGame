using System.Collections.Generic;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// ツリーが完成したときに、ツリー全体を輝かせる演出です。
    /// <list type="bullet">
    /// <item>飾りの玉が明るくなったり暗くなったりして、きらきら点滅する</item>
    /// <item>ツリーのまわりに光の粒がきらめく</item>
    /// <item>ツリーの後ろに大きな光がふわっと広がる</item>
    /// <item>暖かい色のライトが明るくなり、スターがゆっくり回って脈打つ</item>
    /// </list>
    /// 作った物はすべてこのオブジェクトの子なので、シーンが替わると一緒に消えます。
    /// </summary>
    public sealed class TreeGlow : MonoBehaviour
    {
        private const float RampSeconds = 1.5f;
        private const int SparkleCount = 60;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private sealed class Blinker
        {
            public Renderer Renderer;
            public Color BaseColor;
            public float Phase;
        }

        private sealed class SparkleDot
        {
            public SpriteRenderer Sprite;
            public float Phase;
            public float Speed;
            public float Size;
        }

        private readonly List<Blinker> blinkers = new List<Blinker>();
        private readonly List<SparkleDot> sparkles = new List<SparkleDot>();
        private MaterialPropertyBlock block;
        private Transform star;
        private Vector3 starScale;
        private Light warmLight;
        private SpriteRenderer halo;
        private float haloSize;
        private Color ambientStart;
        private float elapsed;

        /// <summary>
        /// 輝き始めます。
        /// </summary>
        /// <param name="ornaments">飾りの玉</param>
        /// <param name="starObject">スター</param>
        /// <param name="treeBounds">ツリー全体の箱</param>
        /// <param name="cone">ツリーの形</param>
        /// <param name="random">乱数</param>
        public void Begin(IList<GameObject> ornaments, GameObject starObject, Bounds treeBounds, OrnamentPlacer.Cone cone, System.Random random)
        {
            block = new MaterialPropertyBlock();
            foreach (GameObject ornament in ornaments)
            {
                foreach (Renderer renderer in ornament.GetComponentsInChildren<Renderer>())
                {
                    Material material = renderer.sharedMaterial;
                    if (material == null || !material.HasProperty(BaseColorId))
                    {
                        continue;
                    }

                    Blinker blinker = new Blinker();
                    blinker.Renderer = renderer;
                    blinker.BaseColor = material.GetColor(BaseColorId);
                    blinker.Phase = (float)random.NextDouble() * Mathf.PI * 2f;
                    blinkers.Add(blinker);
                }
            }

            if (starObject != null)
            {
                star = starObject.transform;
                starScale = star.localScale;
            }

            float height = treeBounds.size.y;
            warmLight = new GameObject("輝きのライト").AddComponent<Light>();
            warmLight.transform.SetParent(transform, false);
            warmLight.type = LightType.Point;
            warmLight.color = new Color(1f, 0.85f, 0.6f);
            warmLight.range = height * 2.5f;
            warmLight.intensity = 0f;
            warmLight.transform.position = treeBounds.center + new Vector3(0f, 0f, -treeBounds.extents.z - height * 0.4f);

            halo = CreateSprite("後ろの光", ProceduralSprites.Glow, new Color(1f, 0.85f, 0.45f, 0f));
            halo.transform.position = treeBounds.center + new Vector3(0f, 0f, treeBounds.extents.z + 0.05f * height);
            haloSize = height * 1.5f;
            halo.transform.localScale = Vector3.one * haloSize;

            for (int i = 0; i < SparkleCount; i++)
            {
                float t = Mathf.Sqrt((float)random.NextDouble());
                float y = Mathf.Lerp(cone.TopY + height * 0.05f, cone.BottomY - height * 0.03f, t);
                float radius = Mathf.Lerp(cone.TopRadius, cone.BottomRadius, t) * 1.25f;
                float x = cone.AxisXZ.x + ((float)random.NextDouble() * 2f - 1f) * radius;
                Color color = i % 3 == 0 ? new Color(1f, 1f, 1f, 0f) : new Color(1f, 0.85f, 0.4f, 0f);
                SparkleDot dot = new SparkleDot();
                dot.Sprite = CreateSprite("光の粒", ProceduralSprites.Sparkle, color);
                dot.Sprite.transform.position = new Vector3(x, y, cone.AxisXZ.y - radius * 0.9f);
                dot.Phase = (float)random.NextDouble() * Mathf.PI * 2f;
                dot.Speed = 2f + (float)random.NextDouble() * 4f;
                dot.Size = height * (0.025f + (float)random.NextDouble() * 0.035f);
                sparkles.Add(dot);
            }

            ambientStart = RenderSettings.ambientLight;
        }

        private SpriteRenderer CreateSprite(string name, Sprite sprite, Color color)
        {
            GameObject spriteObject = new GameObject(name);
            spriteObject.transform.SetParent(transform, false);
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            return renderer;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float ramp = Mathf.Clamp01(elapsed / RampSeconds);
            float time = Time.time;

            foreach (Blinker blinker in blinkers)
            {
                if (blinker.Renderer == null)
                {
                    continue;
                }

                float wave = 0.5f + 0.5f * Mathf.Sin(time * 4f + blinker.Phase);
                float brightness = 1f + ramp * (0.6f + 1.4f * wave);
                Color color = blinker.BaseColor * brightness;
                color.a = blinker.BaseColor.a;
                blinker.Renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                blinker.Renderer.SetPropertyBlock(block);
            }

            foreach (SparkleDot dot in sparkles)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(time * dot.Speed + dot.Phase);
                Color color = dot.Sprite.color;
                color.a = ramp * wave * wave;
                dot.Sprite.color = color;
                dot.Sprite.transform.localScale = Vector3.one * dot.Size * (0.6f + 0.6f * wave);
            }

            if (star != null)
            {
                star.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
                star.localScale = starScale * (1f + 0.08f * ramp * Mathf.Sin(time * 3f));
            }

            warmLight.intensity = ramp * (2.2f + 0.4f * Mathf.Sin(time * 2f));
            Color haloColor = halo.color;
            haloColor.a = ramp * (0.35f + 0.08f * Mathf.Sin(time * 1.5f));
            halo.color = haloColor;
            RenderSettings.ambientLight = Color.Lerp(ambientStart, ambientStart * 1.6f, ramp);
        }
    }
}
