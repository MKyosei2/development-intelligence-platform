import time

retained = []
fps = 60.0
for second in range(180):
    retained.append(bytearray(512 * 1024))
    fps = max(20.0, fps - 0.15)
    frame_time = 1000.0 / fps
    print(f"DIP_METRIC fps={fps:.3f} frame_time_ms={frame_time:.3f}", flush=True)
    time.sleep(1)
