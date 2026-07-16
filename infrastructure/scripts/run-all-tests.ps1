$ErrorActionPreference = "Stop"

dotnet test --configuration Release

Push-Location services/python-analysis
try {
    python -m pytest
}
finally {
    Pop-Location
}

cmake -S collectors/native-collector -B collectors/native-collector/build -DBUILD_TESTING=ON
cmake --build collectors/native-collector/build --config Release
ctest --test-dir collectors/native-collector/build -C Release --output-on-failure

Write-Host "All test suites completed."
