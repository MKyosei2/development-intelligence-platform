# Purpose: Defines a focused responsibility within the development intelligence platform.
from app.schemas.common import EvidenceItem


class EvidenceBuilder:
    @staticmethod
    def ids(items: list[EvidenceItem]) -> list[str]:
        seen: set[str] = set()
        result: list[str] = []
        for item in items:
            if item.evidence_id not in seen:
                result.append(item.evidence_id)
                seen.add(item.evidence_id)
        return result
