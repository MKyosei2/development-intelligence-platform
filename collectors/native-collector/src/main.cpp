// Purpose: Defines a focused responsibility within the development intelligence platform.
#include "dip/DuplicateDetector.hpp"
#include "dip/FileScanner.hpp"
#include "dip/LogAnalyzer.hpp"
#include "dip/ReportWriter.hpp"
#include <fstream>
#include <iostream>
#include <sstream>

int main(int argc, char** argv)
{
    if (argc < 3)
    {
        std::cerr << "Usage: dip-native-collector <root> <output.json> [log.txt]\n";
        return 1;
    }

    const std::filesystem::path root = argv[1];
    const std::filesystem::path output = argv[2];

    dip::FileScanner scanner;
    dip::DuplicateDetector duplicateDetector;
    dip::LogAnalyzer logAnalyzer;
    dip::ReportWriter writer;

    const auto files = scanner.Scan(root);
    const auto duplicates = duplicateDetector.FindDuplicates(files);

    std::vector<dip::Issue> issues;
    for (const auto& group : duplicates)
    {
        issues.push_back({
            "duplicate",
            "Warning",
            "Duplicate files detected",
            std::to_string(group.size()) + " files share the same content hash.",
            group.front().path.string()
        });
    }

    if (argc >= 4)
    {
        std::ifstream log(argv[3]);
        std::stringstream buffer;
        buffer << log.rdbuf();
        auto logIssues = logAnalyzer.Analyze(buffer.str());
        issues.insert(issues.end(), logIssues.begin(), logIssues.end());
    }

    writer.WriteJson(output, files, duplicates, issues);
    std::cout << "Scanned " << files.size() << " files.\n";
    return 0;
}
