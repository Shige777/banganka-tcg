# Banngannka — Claude Code フェーズ別プロンプト集

以下をClaude Codeにコピペして使ってください。
CLAUDE.mdがプロジェクトルートにあれば、基本ルールは自動で読み込まれます。

---

## Phase 1: スペック確定・基盤構築 (1-2週)

```
Unityプロジェクト「Banngannka」を新規作成してください。

【やること】
1. Unity プロジェクト初期化 (2D, URP)
2. フォルダ構造を作成:
   - Assets/Scripts/{Battle, Cards, UI, Network, Data, Audio, Utils}
   - Assets/Prefabs/{Cards, UI, Effects}
   - Assets/StreamingAssets/Cards/
   - Assets/Resources/{Sprites, Audio, Fonts}
3. docs/ フォルダに Banngannka_Rebuild_Pack の全 .md ファイルをコピー
4. .gitignore (Unity用) を作成
5. 基本的なシーン構造 (Boot, Home, Battle, Cards, Story, Shop) を作成
6. asmdef でアセンブリ分割 (Battle, Network, Data, UI, Tests)

【設計書】docs/IMPLEMENTATION_PLAN.md Phase 1 を参照
```

---

## Phase 2: コアデータ (1-2週)

```
カードデータシステムを実装してください。

【やること】
1. docs/CARD_SCHEMA.md に基づいてC#のカードデータクラスを作成
   - CardData (共通フィールド: id, name, type, cpCost, aspectTag, rarity, effectKey, params, emotionTag)
   - ManifestationData (battlePower, wishDamage, keywords, wishTrigger)
   - IncantationData (target, 条件パラメータ)
   - AlgorithmData (globalRule, ownerBonus)
   - LeaderData (basePower, baseWishDamage, keyAspect, levelCap, evoGaugeMax, skills)
2. 6リーダー定義を ScriptableObject で作成 (docs/CARD_SCHEMA.md §5 参照)
3. 162枚のカードプールを JSON 形式で StreamingAssets/Cards/ に作成 (docs/GAME_DESIGN.md Appendix A 参照)
4. CardDatabase クラス (ロード・検索・フィルタ) を実装
5. プリセットデッキ3種を定義 (docs/DECK_BUILDER_SPEC.md §8 参照)

【重要ルール】
- コストカーブ: 戦力 ≤ CP×2000+1000 ± キーワードペナルティ
- レアリティ分布: 78C / 48R / 24SR / 12SSR = 162枚
- アスペクト6色均等
```

---

## Phase 3: Firebase基盤 (2-3週)

```
Firebase基盤を構築してください。

【やること】
1. Firebase Unity SDK 統合 (Auth, Firestore, RTDB, Functions, Analytics)
2. 認証フロー実装 (匿名ログイン + Apple Sign-in)
3. Firestore スキーマ実装 (docs/BACKEND_DESIGN.md 準拠):
   - cardMaster/{id} — カードデータ正典
   - users/{uid} — プレイヤーデータ
   - users/{uid}/decks/{deckId} — デッキ
   - users/{uid}/collection/{cardId} — カードコレクション
4. Security Rules を docs/SECURITY_SPEC.md に基づいて設定
5. Cloud Functions v2 プロジェクト初期化 (TypeScript)
6. 起動時のカードデータ・ハッシュ同期メカニズム

【設計書】docs/BACKEND_DESIGN.md, docs/SECURITY_SPEC.md
```

---

## Phase 4: コアバトルロジック (3-4週)

```
対戦バトルのコアロジックを実装してください。

【やること — docs/GAME_DESIGN.md §3-§12 を必ず読んでから着手】
1. MatchState ステートマシン (Mulligan → TurnStart → Draw → Main → Battle → TurnEnd → Victory)
2. ターン進行 (§10): CP回復(§4.1)、ドロー、90秒タイマー、3回タイムアウト敗北
3. 顕現プレイ: 前衛3/後衛3配置、召喚酔い、キーワード(Rush/Blocker/GuardBreak)
4. 詠術プレイ: EffectKey解決 (§11 の全EffectKey対応)
5. 界律プレイ: 1枠上書き、裏伏せ→公開、全員効果+設置者ボーナス
6. 戦闘: アタック宣言→ブロック(5秒)→戦力比較→退場→願主ダメージ
7. 願力システム: HP閾値(85/70/55/40/25/10%)で願力カード発動
8. 奇襲(Ambush): 防衛/報復の2種、手札コスト、1ターン1回、3秒ウィンドウ
9. リーダー進化: ゲージ(Lv2=3pt, Lv3=4pt)、スキル発動
10. 勝利判定: 鯱鰈勝利(HP0+直接攻撃) / 塗り勝利(24ターン制限)
11. バトルログ (9イベントタイプ)

【テスト】各メカニクスにUnit Test必須。docs/TEST_PLAN.md §L1 参照
```

---

## Phase 5: ネットワーク・マッチメイキング (2-3週)

```
オンライン対戦とマッチメイキングを実装してください。

【やること】
1. Cloud Functions: createRoom, joinRoom, startMatch, processCommand, checkTimeout
2. processCommand: 7コマンドタイプの検証 (playCard, attack, block, endTurn, useSkill, setAlgorithm, ambush)
3. RTDB リスナー (Unity側): ルーム状態・ターン更新の同期
4. マリガン同期フロー
5. 切断処理: 60秒猶予→再接続 / タイムアウト敗北
6. AI Bot対戦 (Cloud Functions): 3難易度 (docs/AI_BOT_SPEC.md)
7. フレンド対戦 (ルームコード方式)
8. マッチメイキングUI

【設計書】docs/NETWORK_SPEC.md, docs/BACKEND_DESIGN.md, docs/AI_BOT_SPEC.md
```

---

## Phase 6: 画面シェル (2-3週)

```
5画面のUI基盤を実装してください。

【やること】
1. ボトムナビゲーション (Home/Battle/Cards/Story/Shop) — 常時表示
2. 各画面のシェル実装:
   - Home: 背景、バトルCTA、バナー、ミッション進捗
   - Battle: フィールドグリッド(上中下段)、HP/CPパネル、ターン管理
   - Cards: カードグリッド表示、フィルタ、ソート
   - Story: チャプターマップ
   - Shop: 商品グリッド、通貨表示
3. デッキビルダーUI (docs/DECK_BUILDER_SPEC.md 完全準拠):
   - 34枚スロット、フィルタ(願相/タイプ/コスト/レアリティ)
   - コストカーブ表示、デッキコード(BNG1:{base64}:{CRC8})
4. オンボーディングフロー

【設計書】docs/SCREEN_SPEC.md, docs/UI_STYLE_GUIDE.md, docs/DECK_BUILDER_SPEC.md
【重要】アスペクト識別は色+アイコンの二重表示必須 (ACCESSIBILITY_SPEC.md)
```

---

## Phase 7: マネタイズ (3-4週)

```
課金・報酬システムを実装してください。

【やること】
1. 通貨システム: ゴールド + 願晶 (有償)
2. パック開封 (シリーズ/願相ピックアップ) + 開封アニメーション
3. カード生成・分解 (docs/CARD_SCHEMA.md §6.1 のコスト表準拠)
4. 願道パス (バトルパス): 30レベル
5. リワード広告 (AdMob, モバイルのみ)
6. IAP: 5通貨パック + スターターバンドル
7. Cloud Functions: purchaseItem, verifyReceipt
8. デイリー/ウィークリーミッション
9. ログインボーナス
10. デイリーショップローテーション

【設計書】docs/MONETIZATION_DESIGN.md, docs/ASSET_SHOP.md
```

---

## Phase 8: 画面エンリッチメント (3-4週)

```
各画面にデータ・アニメーション・サウンドを統合してください。

【やること】
1. バトル画面: カード・リーダー・HPデータバインド
2. バトルアニメーション (docs/ANIMATION_SPEC.md 全パターン)
3. カードコレクション画面 (docs/COLLECTION_UX_SPEC.md)
4. ストーリーモード: チャプターデータ+ノード
5. ショップ: データバインド+購入フロー
6. ホーム: イベント・ミッション進捗表示
7. BGM 11曲 + SE 48種 統合 (docs/SOUND_DESIGN_SPEC.md)
8. フレンドシステム (docs/SOCIAL_SPEC.md P0機能)
9. エモート6種

【設計書】docs/ANIMATION_SPEC.md, docs/SOUND_DESIGN_SPEC.md, docs/SOCIAL_SPEC.md
```

---

## Phase 9: チュートリアル・ストーリー (1-2週)

```
チュートリアルとストーリーCh1を実装してください。

【やること】
1. 12ステップチュートリアル (docs/TUTORIAL_FLOW.md 完全準拠)
   - 固定デッキ・固定AIダミー
   - HP100 (通常値)、各ステップのNal台詞
   - スキップ+再開機能
   - 敗北時リカバリー (巻き戻し3回→強制勝利)
2. ストーリーCh1 (docs/STORY_CHAPTERS.md)
3. Nalコンパニオン表示 (docs/COMPANION_CHARACTER.md)
4. チュートリアル完了後のCTAパルス

【設計書】docs/TUTORIAL_FLOW.md, docs/STORY_BIBLE.md, docs/COMPANION_CHARACTER.md
```

---

## Phase 10: QA (3-4週)

```
品質保証テストを実施してください。

【やること — docs/TEST_PLAN.md を全セクション読んでから着手】
1. アクセシビリティP0 (docs/ACCESSIBILITY_SPEC.md §8)
2. WCAG 2.1 AA コントラスト監査
3. パフォーマンス (docs/PERFORMANCE_SPEC.md §8): 60fps, 800MB上限, 200MB DL, 5秒起動
4. Unit Test: TEST_PLAN.md §L1 全シナリオ
5. Integration Test: §L2
6. Network Test: §L3 (Firebase emulator)
7. E2E: §L4 (端末3台)
8. バランステスト: §L5 (各組み合わせ最低3,000自動対戦)
9. エラーハンドリング全パス (docs/ERROR_HANDLING.md)
10. セキュリティ検証 (docs/SECURITY_SPEC.md)
11. App Store準備 (docs/APPSTORE_CHECKLIST.md)
12. 用語監査 (GAME_DESIGN.md + CARD_TEXT_GUIDELINES.md の禁止用語)

【目標】先攻勝率45-55%、アーキタイプ別勝率45-55%
```

---

## Phase 11: CI/CD (全フェーズ並行)

```
CI/CDパイプラインを構築してください。

【やること】
1. Git設定 (Unity .gitignore, Git LFS for assets)
2. GitHub Actions:
   - Push: Unit Test実行
   - PR: レビュー + テストレポート
   - Main merge: Cloud Build → TestFlight自動配布
3. Firebase emulator CI統合
4. カードデータ検証自動化 (docs/CARD_SCHEMA.md §9 バリデーション)
5. ビルド番号自動インクリメント
6. Crashlytics / Analytics 環境分離 (dev/staging/prod)

【設計書】docs/IMPLEMENTATION_PLAN.md §11
```

---

## 汎用：困ったときのプロンプト

### 設計書を確認させたいとき
```
docs/GAME_DESIGN.md の §7 (リーダーシステム) を読んで、
現在の LeaderController.cs の実装と設計書の差分を報告してください。
```

### バグ修正時
```
[バグの症状を書く]

関連する設計書: docs/GAME_DESIGN.md §[該当セクション]
正しい挙動は設計書に定義されているので、まず読んでから修正してください。
```

### 新しいカードを追加するとき
```
docs/CARD_SET_TEMPLATE.md のテンプレートに従って、
Set 2 のカードを [枚数] 枚設計してください。
コストカーブは docs/GAME_DESIGN.md §12b、
テキストは docs/CARD_TEXT_GUIDELINES.md に準拠すること。
```
