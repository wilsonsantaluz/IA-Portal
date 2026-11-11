# Local NVIDIA GPU access through Docker Desktop / WSL2

Verified2026-11-10 on the owner's workstation:

- NVIDIA GeForce MX450, 2048MiB reported by host nvidia-smi (2047MiB through CUDA).
- Windows NVIDIA driver581.80, WSL2.5.9.0/kernel6.6.87.2.
- Docker GPU passthrough works without changing host drivers, WSL settings or the
  running portal. A real CUDA kernel loaded via the driver API changed41.0 to42.0
  inside an isolated Linux container. Compute capability7.5 was reported.

## Reproduce the bounded smoke

```powershell
docker run --rm --gpus all --network none --mount type=bind,source=C:\source\IA-Portal\scripts\gpu_probe.py,target=/probe.py,readonly python:3.12-slim python /probe.py
```

The probe loads the injected CUDA driver, creates a context, allocates one float,
JIT-loads a small PTX kernel, launches it, synchronizes and copies back the checked
result. It frees memory/module/context on exit. No model, external API, training,
private mount or public GPU service is involved. This is compatibility validation,
not an inference benchmark or sustained GPU load; a utilization graph can stay
near0% because the operation is very short. The image was pulled before the
network-disabled test. No existing container was recreated or granted GPU access.

## Current application workloads

| Component | Current GPU behavior |
| --- | --- |
| ONNX embeddings/reranker in gateway | CPU NuGet package1.23.2 and default InferenceSession; CUDA provider not selected |
| Current documentary chat | Text retrieval plus external LLM where configured; local GPU does not run the remote model |
| OCR | Existing Tesseract CLI; no GPU backend configured |
| ML service | Existing .NET arithmetic endpoint; not a neural GPU pipeline |
| SQLite, Trino, Qdrant, API, SSH | No CUDA acceleration implemented by the portal |
| WhatsApp Chromium | No graphics/CUDA passthrough configured; CUDA compute support alone does not accelerate DOM automation |

The existing ONNX files are about522MiB and545MiB on disk. File size does not
establish VRAM requirements or compatible GPU operators. A useful next increment
would be an **optional CUDA execution provider for embeddings**, measuring actual
model/operator compatibility, latency and peak VRAM with small bounded batches.
It requires GPU-enabled ONNX Runtime plus compatible CUDA/cuDNN libraries and
container device requests. Adding only `gpus: all` to today's CPU image is not
accelerated inference. Do not silently enable advanced RAG or change the current
retrieval behavior just to increase GPU utilization.

The2GiB dedicated VRAM budget calls for small models and conservative concurrency;
loading multiple inference sessions may exhaust it. GPU memory does not replace
the workstation RAM used by WSL, databases and other services. The integrated
Intel GPU is a separate acceleration path; no OpenVINO/DirectML integration was
implemented or benchmarked in this check.

Primary references: [Docker Desktop NVIDIA GPU support](https://docs.docker.com/desktop/features/gpu/),
[NVIDIA CUDA on WSL](https://docs.nvidia.com/cuda/wsl-user-guide/index.html),
[ONNX Runtime CUDA provider requirements](https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html).
NVIDIA documents using the Windows driver exposed to WSL, not installing a Linux
display driver over it. This host already passes the compute smoke; no driver or
WSL upgrade was needed by this increment.
