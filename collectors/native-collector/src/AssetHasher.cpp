#include "dip/AssetHasher.hpp"
#include <fstream>
#include <iomanip>
#include <sstream>

namespace dip
{
    std::string AssetHasher::HashFile(const std::filesystem::path& path) const
    {
        std::ifstream stream(path, std::ios::binary);
        if (!stream) return {};

        std::uint64_t hash = 1469598103934665603ull;
        char buffer[8192];

        while (stream.read(buffer, sizeof(buffer)) || stream.gcount() > 0)
        {
            for (std::streamsize i = 0; i < stream.gcount(); ++i)
            {
                hash ^= static_cast<unsigned char>(buffer[i]);
                hash *= 1099511628211ull;
            }
        }

        std::ostringstream output;
        output << std::hex << std::setfill('0') << std::setw(16) << hash;
        return output.str();
    }
}
