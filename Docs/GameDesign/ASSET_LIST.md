# ASSET_LIST.md

## 0. 目的

このファイルは、`万願果` を作るために必要な素材のカテゴリ一覧と、詳細仕様ファイルへの索引です。
各カテゴリの個別素材名・サイズ・AI生成ルールは、対応する `ASSET_*.md` ファイルに記載されています。

---

## 1. 詳細仕様ファイル一覧

| カテゴリ | 詳細ファイル | 内容概要 |
|---|---|---|
| ブランド | `ASSET_BRAND.md` | ロゴ・アイコン・スプラッシュ・OGP |
| 共通UI | `ASSET_COMMON_UI.md` | カード枠・ボタン・パネル・ナビ・ゲージ |
| カードイラスト | `ASSET_CARD_ILLUSTRATIONS.md` | 願主・顕現・詠術・界律の全イラスト |
| カードエフェクト | `ANIMATION_SPEC.md §10` | 召喚・消滅・ドロー・レアリティ演出（統合済み） |
| バトルUI | `ASSET_BATTLE_UI.md` | バトル画面の全UI素材 |
| ホーム画面 | `ASSET_HOME.md` | ホーム背景・メインビジュアル・ウィジェット |
| ストーリー | `ASSET_STORY.md` | 章バナー・立ち絵・ノベル背景・ノベルUI |
| ショップ | `ASSET_SHOP.md` | 商品バナー・通貨アイコン・ラベル |
| デッキ/コレクション | `ASSET_DECK_COLLECTION.md` | デッキ管理・コレクション画面UI |
| マッチメイキング | `ASSET_MATCHMAKING.md` | ルーム・マッチング画面UI |
| オンボーディング | `ASSET_ONBOARDING.md` | 初回起動〜最初のバトルまでの全素材 |
| チュートリアル | `ASSET_TUTORIAL.md` | ナル案内・チュートリアル専用素材 |
| Steam / 告知 | `ASSET_STEAM.md` | Steam向け・SNS告知用素材 |
| アカウント設定 | `ASSET_ACCOUNT_SETTINGS.md` | 設定・実績・プロフィール素材 |
| オーディオ | `SOUND_DESIGN_SPEC.md §7-12` | BGM・SE・ジングル・ボイス（統合済み） |

---

## 2. カテゴリ別概要

### ブランド素材（→ ASSET_BRAND.md）
ロゴ、タイトル画像、アプリアイコン、スプラッシュ画面、OGP

### 共通UI素材（→ ASSET_COMMON_UI.md）
共通カード枠、レアリティ差分、願相チップ、コストバッジ、ボタン、パネル、下部ナビアイコン、ゲージ素材

### カードイラスト（→ ASSET_CARD_ILLUSTRATIONS.md）
願主イラスト（6名×Lv3）、顕現イラスト（MVP84種）、詠術イラスト（MVP48種）、界律イラスト（MVP24種）、カード裏面

### バトルUI（→ ASSET_BATTLE_UI.md）
バトル背景（願相別6種）、LP/コストパネル、ターン管理UI、手札エリア、攻撃エフェクト、勝敗画面

### カードエフェクト（→ ANIMATION_SPEC.md §10）
召喚エフェクト（願相別6種）、攻撃モーション、レアリティ演出、消滅エフェクト、ドロー演出、詠術/界律専用エフェクト

### ストーリー素材（→ ASSET_STORY.md）
章バナー（6種）、キャラクター立ち絵（6願主＋ナル）、章背景（6種）、ノベルUI、章ノード

### ショップ素材（→ ASSET_SHOP.md）
商品バナー、通貨アイコン（有償/無償）、セールラベル、価格タグ、購入ボタン

### オーディオ（→ SOUND_DESIGN_SPEC.md §7-12）
BGM（タイトル/ホーム/バトル/ストーリー章別）、ジングル（勝利/敗北/レベルアップ）、SE（UI/カード/戦闘系）、ボイス（MVP後）

---

## 3. 優先度サマリー

### 最優先（MVP最初のバトルが成立するために必要）

- `ASSET_BRAND.md`：アイコン、ロゴ、スプラッシュ
- `ASSET_COMMON_UI.md`：カード枠、願相チップ、コストバッジ、ナビアイコン
- `ASSET_BATTLE_UI.md`：バトル背景（曙・玄）、LP/コストパネル、エンドターンボタン
- `ASSET_CARD_ILLUSTRATIONS.md`：顕現MVP12種（プレースホルダーでも可）
- `ASSET_HOME.md`：ホーム背景、メインCTA
- `SOUND_DESIGN_SPEC.md`：基本SE（カードプレイ・攻撃・ボタン）、バトルBGM

### 中優先（MVP完成度向上）

- `ANIMATION_SPEC.md §10`：召喚エフェクト6種、ドロー演出
- `ASSET_STORY.md`：章バナー6種、第一章背景・立ち絵
- `ASSET_ONBOARDING.md`：スプラッシュ、名前入力、ローディング
- `SOUND_DESIGN_SPEC.md`：勝利/敗北ジングル、願主レベルアップSE

### 後優先（ポリッシュ・MVP後）

- SSR演出素材（`ANIMATION_SPEC.md` §10.3）
- 全願主立ち絵Lv2/3（`ASSET_CARD_ILLUSTRATIONS.md` §1）
- ボイス（`SOUND_DESIGN_SPEC.md` §10）
- 季節イベント差分素材

---

## 4. AI生成ルール（全素材共通）

- 素材名は役割が分かる命名にする（ファイル別命名規則は各 `ASSET_*.md` を参照）
- 解像度と用途は各ファイルの表に従う
- 正式採用前提のものは保存先を統一する（`AI_HANDOFF/Assets/` 推奨）
- 世界観トーン・願相カラーは `ART_DIRECTION.md` と `UI_STYLE_GUIDE.md §6` に揃える
- 仮素材（プレースホルダー）は後から差し替えを前提として命名・管理する
