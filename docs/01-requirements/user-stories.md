# User Stories

## PM / 制作進行（最優先）

- PMとして、専門知識がなくてもプロジェクトの「報告状態」と「客観的に確認できた状態」を一画面で比較したい
- PMとして、担当者の自己申告だけに依存せず、Git・Build・Test・Runtime・Asset・Performance・QA等のEvidenceから現在の実態を知りたい
- PMとして、誰かを「嘘つき」と判定するのではなく、報告とEvidenceが一致していない箇所だけを安全に確認したい
- PMとして、Reported ProgressとEvidence-backed Progressの差を把握したい
- PMとして、今日最も危険な問題、Blocker、優先対応を専門用語なしで知りたい
- PMとして、リリース見込みと、その判断を支えるEvidenceの信頼度を確認したい
- PMとして、過去の報告・Build・CI・Asset・Qualityの変化をEvidence Timelineとして追跡したい

## Tech Lead / Engineer / TA

- Tech Leadとして、品質を悪化させたPRを特定したい
- Tech Leadとして、PM向けの簡略表示から根拠となった技術Evidenceへドリルダウンしたい
- Engineerとして、自分のPRがFailした理由を知りたい
- Engineerとして、報告がContradictedになった場合に、どのEvidenceが矛盾したか確認したい
- TAとして、UnityやMayaのアセット問題をCIで検出し、Project TruthのEvidenceとして残したい

## 設計原則

DIPは人物の信用度や「嘘をついた確率」を算出しない。評価対象は常にClaim（報告内容）とEvidence（観測事実）の一致度である。
