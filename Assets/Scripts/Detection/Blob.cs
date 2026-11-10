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
