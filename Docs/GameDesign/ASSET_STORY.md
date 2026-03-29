# ASSET_STORY.md
> 万願果 ストーリー画面素材リスト v1

---

## 0. 目的

ストーリー画面（章マップ・ノベルパート・キャラクター立ち絵・背景等）に必要な素材を一覧化する。
`STORY_BIBLE.md` および `STORY_CHAPTERS.md` の世界観・章構成と整合させること。

---

## 1. ストーリー画面背景

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| STORY_SCREEN_BG | 1920×1080 | ストーリー画面メイン背景（章マップ） | Niji Journey |
| STORY_CHAPTER_SELECT_OVERLAY | 1920×1080 | 章選択オーバーレイ（半透明） | Leonardo AI |

---

## 2. 章バナー・ラベル

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| STORY_CHAPTER_BANNER_01 | 640×160 | 第一章「灯凪の章」バナー（古代） | Niji Journey |
| STORY_CHAPTER_BANNER_02 | 640×160 | 第二章「Aldricの章」バナー（中世） | Niji Journey |
| STORY_CHAPTER_BANNER_03 | 640×160 | 第三章「崔鋒の章」バナー（近世） | Niji Journey |
| STORY_CHAPTER_BANNER_04 | 640×160 | 第四章「Rahimの章」バナー（近代） | Niji Journey |
| STORY_CHAPTER_BANNER_05 | 640×160 | 第五章「Amaraの章」バナー（近未来） | Niji Journey |
| STORY_CHAPTER_BANNER_06 | 640×160 | 終章「Vaelの章」バナー（交界） | Niji Journey |
| STORY_CHAPTER_NODE_LOCKED | 64×64 | 章ノード・未解放 | Leonardo AI |
| STORY_CHAPTER_NODE_OPEN | 64×64 | 章ノード・解放済み | Leonardo AI |
| STORY_CHAPTER_NODE_CURRENT | 64×64 | 章ノード・進行中 | Leonardo AI |
| STORY_CHAPTER_NODE_CLEAR | 64×64 | 章ノード・クリア済み | Leonardo AI |

---

## 3. キャラクター立ち絵

ノベルパートで使用する立ち絵（バスト〜ウエスト）。各キャラ最低3表情。

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| STORY_STAND_TOMONA_NEUTRAL | 480×720 | 灯凪・通常表情 | Niji Journey |
| STORY_STAND_TOMONA_SAD | 480×720 | 灯凪・悲しみ | Niji Journey |
| STORY_STAND_TOMONA_RESOLVE | 480×720 | 灯凪・決意 | Niji Journey |
| STORY_STAND_ALDRIC_NEUTRAL | 480×720 | Aldric・通常 | Niji Journey |
| STORY_STAND_ALDRIC_STERN | 480×720 | Aldric・厳しい表情 | Niji Journey |
| STORY_STAND_ALDRIC_BROKEN | 480×720 | Aldric・崩れた表情 | Niji Journey |
| STORY_STAND_SAIFENG_NEUTRAL | 480×720 | 崔鋒・通常（静かな目） | Niji Journey |
| STORY_STAND_SAIFENG_INTENSE | 480×720 | 崔鋒・鋭い目 | Niji Journey |
| STORY_STAND_SAIFENG_RARE_SMILE | 480×720 | 崔鋒・稀な笑み | Niji Journey |
| STORY_STAND_RAHIM_NEUTRAL | 480×720 | Rahim・通常（楽観的） | Niji Journey |
| STORY_STAND_RAHIM_DOUBT | 480×720 | Rahim・迷い顔 | Niji Journey |
| STORY_STAND_RAHIM_EARNEST | 480×720 | Rahim・真剣 | Niji Journey |
| STORY_STAND_AMARA_NEUTRAL | 480×720 | Amara・通常（観察眼） | Niji Journey |
| STORY_STAND_AMARA_SHARP | 480×720 | Amara・核心を突く表情 | Niji Journey |
| STORY_STAND_AMARA_FRAGILE | 480×720 | Amara・脆さが見える表情 | Niji Journey |
| STORY_STAND_VAEL_NEUTRAL | 480×720 | Vael・通常（謎めいた） | Niji Journey |
| STORY_STAND_VAEL_MIRROR | 480×720 | Vael・問う表情 | Niji Journey |
| STORY_STAND_NARU_NEUTRAL | 240×360 | ナル・通常（浮遊・猫型） | Niji Journey |
| STORY_STAND_NARU_SURPRISE | 240×360 | ナル・驚き | Niji Journey |
| STORY_STAND_NARU_QUIET | 240×360 | ナル・珍しく静か | Niji Journey |

> 各立ち絵は `--sref`（スタイルリファレンス）+ seed番号で全章を通じてキャラを統一する。（※ Niji v7 は `--cref` 非対応）

---

## 4. 章背景（ノベルパート）

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| STORY_BG_ANCIENT_CITY | 1920×1080 | 古代都市国家の情景（第一章） | Niji Journey |
| STORY_BG_MEDIEVAL_FIELD | 1920×1080 | 中世征服王朝・野原（第二章） | Niji Journey |
| STORY_BG_EARLY_MODERN_CITY | 1920×1080 | 近世革命期・都市（第三章） | Niji Journey |
| STORY_BG_INDUSTRIAL_RUINS | 1920×1080 | 近代産業崩壊期・廃工場（第四章） | Niji Journey |
| STORY_BG_NEON_RUINS | 1920×1080 | 近未来崩壊後・廃ネオン街（第五章） | Niji Journey |
| STORY_BG_KOKAI_VOID | 1920×1080 | 交界の虚空（終章） | Niji Journey |
| STORY_BG_KOKAI_GENERAL | 1920×1080 | 交界・汎用（章間シーン用） | Niji Journey |

---

## 5. ノベルUI

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| STORY_TEXTBOX_BG | 1920×220 | テキストボックス背景 | Leonardo AI |
| STORY_NAMEPLATE_BG | 320×48 | キャラ名前帯 | Leonardo AI |
| STORY_BTN_NEXT | 48×48 | 次へボタン（▶） | Leonardo AI |
| STORY_BTN_SKIP | 120×40 | スキップボタン | Leonardo AI |
| STORY_BTN_AUTO | 120×40 | オートボタン | Leonardo AI |
| STORY_BTN_BACKLOG | 120×40 | バックログボタン | Leonardo AI |
| STORY_BACKLOG_PANEL | 1920×1080 | バックログ表示パネル | Leonardo AI |

---

## 6. 優先度

### 最優先
- STORY_SCREEN_BG
- STORY_CHAPTER_BANNER 6種
- STORY_CHAPTER_NODE 4種（locked/open/current/clear）
- STORY_TEXTBOX_BG / NAMEPLATE_BG
- STORY_STAND_NARU 3種

### 中優先
- STORY_BG_ANCIENT_CITY（第一章から着手）
- STORY_STAND_TOMONA 3種（第一章の主役）
- STORY_BTN_NEXT / SKIP

### 後優先
- 残り願主立ち絵（第二章以降）
- 残り章背景
- STORY_BTN_AUTO / BACKLOG
- STORY_BACKLOG_PANEL

---

## 7. AI生成ルール

- 各章の背景は `ART_DIRECTION.md §3` の願相カラーとビジュアルテーマに準拠する
- 立ち絵は全身が映える縦長構図（`--ar 2:3`）で生成し、トリミングして使用
- 交界の背景は「時代の重なり・歪み」演出を積極的に使う
- ナルの立ち絵は他の立ち絵より小さく（浮遊・猫型サイズ感）、左右どちらにも配置できる構図で作る
- 章バナーは各時代・願相カラーを基調に章番号を入れる
