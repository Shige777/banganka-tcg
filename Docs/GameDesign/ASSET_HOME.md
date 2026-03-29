# ASSET_HOME.md
> 万願果 ホーム画面素材リスト v1

---

## 0. 目的

ゲームのメイン入口となるホーム画面に必要な素材を一覧化する。
ホームはプレイヤーが最も頻繁に目にする画面であり、世界観の第一印象を決定づける。

---

## 1. 背景・雰囲気

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| HOME_BG_MAIN | 1920×1080 | ホームメイン背景（交界の情景） | Niji Journey |
| HOME_BG_EVENT | 1920×1080 | イベント期間用差し替え背景（差分） | Niji Journey |
| HOME_BG_OVERLAY | 1920×1080 | グラデーションオーバーレイ（UI可読性確保） | Leonardo AI |

---

## 2. メインビジュアル・バナー

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| HOME_MAIN_VISUAL_LEADER | 800×600 | ホーム中央に表示する願主立ち絵（選択中の願主） | Niji Journey |
| HOME_BANNER_SLOT | 960×240 | イベント・新弾バナースロット枠 | Leonardo AI |
| HOME_BANNER_PLACEHOLDER | 960×240 | MVP用プレースホルダーバナー | Leonardo AI |
| HOME_EVENT_LABEL | 240×48 | 「開催中」ラベルバッジ | Leonardo AI |

---

## 3. ウィジェット・情報エリア

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| HOME_WIDGET_PANEL | 480×160 | ミッション・デイリー進捗パネル背景 | Leonardo AI |
| HOME_MISSION_ICON | 48×48 | ミッションアイコン（汎用） | Leonardo AI |
| HOME_DAILY_CHECK_ICON | 48×48 | デイリーチェックインアイコン | Leonardo AI |
| HOME_NEWS_PANEL | 800×120 | お知らせ帯 | Leonardo AI |
| HOME_NOTICE_BADGE | 24×24 | 未読通知バッジ（赤丸） | Leonardo AI |

---

## 4. CTAボタン

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| HOME_BTN_BATTLE_MAIN | 320×80 | バトル開始メインCTA | Leonardo AI |
| HOME_BTN_STORY_MAIN | 240×64 | ストーリー続きCTA | Leonardo AI |

---

## 5. 優先度

### 最優先
- HOME_BG_MAIN
- HOME_MAIN_VISUAL_LEADER（代表として灯凪またはAldric）
- HOME_BTN_BATTLE_MAIN
- HOME_BG_OVERLAY

### 中優先
- HOME_BANNER_SLOT / PLACEHOLDER
- HOME_WIDGET_PANEL
- HOME_NEWS_PANEL

### 後優先
- HOME_BG_EVENT
- HOME_EVENT_LABEL
- HOME_MISSION_ICON / DAILY_CHECK_ICON

---

## 6. AI生成ルール

- HOME_BG_MAIN は交界の壮大さを感じさせる。霧・時代の重なり・光の筋を意識する
- 願主立ち絵は他の画面（ASSET_STORY.md 等）の `--sref` + seed番号と統一する（※ Niji v7 は `--cref` 非対応）
- オーバーレイは上下に強めのグラデ（下部は特に下部ナビ視認性のため暗く）
- バナースロットはプレースホルダーでも見栄えを確保する（枠素材として機能する）
