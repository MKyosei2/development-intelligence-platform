# Artifacts
Build、Release、Coverage、Sample Dataなど再利用される生成物の置き場です。

- 原則GitへCommitしない
- ReleaseにはSHA-256 Manifestを付ける
- Secret、個人情報、絶対Pathを含めない
- GitHub ReleasesまたはObject Storageへ保存する

`reports/`は評価結果、`artifacts/`は実行・配布対象という区別です。
