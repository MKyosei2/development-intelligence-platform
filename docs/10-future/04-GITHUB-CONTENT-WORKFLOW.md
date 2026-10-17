# GitHubドキュメント運用とZIP内容の配置方法

**対象:** このZIPに含まれる新規Markdown資料。既存のルート`README.md`や既存ソースファイルは編集しない。

## 1. GitHubの保存場所は3種類

1. **通常のリポジトリ:** `docs/10-future/**/*.md` はリポジトリに追加する（Git履歴・PRレビュー・ブランチと一体）。
2. **GitHub Wiki:** ZIP内`wiki-upload/*.md`は**Wiki専用の別Gitリポジトリ**へ登録する。通常のコードリポジトリ直下に置いてもWikiページにはならない。
3. **GitHub Issue／Discussion:** `issue-drafts/*.md`は投稿するための**原稿**。ファイルとしてpushしてもIssue/Discussionは作成されない。

## 2. ローカルでの配置（例）

- ZIPの`docs/10-future/`を、DIPの既存`docs/`の下にコピーする。
- ZIPの`wiki-upload/`を、DIP本体のリポジトリへ**そのまま置かない**。
- 既存`README.md`、既存`docs/`の別文書、`.github/ISSUE_TEMPLATE/*.yml`、ソースコードには触らない。
- `git status --short`で追加ファイルだけであることを確認してからコミットする。

## 3. Wikiへの追加

1. GitHubリポジトリの`Wiki`を開く。初回はHomeページをWeb画面で作り、WikiのGitリポジトリを初期化する必要がある場合がある。
2. Wiki画面上でページを1つずつ作成するか、Wiki Gitリポジトリ（通常は`https://github.com/MKyosei2/development-intelligence-platform.wiki.git`）をcloneする。
3. ZIPの`wiki-upload/`内のMarkdownだけを、そのWikiリポジトリ直下へコピーする。フォルダ`wiki-upload/`自体をWiki側に残さない。
4. Home.mdと_Sidebar.mdを含めて保存／コミットする。既にWikiに同名ページがある場合は内容を比較し、誤って上書きしない。
5. Wikiページ内のリンクと、通常リポジトリへのリンクを開いて検証する。

## 4. Issues / Projects / Discussions

- `issue-drafts/`の各Markdownから本文をIssueへコピーし、タイトル・Labels・Milestoneを手動で設定する。
- GitHub Projectを作成し、Issueを追加する。Projectsのアイテムはファイルだけでは生成されない。
- 現在のリポジトリではDiscussionsが無効だった（確認時点）。`Settings > General > Features > Discussions`で有効化した後、検討段階のテーマを議論する。設定変更はこのZIPには含めない。
- `.github/ISSUE_TEMPLATE/config.yml`は現在Discussionsへの案内があるため、Discussionsを使う運用にすると整合する。ここではYAMLを変更しない。

## 5. 必須の注意

- 仕様のStatusをImplemented/Runtime Verifiedへ変更するときは、対象SHAとEvidenceを付ける。
- Wikiと正式な設計書の説明が食い違った場合は、コード／改訂版Evidence／採用済みADRの順に根拠を確認する。
- 機能構想を実績としてREADMEへ転載しない（今回はREADMEを固定）。
- IssueやRFCには機密情報、実APIキー、第三者ゲームの無断公開アセットを掲載しない。

## 6. 推奨する標準フロー

`Wiki（背景・利用価値） -> Discussion（必要なら議論） -> RFC（正式設計案） -> Issue（作業分解） -> Project/Milestone（計画） -> PR（実装とレビュー） -> Actions/Evidence（検証） -> Release（成果公開）`

このフローはすべて必須ではない。小さな修正はIssue/PRから始めてよい。
