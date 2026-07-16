#pragma once
#include <cstdint>
#include <filesystem>
#include <string>
#include <vector>

namespace dip
{
    struct FileEntry
    {
        std::filesystem::path path;
        std::uintmax_t sizeBytes{};
        std::string hash;
    };

    struct Issue
    {
        std::string category;
        std::string severity;
        std::string title;
        std::string description;
        std::string source;
    };
}
