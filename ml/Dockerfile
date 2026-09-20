# Build context: ml/ — this image is self-contained and deployable independently of the .NET services.
FROM python:3.12-slim AS base
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends libgomp1 curl \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app
COPY tests ./tests

ENV BKEEPER_ML_MODELS_DIR=/app/models
RUN mkdir -p /app/models

EXPOSE 8090
HEALTHCHECK --interval=5s --timeout=3s --start-period=10s --retries=10 CMD curl -f http://localhost:8090/health || exit 1
CMD ["uvicorn", "app.serve:app", "--host", "0.0.0.0", "--port", "8090"]
