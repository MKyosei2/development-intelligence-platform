#pragma once
#include "dip/CollectorTypes.hpp"
#include <string_view>

namespace dip
{
    class LogAnalyzer
    {
    public:
        std::vector<Issue> Analyze(std::string_view text) const;
    };
}
