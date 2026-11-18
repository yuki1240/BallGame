using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// ツリーを画面に収めるための計算です（画面に関係しないので、テストで確かめられます）。
    /// <para>考え方：スターまで含めた「ツリー全体の箱」が、画面の高さの 92%（上下に 4% ずつ余白）と、
    /// 画面の幅の 90%（左右に 5% ずつ余白）の両方に入るように、カメラの距離を決めます。
    /// 高さと幅で必要な距離のうち、遠い方（＝小さく映る方）を使うので、画面の比率が変わってもスターがはみ出しません。</para>
    /// <list type="bullet">
    /// <item>16:9 の画面：高さで決まり、画面の高さいっぱいに映る</item>
    /// <item>4:3 や縦長の画面：必要なときだけ幅で決まり、少し小さく映る</item>
    /// </list>
    /// </summary>
    public static class TreeLayout
    {
        /// <summary>上下それぞれの余白（画面の高さに対する割合）。</summary>
        public const float VerticalMargin = 0.04f;

        /// <summary>左右それぞれの余白（画面の幅に対する割合）。</summary>
        public const float HorizontalMargin = 0.05f;

        /// <summary>
        /// ツリー全体の箱の中心から、カメラまでの距離を求めます。
        /// </summary>
        /// <param name="size">ツリー全体の箱の大きさ（スター込み）</param>
        /// <param name="verticalFovDegrees">カメラの縦の画角（度）</param>
        /// <param name="aspect">画面の横÷縦</param>
        /// <param name="limitedByWidth">幅で決まったとき true</param>
        /// <returns>距離</returns>
        public static float ComputeCameraDistance(Vector3 size, float verticalFovDegrees, float aspect, out bool limitedByWidth)
        {
            float tanVertical = Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            float tanHorizontal = tanVertical * Mathf.Max(0.01f, aspect);
            float byHeight = size.y * 0.5f / ((1f - 2f * VerticalMargin) * tanVertical);
            float byWidth = size.x * 0.5f / ((1f - 2f * HorizontalMargin) * tanHorizontal);
            limitedByWidth = byWidth > byHeight;
            // 手前の面が収まるよう、奥行きの半分を足す
            return Mathf.Max(byHeight, byWidth) + size.z * 0.5f;
        }

        /// <summary>
        /// 箱の手前の面で、画面に占める高さの割合を返します（確かめ用）。
        /// </summary>
        /// <param name="size">ツリー全体の箱の大きさ</param>
        /// <param name="distance">箱の中心からカメラまでの距離</param>
        /// <param name="verticalFovDegrees">縦の画角</param>
        /// <returns>高さの割合</returns>
        public static float HeightFraction(Vector3 size, float distance, float verticalFovDegrees)
        {
            float front = distance - size.z * 0.5f;
            float visible = 2f * front * Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            return size.y / visible;
        }

        /// <summary>
        /// 箱の手前の面で、画面に占める幅の割合を返します（確かめ用）。
        /// </summary>
        /// <param name="size">ツリー全体の箱の大きさ</param>
        /// <param name="distance">箱の中心からカメラまでの距離</param>
        /// <param name="verticalFovDegrees">縦の画角</param>
        /// <param name="aspect">画面の横÷縦</param>
        /// <returns>幅の割合</returns>
        public static float WidthFraction(Vector3 size, float distance, float verticalFovDegrees, float aspect)
        {
            float front = distance - size.z * 0.5f;
            float visible = 2f * front * Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad) * aspect;
            return size.x / visible;
        }
    }
}
