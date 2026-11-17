using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// 花と演出の選択のテスト。
    /// </summary>
    public sealed class HitReactionSelectionTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            created.ForEach(item => Object.DestroyImmediate(item));
            created.Clear();
        }

        [Test]
        public void 下半分は花で上半分は演出()
        {
            Assert.That(HitReactionSelector.Classify(0.1f, 0.5f), Is.EqualTo(HitReactionKind.Flower));
            Assert.That(HitReactionSelector.Classify(0.49f, 0.5f), Is.EqualTo(HitReactionKind.Flower));
            Assert.That(HitReactionSelector.Classify(0.5f, 0.5f), Is.EqualTo(HitReactionKind.Effect));
            Assert.That(HitReactionSelector.Classify(0.9f, 0.5f), Is.EqualTo(HitReactionKind.Effect));
        }

        [Test]
        public void 境目を変えると判定も変わる()
        {
            Assert.That(HitReactionSelector.Classify(0.6f, 0.7f), Is.EqualTo(HitReactionKind.Flower));
            Assert.That(HitReactionSelector.Classify(0.6f, 0.3f), Is.EqualTo(HitReactionKind.Effect));
        }

        [Test]
        public void 未指定の演出は背景ごとの既定になる()
        {
            Assert.That(HitReactionSelector.ResolveEffects(null, 0), Is.EqualTo(new[] { EffectKind.Ring, EffectKind.Flash, EffectKind.Particles }));
            Assert.That(HitReactionSelector.ResolveEffects(new BackgroundTheme(), 1), Is.EqualTo(new[] { EffectKind.DoubleRing, EffectKind.Ripple, EffectKind.Cross }));
            Assert.That(HitReactionSelector.ResolveEffects(null, 2), Is.EqualTo(new[] { EffectKind.Swirl, EffectKind.Petals, EffectKind.FlashParticles }));
        }

        [Test]
        public void 指定した演出の中からだけ選ぶ()
        {
            BackgroundTheme theme = new BackgroundTheme { effects = new List<EffectKind> { EffectKind.Cross, EffectKind.Swirl } };
            IReadOnlyList<EffectKind> effects = HitReactionSelector.ResolveEffects(theme, 0);
            System.Random random = new System.Random(3);
            HashSet<EffectKind> picked = new HashSet<EffectKind>();
            for (int i = 0; i < 100; i++)
            {
                picked.Add(HitReactionSelector.PickEffect(effects, random));
            }

            Assert.That(picked, Is.EquivalentTo(new[] { EffectKind.Cross, EffectKind.Swirl }));
        }

        [Test]
        public void 花は割り当てた中からランダムに1枚選び無ければnull()
        {
            Sprite a = CreateSprite("flower_a");
            Sprite b = CreateSprite("flower_b");
            BackgroundTheme theme = new BackgroundTheme { flowers = new List<Sprite> { a, null, b } };
            System.Random random = new System.Random(5);
            HashSet<Sprite> picked = new HashSet<Sprite>();
            for (int i = 0; i < 100; i++)
            {
                picked.Add(HitReactionSelector.PickFlower(theme, random));
            }

            Assert.That(picked, Is.EquivalentTo(new[] { a, b }));
            Assert.That(HitReactionSelector.PickFlower(new BackgroundTheme(), random), Is.Null);
            Assert.That(HitReactionSelector.PickFlower(null, random), Is.Null);
        }

        [Test]
        public void 背景の割り当ては名前を優先し無ければ並び順()
        {
            GameEffectSettings settings = ScriptableObject.CreateInstance<GameEffectSettings>();
            created.Add(settings);
            BackgroundTheme first = new BackgroundTheme();
            BackgroundTheme named = new BackgroundTheme { backgroundName = "Sea" };
            settings.themes = new List<BackgroundTheme> { first, named };

            Assert.That(settings.FindTheme("sea", 0), Is.SameAs(named));
            Assert.That(settings.FindTheme("Forest", 0), Is.SameAs(first));
            Assert.That(settings.FindTheme("Forest", 1), Is.Null);
            Assert.That(settings.FindTheme("Forest", 5), Is.Null);
        }

        [Test]
        public void 演出設定の初期値()
        {
            GameEffectSettings settings = ScriptableObject.CreateInstance<GameEffectSettings>();
            created.Add(settings);

            Assert.That(settings.flowerBoundary, Is.EqualTo(0.5f));
            Assert.That(settings.maxConcurrent, Is.EqualTo(24));
            Assert.That(settings.effectScale, Is.EqualTo(1.25f));
            Assert.That(settings.effectDuration, Is.EqualTo(1f));
            Assert.That(settings.flowerHeight, Is.EqualTo(360f));
            Assert.That(settings.flowerGrowSeconds, Is.EqualTo(1f));
            Assert.That(settings.flowerDisplaySeconds, Is.EqualTo(3f));
            Assert.That(settings.GetFlowerHoldSeconds(), Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void 花は根元から伸びて伸びきると等倍()
        {
            Assert.That(FlowerPlayer.EvaluateGrowthScale(0f).y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(FlowerPlayer.EvaluateGrowthScale(1f).y, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(FlowerPlayer.EvaluateGrowthScale(0.8f).y, Is.GreaterThan(1f));
        }

        [Test]
        public void すべての演出に部品がある()
        {
            System.Random random = new System.Random(1);
            foreach (EffectKind kind in System.Enum.GetValues(typeof(EffectKind)))
            {
                Assert.That(EffectRecipes.Build(kind, random), Is.Not.Empty, kind.ToString());
            }
        }

        private Sprite CreateSprite(string name)
        {
            Texture2D texture = new Texture2D(4, 8);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 8), new Vector2(0.5f, 0f));
            sprite.name = name;
            created.Add(sprite);
            created.Add(texture);
            return sprite;
        }
    }
}
