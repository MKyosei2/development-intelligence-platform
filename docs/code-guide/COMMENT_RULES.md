# コメント規則

ソース内コメントは簡潔な英語を使用する。

## ファイル先頭

手書きソースには原則として `Purpose:` を1つだけ置く。

```csharp
// Purpose: Coordinates project quality evaluation.
```

PowerShell / Python は `# Purpose:` を使用する。

## コメントで書くこと

- なぜその処理・制約が必要なのか
- 非自明な失敗条件
- セキュリティ・互換性・性能上の理由
- 他レイヤとの境界

## コメントで書かないこと

- コードをそのまま英語にしただけの説明
- 行ごとの逐語説明
- 古くなった TODO
- 同じ Purpose の重複

Generated code、EF Migration、third-party/license header は自動整理対象外とする。