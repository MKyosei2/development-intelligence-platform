// Volatile access keeps the intentionally leaked allocation observable in optimized
// builds without retaining it in a global root that LeakSanitizer would classify as reachable.
int main()
{
    volatile int* p = new int[4096];
    p[0] = 42;
    return p[0] == 42 ? 0 : 1;
}
