using System;
using System.Collections.Generic;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// Kinect の深度と、プロジェクターの画面（壁座標）を結び付けます。
    /// <para>壁座標は、投影範囲の左下が (0, 0)、右上が (1, 1) です。</para>
    /// <para>Kinect は壁に沿って見ているので、壁の横の位置は深度画像の画素の位置にはほとんど出ず、深度（距離）に出ます。
    /// そのため、深度画像の上の台形をそのまま変換するのではなく、深度を使って「壁の面の上の mm の位置」に直してから変換します。</para>
    /// <list type="number">
    /// <item>基準の深度から、壁の面（3Dの平面）を作ります。</item>
    /// <item>タップした四隅の視線と壁の面が交わる点を、四隅の3Dの位置にします（端の画素は深度がぶれやすいため、画素の深度は使いません）。</item>
    /// <item>四隅の3Dの位置から、壁の面の上に「横 u・高さ v」の mm の座標を作ります。</item>
    /// <item>Kinect の位置（左・右）の設定に合わせて、Kinect に近い側を画面の左（または右）にそろえます。</item>
    /// <item>mm の座標の上で、四隅から壁座標への射影変換を作ります（ほぼ長方形なので計算が安定します）。</item>
    /// </list>
    /// <para>深度画像の位置（0〜1）は、画素 i の中心が (i + 0.5) / 幅 になる決まりです（<see cref="QuadMask"/> と同じ）。</para>
    /// </summary>
    public sealed class WallMapping
    {
        /// <summary>壁の面を作るのに必要な点の数。</summary>
        public const int MinimumPlanePoints = 200;

        /// <summary>左右の辺の距離がこれ以上違うとき、近い側を Kinect の側と判断します（mm）。</summary>
        public const float NearSideMinimumMm = 150f;

        /// <summary>投影面の幅・高さの最小（mm）。これより小さいときは四隅の取り方がおかしいとみなします。</summary>
        public const float MinimumSizeMm = 100f;

        // 射影変換の計算を安定させるため、mm の座標を m（1/1000）にして渡す
        private const float MetersPerMm = 0.001f;

        private readonly int width;
        private readonly int height;
        private readonly DepthIntrinsics intrinsics;
        private readonly WallPlane plane;
        private readonly Vector3 origin;
        private readonly Vector3 axisU;
        private readonly Vector3 axisV;
        private readonly Homography planeToWall;
        private readonly Homography wallToPlane;
        private readonly Vector3[] cornerPoints;

        private WallMapping(int width, int height, DepthIntrinsics intrinsics, WallPlane plane, Vector3[] cornerPoints, Vector3 axisU, Vector3 axisV, Homography planeToWall, bool nearSideDetected)
        {
            this.width = width;
            this.height = height;
            this.intrinsics = intrinsics;
            this.plane = plane;
            this.cornerPoints = cornerPoints;
            origin = cornerPoints[3];
            this.axisU = axisU;
            this.axisV = axisV;
            this.planeToWall = planeToWall;
            wallToPlane = planeToWall.Inverse();
            NearSideDetected = nearSideDetected;
            WidthMm = (Vector3.Distance(cornerPoints[0], cornerPoints[1]) + Vector3.Distance(cornerPoints[3], cornerPoints[2])) * 0.5f;
            HeightMm = (Vector3.Distance(cornerPoints[0], cornerPoints[3]) + Vector3.Distance(cornerPoints[1], cornerPoints[2])) * 0.5f;
        }

        /// <summary>壁の面。</summary>
        public WallPlane Plane
        {
            get { return plane; }
        }

        /// <summary>内部パラメータ。</summary>
        public DepthIntrinsics Intrinsics
        {
            get { return intrinsics; }
        }

        /// <summary>投影面の幅（mm、上の辺と下の辺の平均）。</summary>
        public float WidthMm { get; private set; }

        /// <summary>投影面の高さ（mm、左の辺と右の辺の平均）。</summary>
        public float HeightMm { get; private set; }

        /// <summary>
        /// 左右の辺の距離の違いから、Kinect に近い側を判断できたか。
        /// できないとき（Kinect が投影面の正面や下にあるとき）は、タップした順番の左右をそのまま使います。
        /// </summary>
        public bool NearSideDetected { get; private set; }

        /// <summary>四隅の3Dの位置（左上・右上・右下・左下の順、mm）。Kinect の位置の設定に合わせて並べ直したもの。</summary>
        public IList<Vector3> CornerPoints
        {
            get { return cornerPoints; }
        }

        /// <summary>
        /// 校正から作ります。作れないときは null を返し、理由を error に入れます。
        /// </summary>
        /// <param name="data">校正</param>
        /// <param name="insideMask">四隅の内側の表</param>
        /// <param name="error">作れなかった理由（画面に出す文）</param>
        /// <returns>対応付け（作れないときは null）</returns>
        public static WallMapping Create(CalibrationData data, bool[] insideMask, out string error)
        {
            error = string.Empty;
            if (data == null || !data.IsComplete)
            {
                error = "基準と四隅がまだそろっていません。";
                return null;
            }

            if (!QuadMask.IsConvex(data.Corners))
            {
                error = "四隅の形がねじれています。左上→右上→右下→左下の順にタップし直してください。";
                return null;
            }

            DepthIntrinsics intrinsics = DepthIntrinsics.ForResolution(data.Width, data.Height);
            WallPlane plane = BuildPlane(data, insideMask, intrinsics);
            if (!plane.IsValid)
            {
                error = "四隅の内側に、壁の深度が取れている所が少なすぎます。壁が灰色に写っている所を囲んでください。";
                return null;
            }

            Vector3[] tapped = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 corner = data.Corners[i];
                Vector3 ray = intrinsics.Ray(corner.x * data.Width - 0.5f, corner.y * data.Height - 0.5f);
                if (!IntersectPlane(plane, ray, out tapped[i]))
                {
                    error = "四隅の一部が壁の面の外にあります。壁が写っている所の角をタップし直してください。";
                    return null;
                }
            }

            bool nearSideDetected;
            Vector3[] corners = OrderCorners(tapped, data.KinectOnRight, out nearSideDetected);

            // 壁の面の上の座標の向き：u は左→右、v は下→上
            Vector3 normal = plane.Normal;
            Vector3 rawU = ((corners[1] - corners[0]) + (corners[2] - corners[3])) * 0.5f;
            Vector3 axisU = rawU - normal * Vector3.Dot(normal, rawU);
            if (axisU.magnitude < 1f)
            {
                error = "四隅の左右が近すぎます。4つの点を離してタップし直してください。";
                return null;
            }

            axisU.Normalize();
            Vector3 rawV = ((corners[0] - corners[3]) + (corners[1] - corners[2])) * 0.5f;
            Vector3 axisV = Vector3.Cross(normal, axisU).normalized;
            if (Vector3.Dot(axisV, rawV) < 0f)
            {
                axisV = -axisV;
            }

            Vector2[] planeCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = corners[i] - corners[3];
                planeCorners[i] = new Vector2(Vector3.Dot(offset, axisU) * MetersPerMm, Vector3.Dot(offset, axisV) * MetersPerMm);
            }

            Homography planeToWall;
            try
            {
                planeToWall = Homography.FromPoints(planeCorners, Homography.WallCorners);
                planeToWall.Inverse();
            }
            catch (InvalidOperationException)
            {
                error = "四隅から変換を作れません。4つの点を離してタップし直してください。";
                return null;
            }

            WallMapping mapping = new WallMapping(data.Width, data.Height, intrinsics, plane, corners, axisU, axisV, planeToWall, nearSideDetected);
            if (mapping.WidthMm < MinimumSizeMm || mapping.HeightMm < MinimumSizeMm)
            {
                error = "四隅で囲んだ範囲が小さすぎます（幅 " + Mathf.RoundToInt(mapping.WidthMm / 10f) + "cm × 高さ " + Mathf.RoundToInt(mapping.HeightMm / 10f) + "cm）。投影範囲の角をタップし直してください。";
                return null;
            }

            return mapping;
        }

        /// <summary>
        /// タップした四隅を、Kinect の位置の設定に合わせて並べ直します（左上・右上・右下・左下の順）。
        /// <para>左右の辺の距離がはっきり違うときは、近い側を Kinect の側にします（タップの左右が逆でも直ります）。
        /// はっきりしないときは、タップした順番の左側を Kinect の側とみなします。</para>
        /// </summary>
        /// <param name="tapped">タップした順の四隅（3D、mm）</param>
        /// <param name="kinectOnRight">Kinect を右側に置いたとき true</param>
        /// <param name="nearSideDetected">近い側を判断できたとき true</param>
        /// <returns>並べ直した四隅</returns>
        public static Vector3[] OrderCorners(IList<Vector3> tapped, bool kinectOnRight, out bool nearSideDetected)
        {
            float left = (tapped[0].magnitude + tapped[3].magnitude) * 0.5f;
            float right = (tapped[1].magnitude + tapped[2].magnitude) * 0.5f;
            nearSideDetected = Mathf.Abs(left - right) >= NearSideMinimumMm;
            bool nearIsLeft = !nearSideDetected || left < right;
            bool swap = nearIsLeft == kinectOnRight;
            return swap
                ? new[] { tapped[1], tapped[0], tapped[3], tapped[2] }
                : new[] { tapped[0], tapped[1], tapped[2], tapped[3] };
        }

        /// <summary>
        /// 3Dの位置（mm）を、壁の面の上の座標（横 u・高さ v、mm、左下の角が原点）に直します。
        /// </summary>
        /// <param name="point">3Dの位置</param>
        /// <returns>壁の面の上の座標</returns>
        public Vector2 PointToPlane(Vector3 point)
        {
            Vector3 offset = plane.ProjectOnto(point) - origin;
            return new Vector2(Vector3.Dot(offset, axisU), Vector3.Dot(offset, axisV));
        }

        /// <summary>
        /// 壁に触れた3Dの位置（mm）を壁座標に写します。壁から浮いている分は、壁の面に下ろしてから写します。
        /// </summary>
        /// <param name="contactPoint">3Dの位置</param>
        /// <returns>壁座標</returns>
        public Vector2 PointToWall(Vector3 contactPoint)
        {
            Vector2 onPlane = PointToPlane(contactPoint);
            return planeToWall.Transform(onPlane * MetersPerMm);
        }

        /// <summary>
        /// 壁座標の位置にある、壁の面の上の3Dの位置（mm）を返します（模擬深度でボールを置くのに使います）。
        /// </summary>
        /// <param name="wall01">壁座標</param>
        /// <returns>3Dの位置</returns>
        public Vector3 WallToPoint(Vector2 wall01)
        {
            Vector2 onPlane = wallToPlane.Transform(wall01) / MetersPerMm;
            return origin + axisU * onPlane.x + axisV * onPlane.y;
        }

        /// <summary>
        /// 深度画像の位置（0〜1、上端が 0）の視線が壁の面と交わる所を、壁座標に写します。
        /// </summary>
        /// <param name="image01">深度画像の位置</param>
        /// <returns>壁座標（視線が壁の面と交わらないときは (-1, -1)）</returns>
        public Vector2 ImageToWall(Vector2 image01)
        {
            Vector3 point;
            Vector3 ray = intrinsics.Ray(image01.x * width - 0.5f, image01.y * height - 0.5f);
            return IntersectPlane(plane, ray, out point) ? PointToWall(point) : new Vector2(-1f, -1f);
        }

        /// <summary>
        /// 壁座標を深度画像の位置（0〜1、上端が 0）に写します。
        /// </summary>
        /// <param name="wall01">壁座標</param>
        /// <returns>深度画像の位置</returns>
        public Vector2 WallToImage(Vector2 wall01)
        {
            Vector2 pixel = intrinsics.Project(WallToPoint(wall01));
            return new Vector2((pixel.x + 0.5f) / width, (pixel.y + 0.5f) / height);
        }

        private static bool IntersectPlane(WallPlane plane, Vector3 ray, out Vector3 point)
        {
            point = Vector3.zero;
            float denominator = Vector3.Dot(plane.Normal, ray);
            if (Mathf.Abs(denominator) < 1e-6f)
            {
                return false;
            }

            float t = plane.Offset / denominator;
            if (t <= 0f)
            {
                return false;
            }

            point = ray * t;
            return true;
        }

        private static WallPlane BuildPlane(CalibrationData data, bool[] insideMask, DepthIntrinsics intrinsics)
        {
            List<Vector3> points = new List<Vector3>();
            int step = 2;
            for (int y = 0; y < data.Height; y += step)
            {
                for (int x = 0; x < data.Width; x += step)
                {
                    int i = y * data.Width + x;
                    ushort d = data.Baseline[i];
                    if (d == 0 || insideMask == null || !insideMask[i])
                    {
                        continue;
                    }

                    points.Add(intrinsics.ToPoint(x, y, d));
                }
            }

            if (points.Count < MinimumPlanePoints / (step * step))
            {
                return default;
            }

            return WallPlane.FitRobust(points, 25f);
        }
    }
}
