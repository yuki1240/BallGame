using System.Collections.Generic;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 深度から、ボールが壁に当たったことを見つけます。
    /// <para>1フレームの流れ（<see cref="Process"/>）：</para>
    /// <list type="number">
    /// <item>画素を調べる：基準より手前に出て、しかも壁の面から手前に出ている画素を「変化した画素」にする</item>
    /// <item>塊を作る：となり合っていて深度が近い「変化した画素」を1つの塊にまとめる（深度が大きく違えば別の物）</item>
    /// <item>塊ごとに見分ける：実際の大きさ（mm）で、ボールくらいなら「ボール」、大きい・長いなら「体・手」</item>
    /// <item>ボールを追いかける：前のフレームと結び付け、壁へ飛んできて触れた瞬間（または触れて離れた瞬間）に1回だけ当たりを出す</item>
    /// </list>
    /// <para>手で壁を触ったときは、手が腕・体とつながった大きな塊になるので、ボールとはみなしません。
    /// 画面全体ではなく塊ごとに見分けるので、子どもが壁の近くにいても、ほかの子のボールは取れます。</para>
    /// </summary>
    public sealed class HitDetector
    {
        /// <summary>画素の見分けの結果：変化なし。</summary>
        public const byte ClassNone = 0;

        /// <summary>画素の見分けの結果：ボールと見た塊。</summary>
        public const byte ClassBall = 1;

        /// <summary>画素の見分けの結果：体・手と見た塊。</summary>
        public const byte ClassBody = 2;

        /// <summary>画素の見分けの結果：小さすぎる塊（ちらつき）。</summary>
        public const byte ClassNoise = 3;

        private const int HistoryLength = 6;

        private sealed class Candidate
        {
            public Vector3 Centroid;
            public Vector3 ContactPoint;
            public Vector2 ContactImage;
            public float MinHeight;
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
            public Vector3 PendingPoint;
            public Vector2 PendingImage;
            public bool Matched;
        }

        private struct RecentHit
        {
            public Vector3 Point;
            public float Time;
        }

        private readonly DetectionSettings settings;
        private readonly List<WallHit> hits = new List<WallHit>();
        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly List<Track> tracks = new List<Track>();
        private readonly List<RecentHit> recentHits = new List<RecentHit>();

        private int width;
        private int height;
        private ushort[] baseline;
        private float[] rayX;
        private float[] rayY;
        private float[] rayDotNormal;
        private float[] depthThreshold;
        private bool[] insideMask;
        private bool[] blindMask;
        private WallMapping mapping;
        private DepthIntrinsics intrinsics;
        private float planeOffset;

        private bool[] changed;
        private float[] heights;
        private byte[] classes;
        private int[] pixelList;
        private int[] queue;
        private int frameIndex;
        private int blindCount;
        private int insideCount;

        /// <summary>
        /// 検知器を作ります。
        /// </summary>
        /// <param name="settings">しきい値（null なら初期値）</param>
        public HitDetector(DetectionSettings settings = null)
        {
            this.settings = settings ?? new DetectionSettings();
        }

        /// <summary>しきい値。</summary>
        public DetectionSettings Settings
        {
            get { return settings; }
        }

        /// <summary>校正を取り込んで、検知できる状態か。</summary>
        public bool IsConfigured
        {
            get { return mapping != null; }
        }

        /// <summary>深度画像と壁座標の対応付け。</summary>
        public WallMapping Mapping
        {
            get { return mapping; }
        }

        /// <summary>画素ごとの見分けの結果（<see cref="ClassNone"/> など）。プレビューの色分けに使います。</summary>
        public byte[] Classes
        {
            get { return classes; }
        }

        /// <summary>四隅の内側の表。</summary>
        public bool[] InsideMask
        {
            get { return insideMask; }
        }

        /// <summary>四隅の内側で、深度が取れない・不安定な画素（ボールを取りにくい所）。</summary>
        public bool[] BlindMask
        {
            get { return blindMask; }
        }

        /// <summary>四隅の内側のうち、ボールを取りにくい画素の割合（0〜1）。</summary>
        public float BlindRatio
        {
            get { return insideCount == 0 ? 0f : blindCount / (float)insideCount; }
        }

        /// <summary>直前のフレームで見つけたボールらしい塊の数。</summary>
        public int LastBallCount { get; private set; }

        /// <summary>直前のフレームで見つけた体・手の塊の数。</summary>
        public int LastBodyCount { get; private set; }

        /// <summary>
        /// 校正を取り込みます。
        /// </summary>
        /// <param name="data">校正</param>
        /// <param name="error">取り込めなかった理由（画面に出す文）</param>
        /// <returns>検知できる状態になったとき true</returns>
        public bool Configure(CalibrationData data, out string error)
        {
            mapping = null;
            ResetTracking();
            error = string.Empty;
            if (data == null || !data.IsComplete)
            {
                error = "基準と四隅がまだそろっていません。";
                return false;
            }

            width = data.Width;
            height = data.Height;
            int count = width * height;
            baseline = data.Baseline;
            insideMask = QuadMask.Build(data.Corners, width, height);
            WallMapping created = WallMapping.Create(data, insideMask, out error);
            if (created == null)
            {
                return false;
            }

            settings.BallDiameterMm = data.BallDiameterMm;
            intrinsics = created.Intrinsics;
            planeOffset = created.Plane.Offset;
            Vector3 normal = created.Plane.Normal;
            rayX = new float[count];
            rayY = new float[count];
            rayDotNormal = new float[count];
            depthThreshold = new float[count];
            blindMask = new bool[count];
            changed = new bool[count];
            heights = new float[count];
            classes = new byte[count];
            pixelList = new int[count];
            queue = new int[count];
            blindCount = 0;
            insideCount = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    Vector3 ray = intrinsics.Ray(x, y);
                    rayX[i] = ray.x;
                    rayY[i] = ray.y;
                    rayDotNormal[i] = Vector3.Dot(normal, ray);
                    ushort noise = data.Noise != null ? data.Noise[i] : (ushort)0;
                    bool unstable = noise == CalibrationData.UnstableNoise;
                    depthThreshold[i] = unstable ? 40f : Mathf.Min(80f, Mathf.Max(settings.NoiseFloorMm, noise * settings.NoiseMultiplier));
                    bool blind = insideMask[i] && (baseline[i] == 0 || unstable);
                    blindMask[i] = blind;
                    insideCount += insideMask[i] ? 1 : 0;
                    blindCount += blind ? 1 : 0;
                }
            }

            mapping = created;
            return true;
        }

        /// <summary>
        /// 追いかけている物と、最近の当たりを忘れます（シーンを替えたときなど）。
        /// </summary>
        public void ResetTracking()
        {
            tracks.Clear();
            recentHits.Clear();
            frameIndex = 0;
        }

        /// <summary>
        /// ボールの直径を変えます。
        /// </summary>
        /// <param name="diameterMm">直径（mm）</param>
        public void SetBallDiameter(float diameterMm)
        {
            settings.BallDiameterMm = Mathf.Clamp(diameterMm, CalibrationData.MinBallDiameterMm, CalibrationData.MaxBallDiameterMm);
        }

        /// <summary>
        /// 1フレーム分の深度を調べ、このフレームで見つけた当たりを返します。
        /// </summary>
        /// <param name="depth">深度（幅×高さ、mm、0 は取れていない）</param>
        /// <param name="timeSeconds">時刻（秒）</param>
        /// <returns>当たりの一覧（次の呼び出しで上書きされます）</returns>
        public List<WallHit> Process(ushort[] depth, float timeSeconds)
        {
            hits.Clear();
            if (!IsConfigured || depth == null || depth.Length != width * height)
            {
                return hits;
            }

            frameIndex++;
            ClassifyPixels(depth);
            FindBlobs(depth);
            UpdateTracks(timeSeconds);
            return hits;
        }

        private void ClassifyPixels(ushort[] depth)
        {
            float tolerance = settings.WallToleranceMm;
            for (int i = 0; i < depth.Length; i++)
            {
                changed[i] = false;
                classes[i] = ClassNone;
                ushort d = depth[i];
                if (d == 0)
                {
                    continue;
                }

                ushort b = baseline[i];
                if (b != 0 && b - d < depthThreshold[i])
                {
                    // 基準とほぼ同じ（または奥）：変化なし
                    continue;
                }

                // 壁の面から手前へ出ている距離（mm）
                float h = d * rayDotNormal[i] - planeOffset;
                if (h < tolerance)
                {
                    continue;
                }

                changed[i] = true;
                heights[i] = h;
            }
        }

        private void FindBlobs(ushort[] depth)
        {
            candidates.Clear();
            LastBallCount = 0;
            LastBodyCount = 0;
            for (int start = 0; start < changed.Length; start++)
            {
                if (!changed[start])
                {
                    continue;
                }

                int count = CollectBlob(start, depth);
                float area = 0f;
                float minHeight = float.MaxValue;
                Vector3 sum = Vector3.zero;
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                for (int k = 0; k < count; k++)
                {
                    int i = pixelList[k];
                    float d = depth[i];
                    float size = intrinsics.PixelSizeMm(d);
                    area += size * size;
                    Vector3 p = new Vector3(rayX[i] * d, rayY[i] * d, d);
                    sum += p;
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                    if (heights[i] < minHeight)
                    {
                        minHeight = heights[i];
                    }
                }

                float extent = (max - min).magnitude;
                byte kind;
                if (count < settings.MinPixels)
                {
                    kind = ClassNoise;
                }
                else if (area > settings.MaxBallAreaMm2 || extent > settings.MaxBallExtentMm)
                {
                    kind = ClassBody;
                    LastBodyCount++;
                }
                else if (area < settings.MinBallAreaMm2)
                {
                    kind = ClassNoise;
                }
                else
                {
                    kind = ClassBall;
                    LastBallCount++;
                }

                for (int k = 0; k < count; k++)
                {
                    classes[pixelList[k]] = kind;
                }

                if (kind != ClassBall)
                {
                    continue;
                }

                // ボールの中心を求める：見えている面（カメラ側の半球）の重心は、中心よりカメラ側へ半径の半分ずれているので、
                // 視線の向きに半径の半分だけ奥へ戻す。触れた位置は、この中心を壁の面に下ろした所（WallMapping.PointToWall）
                Vector3 centroid = sum / count;
                float centroidLength = centroid.magnitude;
                Vector3 center = centroidLength > 1f ? centroid + centroid / centroidLength * (settings.BallDiameterMm * 0.25f) : centroid;
                Vector2 image = intrinsics.Project(center);

                Candidate candidate = new Candidate();
                candidate.Centroid = centroid;
                candidate.MinHeight = minHeight;
                candidate.ContactPoint = center;
                candidate.ContactImage = new Vector2(image.x / width, image.y / height);
                candidates.Add(candidate);
            }
        }

        // start からつながっている「変化した画素」を集め、changed を消していく（集めた数を返す）
        private int CollectBlob(int start, ushort[] depth)
        {
            int head = 0;
            int tail = 0;
            int count = 0;
            queue[tail++] = start;
            changed[start] = false;
            while (head < tail)
            {
                int i = queue[head++];
                pixelList[count++] = i;
                int x = i % width;
                int y = i / width;
                float d = depth[i];
                float jump = Mathf.Max(settings.DepthJumpMm, d * 0.05f);
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
                        if (!changed[n] || Mathf.Abs(depth[n] - d) > jump)
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
                Track track = candidate.Matched ? FindTrack(candidate) : null;
                if (track == null)
                {
                    track = new Track();
                    tracks.Add(track);
                    track.Matched = true;
                }

                track.Position = candidate.Centroid;
                track.LastFrame = frameIndex;
                track.Seen++;
                HandleCandidate(track, candidate, timeSeconds);
                track.History.Add(candidate.MinHeight);
                if (track.History.Count > HistoryLength)
                {
                    track.History.RemoveAt(0);
                }
            }

            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                Track track = tracks[i];
                if (frameIndex - track.LastFrame <= settings.TrackLostFrames)
                {
                    continue;
                }

                // 壁で見え始めてすぐ見えなくなった物は、速いボールとして当たりにする
                if (track.Pending && !track.Emitted && track.Seen <= 3)
                {
                    Emit(track.PendingPoint, track.PendingImage, timeSeconds);
                }

                tracks.RemoveAt(i);
            }

            PruneRecentHits(timeSeconds);
        }

        private readonly Dictionary<Candidate, Track> matches = new Dictionary<Candidate, Track>();

        private void MatchCandidates()
        {
            matches.Clear();
            float limit = settings.TrackMatchMm;
            // 近い組から順に結び付ける
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

                        float distance = Vector3.Distance(candidate.Centroid, track.Position);
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

        private Track FindTrack(Candidate candidate)
        {
            Track track;
            return matches.TryGetValue(candidate, out track) ? track : null;
        }

        private void HandleCandidate(Track track, Candidate candidate, float timeSeconds)
        {
            if (track.Emitted)
            {
                // 当たりを出したボール（跳ね返り・転がり）は、二度と数えない
                return;
            }

            float contact = settings.ContactMm;
            bool touching = candidate.MinHeight <= contact;
            if (track.Pending)
            {
                if (candidate.MinHeight >= track.PendingHeight + settings.ApproachMm)
                {
                    // 壁に触れてから離れた：触れた所で当たり
                    Emit(track.PendingPoint, track.PendingImage, timeSeconds);
                    track.Emitted = true;
                }
                else if (touching && candidate.MinHeight < track.PendingHeight)
                {
                    SetPending(track, candidate);
                }
                else if (frameIndex - track.PendingFrame > settings.LeaveWaitFrames)
                {
                    // 壁に付いたまま離れない物（置いた物・転がる物）は当たりにしない
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

            if (track.History.Count > 0 && farthest - candidate.MinHeight >= settings.ApproachMm)
            {
                // 遠くから飛んできて壁に触れた：この瞬間で当たり
                Emit(candidate.ContactPoint, candidate.ContactImage, timeSeconds);
                track.Emitted = true;
                return;
            }

            // 最初から壁の近くに見えた：離れるのを待ってから決める
            track.Pending = true;
            track.PendingFrame = frameIndex;
            SetPending(track, candidate);
        }

        private static void SetPending(Track track, Candidate candidate)
        {
            track.PendingHeight = candidate.MinHeight;
            track.PendingPoint = candidate.ContactPoint;
            track.PendingImage = candidate.ContactImage;
        }

        private void Emit(Vector3 point, Vector2 image, float timeSeconds)
        {
            Vector2 wall = mapping.PointToWall(point);
            const float margin = 0.03f;
            if (wall.x < -margin || wall.x > 1f + margin || wall.y < -margin || wall.y > 1f + margin)
            {
                // 投影範囲の外
                return;
            }

            float radius = settings.BounceRadiusMm;
            foreach (RecentHit recent in recentHits)
            {
                if (timeSeconds - recent.Time < settings.BounceSeconds && Vector3.Distance(recent.Point, point) < radius)
                {
                    // 同じボールの跳ね返り
                    return;
                }
            }

            RecentHit hit;
            hit.Point = point;
            hit.Time = timeSeconds;
            recentHits.Add(hit);
            hits.Add(new WallHit(new Vector2(Mathf.Clamp01(wall.x), Mathf.Clamp01(wall.y)), image, timeSeconds));
        }

        private void PruneRecentHits(float timeSeconds)
        {
            for (int i = recentHits.Count - 1; i >= 0; i--)
            {
                if (timeSeconds - recentHits[i].Time > settings.BounceSeconds)
                {
                    recentHits.RemoveAt(i);
                }
            }
        }
    }
}
