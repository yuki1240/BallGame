using System;
using System.Runtime.InteropServices;

namespace BallGame
{
    /// <summary>
    /// Kinect20.dll を P/Invoke で直接呼び出します（Microsoft.Kinect.dll は使いません）。
    /// COM のインターフェースは仮想関数表の番号で呼び出します。番号は Kinect.h の宣言順です。
    /// </summary>
    internal static class KinectNative
    {
        /// <summary>成功。</summary>
        public const int SOk = 0;

        /// <summary>IUnknown::Release の番号。</summary>
        public const int SlotRelease = 2;

        /// <summary>IKinectSensor::Open の番号。</summary>
        public const int SlotSensorOpen = 6;

        /// <summary>IKinectSensor::Close の番号。</summary>
        public const int SlotSensorClose = 7;

        /// <summary>IKinectSensor::get_IsAvailable の番号。</summary>
        public const int SlotSensorIsAvailable = 9;

        /// <summary>IKinectSensor::get_DepthFrameSource の番号。</summary>
        public const int SlotSensorDepthFrameSource = 11;

        /// <summary>IDepthFrameSource::OpenReader の番号（get_IsActive の次）。</summary>
        public const int SlotSourceOpenReader = 7;

        /// <summary>IDepthFrameReader::AcquireLatestFrame の番号。</summary>
        public const int SlotReaderAcquireLatestFrame = 6;

        /// <summary>IDepthFrame::CopyFrameDataToArray の番号。</summary>
        public const int SlotFrameCopyToArray = 3;

        /// <summary>IUnknown::Release。</summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint ReleaseMethod(IntPtr self);

        /// <summary>引数なしのメソッド（Open / Close）。</summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int NoArgumentMethod(IntPtr self);

        /// <summary>BOOLEAN を返すプロパティ（get_IsAvailable）。</summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GetBooleanMethod(IntPtr self, out byte value);

        /// <summary>インターフェースを返すメソッド（get_DepthFrameSource / OpenReader / AcquireLatestFrame）。</summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GetPointerMethod(IntPtr self, out IntPtr value);

        /// <summary>IDepthFrame::CopyFrameDataToArray。</summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CopyFrameMethod(IntPtr self, uint capacity, IntPtr buffer);

        /// <summary>既定の Kinect センサーを取得します。</summary>
        [DllImport("Kinect20.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetDefaultKinectSensor(out IntPtr sensor);

        /// <summary>
        /// COM オブジェクトの仮想関数表から関数を取り出します。
        /// </summary>
        /// <typeparam name="T">関数の型</typeparam>
        /// <param name="comObject">COM オブジェクト</param>
        /// <param name="slot">番号</param>
        /// <returns>呼び出し用のデリゲート</returns>
        public static T GetMethod<T>(IntPtr comObject, int slot) where T : class
        {
            IntPtr table = Marshal.ReadIntPtr(comObject);
            IntPtr function = Marshal.ReadIntPtr(table, slot * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer(function, typeof(T)) as T;
        }

        /// <summary>
        /// COM オブジェクトを Release します。null は無視します。
        /// </summary>
        /// <param name="comObject">COM オブジェクト</param>
        public static void Release(ref IntPtr comObject)
        {
            if (comObject == IntPtr.Zero)
            {
                return;
            }

            GetMethod<ReleaseMethod>(comObject, SlotRelease)(comObject);
            comObject = IntPtr.Zero;
        }
    }
}
