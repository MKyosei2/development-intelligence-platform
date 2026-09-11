# Real Project Demo — 2〜3分

READMEのGIFはsample-data previewとして残します。採用レビュー用の本番デモは、**MAYAtoUnityを実ProjectとしてDIPへ接続した場面**を2〜3分で見せます。

## 録画前提

録画時点では次を満たしていることを確認します。

- DIPはfinal publication SHAをcheckoutしている。
- `tools/verify_final_ci_matrix.py`で11/11 Greenを確認済み。
- MAYAtoUnityのtarget SHAを画面に表示できる。
- Unity EditorはMAYAtoUnityのProjectVersionに対応している。
- `unity-real-project-evidence.json`が同じDIP SHAへbindingされる。

## 0:00–0:25 — 2つのrevisionを見せる

TerminalでDIP SHAとMAYAtoUnity SHAを表示します。

```powershell
cd <DIP>
git rev-parse HEAD
cd <MAYAtoUnity>
git rev-parse HEAD
```

説明は「DIPの実装revisionと、解析対象Projectのrevisionを別々に追跡する」です。

## 0:25–1:05 — 実Unity ProjectでCollector実行

MAYAtoUnityのProject画面またはProject structureを短く表示し、実Unity Editorを使ったverificationを実行します。

```powershell
$sha = (git -C <DIP> rev-parse HEAD).Trim()
python <DIP>/tools/verify_unity_real_project.py --expected-dip-sha $sha --project <MAYAtoUnity>
```

見せるポイント:

- actual Unity processが起動する。
- target ProjectはMAYAtoUnity。
- `dipSourceCommit`がfinal SHA。
- `targetCommit`がMAYAtoUnity SHA。

## 1:05–1:45 — Engine EvidenceをDIPへ投入

Native DIP runtimeを起動し、実Collector reportをDIPへ投入します。

```powershell
python tools/publish_real_project_evidence.py `
  --engine-evidence artifacts/real-project-runtime/unity/unity-real-project-evidence.json `
  --report artifacts/real-project-runtime/unity/unity-real-project-report.json `
  --expected-dip-sha $sha
```

DIP UIでProject Truth / Quality Gateを表示します。

## 1:45–2:15 — Project Truth

次を画面で確認します。

- Latest pipeline / target revision
- Quality Gate status
- Critical / Warning issue count
- 100% Complete claimのverification status
- Project Truth / risk

重要なのは「Dashboardを見せる」ことより、**実Engine EvidenceがProject Truthの判断材料へ変換された**ことです。

## 2:15–2:40 — Final exact-revision Evidence

最後にfinal Evidence summaryを表示します。

```text
Required CI       11/11 Green
Benchmark         exact SHA
Unity runtime     verified
Unreal runtime    verified
Project Truth     captured
DIP source SHA    <same final SHA>
```

## 録画ファイル

公開用ファイル名は次に固定します。

```text
dip-real-project-demo.mp4
```

Git repositoryへ大容量動画を直接追加する必要はありません。Release assetまたは動画ホスティング先へ置き、READMEからリンクします。

動画が実際に存在する前にREADMEへ「実演動画あり」とは書きません。録画が未完了なら`Runtime Evidence tooling implemented / recording pending`と明確に区別します。
