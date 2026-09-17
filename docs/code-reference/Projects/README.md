# Projects

## 役割
Projectの作成・更新・ライフサイクル変更・監査参照をApplication層で扱うFeature。

## 主な読み始め
- `src/Dip.Application/Features/Projects/ProjectService.cs`
- `src/Dip.Application/Features/Projects/Commands/CreateProjectCommand.cs`
- `src/Dip.Application/Features/Projects/Commands/UpdateProjectCommand.cs`
- `src/Dip.Application/Features/Projects/Commands/ProjectLifecycleCommand.cs`
- `src/Dip.Application/Features/Projects/Dtos/ProjectDto.cs`
- `src/Dip.Infrastructure/Persistence/Repositories/AuditLogRepository.cs`

## 読み順
1. `ProjectService.cs`でProject lifecycleとaudit取得を確認する。
2. Commands / DTOsでApplication境界を確認する。
3. `IAuditLogRepository.cs`と`AuditLogRepository.cs`で永続化境界とbatch lifecycle取得を確認する。
4. `tests/Dip.Application.Tests/Projects/ProjectServiceTests.cs`で期待動作を確認する。
