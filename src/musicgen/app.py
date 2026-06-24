import base64
import logging
import os
import random
import subprocess
import threading
import uuid
from pathlib import Path
from typing import Optional

os.environ.setdefault("ACESTEP_DEVICE", "auto")
os.environ.setdefault("ACESTEP_CONFIG_PATH", "acestep-v15-turbo")
os.environ.setdefault("ACESTEP_INIT_LLM", "false")
os.environ.setdefault("ACESTEP_CHECKPOINTS_DIR", "/models/checkpoints")
os.environ.setdefault("HF_HOME", "/models/huggingface")
os.environ.setdefault("OMP_NUM_THREADS", os.getenv("MUSICGEN_CPU_THREADS", "4"))
os.environ.setdefault("MKL_NUM_THREADS", os.getenv("MUSICGEN_CPU_THREADS", "4"))
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
MIN_DURATION = int(os.getenv("MUSICGEN_MIN_DURATION_SECONDS", "10"))
MAX_DURATION = int(os.getenv("MUSICGEN_MAX_DURATION_SECONDS", "60"))
INFERENCE_STEPS = int(os.getenv("ACESTEP_INFERENCE_STEPS", "8"))
INFERENCE_SHIFT = float(os.getenv("ACESTEP_INFERENCE_SHIFT", "3.0"))
INFER_METHOD = os.getenv("ACESTEP_INFER_METHOD", "ode")
SAMPLER_MODE = os.getenv("ACESTEP_SAMPLER_MODE", "euler")
MP3_BITRATE = os.getenv("MUSICGEN_MP3_BITRATE", "128k")
MP3_SAMPLE_RATE = int(os.getenv("MUSICGEN_MP3_SAMPLE_RATE", "48000"))

logger = logging.getLogger("musicgen")

app = FastAPI(title="ACE-Step MusicGen", version="1.0.0")
_handler = None
_model_loaded = False
_model_loading = False
_model_lock = threading.Lock()
_generate_lock = threading.Lock()


class GenerateRequest(BaseModel):
    prompt: str = Field(..., min_length=1)
    durationSeconds: int = 15
    bpm: Optional[int] = None
    format: str = "mp3"


@app.get("/health")
def health():
    return {"status": "ok", "modelLoaded": _model_loaded, "modelLoading": _model_loading}


@app.on_event("startup")
def startup():
    if _env_bool("MUSICGEN_PRELOAD_MODEL", True):
        threading.Thread(target=_preload_model, name="musicgen-model-preload", daemon=True).start()


def _env_bool(name: str, default: bool) -> bool:
    value = os.getenv(name)
    if value is None:
        return default
    return value.strip().lower() in {"1", "true", "yes", "on"}


def _preload_model():
    try:
        _load_model()
    except Exception:
        logger.exception("ACE-Step model preload failed")


def _load_model():
    global _handler, _model_loaded, _model_loading
    if _model_loaded and _handler is not None:
        return _handler
    with _model_lock:
        if _model_loaded and _handler is not None:
            return _handler

        _model_loading = True
        from acestep.gpu_config import get_gpu_config, set_global_gpu_config
        from acestep.handler import AceStepHandler

        try:
            set_global_gpu_config(get_gpu_config())
            OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
            PROJECT_ROOT.mkdir(parents=True, exist_ok=True)

            handler = AceStepHandler()
            device = DEVICE.strip().lower() or "auto"
            use_flash = (
                _env_bool("ACESTEP_USE_FLASH_ATTENTION", handler.is_flash_attention_available(device))
                if device != "cpu"
                else False
            )
            quantization = os.getenv("ACESTEP_QUANTIZATION") or None
            status, ok = handler.initialize_service(
                project_root=str(PROJECT_ROOT),
                config_path=MODEL_NAME,
                device=device,
                use_flash_attention=use_flash,
                compile_model=_env_bool("ACESTEP_COMPILE_MODEL", False),
                offload_to_cpu=_env_bool("ACESTEP_OFFLOAD_TO_CPU", False),
                offload_dit_to_cpu=_env_bool("ACESTEP_OFFLOAD_DIT_TO_CPU", False),
                quantization=quantization,
                prefer_source=os.getenv("ACESTEP_DOWNLOAD_SOURCE") or None,
            )
            if not ok:
                raise RuntimeError(status)
            _handler = handler
            _model_loaded = True
            return handler
        finally:
            _model_loading = False


def _coerce_duration(value: int) -> int:
    duration = int(value)
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
            [
                "ffmpeg",
                "-y",
                "-loglevel",
                "error",
                "-i",
                str(wav_path),
                "-codec:a",
                "libmp3lame",
                "-ar",
                str(MP3_SAMPLE_RATE),
                "-b:a",
                MP3_BITRATE,
                str(mp3_path),
            ],
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
            use_adg=False,
            shift=INFERENCE_SHIFT,
            infer_method=INFER_METHOD,
            sampler_mode=SAMPLER_MODE,
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
            mp3_bitrate=MP3_BITRATE,
            mp3_sample_rate=MP3_SAMPLE_RATE,
        )

        with _generate_lock:
            import torch

            with torch.inference_mode():
                result = generate_music(handler, None, params, config, save_dir=str(OUTPUT_DIR))

        if not result.success or not result.audios:
            raise RuntimeError(result.error or result.status_message or "ACE-Step generation failed")

        audio = result.audios[0]
        audio_path = audio.get("path")
        if audio_path and Path(audio_path).exists():
            generated_path = Path(audio_path)
            audio_bytes = generated_path.read_bytes()
            if _env_bool("MUSICGEN_DELETE_OUTPUT_AFTER_RESPONSE", True):
                try:
                    generated_path.unlink(missing_ok=True)
                except OSError:
                    logger.warning("Could not delete generated audio file %s", generated_path)
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
        logger.exception("ACE-Step generation failed")
        return JSONResponse(status_code=500, content={"error": str(exc)})
