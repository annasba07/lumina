using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SuperWhisperWPF.Core;
using Microsoft.Win32;

namespace SuperWhisperWPF.Services
{
    /// <summary>
    /// Provides export functionality for transcriptions to various file formats.
    /// Supports TXT, Markdown, and JSON export formats.
    /// </summary>
    public static class ExportService
    {
        /// <summary>
        /// Supported export formats
        /// </summary>
        public enum ExportFormat
        {
            Text,
            Markdown,
            Json
        }

        /// <summary>
        /// Transcription data model for export
        /// </summary>
        public class TranscriptionData
        {
            public string Text { get; set; }
            public DateTime Timestamp { get; set; }
            public int WordCount { get; set; }
            public TimeSpan Duration { get; set; }
            public string ApplicationVersion { get; set; }
        }

        /// <summary>
        /// Shows a save dialog and exports the transcription to the selected file.
        /// </summary>
        /// <param name="transcription">The transcription data to export.</param>
        /// <returns>True if export succeeded, false if cancelled or failed.</returns>
        public static async Task<bool> ExportWithDialogAsync(TranscriptionData transcription)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export Transcription",
                FileName = $"transcription_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}",
                Filter = "Text Files (*.txt)|*.txt|Markdown Files (*.md)|*.md|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".txt",
                AddExtension = true
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var format = DetermineFormat(dialog.FileName);
                    await ExportAsync(transcription, dialog.FileName, format);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Export failed: {ex.Message}", ex);
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Exports transcription to a file in the specified format.
        /// </summary>
        /// <param name="transcription">The transcription data to export.</param>
        /// <param name="filePath">The target file path.</param>
        /// <param name="format">The export format.</param>
        public static async Task ExportAsync(TranscriptionData transcription, string filePath, ExportFormat format)
        {
            string content = format switch
            {
                ExportFormat.Markdown => GenerateMarkdown(transcription),
                ExportFormat.Json => GenerateJson(transcription),
                _ => GenerateText(transcription)
            };

            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
            Logger.Info($"Transcription exported to {filePath} as {format}");
        }

        /// <summary>
        /// Exports transcription to clipboard in the specified format.
        /// </summary>
        /// <param name="transcription">The transcription data to export.</param>
        /// <param name="format">The export format.</param>
        public static void ExportToClipboard(TranscriptionData transcription, ExportFormat format)
        {
            string content = format switch
            {
                ExportFormat.Markdown => GenerateMarkdown(transcription),
                ExportFormat.Json => GenerateJson(transcription),
                _ => GenerateText(transcription)
            };

            System.Windows.Clipboard.SetText(content);
            Logger.Info($"Transcription copied to clipboard as {format}");
        }

        /// <summary>
        /// Generates plain text export content.
        /// </summary>
        private static string GenerateText(TranscriptionData transcription)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=====================================");
            sb.AppendLine($"Lumina Transcription");
            sb.AppendLine("=====================================");
            sb.AppendLine();
            sb.AppendLine($"Date: {transcription.Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Duration: {transcription.Duration:mm\\:ss}");
            sb.AppendLine($"Word Count: {transcription.WordCount}");
            sb.AppendLine();
            sb.AppendLine("Transcription:");
            sb.AppendLine("--------------");
            sb.AppendLine(transcription.Text);
            sb.AppendLine();
            sb.AppendLine("=====================================");
            sb.AppendLine($"Generated with Lumina v{transcription.ApplicationVersion}");

            return sb.ToString();
        }

        /// <summary>
        /// Generates Markdown export content.
        /// </summary>
        private static string GenerateMarkdown(TranscriptionData transcription)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Lumina Transcription");
            sb.AppendLine();
            sb.AppendLine("## Metadata");
            sb.AppendLine();
            sb.AppendLine($"- **Date:** {transcription.Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- **Duration:** {transcription.Duration:mm\\:ss}");
            sb.AppendLine($"- **Word Count:** {transcription.WordCount}");
            sb.AppendLine($"- **Application:** Lumina v{transcription.ApplicationVersion}");
            sb.AppendLine();
            sb.AppendLine("## Transcription");
            sb.AppendLine();
            sb.AppendLine(transcription.Text);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"*Generated with [Lumina]({Constants.App.GITHUB_URL}) on {DateTime.Now:yyyy-MM-dd}*");

            return sb.ToString();
        }

        /// <summary>
        /// Generates JSON export content.
        /// </summary>
        private static string GenerateJson(TranscriptionData transcription)
        {
            var exportData = new
            {
                metadata = new
                {
                    application = Constants.App.NAME,
                    version = transcription.ApplicationVersion,
                    exportDate = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK"),
                    source = Constants.App.GITHUB_URL
                },
                transcription = new
                {
                    text = transcription.Text,
                    timestamp = transcription.Timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK"),
                    duration = transcription.Duration.TotalSeconds,
                    wordCount = transcription.WordCount,
                    characterCount = transcription.Text?.Length ?? 0
                }
            };

            return JsonConvert.SerializeObject(exportData, Formatting.Indented);
        }

        /// <summary>
        /// Determines the export format based on file extension.
        /// </summary>
        private static ExportFormat DetermineFormat(string filePath)
        {
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension switch
            {
                ".md" => ExportFormat.Markdown,
                ".json" => ExportFormat.Json,
                _ => ExportFormat.Text
            };
        }

        /// <summary>
        /// Gets a suggested filename based on the current timestamp.
        /// </summary>
        public static string GetSuggestedFileName(ExportFormat format)
        {
            var extension = format switch
            {
                ExportFormat.Markdown => ".md",
                ExportFormat.Json => ".json",
                _ => ".txt"
            };

            return $"lumina_transcription_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{extension}";
        }
    }
}