#pragma once
#include "dip/CollectorTypes.hpp"

namespace dip
{
    class ReportWriter
    {
    public:
        void WriteJson(
            const std::filesystem::path& output,
            const std::vector<FileEntry>& files,
            const std::vector<std::vector<FileEntry>>& duplicates,
            const std::vector<Issue>& issues) const;
    };
}
