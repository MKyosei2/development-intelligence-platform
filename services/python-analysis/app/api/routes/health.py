# Purpose: Defines a focused responsibility within the development intelligence platform.
from fastapi import APIRouter

router = APIRouter(tags=["health"])


@router.get("/health")
def health() -> dict[str, str]:
    return {"status": "healthy"}
