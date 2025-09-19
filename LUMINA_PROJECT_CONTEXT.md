# Lumina - Complete Project Context

## Project Overview
**Lumina** (formerly SuperWhisperer) is a Windows desktop application for real-time voice transcription using OpenAI's Whisper model. It features a modern, Copilot-inspired UI and runs as a system tray application with global hotkey support.

## Current State (As of January 2025)
- **Version**: Working production build
- **UI Design**: Just transformed to match Microsoft Copilot's aesthetic
- **Repository**: https://github.com/annasba07/lumina.git
- **Author**: annasba07 (ab722@cornell.edu)

## Technical Stack

### Core Technologies
- **Framework**: WPF (.NET 8.0) with C# 12.0
- **UI Library**: ModernWpf for Windows 11 styling
- **AI Model**: OpenAI Whisper (base.en model, 141MB)
- **Audio**: NAudio for audio capture
- **Transcription**: Whisper.NET library
- **Updates**: Velopack for auto-updates
- **Installer**: Inno Setup (optional)

### Architecture
```
Lumina/
├── MainWindow.xaml/cs       # Main UI with Copilot-inspired design
├── RecordingOverlay.xaml/cs # Floating recording indicator
├── AudioCapture.cs          # Audio recording with silence detection
├── WhisperEngine.cs         # Whisper.NET integration
├── GlobalHotkey.cs          # Windows Forms-based hotkey (Ctrl+Space)
├── Logger.cs                # File-based logging system
├── AppSettings.cs           # Application settings management
└── App.xaml/cs              # Application entry point and styles
```

## Recent Major Changes (January 2025)

### UI Transformation to Copilot-Style
1. **Frameless Window Design**
   - Custom title bar with minimize/close buttons
   - Rounded corners (12px radius)
   - Soft shadows for depth

2. **Modern Visual Elements**
   - Greeting: "Good evening! 👋" with personalized welcome
   - Quick action pills: Voice Note, Quick Memo, Meeting Notes
   - Copilot-style color scheme (neutral grays, blue accents)
   - Segoe MDL2 Assets icons replacing emojis

3. **Fixed Issues**
   - Duplicate "Ctrl+Space" text display
   - Replaced emoji icons with professional vector icons
   - Improved button visibility and states

### Key Features
- **Global Hotkey**: Ctrl+Space to start/stop recording
- **System Tray**: Minimizes to tray, runs in background
- **Smart Recording**: Automatic silence detection and speech end detection
- **Real-time Display**: Shows audio levels during recording
- **Text Management**: Copy to clipboard, clear functionality
- **Word Count**: Displays word count for transcriptions

## Project Files Structure

### Essential Files
- `ggml-base.en.bin` - Whisper model file (141MB)
- `lumina-icon.ico` - Application icon
- `SuperWhisperWPF.csproj` - Project configuration
- `App.xaml/MainWindow.xaml` - UI definitions
- `*.cs` files - Core application logic

### Build Output
- `bin/Release/net8.0-windows/Lumina.dll` - Main assembly
- `bin/Release/net8.0-windows/Lumina.exe` - Executable

## Development Setup

### Prerequisites
```bash
# Required
- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension

# Model file
# Download ggml-base.en.bin from Hugging Face if missing
```

### Build & Run
```bash
# Clone repository
git clone https://github.com/annasba07/lumina.git
cd lumina

# Restore dependencies
dotnet restore

# Build
dotnet build --configuration Release

# Run
dotnet run --configuration Release
```

## Key Design Decisions

### Why WPF over WinUI 3/WebView2?
- **Performance**: Direct audio pipeline, minimal latency
- **Simplicity**: Single-purpose app doesn't need web flexibility
- **Resource Efficiency**: ~50-100MB memory vs 300MB+ for WebView2
- **Native Feel**: Pure Windows app with instant hotkey response

### UI Design Philosophy
- **Copilot-Inspired**: Modern, friendly, approachable
- **Minimal Friction**: One hotkey to transcribe
- **Clean Empty State**: Clear call-to-action
- **Professional**: No unnecessary animations or effects

## Current Issues & Known Limitations

1. **Hotkey Conflicts**: Ctrl+Space may conflict with other apps
2. **Window Resizing**: Custom title bar doesn't handle maximize perfectly
3. **Audio Level Display**: Sometimes persists after recording stops
4. **Model Loading**: Takes 1-2 seconds on first transcription

## Future Improvements Roadmap

### High Priority
- [ ] Settings UI for hotkey customization
- [ ] Multiple model support (tiny, small, medium)
- [ ] Dark mode toggle
- [ ] Export transcriptions to file

### Medium Priority
- [ ] Cloud sync for transcriptions
- [ ] Multiple language support
- [ ] Custom vocabulary/terms
- [ ] Keyboard shortcuts for all actions

### Low Priority
- [ ] Voice commands
- [ ] Integration with other apps
- [ ] Transcription history
- [ ] Analytics dashboard

## Code Patterns & Conventions

### Naming
- **XAML Elements**: PascalCase (e.g., `ResultsTextBox`)
- **Event Handlers**: Object_Event (e.g., `ClearButton_Click`)
- **Private Fields**: camelCase with underscore prefix in some places
- **Constants**: UPPER_SNAKE_CASE

### UI Updates
Always use Dispatcher for UI updates from background threads:
```csharp
Dispatcher.Invoke(() => {
    StatusText.Text = "Ready";
});
```

### Logging
Use the Logger class for debugging:
```csharp
Logger.Info("Operation completed");
Logger.Error($"Error: {ex.Message}", ex);
```

## Git Configuration
```bash
git config user.name "annasba07"
git config user.email "ab722@cornell.edu"
```

## Testing Workflow

### Manual Testing Checklist
- [ ] Ctrl+Space starts/stops recording
- [ ] Transcription appears after speaking
- [ ] [BLANK_AUDIO] shows for silence (expected behavior)
- [ ] Copy button works
- [ ] Clear button works
- [ ] Minimize to tray works
- [ ] Window dragging works
- [ ] Close button works

### PowerShell Screenshot Scripts
```powershell
# Capture Lumina window
.\capture-lumina-window.ps1

# Focus Lumina window
.\focus-lumina.ps1
```

## Important Context for AI Assistants

### When Making UI Changes
1. Read `MainWindow.xaml` first to understand current structure
2. Check `App.xaml` for styles and resources
3. Update both XAML and code-behind (`.cs`) files
4. Test window chrome functionality (drag, minimize, close)

### When Modifying Audio/Transcription
1. `AudioCapture.cs` handles recording logic
2. `WhisperEngine.cs` manages the AI model
3. Silence detection is intentional - shows [BLANK_AUDIO]
4. Don't change the model file path

### When Committing
1. Always use descriptive commit messages
2. Include what changed and why
3. Use conventional commits (feat:, fix:, docs:, etc.)
4. Co-author with Claude if AI-assisted

### Build/Deploy Commands
```bash
# Development
dotnet run

# Release build
dotnet build --configuration Release
dotnet publish --configuration Release

# Kill running instances (Windows)
powershell -Command "Stop-Process -Name Lumina -Force"
```

## Support & Resources

### Dependencies (NuGet)
- ModernWpf.MessageBox (0.5.2)
- NAudio (2.2.1)
- Newtonsoft.Json (13.0.3)
- Velopack (0.0.598)
- Whisper.net (1.5.0)
- Whisper.net.Runtime (1.5.0)

### Model Download
If `ggml-base.en.bin` is missing:
1. Download from Hugging Face: https://huggingface.co/ggerganov/whisper.cpp
2. Place in project root directory
3. File should be ~141MB

## Recent Session Summary
In our latest session, we:
1. Fixed duplicate "Ctrl+Space" text bug
2. Replaced emoji icons with Segoe MDL2 Assets icons
3. Completely redesigned UI to match Microsoft Copilot
4. Added custom frameless window with title bar
5. Implemented quick action buttons
6. Added smooth animations and modern shadows
7. Pushed all changes to GitHub repository

The application is now production-ready with a professional, modern interface that rivals Microsoft's own apps while maintaining its core simplicity and performance.