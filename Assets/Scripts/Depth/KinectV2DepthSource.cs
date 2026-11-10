using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WallBurst.Depth
{
    /// <summary>
    /// Kinect for Windows ランタイム 2.0 の Kinect20.dll から深度だけを読む。
    /// 公式の Microsoft.Kinect.dll は C++/CLI のため、Unity では読み込めない。
    /// センサー面は壁とほぼ平行（壁を正面に見る向き）を前提にする。
    /// 取得したフレームは毎回 Release し、長時間運転で COM を溜めない。
    /// </summary>
    public sealed class KinectV2DepthSource : MonoBehaviour, IDepthSource
    {
        private const int ReleaseSlot = 2;
        private const int SensorOpenSlot = 6;
        private const int SensorCloseSlot = 7;
        private const int SensorDepthSourceSlot = 11;
        private const int SourceOpenReaderSlot = 7;
        private const int ReaderAcquireSlot = 6;
        private const int FrameBufferSlot = 4;
        private const int FrameDescriptionSlot = 5;
        private const int DescriptionWidthSlot = 3;
        private const int DescriptionHeightSlot = 4;

        private IntPtr _sensor;
        private IntPtr _reader;
        private ushort[] _buffer;
        private short[] _signedCopy;
        private int _serial;
        private AcquireDelegate _acquire;
        private AccessBufferDelegate _accessBuffer;
        private ReleaseDelegate _releaseFrame;

        /// <summary>幅。未受信のあいだは 0。</summary>
        public int Width { get; private set; }

        /// <summary>高さ。未受信のあいだは 0。</summary>
        public int Height { get; private set; }

        /// <summary>1枚以上コピーできたか。</summary>
        public bool HasFrame { get; private set; }

        /// <summary>画面に出す状態。</summary>
        public string StatusMessage { get; private set; }

        /// <summary>
        /// センサーを開く。
        /// </summary>
        private void Awake()
        {
            StatusMessage = "Kinect を起動しています。";
            TryOpen();
        }

        /// <summary>
        /// 最新の深度があればバッファへコピーする。
        /// </summary>
        private void Update()
        {
            if (_reader == IntPtr.Zero || _acquire == null)
            {
                return;
            }

            IntPtr frame = IntPtr.Zero;
            try
            {
                int result = _acquire(_reader, out frame);
                if (result < 0 || frame == IntPtr.Zero)
                {
                    return;
                }

                CopyFrame(frame);
            }
            catch (Exception exception)
            {
                HasFrame = false;
                StatusMessage = "Kinect の取得に失敗しました。再接続してください。 " + exception.Message;
            }
            finally
            {
                ReleaseFrame(frame);
            }
        }

        /// <summary>
        /// センサーとリーダーを閉じる。二度呼んでも安全。
        /// </summary>
        public void Shutdown()
        {
            HasFrame = false;
            _acquire = null;
            _accessBuffer = null;
            _releaseFrame = null;
            ReleaseCom(ref _reader);
            CloseSensor();
        }

        /// <summary>
        /// 一度閉じてから開き直す。
        /// </summary>
        public void Reconnect()
        {
            Shutdown();
            Width = 0;
            Height = 0;
            _buffer = null;
            _signedCopy = null;
            StatusMessage = "Kinect を再接続しています。";
            TryOpen();
        }

        /// <summary>
        /// 最新深度をコピーする。
        /// </summary>
        /// <param name="destination">コピー先。</param>
        /// <param name="frameSerial">受信のたびに増える番号。</param>
        /// <returns>未受信、またはバッファ不足なら false。</returns>
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
        /// 破棄時にセンサーを閉じる。
        /// </summary>
        private void OnDestroy()
        {
            Shutdown();
        }

        /// <summary>
        /// 既定の Kinect を開き、深度リーダーを作る。
        /// </summary>
        private void TryOpen()
        {
            try
            {
                IntPtr sensor;
                int result = GetDefaultKinectSensor(out sensor);
                if (result < 0 || sensor == IntPtr.Zero)
                {
                    StatusMessage = "Kinect が見つかりません。USB と Kinect for Windows ランタイム 2.0 を確認してください。";
                    return;
                }

                _sensor = sensor;
                int opened = Call<OpenDelegate>(_sensor, SensorOpenSlot)(_sensor);
                if (opened < 0)
                {
                    StatusMessage = "Kinect を開けませんでした。";
                    Shutdown();
                    return;
                }

                IntPtr source;
                int sourceResult = Call<GetPointerDelegate>(_sensor, SensorDepthSourceSlot)(_sensor, out source);
                if (sourceResult < 0 || source == IntPtr.Zero)
                {
                    StatusMessage = "深度ソースを取得できませんでした。";
                    Shutdown();
                    return;
                }

                IntPtr reader;
                int readerResult = Call<GetPointerDelegate>(source, SourceOpenReaderSlot)(source, out reader);
                ReleaseCom(ref source);
                if (readerResult < 0 || reader == IntPtr.Zero)
                {
                    StatusMessage = "深度リーダーを開けませんでした。";
                    Shutdown();
                    return;
                }

                _reader = reader;
                _acquire = Call<AcquireDelegate>(_reader, ReaderAcquireSlot);
                StatusMessage = "Kinect の深度を待っています。";
            }
            catch (DllNotFoundException)
            {
                StatusMessage = "Kinect20.dll が見つかりません。Kinect for Windows ランタイム 2.0 をインストールしてください。";
                Shutdown();
            }
            catch (Exception exception)
            {
                StatusMessage = "Kinect の起動に失敗しました。 " + exception.Message;
                Shutdown();
            }
        }

        /// <summary>
        /// フレームの生バッファをミリメートル配列へ写す。
        /// </summary>
        private void CopyFrame(IntPtr frame)
        {
            if (_accessBuffer == null)
            {
                _accessBuffer = Call<AccessBufferDelegate>(frame, FrameBufferSlot);
                ReadSize(frame);
            }

            uint capacity;
            IntPtr nativeBuffer;
            int result = _accessBuffer(frame, out capacity, out nativeBuffer);
            if (result < 0 || nativeBuffer == IntPtr.Zero || capacity == 0)
            {
                return;
            }

            int length = (int)capacity;
            if (_buffer == null || _buffer.Length != length)
            {
                _buffer = new ushort[length];
                _signedCopy = new short[length];
                if (Width <= 0 || Height <= 0)
                {
                    Width = 512;
                    Height = length / Width;
                }
            }

            Marshal.Copy(nativeBuffer, _signedCopy, 0, length);
            Buffer.BlockCopy(_signedCopy, 0, _buffer, 0, length * sizeof(ushort));
            _serial++;
            HasFrame = true;
            StatusMessage = "Kinect の深度を受信しています。";
        }

        /// <summary>
        /// 最初のフレームから幅と高さを読む。
        /// </summary>
        private void ReadSize(IntPtr frame)
        {
            IntPtr description;
            int result = Call<GetPointerDelegate>(frame, FrameDescriptionSlot)(frame, out description);
            if (result < 0 || description == IntPtr.Zero)
            {
                return;
            }

            try
            {
                int width;
                int height;
                if (Call<GetIntDelegate>(description, DescriptionWidthSlot)(description, out width) >= 0)
                {
                    Width = width;
                }

                if (Call<GetIntDelegate>(description, DescriptionHeightSlot)(description, out height) >= 0)
                {
                    Height = height;
                }
            }
            finally
            {
                ReleaseCom(ref description);
            }
        }

        /// <summary>
        /// 深度フレームを解放する。
        /// </summary>
        private void ReleaseFrame(IntPtr frame)
        {
            if (frame == IntPtr.Zero)
            {
                return;
            }

            if (_releaseFrame == null)
            {
                _releaseFrame = Call<ReleaseDelegate>(frame, ReleaseSlot);
            }

            _releaseFrame(frame);
        }

        /// <summary>
        /// センサーを閉じてから解放する。
        /// </summary>
        private void CloseSensor()
        {
            if (_sensor == IntPtr.Zero)
            {
                return;
            }

            try
            {
                Call<CloseDelegate>(_sensor, SensorCloseSlot)(_sensor);
            }
            catch (Exception)
            {
                StatusMessage = "Kinect の終了処理で警告が出ました。";
            }

            ReleaseCom(ref _sensor);
        }

        /// <summary>
        /// COM オブジェクトを Release する。
        /// </summary>
        private static void ReleaseCom(ref IntPtr instance)
        {
            if (instance == IntPtr.Zero)
            {
                return;
            }

            try
            {
                Call<ReleaseDelegate>(instance, ReleaseSlot)(instance);
            }
            catch (Exception)
            {
                instance = IntPtr.Zero;
                return;
            }

            instance = IntPtr.Zero;
        }

        /// <summary>
        /// vtable のスロットから呼び出し口を得る。毎フレームは作らない。
        /// </summary>
        private static T Call<T>(IntPtr instance, int slot) where T : class
        {
            IntPtr table = Marshal.ReadIntPtr(instance);
            IntPtr method = Marshal.ReadIntPtr(table, slot * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer(method, typeof(T)) as T;
        }

        [DllImport("Kinect20.dll", EntryPoint = "GetDefaultKinectSensor")]
        private static extern int GetDefaultKinectSensor(out IntPtr sensor);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int OpenDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CloseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetPointerDelegate(IntPtr self, out IntPtr result);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int AcquireDelegate(IntPtr self, out IntPtr frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int AccessBufferDelegate(IntPtr self, out uint capacity, out IntPtr buffer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetIntDelegate(IntPtr self, out int value);
    }
}
