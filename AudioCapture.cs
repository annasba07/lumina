using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SuperWhisperWPF
{
    public class AudioCapture : IDisposable
    {
        private WaveInEvent waveIn;
        private readonly List<byte> audioBuffer;
        private bool isRecording;
        private readonly object lockObject = new object();
        
        // Audio settings optimized for Whisper
        private const int SAMPLE_RATE = 16000;
        private const int CHANNELS = 1;
        
        public event EventHandler<byte[]> SpeechEnded;
        public event EventHandler<float> AudioLevelChanged;

        public AudioCapture()
        {
            audioBuffer = new List<byte>();
            InitializeAudioCapture();
        }

        private void InitializeAudioCapture()
        {
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SAMPLE_RATE, CHANNELS),
                BufferMilliseconds = 50 // Low latency
            };
            
            waveIn.DataAvailable += OnDataAvailable;
        }

        public void StartRecording()
        {
            lock (lockObject)
            {
                if (isRecording) return;
                
                audioBuffer.Clear();
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
                    SpeechEnded?.Invoke(this, audioData);
                }
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (!isRecording) return;

            // Add audio data to buffer
            audioBuffer.AddRange(e.Buffer.Take(e.BytesRecorded));
            
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
            StopRecording();
            waveIn?.Dispose();
        }
    }

}