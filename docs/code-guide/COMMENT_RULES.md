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

## Purpose品質ゲート

`Purpose:` は「存在している」だけでは合格としない。次の状態はreadability auditで失敗させる。

- `C ha ng eR is k...` のようにfilename/type名が細切れになった文字分解コメント
- 同一ファイル内の複数 `Purpose:`
- 人が責務を判断できない極端に短い/不自然なPurpose

再検証は次で行う。

```powershell
.\scripts\Verify-SourceReadability.ps1 -Strict
```

監査結果は `docs/code-guide/_generated/source-cleanup-report.md` に生成される。`Readability/documentation findings = 0` の場合だけreadability上のclean状態と扱う。
