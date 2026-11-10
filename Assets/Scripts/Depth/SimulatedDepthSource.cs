using System;
using UnityEngine;

namespace WallBurst.Depth
{
    /// <summary>
    /// Kinect が無いときに、同じ検知パイプラインへ模擬深度を流す。
    /// </summary>
    public sealed class SimulatedDepthSource : MonoBehaviour, IDepthSource
    {
        private SimulatedDepthGenerator _generator;
        private ushort[] _buffer;
        private int _frameIndex;
        private int _serial;

        /// <summary>幅。</summary>
        public int Width
        {
            get { return _generator == null ? 0 : _generator.Width; }
        }

        /// <summary>高さ。</summary>
        public int Height
        {
            get { return _generator == null ? 0 : _generator.Height; }
        }

        /// <summary>1枚以上作ったか。</summary>
        public bool HasFrame { get; private set; }

        /// <summary>状態文。</summary>
        public string StatusMessage { get; private set; }

        /// <summary>
        /// 320×240 で生成器を作る。
        /// </summary>
        private void Awake()
        {
            _generator = new SimulatedDepthGenerator(320, 240);
            _buffer = new ushort[_generator.Width * _generator.Height];
            StatusMessage = "シミュレーション中です。Kinect が繋がると切り替えられます。";
        }

        /// <summary>
        /// 毎フレーム、揺れの入った壁を作り直す。
        /// </summary>
        private void Update()
        {
            _frameIndex++;
            _generator.Fill(_buffer, _frameIndex);
            _serial++;
            HasFrame = true;
        }

        /// <summary>
        /// 最新の模擬深度をコピーする。
        /// </summary>
        /// <param name="destination">コピー先。</param>
        /// <param name="frameSerial">フレーム番号。</param>
        /// <returns>まだ1枚も無いとき false。</returns>
        public bool TryCopyLatest(ushort[] destination, out int frameSerial)
        {
            frameSerial = _serial;
            if (!HasFrame || destination == null || _buffer == null || destination.Length < _buffer.Length)
            {
                return false;
            }

            Buffer.BlockCopy(_buffer, 0, destination, 0, _buffer.Length * sizeof(ushort));
            return true;
        }

        /// <summary>
        /// 壁より手前のボールを数フレーム混ぜる。
        /// </summary>
        /// <param name="pixelX">中心 X。</param>
        /// <param name="pixelY">中心 Y。</param>
        /// <param name="radiusPixels">半径。</param>
        /// <param name="closerMillimeters">手前への出っ張り。</param>
        /// <param name="lifeFrames">継続フレーム。</param>
        public void Inject(int pixelX, int pixelY, int radiusPixels, int closerMillimeters, int lifeFrames)
        {
            if (_generator == null)
            {
                return;
            }

            _generator.Inject(pixelX, pixelY, radiusPixels, closerMillimeters, lifeFrames);
        }
    }
}
