# Real Project Case Study — MAYAtoUnity × DIP

## 目的

DIPをsample fixtureだけで説明せず、別repositoryとして開発している実Unity Project **MAYAtoUnity** を対象に、Game Development QAとProject Truthの接続を検証します。

Target repository:

- `https://github.com/MKyosei2/MAYAtoUnity`

MAYAtoUnityは`Assets / Packages / ProjectSettings`を持つUnity Projectです。DIP側のverification scriptはtarget revisionをGit SHAで取得し、ProjectVersion.txtから必要Unity versionを解決します。

## Revisionを混同しない

Case Studyでは2つのrevisionを別フィールドで記録します。

| Revision | 意味 |
|---|---|
| `dipSourceCommit` | 検証対象のDIP実装SHA |
| `targetCommit` | DIP Collectorが解析したMAYAtoUnity側のSHA |

MAYAtoUnityのSHAをDIPのSHAとして扱ったり、逆にDIP SHAをMAYAtoUnityのrevisionとして記録したりしません。

## 実行経路

```text
MAYAtoUnity (real Git-backed Unity Project)
        ↓
actual Unity Editor batchmode
        ↓
UnityCollectorExactRevisionCli
        ↓
collector report
        ↓
DIP POST /api/v1/collector-reports
        ↓
Project Truth / Quality Gate
        ↓
final Evidence bundle
```

実Unity Project verification:

```powershell
$sha = (git rev-parse HEAD).Trim()
python tools/verify_unity_real_project.py --expected-dip-sha $sha
```

Projectを指定しない場合、localのMAYAtoUnityを探し、見つからなければ公開repositoryを一時directoryへcloneします。検証のためにMAYAtoUnity本体を編集せず、一時copyへDIP Collector packageを注入します。

## Evidence

実行時に次を生成します。

```text
artifacts/real-project-runtime/unity/unity-real-project-report.json
artifacts/real-project-runtime/unity/unity-real-project.log
artifacts/real-project-runtime/unity/unity-real-project-evidence.json
```

Evidenceには少なくとも次を含めます。

- exact `dipSourceCommit`
- target repository / target commit / target branch
- Unity version / executable
- actual Unity process exit code
- collector external run ID
- quality gate結果
- metric map / issue count

## DIPへの実投入

Engine reportを生成しただけではCase Studyを完了扱いにしません。Native DIP APIへreportを投入し、Project Truth / Quality Gateまで確認します。

```powershell
python tools/publish_real_project_evidence.py `
  --engine-evidence artifacts/real-project-runtime/unity/unity-real-project-evidence.json `
  --report artifacts/real-project-runtime/unity/unity-real-project-report.json `
  --expected-dip-sha $sha
```

結果:

```text
artifacts/real-project-runtime/project-truth/project-truth-evidence.json
```

このartifactにはcollector ingestion response、100% Complete claimへのverification result、Project Truth dashboard、Quality Gate statusを保存します。

## 実利用規模の示し方

READMEへ固定の「最大規模」を手入力しません。実ProjectからCollectorが取得したasset / scene / build / animation / runtime metricをEvidence artifactへ保存し、その値をCase Studyの実測規模として使用します。

これにより、後からMAYAtoUnityが変化しても古い件数をcurrent evidenceとして残しません。

## Unreal

Unrealも`tools/verify_unreal_real_project.py`で同じrevision-boundaryを使用します。Final publicationではactual `UnrealEditor-Cmd`とGit-backed `.uproject`を要求し、DIP内で生成したfixtureをreal-project claimの代替にはしません。

## Publication条件

Case Studyのsource codeが存在するだけでは`Runtime Verified`とは呼びません。Final releaseで`tools/finalize_portfolio_evidence.py`が、CI・benchmark・Unity・Unrealの`dipSourceCommit`を同一SHAとして検証できた時だけfinal EvidenceをPASSにします。
