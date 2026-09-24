// Purpose: Implements File Scanner responsibilities for native collector.
#include "dip/FileScanner.hpp"
#include <filesystem>
#include <fstream>
#include <iostream>

int main_file_scanner_test()
{
    const auto root = std::filesystem::temp_directory_path() / "dip-file-scanner-test";
    std::filesystem::create_directories(root);
    std::ofstream(root / "a.txt") << "hello";

    dip::FileScanner scanner;
    const auto files = scanner.Scan(root);
    std::filesystem::remove_all(root);

    if (files.size() != 1)
    {
        std::cerr << "FileScanner test failed\n";
        return 1;
    }
    return 0;
}
