// Purpose: Implements Duplicate Detector responsibilities for dip.
#pragma once
#include "dip/CollectorTypes.hpp"
#include <unordered_map>

namespace dip
{
    class DuplicateDetector
    {
    public:
        std::vector<std::vector<FileEntry>> FindDuplicates(const std::vector<FileEntry>& files) const;
    };
}
