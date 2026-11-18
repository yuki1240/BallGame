using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// Kinect v2 の深度（512×424、ushort mm）を読む入力源。フレームは毎回 Release します。
    /// Kinect の最短距離（約 50cm）より近い画素は 0 のままです。
    /// </summary>
    public sealed class KinectDepthSource : IDepthSource
    {
        /// <summary>深度の幅。</summary>
        public const int DepthWidth = 512;

        /// <summary>深度の高さ。</summary>
        public const int DepthHeight = 424;

        private IntPtr sensor;
        private IntPtr depthSource;
        private IntPtr reader;
        private KinectNative.GetBooleanMethod isAvailableMethod;
        private KinectNative.GetPointerMethod acquireMethod;
        private bool disposed;

        private KinectDepthSource()
        {
        }

        /// <summary>幅。</summary>
        public int Width
        {
            get { return DepthWidth; }
        }

        /// <summary>高さ。</summary>
        public int Height
        {
            get { return DepthHeight; }
        }

        /// <summary>入力源の種類。</summary>
        public DepthSourceKind Kind
        {
            get { return DepthSourceKind.Kinect; }
        }

        /// <summary>Kinect がつながっていてフレームを出せるか。</summary>
        public bool IsAvailable
        {
            get { return ReadIsAvailable(); }
        }

        /// <summary>
        /// Kinect を開きます。Kinect20.dll が無い・Windows 以外・開けないときは null を返します。
        /// </summary>
        /// <returns>入力源（開けないときは null）</returns>
        public static KinectDepthSource TryOpen()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            KinectDepthSource source = new KinectDepthSource();
            try
            {
                if (source.Open())
                {
                    return source;
                }
            }
            catch (DllNotFoundException)
            {
                Debug.Log("Kinect20.dll が見つかりません（Kinect for Windows Runtime 2.0 が未導入）。模擬深度を使います。");
            }
            catch (EntryPointNotFoundException exception)
            {
                Debug.LogWarning("Kinect20.dll の関数が見つかりません: " + exception.Message);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Kinect を開けませんでした: " + exception.Message);
            }

            source.Dispose();
            return null;
#else
            return null;
#endif
        }

        /// <summary>
        /// 新しいフレームがあれば buffer へ写します。取り出したフレームは必ず Release します。
        /// </summary>
        /// <param name="buffer">写し先（512×424）</param>
        /// <returns>新しいフレームを写したとき true</returns>
        public bool TryGetLatestFrame(ushort[] buffer)
        {
            if (disposed || reader == IntPtr.Zero || buffer == null || buffer.Length != DepthWidth * DepthHeight)
            {
                return false;
            }

            IntPtr frame;
            int result = acquireMethod(reader, out frame);
            if (result != KinectNative.SOk || frame == IntPtr.Zero)
            {
                // 新しいフレームがまだ無い（E_PENDING）
                return false;
            }

            try
            {
                return CopyFrame(frame, buffer);
            }
            finally
            {
                KinectNative.Release(ref frame);
            }
        }

        /// <summary>
        /// Kinect を閉じて、取得したものをすべて Release します。
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            KinectNative.Release(ref reader);
            KinectNative.Release(ref depthSource);
            CloseSensor();
            KinectNative.Release(ref sensor);
        }

        private bool Open()
        {
            int result = KinectNative.GetDefaultKinectSensor(out sensor);
            if (result != KinectNative.SOk || sensor == IntPtr.Zero)
            {
                return false;
            }

            if (KinectNative.GetMethod<KinectNative.NoArgumentMethod>(sensor, KinectNative.SlotSensorOpen)(sensor) != KinectNative.SOk)
            {
                return false;
            }

            isAvailableMethod = KinectNative.GetMethod<KinectNative.GetBooleanMethod>(sensor, KinectNative.SlotSensorIsAvailable);
            return OpenDepthReader();
        }

        private bool OpenDepthReader()
        {
            KinectNative.GetPointerMethod getSource = KinectNative.GetMethod<KinectNative.GetPointerMethod>(sensor, KinectNative.SlotSensorDepthFrameSource);
            if (getSource(sensor, out depthSource) != KinectNative.SOk || depthSource == IntPtr.Zero)
            {
                return false;
            }

            KinectNative.GetPointerMethod openReader = KinectNative.GetMethod<KinectNative.GetPointerMethod>(depthSource, KinectNative.SlotSourceOpenReader);
            if (openReader(depthSource, out reader) != KinectNative.SOk || reader == IntPtr.Zero)
            {
                return false;
            }

            acquireMethod = KinectNative.GetMethod<KinectNative.GetPointerMethod>(reader, KinectNative.SlotReaderAcquireLatestFrame);
            return true;
        }

        private static bool CopyFrame(IntPtr frame, ushort[] buffer)
        {
            KinectNative.CopyFrameMethod copy = KinectNative.GetMethod<KinectNative.CopyFrameMethod>(frame, KinectNative.SlotFrameCopyToArray);
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                return copy(frame, (uint)buffer.Length, handle.AddrOfPinnedObject()) == KinectNative.SOk;
            }
            finally
            {
                handle.Free();
            }
        }

        private bool ReadIsAvailable()
        {
            if (disposed || sensor == IntPtr.Zero || isAvailableMethod == null)
            {
                return false;
            }

            byte available;
            return isAvailableMethod(sensor, out available) == KinectNative.SOk && available != 0;
        }

        private void CloseSensor()
        {
            if (sensor == IntPtr.Zero)
            {
                return;
            }

            try
            {
                KinectNative.GetMethod<KinectNative.NoArgumentMethod>(sensor, KinectNative.SlotSensorClose)(sensor);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Kinect を閉じるときにエラー: " + exception.Message);
            }
        }
    }
}
