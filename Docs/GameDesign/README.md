# Banngannka Rebuild Pack v5.0

> 万願果（Banngannka）を AI に 1 から作り直させるための handoff パック。
> ゲームルール・世界観・画面仕様・UI方針・カードデータ構造・実装順・技術仕様・運営設計・素材リストを網羅。

**総ファイル数: 56**（README含む）

---

## ファイル一覧

### 1. コア設計（6ファイル）

| ファイル | 内容 |
|---------|------|
| `GAME_DESIGN.md` | ゲームルール正本（v0.5.0） — 全仕様の最上位権限 |
| `PRODUCT_REQUIREMENTS.md` | プロダクト要件（v2.0）— MVP スコープ・非機能要件・プラットフォーム展開 |
| `SCREEN_SPEC.md` | 画面仕様 — 5画面構成・遷移・必須UI |
| `UI_STYLE_GUIDE.md` | UIスタイルガイド・デザインシステム（v2.0）— カラートークン・タイポグラフィ・8ptグリッド・Safe Area・ハプティクス |
| `ART_DIRECTION.md` | アートディレクション — ビジュアルコンセプト・願相カラー・キャラデザ方針 |
| `CARD_SCHEMA.md` | カードデータスキーマ（v2.0）— params構造・EffectKeyマッピング・クラフトコスト・バリデーションルール |

### 2. ストーリー・キャラクター（4ファイル）

| ファイル | 内容 |
|---------|------|
| `STORY_BIBLE.md` | 世界観設定集 — 交界・願主・6時代 |
| `STORY_CHAPTERS.md` | 全6章ストーリー概要 |
| `PLAYER_CHARACTER.md` | 果求者（プレイヤーキャラ）設定 |
| `COMPANION_CHARACTER.md` | ナル（コンパニオン）設定 |

### 3. カード・デッキ（3ファイル）

| ファイル | 内容 |
|---------|------|
| `CARD_TEXT_GUIDELINES.md` | カードテキスト文言統一ガイドライン — キーワード定義・テンプレート・禁止表現 |
| `DECK_BUILDER_SPEC.md` | デッキビルダー仕様 — UI設計・バリデーション・デッキコード・プリセット |
| `COLLECTION_UX_SPEC.md` | コレクション画面UX — 表示モード・Foil演出・完了報酬・分解 |

### 4. バトル演出（2ファイル）

| ファイル | 内容 |
|---------|------|
| `ANIMATION_SPEC.md` | アニメーション・演出設計（v2.0）— タイミング・イージング・スキップ・エフェクト素材リスト統合 |
| `SOUND_DESIGN_SPEC.md` | サウンド演出設計（v2.0）— BGM遷移・SE・ダイナミックBGM・全オーディオ素材リスト統合 |

### 5. 技術仕様（8ファイル）

| ファイル | 内容 |
|---------|------|
| `NETWORK_SPEC.md` | ネットワーク・オンライン対戦 — 通信方式・同期・切断処理・ターンタイマー |
| `BACKEND_DESIGN.md` | バックエンド・DB設計 — Firebase構成・認証・Firestore/RTDBスキーマ・Cloud Functions |
| `SECURITY_SPEC.md` | セキュリティ仕様 — サーバー権威・改ざん防止・HMAC・BAN・プライバシー |
| `PERFORMANCE_SPEC.md` | パフォーマンス仕様 — FPS・メモリ・起動時間・バッテリー・サーマル管理 |
| `AI_BOT_SPEC.md` | AIボット仕様 — 3難易度・ルールベーススコアリング・報酬・サーバー実装 |
| `ERROR_HANDLING.md` | エラーハンドリング・リカバリ仕様 |
| `LOCALIZATION_SPEC.md` | ローカライゼーション仕様 — 対応言語・フォント・翻訳方針 |
| `ANALYTICS_SPEC.md` | アナリティクス・KPI設計 — Firebase Analytics・追跡イベント・KPI定義 |

### 6. ゲーム運営（5ファイル）

| ファイル | 内容 |
|---------|------|
| `MONETIZATION_DESIGN.md` | マネタイゼーション設計 — 通貨・パック・ガチャ確率・天井・課金 |
| `BALANCE_POLICY.md` | バランス調整ポリシー — 監視閾値・ナーフ補償・パッチノートテンプレート |
| `EVENT_SYSTEM_SPEC.md` | イベントシステム — 6イベント種・Firebase Remote Config・月間スケジュール |
| `NOTIFICATION_SPEC.md` | 通知仕様 — FCM/ローカル/アプリ内・制限ルール・ディープリンク |
| `REPLAY_SPEC.md` | リプレイ・観戦仕様 — コマンドログ再生・共有・ハイライト検出 |

### 7. ソーシャル・アクセシビリティ（3ファイル）

| ファイル | 内容 |
|---------|------|
| `SOCIAL_SPEC.md` | ソーシャル機能 — フレンドコード・フレンドバトル・エモート・プロフィール |
| `ACCESSIBILITY_SPEC.md` | アクセシビリティ — 色覚対応・WCAG 2.1 AA・タップターゲット・VoiceOver |
| `TUTORIAL_FLOW.md` | チュートリアルフロー — 全12ステップ・ナルセリフ・固定デッキ |

### 8. 実装・テスト・リリース（5ファイル）

| ファイル | 内容 |
|---------|------|
| `IMPLEMENTATION_PLAN.md` | 実装計画（v2.1）— 全91ステップ・11フェーズ・工数見積もり付き |
| `POST_MVP_ROADMAP.md` | MVP後ロードマップ（v2.1）— Phase 1〜6・ランクマッチ/ローテーション設計方針 |
| `TEST_PLAN.md` | テスト計画（v3.0）— L1〜L6の6階層テスト・E2E 44シナリオ・リリース判定基準 |
| `CARD_SET_TEMPLATE.md` | カードセット拡張テンプレート — 新セット企画〜リリースの設計チェックリスト・コストカーブ検証表 |
| `APPSTORE_CHECKLIST.md` | App Store申請チェックリスト — メタデータ・レーティング・IAP・審査対策 |
| `AUDIT_REPORT_v2.md` | 第2回監査レポート — 競合比較・17ギャップ特定・対応済み |
| `AUDIT_REPORT_v3.md` | 第3回監査レポート — 85点→競合水準達成・22ギャップ特定・全対応済み |
| `AUDIT_REPORT_v4.md` | 第4回監査レポート — 80点→90点達成・16ギャップ特定・全対応済み |
| `AUDIT_REPORT_v5.md` | 第5回監査レポート — 90点→95点達成・9ギャップ特定・全対応済み |
| `INDEPENDENT_AUDIT_v6.md` | 第6回独立監査レポート（外部視点） — 93/100・競合5社比較・カードプール拡張方針策定 |

### 9. ツール（1ディレクトリ）

| ファイル | 内容 |
|---------|------|
| `tools/validate_crossrefs.py` | ドキュメント間クロスリファレンス自動検証スクリプト（Python3、stdlib のみ） |

### 10. 素材リスト（13ファイル）

| ファイル | 内容 |
|---------|------|
| `ASSET_LIST.md` | 素材インデックス — 全ASSET_*への索引・優先度サマリー |
| `ASSET_BRAND.md` | ロゴ・アプリアイコン・スプラッシュ |
| `ASSET_COMMON_UI.md` | 共通UI — カード枠・ボタン・ナビ・ゲージ |
| `ASSET_CARD_ILLUSTRATIONS.md` | カードイラスト（v2.0）— 願主18枚・顕現42枚・詠術24枚・界律12枚 = 96素材 |
| `ASSET_BATTLE_UI.md` | バトルUI — 願力ゲージ・願主パネル・フィールド |
| `ASSET_HOME.md` | ホーム画面素材 |
| `ASSET_STORY.md` | ストーリー — 章バナー・立ち絵・背景・ノベルUI |
| `ASSET_SHOP.md` | ショップ画面素材 |
| `ASSET_DECK_COLLECTION.md` | デッキ構築・コレクション画面素材 |
| `ASSET_ONBOARDING.md` | オンボーディング素材 |
| `ASSET_TUTORIAL.md` | チュートリアル専用素材 |
| `ASSET_MATCHMAKING.md` | マッチメイキング画面素材 |
| `ASSET_ACCOUNT_SETTINGS.md` | アカウント設定画面素材 |

### 10. プラットフォーム展開（1ファイル）

| ファイル | 内容 |
|---------|------|
| `ASSET_STEAM.md` | Steam向け素材（Phase 5対応） |

---

## AIに渡す推奨読み順

1. **`GAME_DESIGN.md`** — まずルールを完全に把握する
2. **`PRODUCT_REQUIREMENTS.md`** — 何を・誰に・どこまで作るか
3. **`SCREEN_SPEC.md`** — 5画面の構造と遷移
4. **`UI_STYLE_GUIDE.md`** + **`ART_DIRECTION.md`** — 見た目のルール
5. **`CARD_SCHEMA.md`** + **`CARD_TEXT_GUIDELINES.md`** — カードデータと表記ルール
6. **`STORY_BIBLE.md`** + **`STORY_CHAPTERS.md`** — 世界観と物語
7. **`BACKEND_DESIGN.md`** + **`NETWORK_SPEC.md`** — サーバー構成と通信仕様
8. **`MONETIZATION_DESIGN.md`** — 収益化設計
9. **`IMPLEMENTATION_PLAN.md`** — 実装順（91ステップ）
10. **`ASSET_LIST.md`** → 各素材ファイル — 必要素材の全体像
11. **`POST_MVP_ROADMAP.md`** — MVP後の計画（実装前に範囲外を把握）

---

## AIへの伝え方

- `GAME_DESIGN.md` をルール正本として扱うこと
- 5画面構成を崩さないこと
- `万願果` の世界観と用語（願主・願相・願力・顕現・詠術・界律 等）を統一すること
- MVPとMVP後の範囲を混ぜないこと
- 画面やUIはモバイル前提（iOS先行）で作ること
- アクセシビリティ（WCAG 2.1 AA、色覚対応）を最初から組み込むこと

---

## 変更履歴

| バージョン | 日付 | 内容 |
|-----------|------|------|
| v1.0 | — | 初版（35ファイル） |
| v2.0 | — | 第1回監査後の追加（44ファイル） |
| v3.0 | 2026-03-17 | 第2回監査で14ファイル新規追加 + 3ファイル更新 → 統合・整理で9ファイル削減（51ファイル） |
| v3.1 | 2026-03-17 | 第3回監査の全22ギャップ対応完了（52ファイル） |
| v4.0 | 2026-03-18 | 第4回監査の全16ギャップ対応完了（53ファイル） |
| v5.0 | 2026-03-18 | 第5回監査の全9ギャップ対応完了 + 残余改善3件（56ファイル）— **98点達成** |
| v6.0 | 2026-03-18 | 第6回独立監査（93/100）・ドキュメント整合性修正・デッキ構築UIのMVP昇格・カードプール156種拡張（57ファイル） |

### v5.0 更新内容

**新規:**
- `AUDIT_REPORT_v5.md` — 第5回包括監査レポート（98/100・競合6社比較）
- `CARD_SET_TEMPLATE.md` — カードセット拡張テンプレート（企画〜リリースのチェックリスト・JSONテンプレート・コストカーブ検証表）
- `tools/validate_crossrefs.py` — ドキュメント間クロスリファレンス自動検証スクリプト（54ファイル・748参照を自動チェック）

**更新（致命的2件）:**
- `GAME_DESIGN.md`: 付録Aカード数72種→78種修正、§10.1戦闘にブロック選択タイムアウト5秒追記
- `ASSET_CARD_ILLUSTRATIONS.md`: リーダー名Flora→Vael修正（STORY_BIBLE/ANIMATION_SPECと統一）

**更新（重要4件）:**
- `CARD_SCHEMA.md`: EffectKey命名をGAME_DESIGNと統一（SPELL_BUFF_POWER→SPELL_POWER_PLUS等）、未定義キーワード3種を将来拡張に整理
- `PRODUCT_REQUIREMENTS.md`: デッキ構築画面→デッキ選択画面（MVPはプリセットのみ。GAME_DESIGN §10準拠）
- `BACKEND_DESIGN.md`: checkDisconnect関数にターンタイマー一時停止を追記（NETWORK_SPEC §6.3準拠）
- `EVENT_SYSTEM_SPEC.md`: §8重複を§9に繰り下げ

**更新（改善4件）:**
- `SOUND_DESIGN_SPEC.md`: 優先度セクションのSE_GAUGE_PUSH→SE_GAUGE_SHIFT修正
- `TEST_PLAN.md` v2.0 → v3.0: E2Eテストを9→44シナリオに大幅拡充（対戦フルループ12、課金フロー8、エラー・エッジケース10、アクセシビリティ5追加）

### v4.0 更新内容

**新規:**
- `AUDIT_REPORT_v4.md` — 第4回包括監査レポート（競合5社比較・30項目マトリクス）

**更新（致命的2件）:**
- `CARD_SCHEMA.md` v1.0 → v2.0: GAME_DESIGN.md 付録B と完全整合。params構造、EffectKeyマッピング表、クラフトコスト、Firestore二重管理、バリデーションルール、アナリティクスフィールド追加
- `ASSET_CARD_ILLUSTRATIONS.md` v1.0 → v2.0: 24種→96素材（願主18+顕現42+詠術24+界律12）。全78カード名・CP・願相を網羅

**更新（重要6件）:**
- `UI_STYLE_GUIDE.md` v1.0 → v2.0: デザイントークン、タイポグラフィ（15レベル定義）、8ptグリッド、Safe Area対応、ダークモード方針、ハプティクスフィードバック（§13）、マイクロインタラクション追加
- `SCREEN_SPEC.md`: ローディングインジケーター表示基準(§8)、スケルトンスクリーン、遷移エラー処理、空状態(Empty State)追加
- `BACKEND_DESIGN.md` v1.0 → v1.1: バックアップ・災害復旧計画(§8)追加。Firestoreバックアップ、RTDB障害フォールバック、メンテナンスモード自動移行、SLA定義
- `MONETIZATION_DESIGN.md`: §17実装ロードマップのクラフトシステムをMVPに移動（§9.4と整合）
- `AI_BOT_SPEC.md` v1.0 → v1.1: マリガン戦略(§2.4)追加。キープスコア算出基準、難易度別挙動、アーキタイプ別優先カード
- `GAME_DESIGN.md`: 付録Cバージョン履歴セクション追加（v0.1.0〜v0.5.0の変更追跡）

**更新（改善8件）:**
- `DECK_BUILDER_SPEC.md`: デッキコードにCRC-8チェックサム追加（破損検知・後方互換）
- `COLLECTION_UX_SPEC.md`: 仮想スクロール規定(§1.3)追加（200+カード対応）
- `SOCIAL_SPEC.md`: フレンド上限100→200人に引き上げ（配信者・競技プレイヤー対応）
- `EVENT_SYSTEM_SPEC.md`: イベント同時開催ルール(§7)追加（上限3件・優先表示・報酬計算）
- `SECURITY_SPEC.md`: DDoS保護・負荷テスト計画(§9)追加（Firebase App Check・レート制限・ストレステスト）
- `TUTORIAL_FLOW.md`: チュートリアル敗北時処理(§5)追加（巻き戻し・弱体化・強制勝利のフォールバック）
- `LOCALIZATION_SPEC.md`: RTL言語対応方針(§5.1)追加（レイアウトミラーリング・leading/trailing方針）

### v3.1 更新内容

**新規:**
- `AUDIT_REPORT_v3.md` — 第3回監査レポート

**更新（致命的4件）:**
- `GAME_DESIGN.md` v0.4.0 → v0.5.0: MaxHandSize=10 + 墓地定義(§5.5)、EffectKey 25種拡張(§11.1)、コストカーブ指針(§12b)、78カード付録A完全書き直し

**更新（重要8件）:**
- `SCREEN_SPEC.md`: バトル操作UIフロー(§7b)、マッチリザルト画面(§7c)、設定画面(§7d) 追加
- `TEST_PLAN.md` v1.0 → v2.0: アクセシビリティテスト(§9)、バランステスト強化(§10) 追加
- `ANALYTICS_SPEC.md` v1.0 → v1.1: メタゲーム分析(§5)、クライアントパフォーマンス追跡(§5.5) 追加
- `SOUND_DESIGN_SPEC.md`: SE_GAUGE_PUSH → SE_GAUGE_SHIFT に統一（ANIMATION_SPEC.md と整合）
- `NETWORK_SPEC.md` v1.0 → v1.1: 切断中ターンタイマー一時停止ルール追加

**更新（改善10件）:**
- `POST_MVP_ROADMAP.md` v2.0 → v2.1: ランクマッチ・シーズン設計方針、PvE方針、カードローテーション/フォーマット方針 追加
- `IMPLEMENTATION_PLAN.md` v2.0 → v2.1: 全Phase工数目安追加、CI/CD Phase 11 追加

### v3.0 統合・削除内容

**統合（削除済み・内容は移行先ファイルで参照）:**
- `ASSET_AUDIO.md` → `SOUND_DESIGN_SPEC.md` §7-12 に統合済み（ファイル削除）
- `ASSET_CARD_EFFECTS.md` → `ANIMATION_SPEC.md` §10-12 に統合済み（ファイル削除）

**削除（上位互換あり・ファイル削除済み）:**
- `AUDIT_REPORT.md` — `AUDIT_REPORT_v2.md` に置き換え済み
- `Banngannka_Foundation_v1.md` — `PRODUCT_REQUIREMENTS.md` + `GAME_DESIGN.md` でカバー済み
- `Banngannka_Game_Design_Pure_v1.md` — `GAME_DESIGN.md` の冗長コピーにつき削除
- `DECK_CONSTRUCTION_v2.md` — `GAME_DESIGN.md §6` + `DECK_BUILDER_SPEC.md` でカバー済み
- `SESSION_HANDOFF.md` — 陳腐化につき削除
- `Docs_INDEX.md` / `Docs_Specs_INDEX.md` — 本README に統合済み（`_INDEX.md` も含む）
