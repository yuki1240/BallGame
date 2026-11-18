using System;
using System.Collections.Generic;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// Kinect の深度画像と、プロジェクターの画面（壁座標）を結び付けます。
    /// <para>壁座標は、投影範囲の左下が (0, 0)、右上が (1, 1) です。</para>
    /// <list type="number">
    /// <item>校正でタップした四隅（台形）から、射影変換（台形を長方形に直す計算）を作ります。</item>
    /// <item>基準の深度から、壁の面（3Dの平面）を作ります。</item>
    /// <item>ボールが壁に触れた3Dの位置を、壁の面の上に下ろしてから深度画像に写し、射影変換で壁座標にします。
    /// 横から見ているとボールの見える位置は壁から浮いて写るため、いったん壁の面に下ろしてから写します。</item>
    /// </list>
    /// </summary>
    public sealed class WallMapping
    {
        /// <summary>壁の面を作るのに必要な点の数。</summary>
        public const int MinimumPlanePoints = 200;

        private readonly int width;
        private readonly int height;
        private readonly DepthIntrinsics intrinsics;
        private readonly Homography imageToWall;
        private readonly Homography wallToImage;
        private readonly WallPlane plane;

        private WallMapping(int width, int height, DepthIntrinsics intrinsics, Homography imageToWall, WallPlane plane)
        {
            this.width = width;
            this.height = height;
            this.intrinsics = intrinsics;
            this.imageToWall = imageToWall;
            wallToImage = imageToWall.Inverse();
            this.plane = plane;
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

            Homography homography;
            try
            {
                homography = Homography.FromImageCorners(data.Corners);
                homography.Inverse();
            }
            catch (InvalidOperationException)
            {
                error = "四隅から変換を作れません。4つの点を離してタップし直してください。";
                return null;
            }

            DepthIntrinsics intrinsics = DepthIntrinsics.ForResolution(data.Width, data.Height);
            WallPlane plane = BuildPlane(data, insideMask, intrinsics);
            if (!plane.IsValid)
            {
                error = "四隅の内側に、壁の深度が取れている所が少なすぎます。壁が灰色に写っている所を囲んでください。";
                return null;
            }

            return new WallMapping(data.Width, data.Height, intrinsics, homography, plane);
        }

        /// <summary>
        /// 深度画像の位置（0〜1、上端が 0）を壁座標に写します。
        /// </summary>
        /// <param name="image01">深度画像の位置</param>
        /// <returns>壁座標</returns>
        public Vector2 ImageToWall(Vector2 image01)
        {
            return imageToWall.Transform(image01);
        }

        /// <summary>
        /// 壁座標を深度画像の位置（0〜1、上端が 0）に写します。
        /// </summary>
        /// <param name="wall01">壁座標</param>
        /// <returns>深度画像の位置</returns>
        public Vector2 WallToImage(Vector2 wall01)
        {
            return wallToImage.Transform(wall01);
        }

        /// <summary>
        /// 壁に触れた3Dの位置（mm）を壁座標に写します。
        /// </summary>
        /// <param name="contactPoint">3Dの位置</param>
        /// <returns>壁座標</returns>
        public Vector2 PointToWall(Vector3 contactPoint)
        {
            Vector3 onWall = plane.ProjectOnto(contactPoint);
            Vector2 pixel = intrinsics.Project(onWall);
            return ImageToWall(new Vector2(pixel.x / width, pixel.y / height));
        }

        /// <summary>
        /// 壁座標の位置にある、壁の面の上の3Dの位置（mm）を返します（模擬深度でボールを置くのに使います）。
        /// </summary>
        /// <param name="wall01">壁座標</param>
        /// <returns>3Dの位置（作れないときは Vector3.zero）</returns>
        public Vector3 WallToPoint(Vector2 wall01)
        {
            Vector2 image = WallToImage(wall01);
            Vector3 ray = intrinsics.Ray(image.x * width, image.y * height);
            float denominator = Vector3.Dot(plane.Normal, ray);
            if (Mathf.Abs(denominator) < 1e-6f)
            {
                return Vector3.zero;
            }

            float t = plane.Offset / denominator;
            return t > 0f ? ray * t : Vector3.zero;
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
