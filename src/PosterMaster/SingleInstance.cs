using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PosterMaster;

internal sealed class SingleInstance : IDisposable
{
    private const string InstanceName = "Rezak.6F784C20-2C21-4156-AF93-2C80797A0068";
    private const int SwRestore = 9;
    private const int WmNcDestroy = 0x0082;
    private readonly Mutex _mutex = new(false, @"Local\" + InstanceName);
    private readonly uint _activateMessage = RegisterWindowMessage(InstanceName);
    private bool _ownsMutex;

    public bool StartOrActivate()
    {
        // The other process can own the mutex before its main window is ready.
        var wait = Stopwatch.StartNew();
        do
        {
            try
            {
                _ownsMutex = _mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                _ownsMutex = true;
            }
            if (_ownsMutex) return true;

            nint window = FindInstanceWindow();
            if (window != 0 && GetWindowThreadProcessId(window, out uint processId) != 0)
            {
                // The process handling this launch can pass on its foreground activation permission.
                AllowSetForegroundWindow(processId);
                if (PostMessage(window, _activateMessage, 0, 0))
                    return false;
            }

            Thread.Sleep(50);
        } while (wait.Elapsed < TimeSpan.FromSeconds(5));

        return false;
    }

    public void Register(Window window)
    {
        if (_activateMessage == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        window.SourceInitialized += (_, _) =>
        {
            nint handle = new WindowInteropHelper(window).Handle;
            HwndSource.FromHwnd(handle).AddHook(WindowProcedure);
            if (!SetProp(handle, InstanceName, 1))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        };
    }

    private static nint FindInstanceWindow()
    {
        nint found = 0;
        EnumWindows((window, _) =>
        {
            if (GetProp(window, InstanceName) == 0) return true;
            found = window;
            return false;
        }, 0);
        return found;
    }

    private nint WindowProcedure(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmNcDestroy)
            RemoveProp(window, InstanceName);
        if (message != _activateMessage) return 0;

        if (IsIconic(window))
            ShowWindow(window, SwRestore);
        nint activePopup = GetLastActivePopup(window);
        SetForegroundWindow(activePopup != 0 && IsWindowVisible(activePopup) ? activePopup : window);
        handled = true;
        return 0;
    }

    public void Dispose()
    {
        if (_ownsMutex) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    private delegate bool EnumWindowsCallback(nint window, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetProp(nint window, string name, nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetProp(nint window, string name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint RemoveProp(nint window, string name);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    private static extern nint GetLastActivePopup(nint window);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);
}
