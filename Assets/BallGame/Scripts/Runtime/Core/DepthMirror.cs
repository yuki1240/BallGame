namespace BallGame
{
    /// <summary>
    /// 深度画像の左右反転を行います。上下反転はしません。
    /// </summary>
    public static class DepthMirror
    {
        /// <summary>
        /// 深度画像をその場で左右反転します。
        /// </summary>
        /// <param name="depth">深度（幅×高さ）</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        public static void MirrorX(ushort[] depth, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                MirrorRow(depth, y * width, width);
            }
        }

        private static void MirrorRow(ushort[] depth, int rowStart, int width)
        {
            int left = rowStart;
            int right = rowStart + width - 1;
            while (left < right)
            {
                ushort temp = depth[left];
                depth[left] = depth[right];
                depth[right] = temp;
                left++;
                right--;
            }
        }
    }
}
