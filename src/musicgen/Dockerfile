FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    HF_HOME=/models/huggingface \
    ACESTEP_PROJECT_ROOT=/app \
    ACESTEP_CHECKPOINTS_DIR=/models/checkpoints \
    ACESTEP_DEVICE=auto \
    ACESTEP_CONFIG_PATH=acestep-v15-turbo \
    ACESTEP_DOWNLOAD_SOURCE=huggingface \
    ACESTEP_INIT_LLM=false \
    ACESTEP_LM_BACKEND=pt \
    ACESTEP_NO_INIT=true \
    ACESTEP_DISABLE_TQDM=true \
    ACESTEP_INFERENCE_STEPS=8 \
    ACESTEP_INFERENCE_SHIFT=3.0 \
    MUSICGEN_OUTPUT_DIR=/models/outputs \
    MUSICGEN_PRELOAD_MODEL=true \
    MUSICGEN_CPU_THREADS=4 \
    OMP_NUM_THREADS=4 \
    MKL_NUM_THREADS=4 \
    TORCHAUDIO_USE_BACKEND=ffmpeg \
    TOKENIZERS_PARALLELISM=false

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg git build-essential curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .

RUN python -m pip install --upgrade pip \
    && python -m pip install --index-url https://download.pytorch.org/whl/cpu torch torchvision torchaudio \
    && python -m pip install -r requirements.txt \
    && python -m pip install --no-deps "git+https://github.com/ace-step/ACE-Step-1.5.git" \
    && mkdir -p /models/checkpoints /models/huggingface /models/outputs

# Pre-download the ACE-Step 1.5 model (~10GB) through ACE-Step's downloader so the
# checkpoint layout and model-code sync match the runtime loader. The model lives
# in image layers, so runtime startup loads from local disk instead of re-downloading.
RUN python -c "import os; from acestep.model_downloader import download_main_model; ok, msg = download_main_model('/models/checkpoints', prefer_source=os.getenv('ACESTEP_DOWNLOAD_SOURCE') or None); print(msg); raise SystemExit(0 if ok else 1)" \
    && rm -rf /models/checkpoints/.cache /root/.cache/huggingface

COPY app.py .

RUN python -c "import app"

EXPOSE 8000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8000/health || exit 1

CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000"]
