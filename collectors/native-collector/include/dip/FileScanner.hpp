// Purpose: Defines a focused responsibility within the development intelligence platform.
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
