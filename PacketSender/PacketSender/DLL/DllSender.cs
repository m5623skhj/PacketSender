using System.Runtime.InteropServices;

namespace PacketSender.DLL
{
    public class ClientProxySender
    {
        public static ClientProxySender Instance { get; } = new();

        private IntPtr _dllHandle = IntPtr.Zero;
        private StartDelegate? _start;
        private StopDelegate? _stop;
        private IsConnectedDelegate? _isConnected;
        private SendPacketDelegate? _sendPacket;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool StartDelegate(
            [MarshalAs(UnmanagedType.LPWStr)] string clientCoreOptionFile,
            [MarshalAs(UnmanagedType.LPWStr)] string sessionGetterOptionFile);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void StopDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool IsConnectedDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool SendPacketDelegate(IntPtr streamData, int streamSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);

        private ClientProxySender() { }

        public bool LoadClientProxySenderDll(string dllPath = "ClientProxySender.dll")
        {
            UnloadLibrary();

            _dllHandle = LoadLibrary(dllPath);
            if (_dllHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                throw new Exception($"DLL 로드 실패: {dllPath}, Error Code: {error}");
            }

            try
            {
                var startPtr = GetProcAddress(_dllHandle, "Start");
                if (startPtr == IntPtr.Zero)
                {
                    throw new Exception("Can't find Start()");
                }
                _start = Marshal.GetDelegateForFunctionPointer<StartDelegate>(startPtr);

                var stopPtr = GetProcAddress(_dllHandle, "Stop");
                if (stopPtr == IntPtr.Zero)
                {
                    throw new Exception("Can't find Stop()");
                }
                _stop = Marshal.GetDelegateForFunctionPointer<StopDelegate>(stopPtr);

                var isConnectedPtr = GetProcAddress(_dllHandle, "IsConnected");
                if (isConnectedPtr == IntPtr.Zero)
                {
                    throw new Exception("Can't find IsConnected()");
                }
                _isConnected = Marshal.GetDelegateForFunctionPointer<IsConnectedDelegate>(isConnectedPtr);

                var sendPacketPtr = GetProcAddress(_dllHandle, "SendPacket");
                if (sendPacketPtr == IntPtr.Zero)
                {
                    throw new Exception("Can't find SendPacket()");
                }
                _sendPacket = Marshal.GetDelegateForFunctionPointer<SendPacketDelegate>(sendPacketPtr);

                return true;
            }
            catch
            {
                UnloadLibrary();
                throw;
            }
        }

        public void UnloadLibrary()
        {
            if (_dllHandle == IntPtr.Zero)
            {
                return;
            }

            FreeLibrary(_dllHandle);
            _dllHandle = IntPtr.Zero;
            _start = null;
            _stop = null;
            _isConnected = null;
            _sendPacket = null;
        }

        public bool Start(string clientCoreOptionFile, string sessionGetterOptionFile)
        {
            if (_start == null)
            {
                throw new InvalidOperationException("Dll is unloaded, need LoadLibrary() first");
            }

            return _start.Invoke(clientCoreOptionFile, sessionGetterOptionFile);
        }

        public void Stop()
        {
            if (_stop == null)
            {
                throw new InvalidOperationException("Dll is unloaded, need LoadLibrary() first");
            }

            _stop.Invoke();
        }

        public bool IsConnected()
        {
            if (_isConnected == null)
            {
                throw new InvalidOperationException("Dll is unloaded, need LoadLibrary() first");
            }

            return _isConnected.Invoke();
        }

        public bool SendPacket(byte[] data)
        {
            if (_sendPacket == null)
            {
                throw new InvalidOperationException("Dll is unloaded, need LoadLibrary() first");
            }

            if (data.Length == 0)
            {
                return false;
            }

            var dataPtr = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, dataPtr, data.Length);
                return _sendPacket.Invoke(dataPtr, data.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(dataPtr);
            }
        }

        ~ClientProxySender()
        {
            UnloadLibrary();
        }
    }
}
