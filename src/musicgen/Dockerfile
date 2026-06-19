FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    HF_HOME=/models/huggingface \
    ACESTEP_PROJECT_ROOT=/app \
    ACESTEP_CHECKPOINTS_DIR=/models/checkpoints \
    ACESTEP_DEVICE=cpu \
    ACESTEP_CONFIG_PATH=acestep-v15-turbo \
    ACESTEP_INIT_LLM=false \
    ACESTEP_LM_BACKEND=pt \
    ACESTEP_NO_INIT=true \
    ACESTEP_DISABLE_TQDM=true \
    MUSICGEN_OUTPUT_DIR=/models/outputs \
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

# Pre-download the ACE-Step 1.5 model (~10GB, 28 files) into the image at build time.
# The model then lives in read-only image layers, so at runtime it neither re-downloads
# nor counts against the container's ephemeral storage limit (which evicted the pod when
# the 10GB snapshot was downloaded at runtime). Cold starts load the model from local disk.
RUN python -c "from huggingface_hub import snapshot_download; snapshot_download('ACE-Step/Ace-Step1.5', local_dir='/models/checkpoints', max_workers=8)" \
    && rm -rf /models/checkpoints/.cache /root/.cache/huggingface

COPY app.py .

RUN python -c "import app"

EXPOSE 8000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8000/health || exit 1

CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000"]
