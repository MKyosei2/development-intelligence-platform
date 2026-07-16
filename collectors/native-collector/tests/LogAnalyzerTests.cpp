#include "dip/LogAnalyzer.hpp"
#include <iostream>

int main_file_scanner_test();
int main_duplicate_detector_test();

int main()
{
    if (main_file_scanner_test() != 0) return 1;
    if (main_duplicate_detector_test() != 0) return 1;

    dip::LogAnalyzer analyzer;
    const auto issues = analyzer.Analyze("fatal: out of memory");
    if (issues.empty() || issues.front().severity != "Critical")
    {
        std::cerr << "LogAnalyzer test failed\n";
        return 1;
    }

    return 0;
}
