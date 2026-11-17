using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 演出の部品1つぶんの動き。時間は演出全体を 0〜1 とした割合です。
    /// </summary>
    public struct EffectPieceSpec
    {
        /// <summary>形。</summary>
        public EffectShape Shape;

        /// <summary>大きさ（画素、倍率をかける前）。</summary>
        public Vector2 Size;

        /// <summary>始まりの位置（中心からの画素）。</summary>
        public Vector2 StartPosition;

        /// <summary>終わりの位置。</summary>
        public Vector2 EndPosition;

        /// <summary>始まりの拡大率。</summary>
        public float StartScale;

        /// <summary>終わりの拡大率。</summary>
        public float EndScale;

        /// <summary>始まりの回転（度）。</summary>
        public float StartRotation;

        /// <summary>終わりの回転（度）。</summary>
        public float EndRotation;

        /// <summary>始まりの不透明度。</summary>
        public float StartAlpha;

        /// <summary>動き始める時刻（0〜1）。</summary>
        public float Delay;

        /// <summary>動く長さ（0〜1）。</summary>
        public float Length;

        /// <summary>白に寄せる割合（閃光の芯など）。</summary>
        public float Whiten;
    }

    /// <summary>
    /// 演出の種類ごとに部品の動きを組み立てます。
    /// </summary>
    public static class EffectRecipes
    {
        /// <summary>演出の基本の大きさ（画素）。</summary>
        public const float BaseSize = 220f;

        /// <summary>
        /// 演出の部品を組み立てます。
        /// </summary>
        /// <param name="kind">演出の種類</param>
        /// <param name="random">乱数</param>
        /// <returns>部品の一覧</returns>
        public static List<EffectPieceSpec> Build(EffectKind kind, System.Random random)
        {
            List<EffectPieceSpec> pieces = new List<EffectPieceSpec>();
            switch (kind)
            {
                case EffectKind.Ring:
                    AddRing(pieces, EffectShape.Ring, 0f, 1f, 1.25f);
                    break;
                case EffectKind.Flash:
                    AddFlash(pieces);
                    break;
                case EffectKind.Particles:
                    AddParticles(pieces, random, 16, 170f);
                    break;
                case EffectKind.DoubleRing:
                    AddRing(pieces, EffectShape.Ring, 0f, 0.8f, 1.3f);
                    AddRing(pieces, EffectShape.Ring, 0.2f, 0.8f, 0.85f);
                    break;
                case EffectKind.Ripple:
                    AddRipple(pieces);
                    break;
                case EffectKind.Cross:
                    AddCross(pieces);
                    break;
                case EffectKind.Swirl:
                    AddSwirl(pieces);
                    break;
                case EffectKind.Petals:
                    AddPetals(pieces, random);
                    break;
                default:
                    AddFlash(pieces);
                    AddParticles(pieces, random, 20, 210f);
                    break;
            }

            return pieces;
        }

        private static EffectPieceSpec CreatePiece(EffectShape shape, float size)
        {
            return new EffectPieceSpec
            {
                Shape = shape,
                Size = new Vector2(size, size),
                StartScale = 1f,
                EndScale = 1f,
                StartAlpha = 1f,
                Length = 1f
            };
        }

        private static void AddRing(List<EffectPieceSpec> pieces, EffectShape shape, float delay, float length, float endScale)
        {
            EffectPieceSpec ring = CreatePiece(shape, BaseSize);
            ring.StartScale = 0.15f;
            ring.EndScale = endScale;
            ring.Delay = delay;
            ring.Length = length;
            pieces.Add(ring);
        }

        private static void AddFlash(List<EffectPieceSpec> pieces)
        {
            EffectPieceSpec glow = CreatePiece(EffectShape.SoftCircle, BaseSize * 1.4f);
            glow.StartScale = 0.3f;
            glow.EndScale = 1.3f;
            glow.Length = 0.7f;
            pieces.Add(glow);

            EffectPieceSpec core = CreatePiece(EffectShape.SoftCircle, BaseSize * 0.6f);
            core.StartScale = 1.2f;
            core.EndScale = 0.2f;
            core.Length = 0.5f;
            core.Whiten = 0.7f;
            pieces.Add(core);
        }

        private static void AddParticles(List<EffectPieceSpec> pieces, System.Random random, int count, float distance)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.6f) / count * Mathf.PI * 2f;
                float reach = distance * (0.6f + 0.4f * (float)random.NextDouble());
                EffectPieceSpec dot = CreatePiece(EffectShape.Dot, 22f + 12f * (float)random.NextDouble());
                dot.EndPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                dot.EndScale = 0.2f;
                dot.Delay = 0.05f * (float)random.NextDouble();
                dot.Length = 0.95f - dot.Delay;
                pieces.Add(dot);
            }
        }

        private static void AddRipple(List<EffectPieceSpec> pieces)
        {
            for (int i = 0; i < 3; i++)
            {
                AddRing(pieces, EffectShape.ThinRing, i * 0.18f, 0.64f, 1.2f + i * 0.15f);
            }
        }

        private static void AddCross(List<EffectPieceSpec> pieces)
        {
            for (int i = 0; i < 4; i++)
            {
                EffectPieceSpec bar = CreatePiece(EffectShape.Bar, BaseSize * 1.6f);
                bar.Size = new Vector2(BaseSize * 1.6f, BaseSize * 0.4f);
                bar.StartScale = 0.1f;
                bar.EndScale = 1f;
                bar.StartRotation = i * 45f;
                bar.EndRotation = i * 45f + 20f;
                bar.StartAlpha = i % 2 == 0 ? 1f : 0.6f;
                pieces.Add(bar);
            }
        }

        private static void AddSwirl(List<EffectPieceSpec> pieces)
        {
            const int count = 12;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count;
                EffectPieceSpec dot = CreatePiece(EffectShape.Dot, 26f);
                dot.StartPosition = Polar(angle, 10f);
                dot.EndPosition = Polar(angle + 240f, 150f);
                dot.StartRotation = angle;
                dot.EndRotation = angle + 240f;
                dot.EndScale = 0.4f;
                dot.Delay = i * 0.02f;
                dot.Length = 1f - dot.Delay;
                pieces.Add(dot);
            }

            AddRing(pieces, EffectShape.ThinRing, 0f, 0.6f, 0.8f);
        }

        private static void AddPetals(List<EffectPieceSpec> pieces, System.Random random)
        {
            const int count = 8;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count + (float)random.NextDouble() * 15f;
                EffectPieceSpec petal = CreatePiece(EffectShape.Petal, 70f);
                petal.Size = new Vector2(55f, 80f);
                petal.EndPosition = Polar(angle, 140f + 30f * (float)random.NextDouble());
                petal.StartRotation = angle - 90f;
                petal.EndRotation = angle - 90f + 120f;
                petal.StartScale = 0.3f;
                petal.EndScale = 1f;
                pieces.Add(petal);
            }
        }

        private static Vector2 Polar(float degrees, float radius)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        }
    }
}
