from contextlib import asynccontextmanager
import logging
from fastapi import FastAPI
from app.api.routes import anomaly, health, release_prediction, risk, summary
from app.core.config import get_settings
from app.core.logging import configure_logging

@asynccontextmanager
async def lifespan(_: FastAPI):
    settings = get_settings()
    configure_logging(settings.log_level)
    logging.getLogger(__name__).info("Starting %s in %s mode.", settings.app_name, settings.app_env)
    yield
    logging.getLogger(__name__).info("Stopping analysis service.")

settings = get_settings()
app = FastAPI(title=settings.app_name, version="0.1.0", lifespan=lifespan)
app.include_router(health.router)
app.include_router(anomaly.router)
app.include_router(risk.router)
app.include_router(summary.router)
app.include_router(release_prediction.router)
