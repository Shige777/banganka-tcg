# ASSET_DECK_COLLECTION.md
> 万願果 デッキ・コレクション画面素材リスト v1

---

## 0. 目的

デッキ編集・カードコレクション一覧画面を構成するUI素材を一覧化する。  
プレイ外の「育成・収集」体験を担う画面群。

---

## 1. デッキ編集画面

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| DECK_EDITOR_BG | 1920×1080 | デッキ編集画面背景 | Niji Journey |
| DECK_SLOT_EMPTY | カード比 | デッキ空スロット枠 | Leonardo AI |
| DECK_SLOT_FILLED | カード比 | デッキ埋まりスロット（×2枚表示） | Leonardo AI |
| DECK_COUNT_BADGE | 80×40 | 現在枚数 / 上限枚数バッジ | Leonardo AI |
| DECK_NAME_INPUT_BG | 480×60 | デッキ名入力フィールド背景 | Leonardo AI |
| DECK_BTN_SAVE | 200×56 | 保存ボタン | Leonardo AI |
| DECK_BTN_RESET | 200×56 | リセットボタン | Leonardo AI |
| DECK_BTN_COPY | 200×56 | コピーボタン | Leonardo AI |
| DECK_PANEL_WISH_FILTER | 480×80 | 願相フィルターバー（6色ドット） | Leonardo AI |
| DECK_ICON_SORT | 32×32 | ソートアイコン | Leonardo AI |

---

## 2. カードコレクション一覧

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| COLLECTION_BG | 1920×1080 | コレクション画面背景 | Niji Journey |
| COLLECTION_CARD_OWNED | カード比 | 所持済みカード表示枠 | Leonardo AI |
| COLLECTION_CARD_UNOWNED | カード比 | 未所持カードのシルエット表示 | Leonardo AI |
| COLLECTION_CARD_NEW_BADGE | 60×30 | NEWバッジ（新規入手） | Leonardo AI |
| COLLECTION_CARD_FOIL_OVERLAY | カード比 | フォイル（キラ）加工オーバーレイ | Leonardo AI |
| COLLECTION_PROGRESS_BAR | 480×20 | 図鑑コンプ率ゲージ | Leonardo AI |
| COLLECTION_WISH_TAB | 120×44 | 願相タブボタン（6種） | Leonardo AI |
| COLLECTION_RARITY_TAB | 80×44 | レアリティタブボタン | Leonardo AI |

---

## 3. フィルター・ソートUI

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| FILTER_PANEL_BG | 360×600 | フィルターパネル背景 | Leonardo AI |
| FILTER_CHIP_ACTIVE | 120×36 | フィルターチップ（選択中） | Leonardo AI |
| FILTER_CHIP_INACTIVE | 120×36 | フィルターチップ（未選択） | Leonardo AI |
| FILTER_BTN_APPLY | 200×52 | フィルター適用ボタン | Leonardo AI |
| FILTER_BTN_CLEAR | 200×52 | フィルタークリアボタン | Leonardo AI |
| SORT_DROPDOWN_BG | 240×48 | ソートドロップダウン背景 | Leonardo AI |

---

## 4. カード詳細ポップアップ

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| DETAIL_POPUP_BG | 800×600 | カード詳細ポップアップ背景 | Leonardo AI |
| DETAIL_CARD_LARGE | カード比大 | 拡大カード表示エリア | — |
| DETAIL_FLAVOR_TEXT_BG | 600×80 | フレーバーテキスト帯 | Leonardo AI |
| DETAIL_STATUS_PANEL | 300×200 | ステータス表示パネル | Leonardo AI |
| DETAIL_RELATED_CARDS_RAIL | 780×160 | 関連カードスクロールレール | Leonardo AI |
| DETAIL_BTN_ADD_TO_DECK | 240×52 | デッキに追加ボタン | Leonardo AI |
| DETAIL_BTN_CLOSE | 48×48 | 閉じるボタン | Leonardo AI |

---

## 5. 未獲得カード表示

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| UNOWNED_SILHOUETTE_MASK | カード比 | シルエット化マスク（黒塗り） | Leonardo AI |
| UNOWNED_LOCK_ICON | 40×40 | 鍵アイコン | Leonardo AI |
| UNOWNED_HINT_LABEL | 180×32 | 「入手方法を見る」ラベル | Leonardo AI |

---

## 6. 優先度

### 最優先
- DECK_PANEL_WISH_FILTER
- COLLECTION_CARD_OWNED / UNOWNED
- COLLECTION_WISH_TAB 6種

### 中優先
- DETAIL_POPUP_BG 一式
- FILTER_CHIP 2種
- DECK_SLOT 2種

### 後優先
- COLLECTION_CARD_FOIL_OVERLAY
- DETAIL_RELATED_CARDS_RAIL
- COLLECTION_PROGRESS_BAR

---

## 7. AI生成ルール

- 背景はART_DIRECTIONの暗色ベース（黒・濃紺）を踏襲する
- タブ・チップ類はLeonardo AI Canvasで願相カラーに統一する
- シルエット素材はアルファPNG前提で作成する
