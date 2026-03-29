# ASSET_STEAM.md
> 万願果 Steam固有素材リスト v1

---

## 0. 目的

Steamストアページ・クライアント表示・トレーラー制作に必要な素材を一覧化する。
Steamの規格要件を満たしつつ、世界観を最大限に伝えるキーアートを揃える。

> ⚠️ **MVP スコープ外**
> - 万願果 MVP は **iOS のみ** が対象（`GAME_DESIGN.md §0` 参照）
> - Android / Web / Steam はすべて **MVP対象外** であり、`POST_MVP_ROADMAP.md` Phase 5以降での展開
> - このファイルのアセットは **Steam展開時に着手** すること。MVP実装フェーズでは優先度を付けない

---

## 1. Steamストアページ素材

Steamの公式ガイドラインに基づくサイズ。すべてJPGまたはPNG（透過なし）。

| 素材名 | サイズ（px） | 内容 | ツール |
|---|---|---|---|
| STEAM_CAPSULE_SM | 231×87 | 小カプセル（サイドバー等） | Leonardo AI |
| STEAM_CAPSULE_MD | 467×181 | 中カプセル（検索結果） | Leonardo AI |
| STEAM_CAPSULE_LG | 460×215 | 大カプセル（フィーチャー等） | Leonardo AI |
| STEAM_CAPSULE_HERO | 1920×620 | ヒーローカプセル（ストアトップ） | Niji Journey |
| STEAM_HEADER_CAPSULE | 460×215 | ヘッダーカプセル | Leonardo AI |
| STEAM_PAGE_BACKGROUND | 1438×810 | ストアページ背景 | Niji Journey |
| STEAM_BUNDLE_HEADER | 707×232 | バンドルヘッダー（将来用） | Leonardo AI |

---

## 2. スクリーンショット素材

最低5枚必須（Steam要件）。ゲームの魅力を伝える場面を厳選する。

| 素材名 | サイズ | 内容 | ツール |
|---|---|---|---|
| SS_BATTLE_SCENE | 1920×1080 | バトル画面（迫力のある場面） | — |
| SS_CARD_ART | 1920×1080 | カードアート集（コレクション画面） | — |
| SS_STORY_SCENE | 1920×1080 | ストーリーシーン（果求者・ナルと願主） | — |
| SS_DECK_BUILD | 1920×1080 | デッキ編集画面 | — |
| SS_WORLD_INTRO | 1920×1080 | 世界観紹介（複数時代が混交する背景） | Niji Journey |
| SS_KEY_VISUAL | 1920×1080 | キービジュアル（全願主集合） | Niji Journey |

> SS系はゲーム内実画面キャプチャ + Niji Journeyキーアート合成で作成する。

---

## 3. トレーラー・PV用キーアート

| 素材名 | サイズ | 内容 | ツール |
|---|---|---|---|
| KV_MAIN | 3840×2160（4K） | メインキービジュアル（全願主） | Niji Journey |
| KV_NARU_SOLO | 2560×1440 | ナルソロキービジュアル | Niji Journey |
| KV_WISH_LINEUP | 3840×1080 | 6願相横並びバナー | Niji Journey |
| TRAILER_TITLE_CARD | 1920×1080 | トレーラー用タイトルカード | Leonardo AI |
| TRAILER_END_CARD | 1920×1080 | トレーラーエンドカード（リリース情報） | Leonardo AI |
| TRAILER_LOGO_WHITE | 640×200 | トレーラー用ロゴ（白抜き版） | Leonardo AI |

---

## 4. Steamクライアント内表示

| 素材名 | サイズ | 内容 | ツール |
|---|---|---|---|
| STEAM_LIBRARY_HEADER | 1920×620 | ライブラリヘッダー画像 | Niji Journey |
| STEAM_LIBRARY_LOGO | 640×360 | ライブラリロゴ（透過PNG） | Leonardo AI |
| STEAM_ICON_APP | 32×32 | アプリアイコン（小） | Leonardo AI |
| STEAM_ICON_APP_LG | 256×256 | アプリアイコン（大） | Leonardo AI |
| STEAM_COMMUNITY_BANNER | 1140×360 | Steamコミュニティバナー | Niji Journey |

---

## 5. Steamデック対応素材（Steam Deck）

| 素材名 | サイズ | 内容 | ツール |
|---|---|---|---|
| STEAMDECK_CAPSULE | 600×900 | Steam Deck用縦型カプセル | Leonardo AI |
| STEAMDECK_HERO | 3840×1100 | Steam Deck用ヒーロー画像 | Niji Journey |

---

## 6. 優先度

### 最優先（ストアページ公開に必須）
- STEAM_CAPSULE_SM / MD / LG / HERO
- STEAM_HEADER_CAPSULE
- SS 5枚（最低ライン）
- STEAM_ICON_APP / APP_LG

### 中優先
- KV_MAIN（トレーラー制作に必要）
- STEAM_LIBRARY_HEADER / LOGO
- SS_KEY_VISUAL / SS_WORLD_INTRO

### 後優先
- STEAMDECK 系
- KV_WISH_LINEUP
- STEAM_COMMUNITY_BANNER
- STEAM_BUNDLE_HEADER（将来用）

---

## 7. AI生成ルール

- カプセル系は**ロゴ・タイトル文字なし**のイラストのみで生成し、後からテキストを合成する（Steam規格）
- Niji Journey は `--ar 16:9` でスクリーンショット比率を統一する
- 4K素材（KV_MAIN等）は `--q 2 --style raw` で最高品質を狙う
- Leonardo AI Canvasでロゴ・テキスト合成を行い最終納品形式に仕上げる
- すべてRGB・sRGBカラープロファイルで納品する
