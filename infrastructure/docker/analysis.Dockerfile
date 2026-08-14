FROM python:3.12-slim
WORKDIR /app
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1
COPY services/python-analysis/pyproject.toml /app/pyproject.toml
COPY services/python-analysis/app /app/app
RUN pip install --upgrade pip \
    && pip install .
RUN useradd --create-home --uid 10001 dipuser
USER dipuser
EXPOSE 8100
CMD ["python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8100"]
