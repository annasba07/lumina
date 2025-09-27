using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Whisper.net.Ggml;

namespace SuperWhisperWPF.Core
{
    /// <summary>
    /// Downloads and manages Whisper models for optimal performance.
    /// Supports downloading tiny, base, small models from HuggingFace.
    /// </summary>
    public static class ModelDownloader
    {
        private static readonly HttpClient httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        public static async Task<bool> EnsureModelExistsAsync(string modelType = "tiny")
        {
            var settings = AppSettings.Instance;
            string modelFileName;
            string modelUrl;

            switch (modelType.ToLower())
            {
                case "tiny":
                    modelFileName = settings.TinyModelFileName;
                    modelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin";
                    break;
                case "base":
                    modelFileName = settings.ModelFileName;
                    modelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin";
                    break;
                case "small":
                    modelFileName = "ggml-small.en.bin";
                    modelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin";
                    break;
                default:
                    Logger.Error($"Unknown model type: {modelType}");
                    return false;
            }

            // Check if model already exists
            var modelPath = FindModelPath(modelFileName);
            if (modelPath != null && File.Exists(modelPath))
            {
                Logger.Info($"Model {modelFileName} already exists at {modelPath}");
                return true;
            }

            // Download model
            return await DownloadModelAsync(modelFileName, modelUrl);
        }

        private static async Task<bool> DownloadModelAsync(string modelFileName, string modelUrl)
        {
            try
            {
                Logger.Info($"Downloading {modelFileName} from {modelUrl}...");

                var modelDir = Path.Combine(Environment.CurrentDirectory, "assets\\models");
                if (!Directory.Exists(modelDir))
                {
                    Directory.CreateDirectory(modelDir);
                    Logger.Info($"Created model directory: {modelDir}");
                }

                var targetPath = Path.Combine(modelDir, modelFileName);

                // Download using Whisper.NET's built-in downloader if available
                try
                {
                    var ggmlType = GetGgmlType(modelFileName);
                    if (ggmlType != null)
                    {
                        Logger.Info($"Using Whisper.NET downloader for {ggmlType}");
                        var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType.Value);
                        using var fileWriter = File.OpenWrite(targetPath);
                        await modelStream.CopyToAsync(fileWriter);
                        Logger.Info($"✅ Successfully downloaded {modelFileName} to {targetPath}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Whisper.NET downloader failed: {ex.Message}, trying direct download");
                }

                // Fallback to direct HTTP download
                using var response = await httpClient.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var buffer = new byte[8192];
                var bytesRead = 0L;

                using var fileStream = File.OpenWrite(targetPath);
                using var downloadStream = await response.Content.ReadAsStreamAsync();

                int read;
                while ((read = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    bytesRead += read;

                    if (totalBytes > 0)
                    {
                        var progress = (int)((bytesRead * 100) / totalBytes);
                        if (progress % 10 == 0)
                        {
                            Logger.Info($"Download progress: {progress}% ({bytesRead / (1024 * 1024)}MB / {totalBytes / (1024 * 1024)}MB)");
                        }
                    }
                }

                Logger.Info($"✅ Successfully downloaded {modelFileName} to {targetPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to download model {modelFileName}: {ex.Message}", ex);
                return false;
            }
        }

        private static GgmlType? GetGgmlType(string modelFileName)
        {
            if (modelFileName.Contains("tiny.en")) return GgmlType.TinyEn;
            if (modelFileName.Contains("tiny")) return GgmlType.Tiny;
            if (modelFileName.Contains("base.en")) return GgmlType.BaseEn;
            if (modelFileName.Contains("base")) return GgmlType.Base;
            if (modelFileName.Contains("small.en")) return GgmlType.SmallEn;
            if (modelFileName.Contains("small")) return GgmlType.Small;
            if (modelFileName.Contains("medium.en")) return GgmlType.MediumEn;
            if (modelFileName.Contains("medium")) return GgmlType.Medium;
            if (modelFileName.Contains("large")) return GgmlType.LargeV1;
            return null;
        }

        private static string FindModelPath(string modelFileName)
        {
            var searchPaths = new[]
            {
                Path.Combine(Environment.CurrentDirectory, "assets\\models", modelFileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets\\models", modelFileName),
                Path.Combine(Environment.CurrentDirectory, modelFileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelFileName)
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Compares model performance characteristics
        /// </summary>
        public static string GetModelInfo(string modelType)
        {
            return modelType.ToLower() switch
            {
                "tiny" => "Tiny (39M params): ~5x faster than base, lower accuracy",
                "base" => "Base (74M params): Good balance of speed and accuracy",
                "small" => "Small (244M params): Better accuracy, ~2x slower than base",
                "medium" => "Medium (769M params): High accuracy, ~5x slower than base",
                "large" => "Large (1550M params): Best accuracy, ~10x slower than base",
                _ => "Unknown model type"
            };
        }
    }
}