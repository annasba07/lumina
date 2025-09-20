# Download Inno Setup installer
$downloadUrl = "https://files.jrsoftware.org/is/6/innosetup-6.4.0.exe"
$outputPath = ".\innosetup-installer.exe"

Write-Host "Downloading Inno Setup 6.4.0..." -ForegroundColor Green

try {
    # Download the installer
    Invoke-WebRequest -Uri $downloadUrl -OutFile $outputPath -UseBasicParsing

    Write-Host "Download completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Starting Inno Setup installer..." -ForegroundColor Yellow
    Write-Host "Please follow these steps:" -ForegroundColor Yellow
    Write-Host "1. Click 'Next' through the setup wizard" -ForegroundColor Cyan
    Write-Host "2. Accept the license agreement" -ForegroundColor Cyan
    Write-Host "3. Use default installation path (C:\Program Files (x86)\Inno Setup 6)" -ForegroundColor Cyan
    Write-Host "4. Complete the installation" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "After installation, we'll build the Lumina installer!" -ForegroundColor Green
    Write-Host ""

    # Start the installer
    Start-Process -FilePath $outputPath -Wait

    Write-Host "Installation completed!" -ForegroundColor Green

    # Check if Inno Setup was installed
    $innoPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (Test-Path $innoPath) {
        Write-Host "Inno Setup installed successfully at: $innoPath" -ForegroundColor Green
        Write-Host ""
        Write-Host "Ready to build Lumina installer!" -ForegroundColor Green
    } else {
        Write-Host "Could not verify Inno Setup installation. Please check installation path." -ForegroundColor Red
    }

} catch {
    Write-Host "Error downloading Inno Setup: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please download manually from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
}