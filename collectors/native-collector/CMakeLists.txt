cmake_minimum_required(VERSION 3.24)
project(dip_native_collector LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

option(BUILD_TESTING "Build tests" ON)

add_library(dip_native
    src/FileScanner.cpp
    src/DuplicateDetector.cpp
    src/AssetHasher.cpp
    src/LogAnalyzer.cpp
    src/ReportWriter.cpp
)

target_include_directories(dip_native PUBLIC include)

add_executable(dip-native-collector src/main.cpp)
target_link_libraries(dip-native-collector PRIVATE dip_native)

if(BUILD_TESTING)
    enable_testing()
    add_executable(dip-native-tests
        tests/FileScannerTests.cpp
        tests/DuplicateDetectorTests.cpp
        tests/LogAnalyzerTests.cpp
    )
    target_link_libraries(dip-native-tests PRIVATE dip_native)
    add_test(NAME dip-native-tests COMMAND dip-native-tests)
endif()
