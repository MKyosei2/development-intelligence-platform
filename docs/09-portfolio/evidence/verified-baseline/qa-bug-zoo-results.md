# DIP QA Bug Zoo Results

- Languages exercised: **7**
- Injected bugs: **19**
- Detected: **19/19 (100%)**
- Clean controls: **7**
- False positives: **0/7 (0%)**
- Reproduced: **9/9 (100%)**
- Infrastructure failures: **0**
- Platform-skipped cases: **0**
- Median case duration: **1867 ms**

| Language | Case | Expected | Detected | Reproduced | Infrastructure | Duration ms |
|---|---|---|---:|---:|---:|---:|
| cpp | buffer_overflow | memory-safety | yes | yes | ok | 10299 |
| cpp | clean | clean | yes | n/a | ok | 1873 |
| cpp | compile_error | compile | yes | n/a | ok | 301 |
| cpp | double_free | memory-safety | yes | yes | ok | 683 |
| cpp | memory_leak | memory-leak | yes | n/a | ok | 377 |
| cpp | use_after_free | memory-safety | yes | yes | ok | 1627 |
| csharp | async_warning | async | yes | n/a | ok | 8128 |
| csharp | clean | clean | yes | n/a | ok | 8274 |
| csharp | compile_error | compile | yes | n/a | ok | 7835 |
| csharp | divide_by_zero | runtime | yes | yes | ok | 24658 |
| csharp | hang | hang | yes | yes | ok | 37627 |
| csharp | null_reference | runtime | yes | yes | ok | 24775 |
| go | clean | clean | yes | n/a | ok | 24154 |
| go | compile_error | static-analysis | yes | n/a | ok | 128 |
| javascript | clean | clean | yes | n/a | ok | 132 |
| javascript | runtime_type_error | runtime | yes | yes | ok | 82 |
| python | async_unawaited | async | yes | n/a | ok | 2803 |
| python | clean | clean | yes | n/a | ok | 1538 |
| python | hang | hang | yes | yes | ok | 16929 |
| python | memory_growth | memory-growth | yes | n/a | ok | 1612 |
| python | runtime_type_error | runtime | yes | yes | ok | 1862 |
| python | syntax_error | syntax | yes | n/a | ok | 366 |
| rust | clean | clean | yes | n/a | ok | 9229 |
| rust | compile_error | compile | yes | n/a | ok | 2066 |
| typescript | clean | clean | yes | n/a | ok | 377 |
| typescript | compile_error | compile | yes | n/a | ok | 365 |
