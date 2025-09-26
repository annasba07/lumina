using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SuperWhisperWPF.Security
{
    /// <summary>
    /// Provides data protection utilities for encrypting sensitive information
    /// using Windows Data Protection API (DPAPI) for secure local storage.
    /// </summary>
    public static class DataProtection
    {
        private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("Lumina2025SecureAudio");

        /// <summary>
        /// Encrypts sensitive string data using DPAPI with current user scope.
        /// </summary>
        /// <param name="plainText">The text to encrypt</param>
        /// <returns>Base64 encoded encrypted string</returns>
        public static string ProtectString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] protectedBytes = ProtectedData.Protect(
                    plainBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser
                );
                return Convert.ToBase64String(protectedBytes);
            }
            catch (CryptographicException ex)
            {
                Logger.Error($"Failed to protect string: {ex.Message}", ex);
                return plainText; // Fallback to unprotected in case of error
            }
        }

        /// <summary>
        /// Decrypts a string that was encrypted using ProtectString.
        /// </summary>
        /// <param name="encryptedText">Base64 encoded encrypted string</param>
        /// <returns>Decrypted plain text</returns>
        public static string UnprotectString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                byte[] protectedBytes = Convert.FromBase64String(encryptedText);
                byte[] plainBytes = ProtectedData.Unprotect(
                    protectedBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser
                );
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to unprotect string: {ex.Message}", ex);
                return encryptedText; // Return as-is if it wasn't encrypted
            }
        }

        /// <summary>
        /// Encrypts audio data in memory for secure temporary storage.
        /// </summary>
        /// <param name="audioData">Raw audio bytes</param>
        /// <returns>Encrypted audio bytes</returns>
        public static byte[] ProtectAudioData(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return audioData;

            try
            {
                return ProtectedData.Protect(
                    audioData,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser
                );
            }
            catch (CryptographicException ex)
            {
                Logger.Error($"Failed to protect audio data: {ex.Message}", ex);
                return audioData; // Return unprotected if encryption fails
            }
        }

        /// <summary>
        /// Decrypts audio data that was encrypted using ProtectAudioData.
        /// </summary>
        /// <param name="encryptedData">Encrypted audio bytes</param>
        /// <returns>Decrypted audio bytes</returns>
        public static byte[] UnprotectAudioData(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                return encryptedData;

            try
            {
                return ProtectedData.Unprotect(
                    encryptedData,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser
                );
            }
            catch (CryptographicException ex)
            {
                Logger.Error($"Failed to unprotect audio data: {ex.Message}", ex);
                return encryptedData; // Return as-is if decryption fails
            }
        }

        /// <summary>
        /// Securely wipes sensitive data from memory.
        /// </summary>
        /// <param name="data">Byte array to clear</param>
        public static void SecureWipe(byte[] data)
        {
            if (data != null)
            {
                // Overwrite with random data first
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(data);
                }
                // Then clear
                Array.Clear(data, 0, data.Length);
            }
        }

        /// <summary>
        /// Creates a sanitized version of text for logging (removes sensitive patterns).
        /// </summary>
        /// <param name="text">Text to sanitize</param>
        /// <returns>Sanitized text safe for logging</returns>
        public static string SanitizeForLogging(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove potential sensitive patterns
            // Email addresses
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
                "[EMAIL]"
            );

            // Phone numbers
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b",
                "[PHONE]"
            );

            // Credit card patterns
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b",
                "[CARD]"
            );

            // SSN patterns
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\b\d{3}-\d{2}-\d{4}\b",
                "[SSN]"
            );

            return text;
        }
    }
}