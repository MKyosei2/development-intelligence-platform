// Purpose: Defines a focused responsibility within the development intelligence platform.
#include "dip/FileScanner.hpp"
#include "dip/AssetHasher.hpp"

namespace dip
{
    std::vector<FileEntry> FileScanner::Scan(const std::filesystem::path& root) const
    {
        std::vector<FileEntry> result;
        AssetHasher hasher;

        if (!std::filesystem::exists(root)) return result;

        for (const auto& entry : std::filesystem::recursive_directory_iterator(
                 root, std::filesystem::directory_options::skip_permission_denied))
        {
            if (!entry.is_regular_file()) continue;
            result.push_back(FileEntry{
                entry.path(),
                entry.file_size(),
                hasher.HashFile(entry.path())
            });
        }

        return result;
    }
}
