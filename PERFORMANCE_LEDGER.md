# LUMINA PERFORMANCE TRACKING LEDGER
## Real-World Latency Measurements

### Test Protocol
- **Trigger**: Ctrl+Alt+Space hotkey
- **Speech**: Clear, normal speed English
- **Measurement**: From hotkey release to transcription display
- **Audio Duration**: Noted for each test

---

## PERFORMANCE HISTORY

### 2025-09-28 Session 1: Initial State Discovery
**Architecture**: Original unoptimized WhisperEngine
- Test: "Hello world" (1.0s) → **2,400ms latency**
- Test: "Testing testing" (1.2s) → **2,800ms latency**
- **Conclusion**: Baseline performance ~2,400-2,800ms

### 2025-09-28 Session 2: Research & Optimizations Applied
**Architecture**: OptimizedWhisperEngine with research-based configs
- Greedy decoding (temperature=0)
- No context
- Single segment
- Beam size = 1
- **Result**: Configuration applied but no tests run

### 2025-09-28 Session 3: First Real Improvement
**Architecture**: Unknown configuration (mentioned by user)
- Test: Unknown phrase → **~700-800ms latency** ✅
- **Note**: Best performance achieved but configuration lost

### 2025-09-28 Session 4: GPU Acceleration Confirmed
**Architecture**: OptimizedWhisperEngine with CUDA
- CUDA 581.29 detected
- GPU acceleration active
- Test: "Testing." (1.1s) → **2,696ms latency** ❌
- Test: "Hello, hello." (1.5s) → **2,148ms latency** ❌
- **Conclusion**: GPU enabled but optimizations not working

### 2025-09-28 Session 5: Benchmark Illusion
**Architecture**: Same as Session 4
- Synthetic benchmark: 58-115ms (but empty results)
- Real speech: 2,100-2,700ms
- **Discovery**: Benchmarks were testing silence, not speech

### 2025-09-28 Session 6: Tiny Model Success!
**Architecture**: WhisperEngine with tiny model (39M params)
- Model: ggml-tiny.en.bin (74MB vs 140MB base)
- GPU: CUDA 581.29 active
- Settings: UseTinyModelForSpeed=true
- Test: "Hello?" (1.2s) → **943ms** ✅
- Test: "Testing." (1.2s) → **1105ms**
- Test: Latest (1.0s) → **872ms** ✅
- **Conclusion**: 50% improvement! Now 872-1399ms (vs 2100-2700ms)

### 2025-10-01 Session 7: Major Dead Code Cleanup
**Architecture**: Deleted ~4,200 lines of non-functional code
- Removed: FasterWhisperEngine, HighPerformanceWhisperEngine, SpeculativeEngine
- Removed: All .disabled files, redundant benchmarks
- Removed: Broken native wrapper code
- Re-enabled: VAD with tuned thresholds (0.05 silence, 15% speech ratio)
- **Result**: Cleaner codebase, but VAD still too aggressive (blocking all speech)

### 2025-10-01 Session 8: Whisper.NET 1.8.1 Upgrade
**Architecture**: Upgraded packages for GPU support
- Whisper.net: 1.4.7 → 1.8.1
- Whisper.net.Runtime: 1.4.7 → 1.8.1
- Whisper.net.Runtime.Cuda: 1.7.0 → 1.8.1
- Logs show: "GPU: True", "CUDA GPU acceleration available"
- **Expectation**: Sub-200ms with GPU acceleration
- **Reality**: Still testing...

### 2025-10-01 Session 9: 🚨 CRITICAL GPU DISCOVERY
**Architecture**: Base model (140MB) + Whisper.NET 1.8.1 + VAD disabled
- **Tests with "GPU: True" in logs:**
  - "Hello, hello." (1.5s) → **1,101ms**
  - "Testing, testing." (1.0s) → **1,002ms**
  - "1,2,3..." (1.4s) → **1,088ms**
  - "Hello. Hello." (1.1s) → **1,080ms**

- **CRITICAL FINDING via nvidia-smi monitoring:**
  - GPU Utilization: **0%** during all transcriptions
  - GPU Memory Used: **0 MiB** (nothing allocated)
  - Power Draw: **12-18W** (idle only, not 50-100W for inference)

- **CONCLUSION**: ❌ **GPU IS NOT BEING USED!**
  - Whisper.NET 1.8.1 detects CUDA but runs on CPU
  - Logs incorrectly claim "GPU: True"
  - 1,000-1,100ms latency = typical CPU performance for base model
  - Automatic runtime selection is not working
  - Builder API doesn't expose GPU configuration

---

## KEY FINDINGS

### ✅ What Works
1. GPU/CUDA detection and initialization
2. Model loading and warmup
3. Audio capture pipeline
4. Transcription accuracy (Whisper model is good)
5. Base model: ~1,000-1,100ms on CPU
6. Tiny model: ~900ms on CPU (but less accurate)

### ❌ What's Broken
1. **GPU inference completely non-functional** - nvidia-smi proves 0% utilization
2. Whisper.NET 1.8.1 auto-selection chooses CPU despite CUDA available
3. VAD too aggressive (blocks all real speech)
4. Benchmark methodology was broken (synthetic audio)
5. Lost configuration that achieved 700-800ms

### 🎯 Target vs Reality
- **Target**: <200ms (requires GPU)
- **Best Achieved**: 700-800ms (configuration lost)
- **Current with base model**: 1,000-1,100ms (CPU only, no GPU)
- **Current with tiny model**: 872-1,399ms (CPU only)
- **Gap**: 5-7x slower than target due to CPU-only inference

---

## OPTIMIZATION ATTEMPTS

### Implemented Components (Not Yet Working)
1. **FasterWhisperEngine.cs** - Native CTranslate2 integration
2. **SileroVAD.cs** - Voice activity detection
3. **UltraLowLatencyPipeline.cs** - Parallel processing
4. **NativeWrapperBuilder.cs** - Build system for native libs

### Configuration Changes Attempted
1. Temperature = 0 (greedy decoding)
2. Beam size = 1
3. No context
4. Single segment
5. INT8 quantization (in theory)

---

## NEXT STEPS

### ✅ Completed Investigations
- [x] Confirmed GPU is NOT being used (nvidia-smi shows 0% utilization)
- [x] Deleted 4,200 lines of dead code (40% codebase reduction)
- [x] Upgraded to Whisper.NET 1.8.1 (but GPU still doesn't work)
- [x] Profiled inference bottleneck (99% in processor.ProcessAsync)

### 🚨 Critical Next Steps

#### Option A: Fix GPU Acceleration (Hard)
1. Research Whisper.NET source code for GPU configuration
2. Try using whisper.cpp directly via P/Invoke
3. Investigate ONNX Runtime with DirectML/CUDA (OnnxWhisperEngine exists)
4. Or accept that Whisper.NET can't do GPU on Windows

#### Option B: Alternative Approaches (Pragmatic)
1. **Use Deepgram cloud API** (already implemented, 200-300ms guaranteed)
2. **Accept CPU performance** (~1,000ms is reasonable for local inference)
3. **Hybrid approach**: Deepgram for speed, local for offline
4. **Different library**: faster-whisper Python via IPC

#### Option C: Optimize CPU Performance
1. Use tiny model by default (900ms vs 1,100ms)
2. Fix VAD thresholds (skip actual silence)
3. Implement speculative decoding
4. Multi-threaded audio processing

### Immediate TODO
- [ ] Research why Whisper.NET doesn't use GPU despite CUDA runtime
- [ ] Test ONNX Runtime approach (already have OnnxWhisperEngine.cs)
- [ ] Document that sub-200ms requires cloud API or different approach

---

## TEST COMMAND
```bash
# Run with real-time monitoring
dotnet run --configuration Release

# Test benchmarks
dotnet run --configuration Release -- --latency-benchmark
dotnet run --configuration Release -- --realistic-benchmark
```

---

*Last Updated: 2025-10-01 10:30 PM - Critical GPU finding documented*