using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 深度カメラの内部パラメータ。画素と深度（mm）から3次元の点（mm）を作ります。
    /// 座標は x 右、y 下、z 奥（カメラ座標）です。
    /// </summary>
    public readonly struct DepthIntrinsics
    {
        /// <summary>Kinect v2 の深度（512 画素幅）での焦点距離。</summary>
        public const float KinectFocalLength512 = 365.5f;

        /// <summary>横の焦点距離（画素）。</summary>
        public readonly float FocalX;

        /// <summary>縦の焦点距離（画素）。</summary>
        public readonly float FocalY;

        /// <summary>光軸の横位置（画素）。</summary>
        public readonly float CenterX;

        /// <summary>光軸の縦位置（画素）。</summary>
        public readonly float CenterY;

        /// <summary>
        /// 値を指定して作ります。
        /// </summary>
        /// <param name="focalX">横の焦点距離</param>
        /// <param name="focalY">縦の焦点距離</param>
        /// <param name="centerX">光軸の横位置</param>
        /// <param name="centerY">光軸の縦位置</param>
        public DepthIntrinsics(float focalX, float focalY, float centerX, float centerY)
        {
            FocalX = focalX;
            FocalY = focalY;
            CenterX = centerX;
            CenterY = centerY;
        }

        /// <summary>
        /// 解像度から Kinect v2 相当の内部パラメータを作ります（模擬深度にも同じ比率を使います）。
        /// </summary>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <returns>内部パラメータ</returns>
        public static DepthIntrinsics ForResolution(int width, int height)
        {
            float focal = KinectFocalLength512 * width / 512f;
            return new DepthIntrinsics(focal, focal, width * 0.5f, height * 0.5f);
        }

        /// <summary>
        /// 画素の視線方向（z=1 に正規化）を返します。
        /// </summary>
        /// <param name="pixelX">画素の x</param>
        /// <param name="pixelY">画素の y（上端が 0）</param>
        /// <returns>視線方向</returns>
        public Vector3 GetRay(float pixelX, float pixelY)
        {
            return new Vector3((pixelX - CenterX) / FocalX, (pixelY - CenterY) / FocalY, 1f);
        }

        /// <summary>
        /// 画素と深度から3次元の点を返します。
        /// </summary>
        /// <param name="pixelX">画素の x</param>
        /// <param name="pixelY">画素の y</param>
        /// <param name="depthMm">深度（mm）</param>
        /// <returns>カメラ座標の点（mm）</returns>
        public Vector3 Unproject(float pixelX, float pixelY, float depthMm)
        {
            return GetRay(pixelX, pixelY) * depthMm;
        }
    }

    /// <summary>
    /// 壁の面。四隅の基準深度から作り、点から面までの垂直距離を求めます。
    /// 距離はカメラ側を正にします（壁より手前にある物ほど大きい）。
    /// </summary>
    public readonly struct WallPlane
    {
        /// <summary>単位法線（カメラ側を向く）。</summary>
        public readonly Vector3 Normal;

        /// <summary>面の式 n・p + Offset = 0 の定数項。</summary>
        public readonly float Offset;

        /// <summary>面として使えるか。</summary>
        public readonly bool IsValid;

        private WallPlane(Vector3 normal, float offset, bool isValid)
        {
            Normal = normal;
            Offset = offset;
            IsValid = isValid;
        }

        /// <summary>
        /// 4点（左上・右上・右下・左下の順）から面を作ります。対角線どうしの外積で法線を求めます。
        /// </summary>
        /// <param name="points">壁の上の4点（mm）</param>
        /// <returns>壁の面</returns>
        public static WallPlane FromCornerPoints(IList<Vector3> points)
        {
            if (points == null || points.Count != 4)
            {
                return default;
            }

            Vector3 normal = Vector3.Cross(points[2] - points[0], points[3] - points[1]);
            if (normal.sqrMagnitude < 1e-6f)
            {
                return default;
            }

            normal.Normalize();
            Vector3 centroid = (points[0] + points[1] + points[2] + points[3]) * 0.25f;
            return OrientTowardCamera(normal, -Vector3.Dot(normal, centroid));
        }

        /// <summary>
        /// 法線と定数項から面を作ります。
        /// </summary>
        /// <param name="normal">法線（正規化される）</param>
        /// <param name="offset">定数項</param>
        /// <returns>壁の面</returns>
        public static WallPlane FromNormalAndOffset(Vector3 normal, float offset)
        {
            float length = normal.magnitude;
            if (length < 1e-6f)
            {
                return default;
            }

            return OrientTowardCamera(normal / length, offset / length);
        }

        /// <summary>
        /// 点から面までの垂直距離を返します（カメラ側が正）。
        /// </summary>
        /// <param name="point">点（mm）</param>
        /// <returns>距離（mm）</returns>
        public float DistanceTo(Vector3 point)
        {
            return Vector3.Dot(Normal, point) + Offset;
        }

        private static WallPlane OrientTowardCamera(Vector3 normal, float offset)
        {
            // カメラ（原点）での値が正になるように向きをそろえる
            if (offset < 0f)
            {
                return new WallPlane(-normal, -offset, true);
            }

            return new WallPlane(normal, offset, true);
        }
    }

    /// <summary>
    /// 四隅で囲った範囲の内側かどうかを調べるクラス。
    /// </summary>
    public static class QuadMask
    {
        /// <summary>
        /// 四隅（深度画像上の 0〜1）で囲った範囲の内側にある画素を true にした配列を作ります。
        /// </summary>
        /// <param name="corners">四隅</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <returns>内側なら true の配列（幅×高さ）</returns>
        public static bool[] Build(IList<Vector2> corners, int width, int height)
        {
            Vector2[] polygon = new Vector2[corners.Count];
            for (int i = 0; i < corners.Count; i++)
            {
                polygon[i] = new Vector2(corners[i].x * width, corners[i].y * height);
            }

            bool[] mask = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    mask[y * width + x] = IsPointInPolygon(new Vector2(x + 0.5f, y + 0.5f), polygon);
                }
            }

            return mask;
        }

        /// <summary>
        /// 点が多角形の内側にあるかを返します。
        /// </summary>
        /// <param name="point">点</param>
        /// <param name="polygon">多角形の頂点</param>
        /// <returns>内側なら true</returns>
        public static bool IsPointInPolygon(Vector2 point, IList<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                bool crosses = (a.y > point.y) != (b.y > point.y);
                if (crosses && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }

    /// <summary>
    /// 基準深度と四隅から壁の面を作るクラス。
    /// 四隅で囲った範囲の内側にある有効な基準（0 でない画素）すべてに面を当てはめ、
    /// 面から大きく外れた点（壁でない物）を除いて当てはめ直します。
    /// センサーに近すぎて隅の深度が 0 になっていても面を作れます。
    /// </summary>
    public static class WallPlaneBuilder
    {
        /// <summary>面を作るのに必要な有効な点の数。</summary>
        public const int MinimumPoints = 50;

        /// <summary>外れた点を除いて当てはめ直す回数。</summary>
        public const int RefineIterations = 3;

        /// <summary>外れた点とみなす距離の下限（mm）。</summary>
        public const float MinimumInlierDistanceMm = 25f;

        /// <summary>
        /// 基準深度と四隅から壁の面を作ります。
        /// </summary>
        /// <param name="baseline">基準深度</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="corners">四隅（深度画像上の 0〜1）</param>
        /// <param name="intrinsics">内部パラメータ</param>
        /// <returns>壁の面（作れないときは IsValid が false）</returns>
        public static WallPlane Build(ushort[] baseline, int width, int height, IList<Vector2> corners, DepthIntrinsics intrinsics)
        {
            if (baseline == null || corners == null || corners.Count != 4 || baseline.Length != width * height)
            {
                return default;
            }

            List<Vector3> points = CollectPoints(baseline, width, height, QuadMask.Build(corners, width, height), intrinsics);
            if (points.Count < MinimumPoints)
            {
                return default;
            }

            WallPlane plane = FitPlane(points);
            for (int i = 0; i < RefineIterations && plane.IsValid; i++)
            {
                List<Vector3> inliers = SelectInliers(points, plane);
                if (inliers.Count < MinimumPoints)
                {
                    break;
                }

                plane = FitPlane(inliers);
            }

            return plane;
        }

        /// <summary>
        /// 内側にある有効な基準の数を数えます（面を作れないときの説明用）。
        /// </summary>
        /// <param name="baseline">基準深度</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="corners">四隅</param>
        /// <returns>有効な画素の数</returns>
        public static int CountValidInside(ushort[] baseline, int width, int height, IList<Vector2> corners)
        {
            bool[] mask = QuadMask.Build(corners, width, height);
            int count = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i] && baseline[i] != 0)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 点の集まりに最小二乗で面を当てはめます。
        /// </summary>
        /// <param name="points">点（mm）</param>
        /// <returns>壁の面</returns>
        public static WallPlane FitPlane(IList<Vector3> points)
        {
            if (points == null || points.Count < 3)
            {
                return default;
            }

            Vector3 centroid = Centroid(points);
            Vector3 normal = SmallestVarianceDirection(points, centroid);
            if (normal.sqrMagnitude < 1e-12f)
            {
                return default;
            }

            return WallPlane.FromNormalAndOffset(normal, -Vector3.Dot(normal.normalized, centroid) * normal.magnitude);
        }

        private static List<Vector3> CollectPoints(ushort[] baseline, int width, int height, bool[] mask, DepthIntrinsics intrinsics)
        {
            List<Vector3> points = new List<Vector3>();
            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    int index = y * width + x;
                    if (mask[index] && baseline[index] != 0)
                    {
                        points.Add(intrinsics.Unproject(x + 0.5f, y + 0.5f, baseline[index]));
                    }
                }
            }

            return points;
        }

        private static List<Vector3> SelectInliers(List<Vector3> points, WallPlane plane)
        {
            float[] distances = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                distances[i] = Mathf.Abs(plane.DistanceTo(points[i]));
            }

            float[] sorted = (float[])distances.Clone();
            System.Array.Sort(sorted);
            float limit = Mathf.Max(MinimumInlierDistanceMm, sorted[sorted.Length / 2] * 3f);
            List<Vector3> inliers = new List<Vector3>();
            for (int i = 0; i < points.Count; i++)
            {
                if (distances[i] <= limit)
                {
                    inliers.Add(points[i]);
                }
            }

            return inliers;
        }

        private static Vector3 Centroid(IList<Vector3> points)
        {
            double x = 0;
            double y = 0;
            double z = 0;
            foreach (Vector3 point in points)
            {
                x += point.x;
                y += point.y;
                z += point.z;
            }

            return new Vector3((float)(x / points.Count), (float)(y / points.Count), (float)(z / points.Count));
        }

        private static Vector3 SmallestVarianceDirection(IList<Vector3> points, Vector3 centroid)
        {
            double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (Vector3 point in points)
            {
                double dx = point.x - centroid.x;
                double dy = point.y - centroid.y;
                double dz = point.z - centroid.z;
                xx += dx * dx;
                xy += dx * dy;
                xz += dx * dz;
                yy += dy * dy;
                yz += dy * dz;
                zz += dz * dz;
            }

            // 3つの軸のうち、最もよく解ける向きで法線を求める
            double detX = yy * zz - yz * yz;
            double detY = xx * zz - xz * xz;
            double detZ = xx * yy - xy * xy;
            double best = System.Math.Max(detX, System.Math.Max(detY, detZ));
            if (best <= 0)
            {
                return Vector3.zero;
            }

            if (best == detX)
            {
                return new Vector3((float)detX, (float)(xz * yz - xy * zz), (float)(xy * yz - xz * yy));
            }

            if (best == detY)
            {
                return new Vector3((float)(xz * yz - xy * zz), (float)detY, (float)(xy * xz - yz * xx));
            }

            return new Vector3((float)(xy * yz - xz * yy), (float)(xy * xz - yz * xx), (float)detZ);
        }
    }
}
