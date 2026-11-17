using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// このフレームで壁に当たった位置（壁座標、左下 (0,0)・右上 (1,1)）を集めます。
    /// どの場面からでも同じ方法でヒットを受け取れるようにするための道具です。
    /// </summary>
    public static class WallHitInput
    {
        /// <summary>
        /// このフレームのヒットを壁座標で返し、コンソールに位置を出します。
        /// </summary>
        /// <param name="session">セッション</param>
        /// <param name="sceneLabel">ログに出す場面の名前</param>
        /// <returns>壁座標の一覧（無ければ空）</returns>
        public static List<Vector2> CollectHits(CalibrationSession session, string sceneLabel)
        {
            List<Vector2> hits = new List<Vector2>();
            DetectionResult result = session.LatestResult;
            if (!session.HasNewFrame || result == null || !session.HasHomography)
            {
                return hits;
            }

            foreach (DetectedHit hit in result.Hits)
            {
                Vector2 wallUv = ToWall(session, hit.ImageUv);
                LogHit(sceneLabel, hit, wallUv);
                hits.Add(wallUv);
            }

            return hits;
        }

        /// <summary>
        /// いまボールとして検知されている場所を、壁座標で返します（当たりかどうかに関係なく、見えているあいだ毎フレーム）。
        /// 固定モードで、ボールがどこにあると見られているかを出すのに使います。
        /// </summary>
        /// <param name="session">セッション</param>
        /// <returns>壁座標の一覧（無ければ空）</returns>
        public static List<Vector2> CollectBallPositions(CalibrationSession session)
        {
            List<Vector2> positions = new List<Vector2>();
            DetectionResult result = session.LatestResult;
            if (!session.HasNewFrame || result == null || !session.HasHomography)
            {
                return positions;
            }

            foreach (Vector2 imageUv in result.BallImageUvs)
            {
                positions.Add(ToWall(session, imageUv));
            }

            return positions;
        }

        /// <summary>
        /// 模擬深度のとき、画面のクリック（壁座標）をボールとして入れます。
        /// </summary>
        /// <param name="session">セッション</param>
        /// <param name="wallUv">壁座標</param>
        /// <returns>入れられたとき true</returns>
        public static bool InjectClick(CalibrationSession session, Vector2 wallUv)
        {
            SimulatedDepthSource simulator = session != null ? session.Simulator : null;
            if (simulator == null || !session.HasHomography)
            {
                return false;
            }

            Vector2 imageUv = session.WallToImage.Transform(wallUv);
            if (imageUv.x < 0f || imageUv.y < 0f || imageUv.x > 1f || imageUv.y > 1f)
            {
                return false;
            }

            simulator.InjectBall(imageUv);
            return true;
        }

        /// <summary>
        /// 壁座標を 1920×1080 の Canvas 上の位置（左下が原点）に直します。
        /// </summary>
        /// <param name="wallUv">壁座標</param>
        /// <returns>Canvas 上の位置</returns>
        public static Vector2 ToCanvas(Vector2 wallUv)
        {
            return new Vector2(wallUv.x * UiFactory.ReferenceResolution.x, wallUv.y * UiFactory.ReferenceResolution.y);
        }

        private static Vector2 ToWall(CalibrationSession session, Vector2 imageUv)
        {
            Vector2 raw = session.ImageToWall.Transform(imageUv);
            return new Vector2(Mathf.Clamp01(raw.x), Mathf.Clamp01(raw.y));
        }

        private static void LogHit(string sceneLabel, DetectedHit hit, Vector2 wallUv)
        {
            Vector2 screen = new Vector2(wallUv.x * Screen.width, wallUv.y * Screen.height);
            Debug.Log(string.Format(
                "[HIT][{0}] Kinect=({1:F3}, {2:F3}) 画素=({3:F1}, {4:F1}) → 壁=({5:F3}, {6:F3}) → Unity画面=({7:F1}, {8:F1})",
                sceneLabel, hit.ImageUv.x, hit.ImageUv.y, hit.Pixel.x, hit.Pixel.y, wallUv.x, wallUv.y, screen.x, screen.y));
        }
    }
}
