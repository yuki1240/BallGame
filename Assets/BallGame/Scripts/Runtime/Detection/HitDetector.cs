using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 基準深度と現在の深度を比べて、壁に当たったボールを見つけます。
    /// Kinect は壁に沿って見るため、奥行きの差ではなく「四隅の基準から作った壁の面までの垂直距離」で判断します。
    /// </summary>
    public sealed class HitDetector
    {
        private sealed class Track
        {
            public Vector2 Position;
            public int LastFrame;
            public int ConsecutiveFrames;
            public bool Emitted;
        }

        /// <summary>
        /// 1球ぶんの見え方のまとまり。壁に着いた瞬間（壁にいちばん近づいた点）を選ぶために使います。
        /// </summary>
        private sealed class Burst
        {
            public Vector2 Position;
            public int LastFrame;
            public bool Emitted;
            public int UndecidedFrames;
            public bool Decided;
            public bool Direct;
            public bool Dipped;
            public bool FlightSeen;
            public DetectedHit FirstHit;
            public float BestStrength = float.MaxValue;
            public int BestFrame;
            public bool HasBest;
            public DetectedHit BestHit;
        }

        private readonly DetectionSettings settings;
        private readonly DetectionResult result = new DetectionResult();
        private readonly List<Track> tracks = new List<Track>();
        private readonly List<Burst> bursts = new List<Burst>();
        private readonly Queue<int> floodQueue = new Queue<int>();

        private int width;
        private int height;
        private ushort[] baseline;
        private bool[] insideMask;
        private float[] normalDotRay;
        private float[] pixelNoiseDistance;
        private bool[] unstablePixel;
        private bool[] candidate;
        private bool[] weakCandidate;
        private float[] strength;
        private float[] sizeWeight;
        private float sizeWeightFactor;
        private bool[] filtered;
        private bool[] visited;
        private bool[] ballMask;
        private bool[] blindMask;
        private int blindCount;
        private int insideCount;
        private WallPlane plane;
        private bool isConfigured;
        private int frameIndex;
        private float lastPersonTime = float.NegativeInfinity;

        /// <summary>
        /// 検知器を作ります。
        /// </summary>
        /// <param name="settings">しきい値</param>
        public HitDetector(DetectionSettings settings)
        {
            this.settings = settings ?? new DetectionSettings();
        }

        /// <summary>しきい値。</summary>
        public DetectionSettings Settings
        {
            get { return settings; }
        }

        /// <summary>校正を取り込んで検知できる状態か。</summary>
        public bool IsConfigured
        {
            get { return isConfigured; }
        }

        /// <summary>壁の面。</summary>
        public WallPlane Plane
        {
            get { return plane; }
        }

        /// <summary>幅。</summary>
        public int Width
        {
            get { return width; }
        }

        /// <summary>高さ。</summary>
        public int Height
        {
            get { return height; }
        }

        /// <summary>
        /// 校正データ（四隅と基準深度）を取り込みます。
        /// </summary>
        /// <param name="data">校正データ</param>
        /// <returns>検知できる状態になったとき true</returns>
        public bool Configure(CalibrationData data)
        {
            isConfigured = false;
            ResetTracking();
            if (data == null || !data.IsComplete)
            {
                return false;
            }

            settings.ApplyCalibration(data);
            AllocateBuffers(data.Width, data.Height);
            baseline = data.Baseline;
            DepthIntrinsics intrinsics = DepthIntrinsics.ForResolution(width, height);
            plane = WallPlaneBuilder.Build(baseline, width, height, data.Corners, intrinsics);
            if (!plane.IsValid)
            {
                int valid = WallPlaneBuilder.CountValidInside(baseline, width, height, data.Corners);
                Debug.LogWarning("四隅の内側に有効な基準が少ないため、壁の面を作れませんでした（有効な画素 " + valid + "）。プレビューで壁が灰色に写っている所を四隅で囲み、基準を録り直してください。");
                return false;
            }

            insideMask = QuadMask.Build(data.Corners, width, height);
            normalDotRay = LocalWallNormals.Build(baseline, width, height, intrinsics, plane);
            sizeWeightFactor = ComputeSizeWeightFactor(intrinsics);
            BuildNoiseTables(data.BaselineNoise);
            BuildBlindMask();
            isConfigured = true;
            return true;
        }

        /// <summary>
        /// 追跡中の物体と人の待ち時間を消します。
        /// </summary>
        public void ResetTracking()
        {
            tracks.Clear();
            bursts.Clear();
            frameIndex = 0;
            lastPersonTime = float.NegativeInfinity;
        }

        /// <summary>
        /// 1フレーム分の深度を調べます。
        /// </summary>
        /// <param name="depth">現在の深度（幅×高さ、mm、0 は無効）</param>
        /// <param name="timeSeconds">現在の時刻（秒）</param>
        /// <returns>検知結果（次の呼び出しで上書きされます）</returns>
        public DetectionResult Process(ushort[] depth, float timeSeconds)
        {
            result.Clear();
            frameIndex++;
            if (!isConfigured || depth == null || depth.Length != width * height)
            {
                return result;
            }

            ClassifyPixels(depth);
            RemoveIsolatedPixels();
            ExtractBlobs();
            RecordLargestBlob();
            result.CandidateMask = visited;
            result.PersonReason = GetPersonReason();
            ClearBallMarks();
            if (IsPersonFrame())
            {
                bursts.Clear();
                HandlePersonFrame(timeSeconds);
            }
            else if (timeSeconds - lastPersonTime < settings.PersonCooldownSeconds)
            {
                // 人が去ったあとの待ち時間：見えた物体は覚えるだけで出さない
                result.InCooldown = true;
                bursts.Clear();
                UpdateTracks(false);
            }
            else
            {
                UpdateTracks(true);
            }

            FinishOldBursts();
            MarkHitPixels();
            ForgetOldTracks();
            return result;
        }

        /// <summary>
        /// 画素の深度から壁の面までの距離（基準との差、mm）を返します。
        /// </summary>
        /// <param name="index">画素の番号</param>
        /// <param name="depthMm">深度（mm）</param>
        /// <returns>基準より壁から離れた距離（mm、カメラ側が正）</returns>
        public float GetDistanceFromWall(int index, ushort depthMm)
        {
            // 場所ごとの壁の向きを使い、基準からどれだけ壁の手前へ出たかを測る
            return normalDotRay[index] * (depthMm - baseline[index]);
        }

        /// <summary>
        /// 画素が四隅の内側かどうかを返します。
        /// </summary>
        /// <param name="index">画素の番号</param>
        /// <returns>内側なら true</returns>
        public bool IsInside(int index)
        {
            return insideMask != null && insideMask[index];
        }

        private void AllocateBuffers(int newWidth, int newHeight)
        {
            width = newWidth;
            height = newHeight;
            int count = width * height;
            pixelNoiseDistance = new float[count];
            unstablePixel = new bool[count];
            candidate = new bool[count];
            weakCandidate = new bool[count];
            strength = new float[count];
            sizeWeight = new float[count];
            filtered = new bool[count];
            visited = new bool[count];
            ballMask = new bool[count];
            blindMask = new bool[count];
        }

        /// <summary>
        /// 基準の深度（mm、0 は取れていない）を返します。診断の表に使います。
        /// </summary>
        /// <param name="index">画素の番号</param>
        /// <returns>深度（mm）</returns>
        public ushort GetBaselineDepth(int index)
        {
            return baseline[index];
        }

        /// <summary>
        /// 画素の壁の向きと視線の内積の大きさ（0〜1）を返します。小さいほど壁をかすめて見ている所です。
        /// </summary>
        /// <param name="index">画素の番号</param>
        /// <returns>内積の大きさ</returns>
        public float GetNormalDot(int index)
        {
            return Mathf.Abs(normalDotRay[index]);
        }

        /// <summary>
        /// ボールを検知できない画素（四隅の内側で、基準の深度が取れていない、または基準を録る間に揺れた画素）。
        /// 校正画面のプレビューで紫に出して、深度が取れていない所を確かめるのに使います。
        /// </summary>
        public bool[] BlindMask
        {
            get { return blindMask; }
        }

        /// <summary>
        /// 四隅の内側のうち、ボールを検知できない画素の割合（0〜1）。
        /// </summary>
        public float BlindRatio
        {
            get { return insideCount == 0 ? 0f : blindCount / (float)insideCount; }
        }

        private void BuildBlindMask()
        {
            blindCount = 0;
            insideCount = 0;
            for (int i = 0; i < blindMask.Length; i++)
            {
                bool inside = insideMask[i];
                bool blind = inside && (baseline[i] == 0 || unstablePixel[i]);
                blindMask[i] = blind;
                insideCount += inside ? 1 : 0;
                blindCount += blind ? 1 : 0;
            }
        }

        private void BuildNoiseTables(ushort[] noise)
        {
            for (int i = 0; i < pixelNoiseDistance.Length; i++)
            {
                bool hasNoise = noise != null;
                unstablePixel[i] = hasNoise && noise[i] == BaselineRecorder.UnstableNoise;
                pixelNoiseDistance[i] = hasNoise && !unstablePixel[i] ? Mathf.Abs(normalDotRay[i]) * noise[i] : 0f;
            }
        }

        /// <summary>
        /// 画素ごとのしきい値（mm）を返します。ノイズ床と、その画素の揺れ幅×倍率の大きい方です。
        /// </summary>
        /// <param name="index">画素の番号</param>
        /// <returns>しきい値（mm）</returns>
        public float GetNoiseThreshold(int index)
        {
            float byNoise = Mathf.Min(pixelNoiseDistance[index] * settings.NoiseSigmaMultiplier, settings.NoiseFloorMm * settings.MaxNoiseFloorScale);
            return Mathf.Max(settings.NoiseFloorMm, byNoise);
        }

        /// <summary>
        /// 深度 depthMm の所の1画素の面積が、基準の深度（2m）の所の1画素の何倍かを返します。
        /// </summary>
        /// <param name="depthMm">深度（mm）</param>
        /// <returns>面積の倍率</returns>
        public float GetSizeWeight(float depthMm)
        {
            return depthMm * depthMm * sizeWeightFactor;
        }

        private float ComputeSizeWeightFactor(DepthIntrinsics intrinsics)
        {
            // 1画素の実際の長さは 深度÷焦点距離。基準は Kinect（512×424）で 2m の所の1画素
            float referencePixelMm = settings.ReferenceDepthMm / DepthIntrinsics.KinectFocalLength512;
            return 1f / (intrinsics.FocalX * intrinsics.FocalX * referencePixelMm * referencePixelMm);
        }

        private void ClassifyPixels(ushort[] depth)
        {
            int changed = 0;
            float changedArea = 0f;
            for (int i = 0; i < candidate.Length; i++)
            {
                candidate[i] = false;
                weakCandidate[i] = false;
                strength[i] = 0f;
                if (!insideMask[i] || baseline[i] == 0)
                {
                    continue;
                }

                ushort current = depth[i];
                sizeWeight[i] = GetSizeWeight(current != 0 ? current : baseline[i]);
                if (current == 0)
                {
                    // 基準があった画素が 0 になった：赤外線が返らない物体として候補にする
                    // （基準を録る間に 0 になったことがある不安定な画素は、揺れとみなして使わない）
                    if (!unstablePixel[i])
                    {
                        MarkCandidate(i, settings.ZeroDepthStrength, true);
                        changed++;
                        changedArea += sizeWeight[i];
                    }

                    continue;
                }

                float distance = GetDistanceFromWall(i, current);
                float pixelStrength = distance / GetNoiseThreshold(i);
                bool withinRange = distance <= settings.MaxDistanceMm;
                if (pixelStrength >= 1f)
                {
                    changed++;
                    changedArea += sizeWeight[i];
                }

                if (withinRange && pixelStrength >= settings.WeakRatio)
                {
                    MarkCandidate(i, pixelStrength, pixelStrength >= 1f);
                }
            }

            result.ChangedPixelCount = changed;
            result.ChangedArea = changedArea;
        }

        private void MarkCandidate(int index, float pixelStrength, bool strong)
        {
            strength[index] = pixelStrength;
            weakCandidate[index] = true;
            candidate[index] = strong;
        }

        private void RemoveIsolatedPixels()
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    filtered[index] = candidate[index] && HasCandidateNeighbor(x, y);
                }
            }
        }

        private bool HasCandidateNeighbor(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx != 0 || dy != 0) && IsCandidate(x + dx, y + dy))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsCandidate(int x, int y)
        {
            return x >= 0 && y >= 0 && x < width && y < height && candidate[y * width + x];
        }

        private void ExtractBlobs()
        {
            System.Array.Clear(visited, 0, visited.Length);
            for (int i = 0; i < filtered.Length; i++)
            {
                if (filtered[i] && !visited[i])
                {
                    result.Blobs.Add(FloodFill(i));
                }
            }
        }

        private DepthBlob FloodFill(int start)
        {
            DepthBlob blob = new DepthBlob { MinX = int.MaxValue, MinY = int.MaxValue, MaxX = int.MinValue, MaxY = int.MinValue };
            long sumX = 0;
            long sumY = 0;
            float sumWeight = 0f;
            floodQueue.Clear();
            floodQueue.Enqueue(start);
            visited[start] = true;
            while (floodQueue.Count > 0)
            {
                int index = floodQueue.Dequeue();
                int x = index % width;
                int y = index / width;
                AddPixelToBlob(ref blob, x, y);
                sumWeight += sizeWeight[index];
                blob.PeakStrength = Mathf.Max(blob.PeakStrength, strength[index]);
                sumX += x;
                sumY += y;
                EnqueueNeighbor(x + 1, y);
                EnqueueNeighbor(x - 1, y);
                EnqueueNeighbor(x, y + 1);
                EnqueueNeighbor(x, y - 1);
            }

            blob.CentroidX = (float)sumX / blob.Area;
            blob.CentroidY = (float)sumY / blob.Area;
            blob.NormalizedArea = sumWeight;
            blob.LinearScale = Mathf.Sqrt(sumWeight / blob.Area);
            return blob;
        }

        private static void AddPixelToBlob(ref DepthBlob blob, int x, int y)
        {
            blob.Area++;
            blob.MinX = Mathf.Min(blob.MinX, x);
            blob.MinY = Mathf.Min(blob.MinY, y);
            blob.MaxX = Mathf.Max(blob.MaxX, x);
            blob.MaxY = Mathf.Max(blob.MaxY, y);
        }

        private void EnqueueNeighbor(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            int index = y * width + x;
            if ((filtered[index] || weakCandidate[index]) && !visited[index])
            {
                visited[index] = true;
                floodQueue.Enqueue(index);
            }
        }

        private void RecordLargestBlob()
        {
            result.HasLargestBlob = false;
            foreach (DepthBlob blob in result.Blobs)
            {
                if (!result.HasLargestBlob || blob.NormalizedArea > result.LargestBlob.NormalizedArea)
                {
                    result.LargestBlob = blob;
                    result.HasLargestBlob = true;
                }
            }

            result.LargestBlobRejectReason = result.HasLargestBlob ? GetRejectReason(result.LargestBlob) : string.Empty;
        }

        // プレビューで赤く出すため、当たりを出した位置に印を付ける（飛んでいる間や落ちていく間の軌跡には付けない）
        private void ClearBallMarks()
        {
            System.Array.Clear(ballMask, 0, ballMask.Length);
            result.BallMask = ballMask;
            result.HasBall = false;
        }

        private void MarkHitPixels()
        {
            foreach (DetectedHit hit in result.Hits)
            {
                result.BallImageUvs.Add(hit.ImageUv);
                float radius = Mathf.Max(2f, Mathf.Sqrt(hit.Area / Mathf.PI));
                result.HasBall |= MarkDisc(hit.Pixel, radius);
            }
        }

        private bool MarkDisc(Vector2 center, float radius)
        {
            bool marked = false;
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(center.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(center.y + radius));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - center.x;
                    float dy = y + 0.5f - center.y;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        ballMask[y * width + x] = true;
                        marked = true;
                    }
                }
            }

            return marked;
        }

        private bool IsPersonFrame()
        {
            return result.PersonReason.Length > 0;
        }

        // 人サイズのフレームと判断する理由を返す（そうでなければ空）。診断の記録に出すため、数字も入れる
        private string GetPersonReason()
        {
            if (result.ChangedArea > settings.PersonPixelTotal)
            {
                return "変化の合計が大きい（" + result.ChangedArea.ToString("F0") + " > " + settings.PersonPixelTotal + "）";
            }

            foreach (DepthBlob blob in result.Blobs)
            {
                if (blob.NormalizedWidth > settings.PersonMaxExtent || blob.NormalizedHeight > settings.PersonMaxExtent)
                {
                    return "塊が大きい（幅 " + blob.NormalizedWidth.ToString("F0") + "・高さ " + blob.NormalizedHeight.ToString("F0") + " > " + settings.PersonMaxExtent + "）";
                }
            }

            return string.Empty;
        }

        private void HandlePersonFrame(float timeSeconds)
        {
            result.PersonDetected = true;
            lastPersonTime = timeSeconds;
            // 人がいる間に見えた物体は、去ったあとに残っていても出さない
            UpdateTracks(false);
        }

        private void UpdateTracks(bool allowEmit)
        {
            foreach (DepthBlob blob in result.Blobs)
            {
                if (!IsBallLike(blob))
                {
                    continue;
                }

                Track track = MatchOrCreateTrack(blob);
                if (!allowEmit)
                {
                    track.Emitted = true;
                    continue;
                }

                HandleBlob(track, blob);
            }
        }

        private void HandleBlob(Track track, DepthBlob blob)
        {
            bool isNew;
            Burst burst = FindOrCreateBurst(blob, out isNew);
            if (burst.Emitted)
            {
                // 当たりを出したあとに続けて見える同じボール（壁を伝って落ちる軌跡など）は出さない
                track.Emitted = true;
                return;
            }

            float peak = blob.PeakStrength;
            bool valid = Mathf.Abs(peak - settings.ZeroDepthStrength) > 0.01f;
            if (burst.UndecidedFrames == 0 && !burst.Decided && isNew)
            {
                burst.FirstHit = CreateHit(blob);
            }

            if (!burst.Decided && valid)
            {
                // 深度 0 の画素から見え始めたものは、遠くから飛んできたボールとみなす
                burst.Decided = true;
                burst.Direct = burst.UndecidedFrames == 0 && peak < GetFlightStrength();
            }
            else if (!burst.Decided && ++burst.UndecidedFrames >= settings.ZeroOnlyFrames)
            {
                // 深度 0 のままずっと留まる物体は、見え始めの位置で当たりにする
                burst.Decided = true;
                burst.Direct = true;
                burst.Emitted = true;
                result.Hits.Add(burst.FirstHit);
                track.Emitted = true;
                return;
            }

            if (burst.Decided && burst.Direct)
            {
                // 最初から壁の近くに現れたもの（クリックなど）は、すぐ出す
                if (TryEmit(track, blob))
                {
                    burst.Emitted = true;
                }

                return;
            }

            // 遠くから飛んできたボールは、壁にいちばん近づいた点（当たった瞬間）まで待つ
            if (!valid || !burst.Decided)
            {
                return;
            }

            if (peak >= GetFlightStrength() && !burst.FlightSeen)
            {
                // 遠くから来たことが分かった。見え始めの不安定な値は捨てて、ここから壁に近づく点を探す
                burst.FlightSeen = true;
                burst.BestStrength = float.MaxValue;
                burst.HasBest = false;
                burst.Dipped = false;
            }

            UpdateBestContact(burst, blob);
            if (ShouldEmitContact(burst, peak))
            {
                EmitBurst(burst);
            }
        }

        private void UpdateBestContact(Burst burst, DepthBlob blob)
        {
            if (blob.PeakStrength >= burst.BestStrength)
            {
                return;
            }

            burst.BestStrength = blob.PeakStrength;
            burst.BestFrame = frameIndex;
            burst.HasBest = true;
            burst.BestHit = CreateHit(blob);
            burst.Dipped = burst.FlightSeen && blob.PeakStrength < GetFlightStrength();
        }

        private bool ShouldEmitContact(Burst burst, float currentPeak)
        {
            if (!burst.Dipped && burst.FlightSeen)
            {
                return false;
            }

            bool leaving = currentPeak >= burst.BestStrength + settings.ContactRise;
            bool resting = frameIndex - burst.BestFrame >= settings.ContactStuckFrames;
            return leaving || resting;
        }

        private void EmitBurst(Burst burst)
        {
            burst.Emitted = true;
            result.Hits.Add(burst.BestHit);
        }

        /// <summary>
        /// 飛んでいるボールとみなす強さ（これ未満まで壁に近づいたら、壁に着いたとみなす）を返します。
        /// </summary>
        /// <returns>強さ</returns>
        private float GetFlightStrength()
        {
            float cap = settings.MaxDistanceMm / Mathf.Max(1f, settings.NoiseFloorMm);
            return cap * settings.FlightRatio;
        }

        private Burst FindOrCreateBurst(DepthBlob blob, out bool isNew)
        {
            Vector2 position = new Vector2(blob.CentroidX, blob.CentroidY);
            float radiusPixels = settings.BurstRadius / Mathf.Max(0.05f, blob.LinearScale);
            foreach (Burst burst in bursts)
            {
                if (frameIndex - burst.LastFrame > settings.BurstGapFrames
                    || Vector2.Distance(burst.Position, position) > radiusPixels)
                {
                    continue;
                }

                burst.Position = position;
                burst.LastFrame = frameIndex;
                isNew = false;
                return burst;
            }

            Burst created = new Burst { Position = position, LastFrame = frameIndex };
            bursts.Add(created);
            isNew = true;
            return created;
        }

        private void FinishOldBursts()
        {
            foreach (Burst burst in bursts)
            {
                bool ended = frameIndex - burst.LastFrame > settings.BurstGapFrames;
                if (ended && !burst.Emitted && burst.Decided && !burst.Direct && burst.HasBest && (burst.Dipped || !burst.FlightSeen))
                {
                    EmitBurst(burst);
                }
            }

            bursts.RemoveAll(burst => frameIndex - burst.LastFrame > settings.BurstGapFrames);
        }

        /// <summary>
        /// 塊を採用しない理由を返します。採用できるときは空文字です。
        /// </summary>
        /// <param name="blob">塊</param>
        /// <returns>理由（採用できるときは空文字）</returns>
        public string GetRejectReason(DepthBlob blob)
        {
            if (blob.NormalizedArea < settings.MinArea)
            {
                return "小さすぎる";
            }

            if (blob.PeakStrength < settings.MinPeakStrength)
            {
                return "壁からの離れ方が弱い";
            }

            if (blob.FillRatio < settings.MinFillRatio)
            {
                return "まばら";
            }

            return string.Empty;
        }

        /// <summary>
        /// 塊がボールらしいか（面積・一番強い点・詰まり具合）を返します。
        /// </summary>
        /// <param name="blob">塊</param>
        /// <returns>ボールらしければ true</returns>
        public bool IsBallLike(DepthBlob blob)
        {
            return GetRejectReason(blob).Length == 0;
        }

        private Track MatchOrCreateTrack(DepthBlob blob)
        {
            Vector2 position = new Vector2(blob.CentroidX, blob.CentroidY);
            float radiusPixels = settings.TrackMatchRadius / Mathf.Max(0.05f, blob.LinearScale);
            Track track = FindNearestTrack(position, radiusPixels);
            if (track == null)
            {
                track = new Track { Position = position, LastFrame = frameIndex, ConsecutiveFrames = 1 };
                tracks.Add(track);
                return track;
            }

            track.ConsecutiveFrames = track.LastFrame == frameIndex - 1 ? track.ConsecutiveFrames + 1 : 1;
            track.LastFrame = frameIndex;
            track.Position = position;
            return track;
        }

        private Track FindNearestTrack(Vector2 position, float radiusPixels)
        {
            Track best = null;
            float bestDistance = radiusPixels;
            foreach (Track track in tracks)
            {
                if (track.LastFrame == frameIndex)
                {
                    continue;
                }

                float distance = Vector2.Distance(track.Position, position);
                if (distance <= bestDistance)
                {
                    best = track;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private bool TryEmit(Track track, DepthBlob blob)
        {
            if (track.Emitted)
            {
                // 同じ場所に留まった物体は出し続けない
                return false;
            }

            // 速いボールは壁の近くに1〜2フレームしかいないため、はっきり壁から離れた塊は小さくても1フレームで採用する
            bool accepted = blob.NormalizedArea >= settings.InstantArea
                || blob.PeakStrength >= settings.InstantPeakStrength
                || track.ConsecutiveFrames >= settings.ConfirmFrames;
            if (!accepted)
            {
                return false;
            }

            track.Emitted = true;
            result.Hits.Add(CreateHit(blob));
            return true;
        }

        private DetectedHit CreateHit(DepthBlob blob)
        {
            Vector2 pixel = new Vector2(blob.CentroidX, blob.CentroidY);
            Vector2 uv = new Vector2((blob.CentroidX + 0.5f) / width, (blob.CentroidY + 0.5f) / height);
            return new DetectedHit { Pixel = pixel, ImageUv = uv, Area = blob.Area };
        }

        private void ForgetOldTracks()
        {
            tracks.RemoveAll(track => frameIndex - track.LastFrame > settings.TrackForgetFrames);
        }
    }
}
