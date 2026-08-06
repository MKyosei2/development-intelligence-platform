# Artifacts

Build、Release、Coverage、Sample Dataなど再利用される生成物の置き場です。

- 原則GitへCommitしない
- ReleaseにはSHA-256 Manifestを付ける
- Secret、個人情報、絶対Pathを含めない
- GitHub ReleasesまたはObject Storageへ保存する

`reports/`は評価結果、`artifacts/`は実行・配布対象という区別です。

## Final verification

`scripts/Verify-Solution.ps1`は、次のディレクトリへ最終確認証拠を出力します。

```text
artifacts/final-verification/
```

全検証に成功した場合のみ、次の終了宣言が生成されます。

```text
PROJECT_COMPLETION.txt
```

失敗した場合は終了宣言を生成せず、代わりに
`PROJECT_COMPLETION_FAILED.txt`、Unit／Integration Test結果、API・Worker・Webの標準出力と標準エラーを保存します。
