Add-Type @"
    using System;
    using System.Runtime.InteropServices;
    public class Win32 {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    }
"@

# Find the Lumina window
$lumina = Get-Process | Where-Object {$_.MainWindowTitle -eq "Lumina"} | Select-Object -First 1

if ($lumina) {
    # Bring to front
    [Win32]::SetWindowPos($lumina.MainWindowHandle, -1, 0, 0, 0, 0, 0x0001 -bor 0x0002) # HWND_TOPMOST
    [Win32]::ShowWindow($lumina.MainWindowHandle, 9)  # SW_RESTORE
    [Win32]::SetForegroundWindow($lumina.MainWindowHandle)

    Start-Sleep -Seconds 1
    Write-Host "Lumina window brought to foreground"
} else {
    Write-Host "Lumina window not found"
}