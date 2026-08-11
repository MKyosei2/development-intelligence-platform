import pathlib
import sys

if len(sys.argv) != 2:
    raise SystemExit(2)
data = pathlib.Path(sys.argv[1]).read_bytes()
if data.startswith(b"DIP_CRASH") or (len(data) >= 4 and data[:4] == b"\xff\xff\xff\xff"):
    raise RuntimeError("intentional fuzz fixture crash")
print(len(data))
