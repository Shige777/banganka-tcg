# Banngannka (万願果) — Claude Code Project Rules

## What This Is
1v1対戦デジタルTCG。Unity + Firebase構成。iOS-first → Steam → Android。

## Architecture
- **Engine**: Unity (C#), IL2CPP build
- **Backend**: Firebase (Auth / Firestore / RTDB / Cloud Functions v2 / Storage)
- **Realtime Sync**: Firebase Realtime Database (battle state)
- **Server Logic**: Cloud Functions (command validation, matchmaking, bot AI)
- **Client Data**: Firestore + StreamingAssets dual management

## Source of Truth (設計書の読み方)
設計書は `docs/` (or Banngannka_Rebuild_Pack/) にある。以下の優先順位に従うこと。

### 必ず読むファイル（全タスク共通）
1. **GAME_DESIGN.md** — ゲームルールの絶対正典。矛盾があればこのファイルが正。
2. **CARD_SCHEMA.md** — カードデータ構造の正典。
3. **PRODUCT_REQUIREMENTS.md** — MVP範囲とプラットフォーム仕様。

### タスク別に追加で読むファイル
| タスク | 読むファイル |
|--------|-------------|
| バトルロジック実装 | GAME_DESIGN.md §3-§12, ANIMATION_SPEC.md, SOUND_DESIGN_SPEC.md |
| カードデータ投入 | CARD_SCHEMA.md, CARD_TEXT_GUIDELINES.md, BALANCE_POLICY.md |
| Firebase/バックエンド | BACKEND_DESIGN.md, NETWORK_SPEC.md, SECURITY_SPEC.md |
| UI画面実装 | SCREEN_SPEC.md, UI_STYLE_GUIDE.md, ASSET_*.md (該当画面) |
| デッキビルダー | DECK_BUILDER_SPEC.md, COLLECTION_UX_SPEC.md |
| チュートリアル | TUTORIAL_FLOW.md, COMPANION_CHARACTER.md |
| ストーリーモード | STORY_BIBLE.md, STORY_CHAPTERS.md, CHAR_*.md |
| マネタイズ | MONETIZATION_DESIGN.md, ASSET_SHOP.md |
| テスト | TEST_PLAN.md, ERROR_HANDLING.md |
| パフォーマンス | PERFORMANCE_SPEC.md |
| アクセシビリティ | ACCESSIBILITY_SPEC.md |
| ソーシャル機能 | SOCIAL_SPEC.md |
| イベント/運営 | EVENT_SYSTEM_SPEC.md |
| リリース準備 | APPSTORE_CHECKLIST.md, LOCALIZATION_SPEC.md |
| AI Bot | AI_BOT_SPEC.md |
| リプレイ | REPLAY_SPEC.md |
| 通知 | NOTIFICATION_SPEC.md |

## Critical Rules (絶対に守ること)

### 用語ルール
以下の用語を使用禁止。正式用語を必ず使うこと。
| 禁止 | 正式 |
|------|------|
| ダメージ | 退場させる / 願力をN動かす |
| 破壊 | 退場 |
| タップ / アンタップ | 消耗 / 待機 |
| マナ | CP |
| バーン | 願力をN動かす |
| ヒーロー / チャンピオン | 願主 (Wish Master) |
| デッキ破壊 | (該当メカニクスなし) |

### 数値ルール
- デッキ: **34枚**固定、同名**3枚**上限
- HP: **100** (チュートリアルのみ30)
- CP: 毎ターン最大+1、上限**10**
- コストカーブ: 戦力 ≤ CP×2000+1000 ± キーワードペナルティ
- ターン制限: **24ターン**
- ターンタイマー: **90秒** (3回連続タイムアウト=敗北)
- 願力カード閾値: 85%/70%/55%/40%/25%/10%
- 進化ゲージ: Lv1→2 = 3ポイント、Lv2→3 = 4ポイント
- リーダースキル: 各レベル**1回/ゲーム**、**1回/ターン**、CP不要

### カードタイプ
- 顕現 (Manifestation) — ユニット
- 詠術 (Incantation) — スペル
- 界律 (Algorithm) — 共有フィールドルール (1枠、上書き式)

### アスペクト (願相) 6色
| 名前 | 英名 | HEX | リーダー |
|------|------|-----|---------|
| 曙赤 | Contest | #FF5A36 | Aldric |
| 空青 | Whisper | #4DA3FF | Vael |
| 穏緑 | Weave | #59C36A | 灯凪 |
| 妖紫 | Verse | #9A5BFF | Amara |
| 遊黄 | Manifest | #F4C542 | Rahim |
| 玄白 | Hush | #3A3A46 | 崔鋒 |

### 5画面構造 (変更禁止)
Home / Battle / Cards / Story / Shop — ボトムナビゲーション固定

### アクセシビリティ (設計初期から)
- WCAG 2.1 AA コントラスト比
- アスペクト識別: 色 + アイコンの二重表示（色のみ不可）
- タップターゲット: 最低 44×44pt

### Firebase構造
- Firestore = データの正典 (cardMaster, users, matches)
- RTDB = バトル中のリアルタイム同期のみ
- Cloud Functions = 全コマンド検証 (サーバー権威モデル)
- StreamingAssets = クライアントキャッシュ (起動時ハッシュ同期)

## Code Style
- C# — Unity公式コーディング規約準拠
- Cloud Functions — TypeScript
- ファイル名: PascalCase (Unity C#), camelCase (TypeScript)
- コメント: 日本語OK、ただしクラス名・メソッド名は英語

## Testing
- 5層テスト: Unit (L1) / Integration (L2) / Network (L3) / E2E (L4) / Balance (L5)
- バランステスト: 各デッキ組み合わせ最低3,000自動対戦
- 先攻勝率目標: 45%〜55%
- テスト結果は TEST_PLAN.md に準拠して記録
