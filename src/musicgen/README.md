# ACE-Step Music Generation Container

Tiny FastAPI wrapper for ACE-Step 1.5 DiT-only text-to-music generation on CPU.

## Build and run

```bash
docker build -t musicgen-test src/musicgen
docker run --rm -p 8000:8000 -v musicgen-models:/models musicgen-test
```

Model weights are not baked into the image. They download on the first `/generate` call into `/models`.

## Environment

- `ACESTEP_CONFIG_PATH` default: `acestep-v15-turbo` (smallest 2B turbo DiT config found in ACE-Step 1.5)
- `ACESTEP_DEVICE` default: `cpu`
- `ACESTEP_INIT_LLM=false` disables the 5Hz language model (pure DiT mode)
- `ACESTEP_CHECKPOINTS_DIR` default: `/models/checkpoints`
- `HF_HOME` default: `/models/huggingface`
- `MUSICGEN_OUTPUT_DIR` default: `/models/outputs`
- `ACESTEP_INFERENCE_STEPS` default: `8`

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
  -d '{"prompt":"short upbeat synthwave instrumental","durationSeconds":8,"format":"mp3"}'
```
