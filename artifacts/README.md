# Artifact（生成物）

Build、Release、Coverage、Sample Dataなど、再利用される生成物の置き場です。

- 原則としてGitへCommitしない
- ReleaseにはSHA-256 Manifestを付ける
- Secret、個人情報、絶対Pathを含めない
- GitHub ReleasesまたはObject Storageへ保存する

`reports/`は評価結果、`artifacts/`は実行・配布対象という区別です。

## 最終検証

`scripts/Verify-Solution.ps1` は、次のDirectoryへ最終確認Evidenceを出力します。

```text
artifacts/final-verification/
```

すべての検証に成功した場合のみ、次の終了宣言が生成されます。

```text
PROJECT_COMPLETION.txt
```

失敗した場合は終了宣言を生成せず、代わりに `PROJECT_COMPLETION_FAILED.txt`、Unit / Integration Test結果、API・Worker・Webの標準出力と標準エラーを保存します。
