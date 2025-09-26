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
        
        public event EventHandler<byte[]> SpeechEnded;
        public event EventHandler<float> AudioLevelChanged;
        public event EventHandler<int> ApproachingLimit; // Fired when nearing max duration

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

        public void StartRecording()
        {
            lock (lockObject)
            {
                if (isRecording) return;
                
                audioBuffer.Clear();
                warningShown = false; // Reset warning flag
                isRecording = true;
                waveIn.StartRecording();
            }
        }

        public void StopRecording()
        {
            lock (lockObject)
            {
                if (!isRecording) return;

                isRecording = false;
                waveIn.StopRecording();

                if (audioBuffer.Count > 0)
                {
                    var audioData = audioBuffer.ToArray();
                    // Encrypt audio data before storing
                    encryptedAudioData = DataProtection.ProtectAudioData(audioData);
                    // Clear unencrypted buffer immediately
                    audioBuffer.Clear();
                    // Pass unencrypted data to event (will be encrypted again if stored)
                    SpeechEnded?.Invoke(this, audioData);
                    // Securely wipe the unencrypted data
                    DataProtection.SecureWipe(audioData);
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
                audioBuffer.AddRange(e.Buffer.Take(e.BytesRecorded));

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