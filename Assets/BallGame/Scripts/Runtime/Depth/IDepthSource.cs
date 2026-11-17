using System;

namespace BallGame
{
    /// <summary>
    /// 深度の入力源。深度は ushort のミリメートルで、0 は無効、画像の上端が Y=0 です。
    /// </summary>
    public interface IDepthSource : IDisposable
    {
        /// <summary>幅（画素）。</summary>
        int Width { get; }

        /// <summary>高さ（画素）。</summary>
        int Height { get; }

        /// <summary>入力源の種類。</summary>
        DepthSourceKind Kind { get; }

        /// <summary>フレームを出せる状態か。</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 新しいフレームがあれば buffer へ写します。
        /// </summary>
        /// <param name="buffer">写し先（幅×高さ）</param>
        /// <returns>新しいフレームを写したとき true</returns>
        bool TryGetLatestFrame(ushort[] buffer);
    }
}
