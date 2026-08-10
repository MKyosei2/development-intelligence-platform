# Intentionally retains enough memory for the P1 tracemalloc probe to flag it.
retained = [bytearray(1024 * 1024) for _ in range(80)]
print("retained_mb=80")
