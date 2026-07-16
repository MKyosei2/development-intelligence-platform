#include "dip/LogAnalyzer.hpp"
#include <algorithm>
#include <string>

namespace dip
{
    std::vector<Issue> LogAnalyzer::Analyze(std::string_view text) const
    {
        std::string lower(text);
        std::transform(lower.begin(), lower.end(), lower.begin(),
                       [](unsigned char c) { return static_cast<char>(std::tolower(c)); });

        std::vector<Issue> issues;
        auto add = [&](std::string key, std::string category, std::string severity, std::string title)
        {
            if (lower.find(key) != std::string::npos)
                issues.push_back({category, severity, title, "Matched log pattern: " + key, "log"});
        };

        add("out of memory", "memory", "Critical", "Out of memory detected");
        add("segmentation fault", "native_crash", "Critical", "Segmentation fault detected");
        add("access violation", "native_crash", "Critical", "Access violation detected");
        add("tests failed", "test", "Warning", "Test failure detected");
        add("timeout", "timeout", "Warning", "Timeout detected");

        return issues;
    }
}
