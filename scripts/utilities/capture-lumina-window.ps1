Add-Type @"
    using System;
    using System.Runtime.InteropServices;
    public class Win32 {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
"@

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Find the Lumina window
$lumina = Get-Process | Where-Object {$_.MainWindowTitle -eq "Lumina"} | Select-Object -First 1

if ($lumina) {
    # Bring Lumina to foreground
    [Win32]::ShowWindow($lumina.MainWindowHandle, 3)  # SW_MAXIMIZE = 3
    [Win32]::SetForegroundWindow($lumina.MainWindowHandle)

    Start-Sleep -Seconds 1

    # Get screen bounds
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

    # Create bitmap
    $bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)

    # Create graphics object
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    # Copy screen content
    $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)

    # Save screenshot
    $bitmap.Save("lumina-app-screenshot.png")

    # Cleanup
    $graphics.Dispose()
    $bitmap.Dispose()

    Write-Host "Screenshot of Lumina saved to lumina-app-screenshot.png"
} else {
    Write-Host "Lumina window not found. Make sure the application is running."
}