using System.Collections.Generic;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 【A 方式】台形の深度を、深度を持ったまま壁の上の長方形（1cm マスの壁グリッド、<see cref="RectifiedDepthGrid"/>）に直してから、
    /// その長方形の上でボールの当たりを見つけます。
    /// <para>今までの方式（B：<see cref="HitDetector"/>）は、台形のままの深度画像で判定して、当たった1点だけを壁の位置に直します。
    /// A 方式は、判定そのものを壁の上で行うので、1マスがどこでも 1cm × 1cm で、見た目（画面）と判定の場所が同じになります。</para>
    /// <para>A 方式に関わるものは、すべてこのファイルにまとめてあります。B 方式に戻すときは <see cref="Enabled"/> を false にするか、
    /// ゲーム中・キャリブレーション中に F8 キーを押してください。このファイルごと消すときは、WallBurstSession の「A 方式」と書いた所も消してください。</para>
    /// <para>1フレームの流れ：</para>
    /// <list type="number">
    /// <item>今の深度を壁グリッドに直す（基準の深度も、校正のときに1回だけ壁グリッドに直しておく）</item>
    /// <item>マスを調べる：壁から手前に出ていて、しかも基準より手前に出ているマスを「変化したマス」にする</item>
    /// <item>塊を作る：となり合っていて、壁からの高さが近いマスを1つの塊にまとめる</item>
    /// <item>塊ごとに見分ける：マスの数（＝壁の上の面積）と広がりで、ボールか体・手かを決める</item>
    /// <item>ボールを追いかけ、壁へ近づいて触れた（または触れて離れた）ときに1回だけ当たりを出す</item>
    /// </list>
    /// </summary>
    public sealed class RectifiedHitDetector
    {
        /// <summary>A 方式を使うか（false なら今までの B 方式）。F8 キーで切り替えられます。</summary>
        public static bool Enabled = true;

        /// <summary>画面に出す、今の方式の名前。</summary>
        public static string ModeLabel
        {
            get { return Enabled ? "判定：A（長方形に直してから判定）" : "判定：B（台形のまま判定）"; }
        }

        /// <summary>基準より手前に出たとみなす、壁からの高さの差（mm）の下限。</summary>
        public const float MinRiseMm = 25f;

        /// <summary>となりのマスと同じ物とみなす、壁からの高さの差の上限（mm）に、ボールの直径に足す余裕。</summary>
        public const float JoinMarginMm = 40f;

        private const int HistoryLength = 6;

        private sealed class Candidate
        {
            public Vector3 Position;
            public Vector2 Wall;
            public float LowHeight;
            public bool Matched;
        }

        private sealed class Track
        {
            public Vector3 Position;
            public int LastFrame;
            public int Seen;
            public readonly List<float> History = new List<float>();
            public bool Emitted;
            public bool Pending;
            public int PendingFrame;
            public float PendingHeight;
            public Vector2 PendingWall;
            public bool Matched;
        }

        private struct RecentHit
        {
            public Vector2 Wall;
            public float Time;
        }

        private readonly DetectionSettings settings;
        private readonly WallMapping mapping;
        private readonly RectifiedDepthGrid grid;
        private readonly int width;
        private readonly int height;
        private readonly float[] baselineHeights;
        private readonly bool[] baselineHas;
        private readonly bool[] changed;
        private readonly int[] cellList;
        private readonly int[] queue;
        private readonly float[] sortBuffer;
        private readonly List<WallHit> hits = new List<WallHit>();
        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly List<Track> tracks = new List<Track>();
        private readonly List<RecentHit> recentHits = new List<RecentHit>();
        private readonly Dictionary<Candidate, Track> matches = new Dictionary<Candidate, Track>();
        private int frameIndex;

        /// <summary>
        /// B 方式の検知器の校正（壁の面・四隅の内側）を使って、A 方式の検知器を作ります。
        /// </summary>
        /// <param name="source">校正を取り込んだ B 方式の検知器</param>
        /// <param name="calibration">校正（基準の深度を使います）</param>
        /// <returns>作れないときは null</returns>
        public static RectifiedHitDetector Create(HitDetector source, CalibrationData calibration)
        {
            if (source == null || !source.IsConfigured || calibration == null || !calibration.HasBaseline)
            {
                return null;
            }

            return new RectifiedHitDetector(source, calibration.Baseline);
        }

        private RectifiedHitDetector(HitDetector source, ushort[] baseline)
        {
            settings = source.Settings;
            mapping = source.Mapping;
            grid = new RectifiedDepthGrid(mapping, source.InsideMask, source.Width, source.Height);
            width = grid.Width;
            height = grid.Height;
            int count = width * height;
            baselineHeights = new float[count];
            baselineHas = new bool[count];
            changed = new bool[count];
            cellList = new int[count];
            queue = new int[count];
            sortBuffer = new float[count];

            // 基準の深度を、1回だけ壁グリッドに直しておく
            grid.Build(baseline);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    baselineHas[i] = grid.HasValue(x, y);
                    baselineHeights[i] = grid.GetHeightMm(x, y);
                }
            }
        }

        /// <summary>この検知器が、この対応付けから作ったものか。</summary>
        /// <param name="other">対応付け</param>
        /// <returns>同じなら true</returns>
        public bool IsFor(WallMapping other)
        {
            return ReferenceEquals(mapping, other);
        }

        /// <summary>
        /// 追いかけている物と、最近の当たりを忘れます。
        /// </summary>
        public void ResetTracking()
        {
            tracks.Clear();
            recentHits.Clear();
            frameIndex = 0;
        }

        /// <summary>
        /// 1フレーム分の深度を調べ、このフレームで見つけた当たりを返します。
        /// </summary>
        /// <param name="depth">深度（深度画像の幅×高さ、mm）</param>
        /// <param name="timeSeconds">時刻（秒）</param>
        /// <returns>当たりの一覧（次の呼び出しで上書きされます）</returns>
        public List<WallHit> Process(ushort[] depth, float timeSeconds)
        {
            hits.Clear();
            frameIndex++;
            grid.Build(depth);
            MarkChangedCells();
            FindBlobs();
            UpdateTracks(timeSeconds);
            return hits;
        }

        private void MarkChangedCells()
        {
            float tolerance = settings.WallToleranceMm;
            float rise = Mathf.Max(MinRiseMm, settings.NoiseFloorMm);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    changed[i] = false;
                    if (!grid.HasValue(x, y))
                    {
                        continue;
                    }

                    float h = grid.GetHeightMm(x, y);
                    if (h < tolerance)
                    {
                        continue;
                    }

                    if (baselineHas[i] && h - baselineHeights[i] < rise)
                    {
                        // 基準とほぼ同じ高さ：もともとある物（変化なし）
                        continue;
                    }

                    changed[i] = true;
                }
            }
        }

        private void FindBlobs()
        {
            candidates.Clear();
            float cellArea = RectifiedDepthGrid.CellMm * RectifiedDepthGrid.CellMm;
            int minCells = Mathf.Max(settings.MinPixels, Mathf.CeilToInt(settings.MinBallAreaMm2 / cellArea));
            for (int start = 0; start < changed.Length; start++)
            {
                if (!changed[start])
                {
                    continue;
                }

                int count = CollectBlob(start);
                float area = count * cellArea;
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                float minH = float.MaxValue, maxH = float.MinValue;
                Vector2 sum = Vector2.zero;
                float heightSum = 0f;
                for (int k = 0; k < count; k++)
                {
                    int i = cellList[k];
                    int x = i % width;
                    int y = i / width;
                    float h = grid.GetHeightMm(x, y);
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                    minH = Mathf.Min(minH, h);
                    maxH = Mathf.Max(maxH, h);
                    sum += new Vector2(x + 0.5f, y + 0.5f);
                    heightSum += h;
                    sortBuffer[k] = h;
                }

                if (count < minCells)
                {
                    // 小さすぎる：ちらつき
                    continue;
                }

                float spanX = (maxX - minX + 1) * RectifiedDepthGrid.CellMm;
                float spanY = (maxY - minY + 1) * RectifiedDepthGrid.CellMm;
                float extent = new Vector3(spanX, spanY, maxH - minH).magnitude;
                if (area > settings.MaxBallAreaMm2 || extent > settings.MaxBallExtentMm)
                {
                    // 大きい・長い：体や手
                    continue;
                }

                // 壁にいちばん近い高さ：縁のノイズに引っぱられないよう、低い方から 20% の所の値
                System.Array.Sort(sortBuffer, 0, count);
                float low = sortBuffer[Mathf.Min(count - 1, count / 5)];

                Vector2 center = sum / count;
                Candidate candidate = new Candidate();
                candidate.Wall = new Vector2(center.x / width, center.y / height);
                candidate.Position = new Vector3(center.x * RectifiedDepthGrid.CellMm, center.y * RectifiedDepthGrid.CellMm, heightSum / count);
                candidate.LowHeight = low;
                candidates.Add(candidate);
            }
        }

        // start からつながっている「変化したマス」を集め、changed を消していく（集めた数を返す）
        private int CollectBlob(int start)
        {
            float join = settings.BallDiameterMm + JoinMarginMm;
            int head = 0;
            int tail = 0;
            int count = 0;
            queue[tail++] = start;
            changed[start] = false;
            while (head < tail)
            {
                int i = queue[head++];
                cellList[count++] = i;
                int x = i % width;
                int y = i / width;
                float h = grid.GetHeightMm(x, y);
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= height)
                    {
                        continue;
                    }

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        if ((dx == 0 && dy == 0) || nx < 0 || nx >= width)
                        {
                            continue;
                        }

                        int n = ny * width + nx;
                        if (!changed[n] || Mathf.Abs(grid.GetHeightMm(nx, ny) - h) > join)
                        {
                            continue;
                        }

                        changed[n] = false;
                        queue[tail++] = n;
                    }
                }
            }

            return count;
        }

        private void UpdateTracks(float timeSeconds)
        {
            foreach (Track track in tracks)
            {
                track.Matched = false;
            }

            MatchCandidates();
            foreach (Candidate candidate in candidates)
            {
                Track track;
                if (!candidate.Matched || !matches.TryGetValue(candidate, out track))
                {
                    track = new Track();
                    tracks.Add(track);
                    track.Matched = true;
                }

                track.Position = candidate.Position;
                track.LastFrame = frameIndex;
                track.Seen++;
                HandleCandidate(track, candidate, timeSeconds);
                track.History.Add(candidate.LowHeight);
                if (track.History.Count > HistoryLength)
                {
                    track.History.RemoveAt(0);
                }
            }

            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                if (frameIndex - tracks[i].LastFrame > settings.TrackLostFrames)
                {
                    // 見えなくなった物は忘れる（見えてすぐ消えた物は、ちらつきのことが多いので当たりにしない）
                    tracks.RemoveAt(i);
                }
            }

            for (int i = recentHits.Count - 1; i >= 0; i--)
            {
                if (timeSeconds - recentHits[i].Time > settings.BounceSeconds)
                {
                    recentHits.RemoveAt(i);
                }
            }
        }

        private void MatchCandidates()
        {
            matches.Clear();
            float limit = settings.TrackMatchMm;
            while (true)
            {
                float best = limit;
                Candidate bestCandidate = null;
                Track bestTrack = null;
                foreach (Candidate candidate in candidates)
                {
                    if (candidate.Matched)
                    {
                        continue;
                    }

                    foreach (Track track in tracks)
                    {
                        if (track.Matched)
                        {
                            continue;
                        }

                        float distance = Vector3.Distance(candidate.Position, track.Position);
                        if (distance <= best)
                        {
                            best = distance;
                            bestCandidate = candidate;
                            bestTrack = track;
                        }
                    }
                }

                if (bestCandidate == null)
                {
                    return;
                }

                bestCandidate.Matched = true;
                bestTrack.Matched = true;
                matches[bestCandidate] = bestTrack;
            }
        }

        private void HandleCandidate(Track track, Candidate candidate, float timeSeconds)
        {
            if (track.Emitted)
            {
                return;
            }

            bool touching = candidate.LowHeight <= settings.ContactMm;
            if (track.Pending)
            {
                if (candidate.LowHeight >= track.PendingHeight + settings.ApproachMm && track.Seen >= settings.MinApproachFrames + 1)
                {
                    // 壁に触れてから離れた：触れた所で当たり
                    Emit(track.PendingWall, timeSeconds);
                    track.Emitted = true;
                }
                else if (touching && candidate.LowHeight < track.PendingHeight)
                {
                    track.PendingHeight = candidate.LowHeight;
                    track.PendingWall = candidate.Wall;
                }
                else if (frameIndex - track.PendingFrame > settings.LeaveWaitFrames)
                {
                    // 壁に付いたまま離れない物は当たりにしない
                    track.Pending = false;
                    track.Emitted = true;
                }

                return;
            }

            if (!touching)
            {
                return;
            }

            float farthest = float.MinValue;
            foreach (float h in track.History)
            {
                farthest = Mathf.Max(farthest, h);
            }

            if (track.History.Count >= settings.MinApproachFrames && farthest - candidate.LowHeight >= settings.ApproachMm)
            {
                // 遠くから飛んできて壁に触れた：この瞬間で当たり
                Emit(candidate.Wall, timeSeconds);
                track.Emitted = true;
                return;
            }

            // 最初から壁の近くに見えた：離れるのを待ってから決める
            track.Pending = true;
            track.PendingFrame = frameIndex;
            track.PendingHeight = candidate.LowHeight;
            track.PendingWall = candidate.Wall;
        }

        private void Emit(Vector2 wall, float timeSeconds)
        {
            if (wall.x < 0f || wall.x > 1f || wall.y < 0f || wall.y > 1f)
            {
                return;
            }

            // 跳ね返りとみなす距離を、壁座標の大きさに直して比べる
            float radiusMm = settings.BounceRadiusMm;
            foreach (RecentHit recent in recentHits)
            {
                Vector2 deltaMm = new Vector2((wall.x - recent.Wall.x) * mapping.WidthMm, (wall.y - recent.Wall.y) * mapping.HeightMm);
                if (timeSeconds - recent.Time < settings.BounceSeconds && deltaMm.magnitude < radiusMm)
                {
                    return;
                }
            }

            RecentHit hit;
            hit.Wall = wall;
            hit.Time = timeSeconds;
            recentHits.Add(hit);
            Vector2 image = mapping.WallToImage(wall);
            hits.Add(new WallHit(wall, image, timeSeconds));
        }
    }
}
