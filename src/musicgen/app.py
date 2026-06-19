import base64
import os
import random
import subprocess
import threading
import uuid
from pathlib import Path
from typing import Optional

os.environ.setdefault("ACESTEP_DEVICE", "cpu")
os.environ.setdefault("ACESTEP_CONFIG_PATH", "acestep-v15-turbo")
os.environ.setdefault("ACESTEP_INIT_LLM", "false")
os.environ.setdefault("ACESTEP_CHECKPOINTS_DIR", "/models/checkpoints")
os.environ.setdefault("HF_HOME", "/models/huggingface")
os.environ.setdefault("TORCHAUDIO_USE_BACKEND", "ffmpeg")
os.environ.setdefault("TOKENIZERS_PARALLELISM", "false")
os.environ.setdefault("ACE_STEP_SUPPRESS_AUDIO_TOKENS", "1")

from fastapi import FastAPI, HTTPException
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field

MODEL_NAME = os.getenv("ACESTEP_CONFIG_PATH", "acestep-v15-turbo")
DEVICE = os.getenv("ACESTEP_DEVICE", "cpu")
OUTPUT_DIR = Path(os.getenv("MUSICGEN_OUTPUT_DIR", "/models/outputs"))
PROJECT_ROOT = Path(os.getenv("ACESTEP_PROJECT_ROOT", "/app"))
MIN_DURATION = int(os.getenv("MUSICGEN_MIN_DURATION_SECONDS", "8"))
MAX_DURATION = int(os.getenv("MUSICGEN_MAX_DURATION_SECONDS", "60"))
INFERENCE_STEPS = int(os.getenv("ACESTEP_INFERENCE_STEPS", "8"))

app = FastAPI(title="ACE-Step MusicGen", version="1.0.0")
_handler = None
_model_loaded = False
_model_lock = threading.Lock()
_generate_lock = threading.Lock()


class GenerateRequest(BaseModel):
    prompt: str = Field(..., min_length=1)
    durationSeconds: int = 30
    bpm: Optional[int] = None
    format: str = "mp3"


@app.get("/health")
def health():
    return {"status": "ok", "modelLoaded": _model_loaded}


def _load_model():
    global _handler, _model_loaded
    if _model_loaded and _handler is not None:
        return _handler
    with _model_lock:
        if _model_loaded and _handler is not None:
            return _handler

        from acestep.gpu_config import get_gpu_config, set_global_gpu_config
        from acestep.handler import AceStepHandler

        set_global_gpu_config(get_gpu_config())
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        PROJECT_ROOT.mkdir(parents=True, exist_ok=True)

        handler = AceStepHandler()
        use_flash = False if DEVICE == "cpu" else handler.is_flash_attention_available(DEVICE)
        status, ok = handler.initialize_service(
            project_root=str(PROJECT_ROOT),
            config_path=MODEL_NAME,
            device=DEVICE,
            use_flash_attention=use_flash,
            compile_model=False,
            offload_to_cpu=False,
            offload_dit_to_cpu=False,
            quantization=None,
            prefer_source=os.getenv("ACESTEP_DOWNLOAD_SOURCE") or None,
        )
        if not ok:
            raise RuntimeError(status)
        _handler = handler
        _model_loaded = True
        return handler


def _coerce_duration(value: int) -> int:
    try:
        duration = int(value)
    except Exception:
        duration = 30
    return max(MIN_DURATION, min(MAX_DURATION, duration))


def _mp3_from_tensor(audio_tensor, sample_rate: int) -> bytes:
    import soundfile as sf

    stem = OUTPUT_DIR / f"fallback-{uuid.uuid4().hex}"
    wav_path = stem.with_suffix(".wav")
    mp3_path = stem.with_suffix(".mp3")
    try:
        tensor = audio_tensor.detach().cpu().float()
        array = tensor.numpy()
        if array.ndim == 2 and array.shape[0] <= 8:
            array = array.T
        sf.write(str(wav_path), array, sample_rate)
        subprocess.run(
            ["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav_path), "-codec:a", "libmp3lame", "-b:a", "128k", str(mp3_path)],
            check=True,
        )
        return mp3_path.read_bytes()
    finally:
        for path in (wav_path, mp3_path):
            try:
                path.unlink()
            except FileNotFoundError:
                pass


@app.post("/generate")
def generate(req: GenerateRequest):
    if req.format.lower() != "mp3":
        raise HTTPException(status_code=400, detail={"error": "Only mp3 format is supported"})

    try:
        from acestep.constants import DEFAULT_DIT_INSTRUCTION
        from acestep.inference import GenerationConfig, GenerationParams, generate_music

        duration = _coerce_duration(req.durationSeconds)
        seed = random.randint(0, 2**32 - 1)
        handler = _load_model()

        params = GenerationParams(
            task_type="text2music",
            instruction=DEFAULT_DIT_INSTRUCTION,
            caption=req.prompt.strip(),
            lyrics="[Instrumental]",
            instrumental=True,
            bpm=req.bpm,
            duration=float(duration),
            inference_steps=INFERENCE_STEPS,
            seed=seed,
            thinking=False,
            use_cot_metas=False,
            use_cot_caption=False,
            use_cot_language=False,
            use_constrained_decoding=False,
        )
        config = GenerationConfig(
            batch_size=1,
            use_random_seed=False,
            seeds=[seed],
            audio_format="mp3",
            mp3_bitrate="128k",
            mp3_sample_rate=48000,
        )

        with _generate_lock:
            result = generate_music(handler, None, params, config, save_dir=str(OUTPUT_DIR))

        if not result.success or not result.audios:
            raise RuntimeError(result.error or result.status_message or "ACE-Step generation failed")

        audio = result.audios[0]
        audio_path = audio.get("path")
        if audio_path and Path(audio_path).exists():
            audio_bytes = Path(audio_path).read_bytes()
        elif audio.get("tensor") is not None:
            audio_bytes = _mp3_from_tensor(audio["tensor"], int(audio.get("sample_rate", 48000)))
        else:
            raise RuntimeError("ACE-Step returned no audio file or tensor")

        actual_seed = audio.get("params", {}).get("seed", seed)
        return {
            "audioBase64": base64.b64encode(audio_bytes).decode("ascii"),
            "format": "mp3",
            "durationSeconds": duration,
            "seed": int(actual_seed),
            "model": MODEL_NAME,
        }
    except HTTPException:
        raise
    except Exception as exc:
        return JSONResponse(status_code=500, content={"error": str(exc)})
