using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace SuperWhisperWindows
{
    public static class DependencyChecker
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string dllToLoad);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);
        
        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        public static void CheckDependencies()
        {
            Logger.Info("=== Dependency Check Starting ===");
            
            // Check system architecture
            Logger.Info($"Process Architecture: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}");
            Logger.Info($"OS Architecture: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}");
            
            // Check Visual C++ Redistributables
            CheckVCRedist();
            
            // Check whisper.dll loading
            CheckWhisperDll();
            
            // Check CUDA (if applicable)
            CheckCuda();
            
            Logger.Info("=== Dependency Check Complete ===");
        }
        
        private static void CheckVCRedist()
        {
            Logger.Info("Checking Visual C++ Redistributables...");
            
            var vcRedistPaths = new[]
            {
                @"C:\Windows\System32\vcruntime140.dll",
                @"C:\Windows\System32\msvcp140.dll",
                @"C:\Windows\System32\vcruntime140_1.dll"
            };
            
            bool allFound = true;
            foreach (var path in vcRedistPaths)
            {
                if (File.Exists(path))
                {
                    var version = FileVersionInfo.GetVersionInfo(path);
                    Logger.Info($"✅ Found: {Path.GetFileName(path)} v{version.FileVersion}");
                }
                else
                {
                    Logger.Error($"❌ Missing: {Path.GetFileName(path)}");
                    allFound = false;
                }
            }
            
            if (!allFound)
            {
                Logger.Error("Missing Visual C++ Redistributables!");
                Logger.Error("Download from: https://aka.ms/vs/17/release/vc_redist.x64.exe");
            }
        }
        
        private static void CheckWhisperDll()
        {
            Logger.Info("Testing whisper.dll loading...");
            
            var whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper.dll");
            if (!File.Exists(whisperPath))
            {
                whisperPath = Path.Combine(Environment.CurrentDirectory, "bin\\Release\\net8.0-windows\\whisper.dll");
            }
            
            if (!File.Exists(whisperPath))
            {
                Logger.Error("whisper.dll not found for dependency testing");
                return;
            }
            
            Logger.Info($"Testing DLL load: {whisperPath}");
            
            // Try to load the DLL directly
            var handle = LoadLibrary(whisperPath);
            if (handle != IntPtr.Zero)
            {
                Logger.Info("✅ whisper.dll loaded successfully!");
                FreeLibrary(handle);
            }
            else
            {
                var error = GetLastError();
                Logger.Error($"❌ Failed to load whisper.dll. Error code: 0x{error:X8}");
                Logger.Error("This indicates missing dependencies or architecture mismatch");
                
                // Analyze the file
                AnalyzeWhisperDll(whisperPath);
            }
        }
        
        private static void AnalyzeWhisperDll(string path)
        {
            try
            {
                Logger.Info("Analyzing whisper.dll...");
                
                var fileInfo = new FileInfo(path);
                Logger.Info($"File size: {fileInfo.Length / 1024:F1} KB");
                Logger.Info($"Created: {fileInfo.CreationTime}");
                Logger.Info($"Modified: {fileInfo.LastWriteTime}");
                
                // Check if it's a valid PE file
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var buffer = new byte[64];
                    fs.Read(buffer, 0, 64);
                    
                    if (buffer[0] == 0x4D && buffer[1] == 0x5A) // MZ header
                    {
                        Logger.Info("✅ Valid PE file (Windows executable)");
                        
                        // Check for 32-bit vs 64-bit
                        fs.Seek(60, SeekOrigin.Begin);
                        var peHeaderOffset = new byte[4];
                        fs.Read(peHeaderOffset, 0, 4);
                        var peOffset = BitConverter.ToInt32(peHeaderOffset, 0);
                        
                        fs.Seek(peOffset + 4 + 20, SeekOrigin.Begin);
                        var magicBytes = new byte[2];
                        fs.Read(magicBytes, 0, 2);
                        var magic = BitConverter.ToUInt16(magicBytes, 0);
                        
                        if (magic == 0x010b)
                        {
                            Logger.Error("❌ whisper.dll is 32-bit, but process is 64-bit!");
                            Logger.Error("Need 64-bit version of whisper.dll");
                        }
                        else if (magic == 0x020b)
                        {
                            Logger.Info("✅ whisper.dll is 64-bit (correct)");
                        }
                        else
                        {
                            Logger.Warning($"Unknown architecture magic: 0x{magic:X4}");
                        }
                    }
                    else
                    {
                        Logger.Error("❌ Invalid PE file - whisper.dll may be corrupted");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error analyzing whisper.dll: {ex.Message}");
            }
        }
        
        private static void CheckCuda()
        {
            Logger.Info("Checking for CUDA libraries...");
            
            var cudaPaths = new[]
            {
                @"C:\Windows\System32\cudart64_*.dll",
                @"C:\Windows\System32\cublas64_*.dll"
            };
            
            // This is a simplified check - whisper.dll might not need CUDA
            // but it's good to know if CUDA is available
            var cudaFound = false;
            
            try
            {
                var cudaDir = Environment.GetEnvironmentVariable("CUDA_PATH");
                if (!string.IsNullOrEmpty(cudaDir))
                {
                    Logger.Info($"CUDA_PATH found: {cudaDir}");
                    cudaFound = true;
                }
                else
                {
                    Logger.Info("CUDA_PATH not set (this is usually fine for CPU-only whisper)");
                }
            }
            catch
            {
                Logger.Info("CUDA check completed (not required for CPU whisper)");
            }
        }
        
        public static void ProvideFixSuggestions()
        {
            Logger.Info("=== Fix Suggestions ===");
            Logger.Info("1. Install Visual C++ Redistributable 2019-2022 x64:");
            Logger.Info("   https://aka.ms/vs/17/release/vc_redist.x64.exe");
            Logger.Info("");
            Logger.Info("2. If that doesn't work, try downloading whisper.dll from:");
            Logger.Info("   https://github.com/ggml-org/whisper.cpp/releases/latest");
            Logger.Info("   Look for 'whisper-bin-x64.zip'");
            Logger.Info("");
            Logger.Info("3. Alternative: Use static build with no dependencies:");
            Logger.Info("   https://github.com/Const-me/Whisper/releases");
            Logger.Info("   Download 'WhisperDesktop.zip' and rename 'Whisper.dll' to 'whisper.dll'");
        }
    }
}