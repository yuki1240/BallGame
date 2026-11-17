using NUnit.Framework;
using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// 紅葉の山（景色の絵・色の広がり・完成の流れ）のテスト。
    /// </summary>
    public sealed class AutumnTests
    {
        private static bool[] MakeMask(int width, int height, int mountainRows)
        {
            bool[] mask = new bool[width * height];
            for (int y = 0; y < mountainRows; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    mask[y * width + x] = true;
                }
            }

            return mask;
        }

        [Test]
        public void 景色は山と空の両方を持つ()
        {
            AutumnScenery scenery = AutumnScenery.Generate(200, 112);
            int mountain = scenery.CountMountainPixels();

            Assert.That(mountain, Is.GreaterThan(scenery.Width * scenery.Height / 5));
            Assert.That(mountain, Is.LessThan(scenery.Width * scenery.Height * 9 / 10));
        }

        [Test]
        public void 景色は同じ引数なら同じ絵になる()
        {
            AutumnScenery a = AutumnScenery.Generate(120, 68, 5);
            AutumnScenery b = AutumnScenery.Generate(120, 68, 5);

            Assert.That(a.CountMountainPixels(), Is.EqualTo(b.CountMountainPixels()));
            Assert.That(a.Autumn[1000].r, Is.EqualTo(b.Autumn[1000].r));
        }

        [Test]
        public void 空の色は緑と紅葉で同じ()
        {
            AutumnScenery scenery = AutumnScenery.Generate(120, 68);
            for (int i = 0; i < scenery.Mask.Length; i++)
            {
                if (!scenery.Mask[i])
                {
                    Assert.That(scenery.Green[i].g, Is.EqualTo(scenery.Autumn[i].g));
                }
            }
        }

        [Test]
        public void 山の緑は緑色で紅葉は赤っぽい()
        {
            AutumnScenery scenery = AutumnScenery.Generate(200, 112);
            float greenSum = 0f;
            float redSum = 0f;
            int count = 0;
            for (int i = 0; i < scenery.Mask.Length; i++)
            {
                if (scenery.Mask[i])
                {
                    greenSum += scenery.Green[i].g - scenery.Green[i].r;
                    redSum += scenery.Autumn[i].r - scenery.Autumn[i].b;
                    count++;
                }
            }

            Assert.That(greenSum / count, Is.GreaterThan(0f), "緑の山は赤より緑が強い");
            Assert.That(redSum / count, Is.GreaterThan(0.1f), "紅葉は青より赤が強い");
        }

        [Test]
        public void 始めはどこも緑のまま()
        {
            AutumnColoring coloring = new AutumnColoring(100, 50, MakeMask(100, 50, 40));

            Assert.That(coloring.Coverage, Is.EqualTo(0f));
            Assert.That(coloring.GetProgress(10, 10), Is.EqualTo(0f));
            Assert.That(coloring.Tick(1f), Is.False);
        }

        [Test]
        public void 当たった場所から色が変わり遠くは変わらない()
        {
            AutumnColoring coloring = new AutumnColoring(200, 100, MakeMask(200, 100, 90));
            coloring.AddSource(new Vector2(0.1f, 0.3f));
            coloring.Tick(5f);

            Assert.That(coloring.GetProgress(20, 30), Is.GreaterThan(0.99f), "当たった場所");
            Assert.That(coloring.GetProgress(180, 30), Is.EqualTo(0f), "遠い場所");
        }

        [Test]
        public void 山でない場所は色が変わらない()
        {
            AutumnColoring coloring = new AutumnColoring(200, 100, MakeMask(200, 100, 20));
            coloring.AddSource(new Vector2(0.5f, 0.8f));
            coloring.Tick(5f);

            Assert.That(coloring.GetProgress(100, 80), Is.EqualTo(0f));
        }

        [Test]
        public void 色は少しずつ広がり戻らない()
        {
            AutumnColoring coloring = new AutumnColoring(200, 100, MakeMask(200, 100, 90));
            coloring.AddSource(new Vector2(0.5f, 0.5f));
            coloring.Tick(0.5f);
            float early = coloring.Coverage;
            coloring.Tick(5f);
            float late = coloring.Coverage;

            Assert.That(late, Is.GreaterThan(early));
            coloring.Tick(1f);
            Assert.That(coloring.Coverage, Is.GreaterThanOrEqualTo(late));
        }

        [Test]
        public void 変わった行の範囲が分かり消せる()
        {
            AutumnColoring coloring = new AutumnColoring(200, 100, MakeMask(200, 100, 90));
            coloring.AddSource(new Vector2(0.5f, 0.5f));
            Assert.That(coloring.Tick(0.5f), Is.True);

            int minY;
            int maxY;
            Assert.That(coloring.TryGetDirtyRows(out minY, out maxY), Is.True);
            Assert.That(minY <= maxY, Is.True);
            coloring.ClearDirty();
            Assert.That(coloring.TryGetDirtyRows(out minY, out maxY), Is.False);
        }

        [Test]
        public void たくさん当たると完成して全体が紅葉する()
        {
            AutumnColoring coloring = new AutumnColoring(160, 90, MakeMask(160, 90, 80));
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    coloring.AddSource(new Vector2((i + 0.5f) / 6f, (j + 0.5f) / 4f));
                }
            }

            for (int step = 0; step < 200 && !coloring.IsAllColored; step++)
            {
                coloring.Tick(0.1f);
            }

            Assert.That(coloring.IsAllColored, Is.True);
            Assert.That(coloring.Coverage, Is.GreaterThan(0.99f));
        }

        [Test]
        public void 完成に向かい始めたら新しい当たりは増えない()
        {
            AutumnColoring coloring = new AutumnColoring(60, 30, MakeMask(60, 30, 28), null, 3f, 0.5f);
            coloring.AddSource(new Vector2(0.5f, 0.5f));
            coloring.Tick(1f);
            Assert.That(coloring.IsFinishing, Is.True);

            int before = coloring.SourceCount;
            coloring.AddSource(new Vector2(0.1f, 0.1f));

            Assert.That(coloring.SourceCount, Is.EqualTo(before));
        }

        [Test]
        public void 回数でなくても完成を知らせるとお祝いへ進む()
        {
            ChristmasFlow flow = new ChristmasFlow();

            Assert.That(flow.Complete(), Is.True);
            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Celebrating));
            Assert.That(flow.Complete(), Is.False, "二度目は何もしない");
        }

        [Test]
        public void 回数を増やすと10回では完成しない()
        {
            ChristmasFlow flow = new ChristmasFlow();
            flow.SetGoal(SceneHold.TreeGoal);
            for (int i = 0; i < 10; i++)
            {
                flow.RegisterHit(i * 2f);
            }

            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Playing));
            Assert.That(flow.Goal, Is.EqualTo(SceneHold.TreeGoal));
            Assert.That(flow.IsNextFinal, Is.False);
        }

        [Test]
        public void 回数を戻しても当てた数より少なくならない()
        {
            ChristmasFlow flow = new ChristmasFlow();
            flow.SetGoal(SceneHold.TreeGoal);
            for (int i = 0; i < 30; i++)
            {
                flow.RegisterHit(i * 2f);
            }

            flow.SetGoal(ChristmasFlow.DefaultGoal);

            Assert.That(flow.Goal, Is.EqualTo(31));
            Assert.That(flow.IsNextFinal, Is.True);
        }

        [Test]
        public void 固定モードの入り切りと回数()
        {
            Assert.That(SceneHold.IsHolding, Is.False);
            Assert.That(SceneHold.GetGoal(SceneHold.TreeGoal), Is.EqualTo(ChristmasFlow.DefaultGoal));
            Assert.That(SceneHold.GetOperatorHint(), Is.EqualTo(string.Empty));

            Assert.That(SceneHold.Toggle(), Is.True);
            Assert.That(SceneHold.GetGoal(SceneHold.TreeGoal), Is.EqualTo(1000));
            Assert.That(SceneHold.GetOperatorHint(), Is.Not.EqualTo(string.Empty));

            Assert.That(SceneHold.Toggle(), Is.False);
        }
    }
}
