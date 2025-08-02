using System.Runtime.InteropServices;

public class DLLSender
{
    private static readonly DLLSender _instance = new DLLSender();
    public static DLLSender Instance => _instance;

    private IntPtr _dllHandle = IntPtr.Zero;
    private SendPacketDelegate? _sendPacket;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SendPacketDelegate(string data);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr hModule);

    private DLLSender() { }

    public bool LoadSenderLibrary(string dllName)
    {
        if (_dllHandle != IntPtr.Zero)
        {
            FreeLibrary(_dllHandle);
            _dllHandle = IntPtr.Zero;
            _sendPacket = null;
        }

        _dllHandle = LoadLibrary(dllName);
        if (_dllHandle == IntPtr.Zero)
        {
            throw new Exception("DLL load failed");
        }

        IntPtr procAddress = GetProcAddress(_dllHandle, "SendPacket");
        if (procAddress == IntPtr.Zero)
        {
            throw new Exception("SendPacket function find failed");
        }

        _sendPacket = Marshal.GetDelegateForFunctionPointer<SendPacketDelegate>(procAddress);
        return true;
    }

    public void SendPacket(string data)
    {
        if (_sendPacket == null)
        {
            throw new InvalidOperationException("SendPacket function not loaded");
        }

        _sendPacket.Invoke(data);
    }
}