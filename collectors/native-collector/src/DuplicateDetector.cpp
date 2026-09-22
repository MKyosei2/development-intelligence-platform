// Purpose: Defines a focused responsibility within the development intelligence platform.
#include "dip/DuplicateDetector.hpp"

namespace dip
{
    std::vector<std::vector<FileEntry>> DuplicateDetector::FindDuplicates(
        const std::vector<FileEntry>& files) const
    {
        std::unordered_map<std::string, std::vector<FileEntry>> groups;
        for (const auto& file : files)
        {
            if (!file.hash.empty())
                groups[file.hash].push_back(file);
        }

        std::vector<std::vector<FileEntry>> duplicates;
        for (auto& [_, group] : groups)
        {
            if (group.size() > 1)
                duplicates.push_back(std::move(group));
        }

        return duplicates;
    }
}
