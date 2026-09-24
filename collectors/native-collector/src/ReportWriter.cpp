// Purpose: Implements Report Writer responsibilities for native collector.
#include "dip/ReportWriter.hpp"
#include <fstream>

namespace
{
    std::string Escape(const std::string& value)
    {
        std::string result;
        for (char c : value)
        {
            if (c == '\\' || c == '"') result.push_back('\\');
            if (c == '\n') { result += "\\n"; continue; }
            result.push_back(c);
        }
        return result;
    }
}

namespace dip
{
    void ReportWriter::WriteJson(
        const std::filesystem::path& output,
        const std::vector<FileEntry>& files,
        const std::vector<std::vector<FileEntry>>& duplicates,
        const std::vector<Issue>& issues) const
    {
        std::filesystem::create_directories(output.parent_path());
        std::ofstream stream(output);

        std::uintmax_t totalBytes = 0;
        for (const auto& file : files) totalBytes += file.sizeBytes;

        stream << "{\n";
        stream << "  \"schemaVersion\": \"1.0\",\n";
        stream << "  \"collectorType\": \"native\",\n";
        stream << "  \"collectorVersion\": \"0.1.0\",\n";
        stream << "  \"metrics\": [\n";
        stream << "    {\"name\":\"file_count\",\"value\":" << files.size() << ",\"unit\":\"count\"},\n";
        stream << "    {\"name\":\"total_file_bytes\",\"value\":" << totalBytes << ",\"unit\":\"bytes\"},\n";
        stream << "    {\"name\":\"duplicate_group_count\",\"value\":" << duplicates.size() << ",\"unit\":\"count\"}\n";
        stream << "  ],\n";
        stream << "  \"issues\": [\n";
        for (std::size_t i = 0; i < issues.size(); ++i)
        {
            const auto& issue = issues[i];
            stream << "    {\"category\":\"" << Escape(issue.category)
                   << "\",\"severity\":\"" << Escape(issue.severity)
                   << "\",\"title\":\"" << Escape(issue.title)
                   << "\",\"description\":\"" << Escape(issue.description)
                   << "\",\"source\":\"" << Escape(issue.source) << "\"}";
            if (i + 1 < issues.size()) stream << ",";
            stream << "\n";
        }
        stream << "  ]\n";
        stream << "}\n";
    }
}
