using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SuperWhisperWPF.Security;
using SuperWhisperWPF.Core;

namespace SuperWhisperWPF
{
    /// <summary>
    /// Handles audio capture from the system microphone with optimized settings for Whisper transcription.
    /// Provides real-time audio level monitoring and automatic speech detection.
    /// </summary>
    public class AudioCapture : IDisposable
    {
        private WaveInEvent waveIn;
        private readonly List<byte> audioBuffer;
        private byte[] encryptedAudioData; // Store encrypted version
        private bool isRecording;
        private readonly object lockObject = new object();

        // Audio settings optimized for Whisper - using centralized constants
        private readonly int maxBufferSize;
        private bool warningShown = false;
        
        /// <summary>
        /// Fired when recording stops and audio data is ready for transcription.
        /// </summary>
        public event EventHandler<byte[]> SpeechEnded;

        /// <summary>
        /// Fired continuously during recording with normalized audio levels (0.0 to 1.0).
        /// </summary>
        public event EventHandler<float> AudioLevelChanged;

        /// <summary>
        /// Fired when recording approaches the maximum duration limit.
        /// Provides remaining seconds as event argument.
        /// </summary>
        public event EventHandler<int> ApproachingLimit;

        /// <summary>
        /// Initializes a new instance of the AudioCapture class with optimized settings for Whisper.
        /// </summary>
        public AudioCapture()
        {
            maxBufferSize = Constants.Audio.BYTES_PER_SECOND * Constants.Audio.MAX_RECORDING_SECONDS;
            audioBuffer = new List<byte>(maxBufferSize);
            InitializeAudioCapture();
        }

        private void InitializeAudioCapture()
        {
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(Constants.Audio.SAMPLE_RATE, Constants.Audio.CHANNELS),
                BufferMilliseconds = Constants.Audio.BUFFER_MILLISECONDS
            };
            
            waveIn.DataAvailable += OnDataAvailable;
        }

        /// <summary>
        /// Starts audio recording from the default microphone.
        /// Clears any previous audio buffer and begins capturing at 16kHz mono.
        /// </summary>
        public void StartRecording()
        {
            lock (lockObject)
            {
                if (isRecording) return;

                audioBuffer.Clear();
                warningShown = false; // Reset warning flag
                isRecording = true;

                try
                {
                    waveIn.StartRecording();
                    Logger.Info($"AudioCapture: Started recording (Device: {waveIn.DeviceNumber}, Format: {waveIn.WaveFormat})");
                }
                catch (Exception ex)
                {
                    isRecording = false;
                    Logger.Error($"Failed to start recording: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops audio recording and triggers the SpeechEnded event with captured audio.
        /// Encrypts audio data for security before storage.
        /// </summary>
        public void StopRecording()
        {
            lock (lockObject)
            {
                if (!isRecording) return;

                isRecording = false;
                waveIn.StopRecording();

                // Give a small delay to ensure all audio data is captured
                Thread.Sleep(100);

                if (audioBuffer.Count > 0)
                {
                    var audioData = audioBuffer.ToArray();
                    Logger.Info($"AudioCapture: Captured {audioData.Length} bytes ({audioData.Length / (float)Constants.Audio.BYTES_PER_SECOND:F1}s) of audio");

                    // Encrypt audio data before storing
                    encryptedAudioData = DataProtection.ProtectAudioData(audioData);
                    // Clear unencrypted buffer immediately
                    audioBuffer.Clear();
                    // Pass unencrypted data to event (will be encrypted again if stored)
                    SpeechEnded?.Invoke(this, audioData);
                    // Securely wipe the unencrypted data
                    DataProtection.SecureWipe(audioData);
                }
                else
                {
                    Logger.Warning("AudioCapture: No audio data captured");
                }
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (!isRecording) return;

            lock (lockObject)
            {
                // Check if we've reached max recording duration
                if (audioBuffer.Count + e.BytesRecorded > maxBufferSize)
                {
                    Logger.Warning($"Maximum recording duration of {Constants.Audio.MAX_RECORDING_SECONDS / 3600.0:F1} hours reached");
                    StopRecording();
                    return;
                }

                // Add audio data to buffer
                if (e.BytesRecorded > 0)
                {
                    audioBuffer.AddRange(e.Buffer.Take(e.BytesRecorded));

                    // Log every second for debugging
                    var currentSeconds = audioBuffer.Count / Constants.Audio.BYTES_PER_SECOND;
                    if (currentSeconds > 0 && currentSeconds % 1 == 0)
                    {
                        Logger.Debug($"Recording: {currentSeconds}s captured ({audioBuffer.Count} bytes)");
                    }
                }

                // Check if we should warn about approaching limit
                var remainingBytes = maxBufferSize - audioBuffer.Count;
                var remainingSeconds = remainingBytes / Constants.Audio.BYTES_PER_SECOND;

                if (!warningShown && remainingSeconds <= Constants.Audio.WARNING_SECONDS)
                {
                    warningShown = true;
                    var remainingMinutes = remainingSeconds / 60.0;
                    Logger.Warning($"Recording approaching limit - {remainingMinutes:F1} minutes remaining");

                    // Notify listeners about approaching limit
                    ApproachingLimit?.Invoke(this, (int)remainingSeconds);
                }
            }

            // Calculate audio level for visual feedback
            var audioLevel = CalculateAudioLevel(e.Buffer, e.BytesRecorded);
            AudioLevelChanged?.Invoke(this, audioLevel);
        }

        private float CalculateAudioLevel(byte[] buffer, int bytesRecorded)
        {
            if (bytesRecorded == 0) return 0;
            
            var max = 0f;
            for (int i = 0; i < bytesRecorded; i += 2)
            {
                if (i + 1 < bytesRecorded)
                {
                    var sample = Math.Abs(BitConverter.ToInt16(buffer, i));
                    max = Math.Max(max, sample);
                }
            }
            
            return max / 32768f; // Normalize to 0-1 range
        }


        /// <summary>
        /// Releases all resources used by the AudioCapture instance.
        /// Stops any active recording and disposes of the WaveIn device.
        /// </summary>
        public void Dispose()
        {
            try
            {
                StopRecording();
                waveIn?.Dispose();
                waveIn = null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disposing AudioCapture: {ex.Message}", ex);
            }
        }
    }

}