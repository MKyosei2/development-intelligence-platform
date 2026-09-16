# Evidence

## 役割

Evidenceの入力契約、検証、永続化境界への受け渡しをApplication層で扱う。

## ソース位置

- Application: `src/Dip.Application/Features/Evidence/`

## 主な読み始め

- `src/Dip.Application/Features/Evidence/EvidenceIngestionContracts.cs`
- `src/Dip.Application/Features/Evidence/EvidenceValidator.cs`
- `src/Dip.Application/Features/Evidence/EvidenceIngestionService.cs`

## 読み順

1. 上記のService / Command / Query / ContractからFeatureの入口とデータ境界を確認する。
2. 依存する`Dip.Application.Abstractions`を確認し、外部I/Oとの境界を把握する。
3. 対応するDomain型とtestsを確認し、状態・判定規則・失敗条件を追う。
4. Infrastructure実装がある場合は最後に確認し、DB・GitHub・filesystem・runtime等の具体I/Oを追う。

