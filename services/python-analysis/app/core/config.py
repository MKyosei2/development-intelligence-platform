from functools import lru_cache

from pydantic import model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    app_name: str = "DIP Analysis Service"
    app_env: str = "development"
    log_level: str = "INFO"
    api_key: str = ""
    anomaly_zscore_threshold: float = 2.5
    release_base_confidence: float = 90.0
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    @model_validator(mode="after")
    def validate_production_security(self) -> "Settings":
        # The analysis service is internal-only in the reference deployment, but its
        # analytical routes still fail closed in production. An omitted key is a
        # configuration error, not an implicit request to disable authentication.
        if self.app_env.strip().lower() in {"production", "prod"} and not self.api_key.strip():
            raise ValueError("API_KEY must be configured when APP_ENV=production")
        return self


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()
