# Purpose: Defines a focused responsibility within the development intelligence platform.
import hmac

from fastapi import Header, HTTPException, status

from app.core.config import get_settings


def verify_api_key(x_api_key: str | None = Header(default=None)) -> None:
    settings = get_settings()
    configured_key = settings.api_key.strip()

    # Development may intentionally omit a key for local iteration. Production cannot
    # reach this state because Settings validates it at process startup. Keep a second
    # fail-closed guard here so future initialization changes cannot reopen the route.
    if not configured_key:
        if settings.app_env.strip().lower() in {"production", "prod"}:
            raise HTTPException(
                status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
                detail="Analysis API authentication is not configured.",
            )
        return

    if x_api_key is None or not hmac.compare_digest(x_api_key, configured_key):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid API key.")
