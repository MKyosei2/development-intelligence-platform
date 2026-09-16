# 命名規則

## C#

- 型: `PascalCase`
- interface: `I` + `PascalCase`
- public/internal method・property・event: `PascalCase`
- private field: `_camelCase`
- boolean: `Is` / `Has` / `Can` / `Should` を優先
- 非同期メソッド: `Async` suffix を優先
- 1ファイルに主要型が1つだけの場合: `ファイル名 = 型名.cs`

## 責務名

`Difficulty1` ～ `Difficulty5` のような実装順・難易度名は、active source では使用しない。

- Difficulty1 / DifficultyOne -> EvidenceIntegration
- Difficulty2 / DifficultyTwo -> ArtifactTracking
- Difficulty3 / DifficultyThree -> ProductionTools
- Difficulty4 / DifficultyFour -> QualityAutomation
- Difficulty5 / DifficultyFive -> EnterpriseFleet

EF Migration と `scripts/legacy/difficulty-foundation` は履歴互換性のため例外とする。

## 避ける名前

意味が曖昧な `Temp`, `NewService`, `Test2`, `Misc`, `Utils`, 単独の `Manager` / `Helper` は原則使用しない。