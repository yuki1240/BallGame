using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// 四隅で囲んだ台形の中の深度を、深度を持ったまま「壁の面の上の長方形（壁グリッド）」に直したものです。
    /// <para>1マスは壁の上の 1cm × 1cm です。投影面が 2m × 1m なら、200 × 100 マスになります。
    /// グリッドの左下のマスが壁座標の (0, 0)、右上のマスが (1, 1) の所なので、そのまま画面（1920×1080）いっぱいに引き伸ばせば、実物の壁と重なります。</para>
    /// <para>作り方：四隅の内側の画素を1つずつ、深度から3Dの位置にして、壁の面の上のどのマスかを求めて置きます（<see cref="WallMapping.PointToWall"/>）。
    /// 画素の位置ではなく深度で横の位置を決めるので、Kinect から遠い側（深度画像では詰まって写る側）も正しい位置に並びます。</para>
    /// <para>マスには「壁から手前へ何 mm 出ているか」と、元の画素の深度・番号を入れます。
    /// 1つのマスに画素が2つ以上入ったときは、壁からいちばん離れた（手前にある）ものを残します。</para>
    /// <para>遠い側は1画素が数 cm ぶんになるので、値の入らないマスができます。横に <see cref="MaxFillCells"/> マスまでの穴は、両どなりのマスから埋めます。</para>
    /// </summary>
    public sealed class RectifiedDepthGrid
    {
        /// <summary>1マスの大きさ（mm）。</summary>
        public const float CellMm = 10f;

        /// <summary>横の穴を埋める最大のマス数。</summary>
        public const int MaxFillCells = 10;

        /// <summary>グリッドの幅・高さの最小（マス）。</summary>
        public const int MinCells = 16;

        /// <summary>グリッドの幅・高さの最大（マス）。</summary>
        public const int MaxCells = 1024;

        private readonly WallMapping mapping;
        private readonly int imageWidth;
        private readonly int imageHeight;
        private readonly int[] insidePixels;
        private readonly Vector3[] rays;
        private readonly float[] heights;
        private readonly ushort[] depths;
        private readonly int[] sources;

        /// <summary>
        /// 壁グリッドを作ります（校正が変わったら作り直してください）。
        /// </summary>
        /// <param name="mapping">深度と壁座標の対応付け</param>
        /// <param name="insideMask">四隅の内側の表（深度画像の幅×高さ）</param>
        /// <param name="imageWidth">深度画像の幅</param>
        /// <param name="imageHeight">深度画像の高さ</param>
        public RectifiedDepthGrid(WallMapping mapping, bool[] insideMask, int imageWidth, int imageHeight)
        {
            this.mapping = mapping;
            this.imageWidth = imageWidth;
            this.imageHeight = imageHeight;
            Width = Mathf.Clamp(Mathf.RoundToInt(mapping.WidthMm / CellMm), MinCells, MaxCells);
            Height = Mathf.Clamp(Mathf.RoundToInt(mapping.HeightMm / CellMm), MinCells, MaxCells);
            int count = Width * Height;
            heights = new float[count];
            depths = new ushort[count];
            sources = new int[count];

            int inside = 0;
            for (int i = 0; i < insideMask.Length; i++)
            {
                inside += insideMask[i] ? 1 : 0;
            }

            insidePixels = new int[inside];
            rays = new Vector3[inside];
            int k = 0;
            for (int i = 0; i < insideMask.Length; i++)
            {
                if (!insideMask[i])
                {
                    continue;
                }

                insidePixels[k] = i;
                rays[k] = mapping.Intrinsics.Ray(i % imageWidth, i / imageWidth);
                k++;
            }

            Clear();
        }

        /// <summary>グリッドの幅（マス）。</summary>
        public int Width { get; private set; }

        /// <summary>グリッドの高さ（マス）。</summary>
        public int Height { get; private set; }

        /// <summary>この対応付けから作ったグリッドか。</summary>
        /// <param name="other">対応付け</param>
        /// <returns>同じなら true</returns>
        public bool IsFor(WallMapping other)
        {
            return ReferenceEquals(mapping, other);
        }

        /// <summary>
        /// マスに値が入っているか。
        /// </summary>
        /// <param name="x">横（左が 0）</param>
        /// <param name="y">縦（下が 0）</param>
        /// <returns>入っていれば true</returns>
        public bool HasValue(int x, int y)
        {
            return sources[y * Width + x] >= 0;
        }

        /// <summary>
        /// マスの「壁から手前へ出ている距離」（mm）。値がないマスは 0。
        /// </summary>
        /// <param name="x">横（左が 0）</param>
        /// <param name="y">縦（下が 0）</param>
        /// <returns>距離（mm）</returns>
        public float GetHeightMm(int x, int y)
        {
            return heights[y * Width + x];
        }

        /// <summary>
        /// マスの深度（mm）。値がないマスは 0。
        /// </summary>
        /// <param name="x">横（左が 0）</param>
        /// <param name="y">縦（下が 0）</param>
        /// <returns>深度（mm）</returns>
        public ushort GetDepth(int x, int y)
        {
            return depths[y * Width + x];
        }

        /// <summary>
        /// マスの元になった深度画像の画素の番号。値がないマスは -1。
        /// </summary>
        /// <param name="x">横（左が 0）</param>
        /// <param name="y">縦（下が 0）</param>
        /// <returns>画素の番号</returns>
        public int GetSourcePixel(int x, int y)
        {
            return sources[y * Width + x];
        }

        /// <summary>
        /// 新しい深度で作り直します。
        /// </summary>
        /// <param name="depth">深度（深度画像の幅×高さ、mm、0 は取れていない）</param>
        public void Build(ushort[] depth)
        {
            Clear();
            if (depth == null || depth.Length != imageWidth * imageHeight)
            {
                return;
            }

            WallPlane plane = mapping.Plane;
            for (int k = 0; k < insidePixels.Length; k++)
            {
                int pixel = insidePixels[k];
                ushort d = depth[pixel];
                if (d == 0)
                {
                    // 深度が取れていない画素は置かない
                    continue;
                }

                Vector3 point = rays[k] * d;
                Vector2 wall = mapping.PointToWall(point);
                int cx = Mathf.FloorToInt(wall.x * Width);
                int cy = Mathf.FloorToInt(wall.y * Height);
                if (cx < 0 || cy < 0 || cx >= Width || cy >= Height)
                {
                    continue;
                }

                int cell = cy * Width + cx;
                float h = plane.DistanceTo(point);
                if (sources[cell] < 0 || h > heights[cell])
                {
                    heights[cell] = h;
                    depths[cell] = d;
                    sources[cell] = pixel;
                }
            }

            FillRowGaps();
        }

        private void Clear()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                heights[i] = 0f;
                depths[i] = 0;
                sources[i] = -1;
            }
        }

        // 行ごとに、横の穴（MaxFillCells マスまで）を両どなりの値で埋める
        private void FillRowGaps()
        {
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                int previous = -1;
                for (int x = 0; x < Width; x++)
                {
                    if (sources[row + x] < 0)
                    {
                        continue;
                    }

                    int gap = x - previous - 1;
                    if (previous >= 0 && gap > 0 && gap <= MaxFillCells)
                    {
                        FillGap(row, previous, x);
                    }

                    previous = x;
                }
            }
        }

        private void FillGap(int row, int left, int right)
        {
            int a = row + left;
            int b = row + right;
            for (int x = left + 1; x < right; x++)
            {
                float t = (x - left) / (float)(right - left);
                int cell = row + x;
                int nearer = t < 0.5f ? a : b;
                heights[cell] = Mathf.Lerp(heights[a], heights[b], t);
                depths[cell] = depths[nearer];
                sources[cell] = sources[nearer];
            }
        }
    }
}
