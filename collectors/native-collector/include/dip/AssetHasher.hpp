// Purpose: Implements Asset Hasher responsibilities for dip.
#pragma once
#include <filesystem>
#include <string>

namespace dip
{
    class AssetHasher
    {
    public:
        std::string HashFile(const std::filesystem::path& path) const;
    };
}
