#include "dip/DuplicateDetector.hpp"
#include <iostream>

int main_duplicate_detector_test()
{
    std::vector<dip::FileEntry> files = {
        {"a", 1, "same"},
        {"b", 1, "same"},
        {"c", 1, "other"}
    };
    dip::DuplicateDetector detector;
    const auto groups = detector.FindDuplicates(files);
    if (groups.size() != 1 || groups[0].size() != 2)
    {
        std::cerr << "DuplicateDetector test failed\n";
        return 1;
    }
    return 0;
}
