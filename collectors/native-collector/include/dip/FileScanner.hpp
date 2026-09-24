// Purpose: Implements File Scanner responsibilities for dip.
#pragma once
#include "dip/CollectorTypes.hpp"

namespace dip
{
    class FileScanner
    {
    public:
        std::vector<FileEntry> Scan(const std::filesystem::path& root) const;
    };
}
