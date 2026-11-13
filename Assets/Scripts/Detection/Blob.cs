namespace WallBurst.Detection
{
    /// <summary>
    /// つながった差分画素の塊。
    /// </summary>
    public struct Blob
    {
        /// <summary>画素数。</summary>
        public int Area;

        /// <summary>塊に含まれる画素の残像スコア合計。</summary>
        public int PersistenceSum;

        /// <summary>画素中心 X の合計。</summary>
        public float SumX;

        /// <summary>画素中心 Y の合計。</summary>
        public float SumY;

        /// <summary>含まれる画素の左端。</summary>
        public int MinX;

        /// <summary>含まれる画素の上端。</summary>
        public int MinY;

        /// <summary>含まれる画素の右端。</summary>
        public int MaxX;

        /// <summary>含まれる画素の下端。</summary>
        public int MaxY;

        /// <summary>横の画素数。</summary>
        public int Width
        {
            get { return MaxX - MinX + 1; }
        }

        /// <summary>縦の画素数。</summary>
        public int Height
        {
            get { return MaxY - MinY + 1; }
        }

        /// <summary>重心 X。</summary>
        public float CentroidX
        {
            get { return SumX / Area; }
        }

        /// <summary>重心 Y。</summary>
        public float CentroidY
        {
            get { return SumY / Area; }
        }
    }
}
