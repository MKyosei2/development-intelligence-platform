# DIP Native Collector

大量Fileの走査、Content Hash、重複検出、Native Log分類を行うC++20 CLIです。

## ビルド / テスト

```powershell
cmake -S collectors/native-collector -B collectors/native-collector/build
cmake --build collectors/native-collector/build --config Release
ctest --test-dir collectors/native-collector/build -C Release --output-on-failure
```

## 実行

```powershell
.\collectors\native-collector\build\Release\dip-native-collector.exe `
  C:\GameProject `
  C:\Reports\native-report.json `
  C:\Logs\build.log
```
