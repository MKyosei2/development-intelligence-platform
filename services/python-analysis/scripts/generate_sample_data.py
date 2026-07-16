import json
import random
from datetime import UTC, datetime, timedelta
from pathlib import Path

def main() -> None:
    random.seed(42)
    start = datetime.now(UTC) - timedelta(days=30)
    points = []
    value = 120.0
    for index in range(30):
        value += random.uniform(-4, 4)
        if index == 24:
            value += 45
        points.append({"timestamp": (start + timedelta(days=index)).isoformat(), "value": round(value, 2)})
    output = Path("data/samples/build_duration.json")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({"metric_name": "build_duration", "points": points}, indent=2), encoding="utf-8")
    print(output)

if __name__ == "__main__":
    main()
