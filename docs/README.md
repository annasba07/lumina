# SuperWhisper Windows

A fast, lightweight speech-to-text application for Windows inspired by [SuperWhisper for macOS](https://superwhisper.com). Transform your voice into text with ultra-low latency using OpenAI's Whisper model.

## ✨ Features

- **⚡ Ultra-fast transcription** - Sub-2 second processing time
- **🎯 System-wide hotkey** - Toggle recording with `Ctrl+Space` from anywhere
- **🖥️ System tray integration** - Runs quietly in the background
- **🚀 GPU acceleration** - Automatic CUDA support when available, CPU fallback
- **📋 Easy copying** - Results appear in a dedicated window for easy copy/paste
- **🔇 Voice activity detection** - Clean audio processing with visual feedback
- **🎨 Modern UI** - Clean, animated recording overlay

## 🚀 Quick Start

### Prerequisites
- **Windows 10/11**
- **.NET 8.0 Runtime** ([Download here](https://dotnet.microsoft.com/download/dotnet/8.0))
- **Microphone** with proper permissions

### Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/yourusername/superwhisper-windows.git
   cd superwhisper-windows
   ```

2. **Build and run:**
   ```bash
   cd src
   dotnet run
   ```

   *Note: On first run, the app will automatically download the Whisper model file (~148MB)*

### Usage

1. **Start the application** - Look for the microphone icon in your system tray
2. **Press `Ctrl+Space`** to start recording
3. **Speak clearly** into your microphone
4. **Press `Ctrl+Space`** again to stop recording
5. **Copy your transcription** from the results window that appears

## 🛠️ Technical Details

### Architecture
- **Built with:** C# .NET 8.0, Windows Forms
- **Speech Recognition:** [Whisper.net](https://github.com/sandrohanea/whisper.net) - C# bindings for OpenAI Whisper
- **Audio Capture:** NAudio for low-latency audio recording
- **Model:** OpenAI Whisper Base English model (`ggml-base.en.bin`)

### Performance
- **Latency:** ~1.6 seconds for 3.6 seconds of audio
- **Model Size:** 148MB (downloaded automatically)
- **Memory Usage:** ~200MB during operation
- **GPU Support:** Automatic CUDA acceleration when available

### Audio Specifications
- **Sample Rate:** 16kHz
- **Channels:** Mono
- **Format:** 16-bit PCM
- **Buffer:** 50ms low-latency capture

## 🔧 Development

### Building from Source

```bash
# Navigate to source directory
cd src

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run in debug mode
dotnet run
```

### Project Structure
```
src/
├── AudioCapture.cs         # Low-latency audio recording
├── WhisperEngine.cs        # Whisper.net integration
├── Program.cs              # Main application and system tray
├── RecordingOverlay.cs     # Animated recording UI
├── ResultsWindow.cs        # Transcription results display
├── GlobalHotkey.cs         # System-wide hotkey registration
├── Logger.cs               # Application logging
└── SuperWhisperWindows.csproj
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

### Development Setup
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [OpenAI Whisper](https://github.com/openai/whisper) for the incredible speech recognition model
- [Whisper.net](https://github.com/sandrohanea/whisper.net) for the excellent C# bindings
- [SuperWhisper](https://superwhisper.com) for the original macOS inspiration
- [NAudio](https://github.com/naudio/NAudio) for audio capture capabilities

## 🐛 Issues & Support

If you encounter any issues or have questions:

1. Check the [Issues](https://github.com/yourusername/superwhisper-windows/issues) page
2. Create a new issue with detailed information
3. Include your system specifications and error logs if applicable

## 📊 System Requirements

- **OS:** Windows 10 (1903+) or Windows 11
- **RAM:** 4GB minimum, 8GB recommended
- **Storage:** 500MB free space (for model and dependencies)
- **GPU:** NVIDIA GPU with CUDA support (optional, for acceleration)
- **Microphone:** Any Windows-compatible microphone

---

**Made with ❤️ for the Windows community**