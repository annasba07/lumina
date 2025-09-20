Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

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
$bitmap.Save("lumina-screenshot.png")

# Cleanup
$graphics.Dispose()
$bitmap.Dispose()

Write-Host "Screenshot saved to lumina-screenshot.png"