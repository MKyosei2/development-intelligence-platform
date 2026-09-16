# ソース構成ルール

このリポジトリでは、プロジェクト単位のレイヤ分離を維持しつつ、各レイヤ内部を責務単位で整理する。

## 基本原則

- `src/Dip.Domain/Features/<責務>/<種類>/` : Entity / Enum / Event / Rule / Service / ValueObject
- `src/Dip.Application/Features/<責務>/` : ユースケース、アプリケーションサービス
- `src/Dip.Application/Abstractions/` : Application が外部実装へ要求する境界
- `src/Dip.Application/DependencyInjection/` : Application の責務別 DI 登録
- `src/Dip.Infrastructure/<責務>/` : DB、外部 API、ファイル、QA 実装など
- `src/Dip.Infrastructure/DependencyInjection/` : Infrastructure の責務別 DI 登録
- `src/Dip.Api/` : HTTP/API の composition root と API 固有処理
- `src/Dip.Worker/` : バックグラウンド処理
- `src/Dip.Agent.*` : ローカル Agent
- `tests/` : 対応する責務・レイヤのテスト

## 今回の整理で行わないこと

- namespace の一括変更
- EF Migration の履歴名変更
- 実行ロジックの意味変更
- build/test/CI の成否をソース整理の成功条件にすること

物理配置、ファイル名、コメントを整理し、意味変更は別作業として扱う。