from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    app_name: str = "DIP Analysis Service"
    app_env: str = "development"
    log_level: str = "INFO"
    api_key: str = ""
    anomaly_zscore_threshold: float = 2.5
    release_base_confidence: float = 90.0
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()
