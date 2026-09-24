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
- `Provides infrastructure behavior ...` のようにカテゴリだけを述べ、具体的な責務を示さないPurpose
- 型名やファイル名を言い換えただけで、何を保証・変換・永続化・調停するのかが分からないPurpose

Generated code、EF Migration、third-party/license header は自動整理対象外とする。

## Purpose品質ゲート

`Purpose:` は「存在している」だけでは合格としない。次の状態はreadability auditで失敗させる。

- `C ha ng eR is k...` のようにfilename/type名が細切れになった文字分解コメント
- 同一ファイル内の複数 `Purpose:`
- 人が責務を判断できない極端に短い/不自然なPurpose
- `Defines a focused responsibility ...` など、どのファイルにも適用できるgeneric Purpose
- 異常に圧縮された複数statementのC#行

ローカル再検証は次で行う。

```powershell
.\scripts\Verify-SourceReadability.ps1 `
  -Strict `
  -ReportPath artifacts/source-readability/report.md `
  -InventoryPath artifacts/source-readability/inventory.csv
```

CIでも同じartifact配下へ出力し、tracked documentationを検証実行中に書き換えない。
`Readability/documentation findings = 0` の場合だけreadability上のclean状態と扱う。

## Purposeの具体性

Purposeは、ファイルを開かなくても主要な責務・境界・保証内容のいずれかを判断できる文章にする。

良い例:

```csharp
// Purpose: Persists QA governance state, waivers, baselines, policy evaluations, provenance, health, and release overrides.
```

禁止例:

```csharp
// Purpose: Provides infrastructure behavior for this application boundary.
// Purpose: Coordinates application behavior for this feature area.
// Purpose: Defines a focused responsibility within the development intelligence platform.
```

自動修復はgeneric文へフォールバックしない。責務を型名・Featureから安全に導出できない場合は、レビューで具体化する。
