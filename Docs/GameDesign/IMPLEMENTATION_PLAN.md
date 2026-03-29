# IMPLEMENTATION_PLAN.md
> 万願果 MVP実装計画 v2.1

---

## 0. 目的

1から `万願果` を再構築するときの実装順を定義する。
AIは依存関係を壊さず、上から順に進めることを基本とする。

> **v1.0 → v2.0 変更点**: Firebase バックエンド、ネットワーク同期、マネタイゼーション、デッキ構築UIをMVPフェーズに追加。

---

## 1. 実装原則

- 先に正本と補助仕様を固める
- 次にデータ構造を作る
- その後UIシェルを作る
- バックエンド（Firebase）は画面シェルと並行して構築
- 画面ごとの実データ接続は最後に厚くする
- Unity YAML を直接編集しない

---

## 2. 優先順（MVP）

> **各Phase の目安工数**は1名フルタイム開発者を想定。AI支援込みで短縮可能。

### Phase 1: 仕様と土台（目安: 1〜2週間）

1. `GAME_DESIGN.md` を正本として固定（完了）
2. 補助仕様ファイルを揃える（完了）
3. 共通用語と願相表現を統一（完了）
4. `CARD_SCHEMA.md` でカードデータ構造を確定する（1日）
5. `CARD_TEXT_GUIDELINES.md` でテキスト文法ルールを確定する（1日）
6. `ASSET_LIST.md` + 各 `ASSET_*.md` で素材ロードマップを確定する（2日）

### Phase 2: コアデータ（目安: 1〜2週間）

7. カードスキーマを定義（`CARD_SCHEMA.md` §1〜5 に従う）（2日）
8. 願主データを定義（6願主 + MVP用共通パラメータ）
9. 願相カラートークン + **願相アイコン（二重識別）** を実装に落とす（`UI_STYLE_GUIDE.md §6` + `ACCESSIBILITY_SPEC.md §1`）
10. MVP初期カードプール（顕現90（通常84+奇襲6） / 詠術48 / 界律24 = 計162種）のデータ入力（`GAME_DESIGN.md 付録A`）
11. スターターデッキプリセットを定義（`GAME_DESIGN.md 付録A5`）
12. カードマスターデータをFirestore `cardMaster/` + Unity StreamingAssets に二重管理（`BACKEND_DESIGN.md §5`）

### Phase 3: Firebase基盤（目安: 2〜3週間）

13. Firebaseプロジェクト作成（Sparkプラン → Blazeプランへ移行準備）（0.5日）
14. Firebase Authentication 実装（匿名認証 + Apple Sign-in）
15. Firestore スキーマ作成（`BACKEND_DESIGN.md §3` の全コレクション）
16. Firestore Security Rules 実装・テスト
17. Firebase Realtime Database 初期設計（`NETWORK_SPEC.md` のmatchesパス構造）
18. Cloud Functions v2 プロジェクト初期化

### Phase 4: コア対戦ロジック（目安: 3〜4週間）

19. ゲームステートモデル（`GAME_DESIGN.md 付録B5` MatchState）（2日）
20. ターン進行（`GAME_DESIGN.md §10`）
21. CP管理 / マリガン（`GAME_DESIGN.md §9`）
22. 顕現プレイ・配置・召喚酔い処理
23. 詠術プレイ・EffectKey 解決（`GAME_DESIGN.md §11`）
24. 界律プレイ・上書き処理
25. 戦闘ロジック（戦力比較・退場）
26. 直撃処理（願力への押し込み）
27. 願主レベルアップ（願成ゲージ処理）
28. 勝利条件判定（24ターン制 / 端到達）
29. 必須ログ出力（`GAME_DESIGN.md §13`）
30. **ターンタイマー（90秒）+ 連続タイムアウト自動敗北** (`GAME_DESIGN.md §10.1a`)

### Phase 5: ネットワーク・マッチメイキング（目安: 2〜3週間）

31. Cloud Functions: createRoom / joinRoom / startMatch 実装（3日）
32. Cloud Functions: processCommand（全7コマンドタイプの検証ロジック）
33. Cloud Functions: checkTurnTimeout / checkDisconnect
34. RTDB リアルタイムリスナー実装（Unity側）
35. ルーム作成・参加UIの実装
36. マリガン同期処理
37. 切断・再接続処理（60秒グレース期間）
38. **AI Bot対戦**（`AI_BOT_SPEC.md` のCloud Functions実装）

### Phase 6: 画面シェル（目安: 2〜3週間）

39. 下部ナビゲーション（5タブ統合・`PRODUCT_REQUIREMENTS.md §4`）（1日）
40. ホーム画面シェル（背景・バトルCTA・バナー枠）
41. バトル画面シェル（フィールドグリッド・LP/コストパネル・ターン管理）
42. カード画面シェル（一覧グリッド・フィルター枠）
43. ストーリー画面シェル（章マップ・ノード）
44. ショップ画面シェル（商品グリッド・通貨表示）
45. **デッキ構築画面**（`DECK_BUILDER_SPEC.md`）— **MVP必須**。プリセット3種の選択 + カスタム構築（34枚バリデーション・フィルタ・ソート・デッキコード）を実装。工数目安: 3〜4日
46. オンボーディング基本フロー（スプラッシュ→名前入力→最初のバトルへ）

### Phase 7: マネタイゼーション実装（目安: 3〜4週間）

47. 通貨システム（ゴールド + 願晶）実装（2日）
48. **パックシステム**（シリーズ別パック + 願相ピックアップ）
49. **パック開封UI + 演出**（`ANIMATION_SPEC.md §5`）
50. **カード生成/分解**（クラフトシステム）
51. **バトルパス（願道パス）** — 30レベル + 報酬テーブル
52. **リワード広告**（AdMob統合、モバイルのみ）
53. **IAP（App Store課金）** — 願晶5種 + スターターバンドル
54. Cloud Functions: purchaseItem / verifyReceipt
55. **デイリー/ウィークリーミッション**
56. **ログインストリーク**
57. **デイリーショップ**（ローテーション）

### Phase 8: 画面強化 + データ接続（目安: 3〜4週間）

58. バトル画面の実データ接続（カード表示・願主表示・HPゲージ）（3日）
59. バトルアニメーション実装（`ANIMATION_SPEC.md §3` 全シーケンス）
60. カード画面の実データ接続（コレクション画面 `COLLECTION_UX_SPEC.md`）
61. ストーリー画面の章データ接続（章1〜章6のノード・バナー）
62. ショップ画面の商品データ接続（通貨・商品リスト・パック）
63. ホームのイベント導線（バナー・ミッション進捗）
64. BGM/SEの統合（`SOUND_DESIGN_SPEC.md` 準拠）
65. アセット差し替えパイプライン整備（仮素材 → 正式素材の置換フロー）
66. **フレンドシステム**（`SOCIAL_SPEC.md` の P0 項目）
67. **エモートシステム**（基本6種）

### Phase 9: チュートリアル + ストーリー（目安: 1〜2週間）

68. チュートリアル実装（`TUTORIAL_FLOW.md` の12ステップ）（3日）
69. ストーリー第1章データ入力（`STORY_CHAPTERS.md`）
70. ナルのコンパニオンUI実装（`COMPANION_CHARACTER.md`）

### Phase 10: 品質保証（目安: 3〜4週間）

71. アクセシビリティ P0 項目確認（`ACCESSIBILITY_SPEC.md §8`）（2日）
72. コントラスト比テスト（WCAG 2.1 AA）
73. パフォーマンステスト（`PERFORMANCE_SPEC.md §8` 全項目）
74. ユニットテスト（`TEST_PLAN.md §L1` 全20ケース）
75. 統合テスト（`TEST_PLAN.md §L2`）
76. ネットワークテスト（`TEST_PLAN.md §L3` Firebase emulator）
77. E2Eテスト（`TEST_PLAN.md §L4` 端末3台）
78. バランステスト（`TEST_PLAN.md §L5` 各組み合わせ最低3,000自動対戦）
79. エラーハンドリング全パス確認（`ERROR_HANDLING.md`）
80. セキュリティ検証（`SECURITY_SPEC.md` チート対策・レート制限）
81. App Store提出準備（`APPSTORE_CHECKLIST.md`）
82. TestFlightベータテスト（1〜2週間）
83. 視認性調整（コントラスト・文字サイズ・タップ領域）
84. 文言統一（全画面の用語を `GAME_DESIGN.md` + `CARD_TEXT_GUIDELINES.md` に合わせる）
85. 仮素材から正式素材へ差し替え（`ASSET_*.md` の最優先素材から順に）

---

### Phase 11: CI/CD・自動化（全Phase並行）

86. Git リポジトリ初期設定（Unity .gitignore・LFS設定）
87. GitHub Actions / Cloud Build ワークフロー構築
    - Push時: Unity Test Framework (L1ユニットテスト) 自動実行
    - PR時: コードレビュー + テスト結果レポート
    - mainマージ時: Unity Cloud Build → TestFlight自動デプロイ
88. Firebase エミュレータ CI 統合（Cloud Functions テスト）
89. カードデータバリデーション自動チェック（CARD_SCHEMA.md 準拠）
90. ビルド番号自動インクリメント
91. Crashlytics + Analytics の Staging/Production 環境分離

> **MVP全体の目安工期: 約20〜26週間**（1名開発者。AI支援で30〜40%短縮見込み）

---

## 3. AIタスク分解ルール

- 1タスク1成果物を基本にする
- シーンを触る場合は Editor スクリプト経由（Unity YAML を直接編集しない）
- データ定義と見た目変更を同時に大きくやりすぎない
- 常に現在の仕様書を参照する
- `GAME_DESIGN.md` が正本。矛盾が出たらこちらを優先

---

## 4. 最初に作るべきもの

- Firebase Authentication + Firestore スキーマ
- 5画面シェル（ナビゲーション統合）
- カード表示基本UI（顕現・詠術・界律の枠・コスト・願相 + 願相アイコン）
- 願主表示UI（レベル・願成ゲージ）
- HPゲージUI（HP値0-100 + 閾値ライフカード表示）
- 願相カラートークン + 二重識別アイコン（6属性HEX値 + アイコンの統一実装）
- デッキ構築画面（構築ルールのバリデーション付き）

---

## 5. 後回しにしてよいもの（MVP対象外）

- ~~ランクマッチ~~ → **MVP昇格済み**（`GAME_DESIGN.md §14`）
- ~~ドラフトモード~~ → **MVP昇格済み**（`GAME_DESIGN.md §15`）
- シーズン報酬拡張（ランクマッチ自体はMVP。コスメ報酬は `POST_MVP_ROADMAP.md Phase 3`）
- 観戦拡張（`POST_MVP_ROADMAP.md Phase 3`）
- リプレイ（`POST_MVP_ROADMAP.md Phase 3`）
- 量産カードプール（MVP後フェーズ）
- フルアニメーション・ボイス実装（MVP後フェーズ）
- 大量シナリオ（第二章以降は MVP 後）
- Steam / Android 版（`POST_MVP_ROADMAP.md Phase 5`）
- 構築制限イベント（`POST_MVP_ROADMAP.md Phase 2`）
- コラボイベント（`POST_MVP_ROADMAP.md Phase 6`）

---

## 6. 関連ファイル

- `GAME_DESIGN.md` — ゲームルール正本
- `CARD_SCHEMA.md` — カードデータ構造
- `CARD_TEXT_GUIDELINES.md` — テキスト文法ルール
- `PRODUCT_REQUIREMENTS.md` — プロダクト要件
- `SCREEN_SPEC.md` — 画面設計
- `UI_STYLE_GUIDE.md` — UI外観ルール
- `ACCESSIBILITY_SPEC.md` — アクセシビリティ設計
- `POST_MVP_ROADMAP.md` — MVP後の拡張計画
- `BACKEND_DESIGN.md` — Firebase バックエンド設計
- `NETWORK_SPEC.md` — ネットワーク同期設計
- `MONETIZATION_DESIGN.md` — マネタイゼーション設計
- `ANIMATION_SPEC.md` — アニメーション・演出タイミング
- `SOUND_DESIGN_SPEC.md` — サウンド演出設計
- `DECK_BUILDER_SPEC.md` — デッキ構築UI設計
- `SOCIAL_SPEC.md` — ソーシャル機能設計
- `AI_BOT_SPEC.md` — AI Bot設計
- `SECURITY_SPEC.md` — セキュリティ設計
- `PERFORMANCE_SPEC.md` — パフォーマンス要件
- `TEST_PLAN.md` — テスト計画
- `APPSTORE_CHECKLIST.md` — App Store提出チェックリスト
- `ASSET_LIST.md` + 各 `ASSET_*.md` — 素材リスト群
