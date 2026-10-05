# Betting Desk: one container, one worker. The Odds API key stays in the
# server's environment; the browser only ever talks to this app.
FROM python:3.13-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

WORKDIR /app
COPY requirements.txt .
RUN pip install -r requirements.txt

COPY app ./app
COPY static ./static

# Line history, alerts and the scheduler's spend live in /app/data. Mount a
# volume there if you want them to survive a redeploy.
RUN useradd --create-home --uid 10001 desk && mkdir -p /app/data && chown desk /app/data
USER desk
VOLUME ["/app/data"]

EXPOSE 8000
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s \
  CMD python -c "import os,urllib.request;urllib.request.urlopen('http://127.0.0.1:%s/healthz' % os.getenv('PORT','8000'),timeout=4)"

# One worker on purpose: each worker would run its own scheduler and spend
# its own daily budget. PORT is set by Render/Railway; 8000 locally.
CMD ["sh", "-c", "exec uvicorn app.main:app --host 0.0.0.0 --port ${PORT:-8000} --workers 1 --proxy-headers --forwarded-allow-ips='*'"]
