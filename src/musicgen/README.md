# ACE-Step Music Generation Container

FastAPI wrapper for ACE-Step 1.5 DiT-only text-to-music generation. It defaults to
ACE-Step's 2B turbo model with 8 inference steps, `shift=3.0`, ODE/Euler sampling,
and no LLM/ADG path for lower latency. `ACESTEP_DEVICE=auto` uses the best available
device for the installed PyTorch runtime; the default Dockerfile installs CPU PyTorch.

## Build and run

```bash
docker build -t musicgen-test src/musicgen
docker run --rm -p 8000:8000 musicgen-test
```

The Docker build downloads the main ACE-Step 1.5 model into the image through ACE-Step's
own downloader, so runtime generation loads local checkpoints instead of downloading on
the first `/generate` request.

## Environment

- `ACESTEP_CONFIG_PATH` default: `acestep-v15-turbo` (smallest 2B turbo DiT config found in ACE-Step 1.5)
- `ACESTEP_DEVICE` default: `auto`
- `ACESTEP_INIT_LLM=false` disables the 5Hz language model (pure DiT mode)
- `ACESTEP_CHECKPOINTS_DIR` default: `/models/checkpoints`
- `HF_HOME` default: `/models/huggingface`
- `MUSICGEN_OUTPUT_DIR` default: `/models/outputs`
- `ACESTEP_INFERENCE_STEPS` default: `8`
- `ACESTEP_INFERENCE_SHIFT` default: `3.0` (recommended for turbo models)
- `MUSICGEN_PRELOAD_MODEL` default: `true`

## HTTP contract

`GET /health` returns:

```json
{"status":"ok","modelLoaded":false}
```

`POST /generate` accepts:

```json
{"prompt":"lo-fi jazz groove", "durationSeconds":30, "bpm":90, "format":"mp3"}
```

and returns:

```json
{"audioBase64":"...", "format":"mp3", "durationSeconds":30, "seed":123, "model":"acestep-v15-turbo"}
```

Example:

```bash
curl -X POST http://localhost:8000/generate \
  -H 'content-type: application/json' \
  -d '{"prompt":"short upbeat synthwave instrumental","durationSeconds":10,"format":"mp3"}'
```
