# ASSET_COMMON_UI.md
> 万願果 共通UI素材リスト v1

---

## 0. 目的

全画面で共有して使われるUI部品（カード枠・ボタン・パネル・ナビアイコン等）を一覧化する。
一貫したビジュアル言語を保つため、バラバラに作らず本ファイルで統一管理する。
`UI_STYLE_GUIDE.md` および `ART_DIRECTION.md §3` のカラー定義と整合させること。

---

## 1. カード枠・レアリティ

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| CARD_FRAME_BASE | 300×420 | 共通カード枠（全カードタイプ） | Leonardo AI |
| CARD_FRAME_RARITY_C | 300×420 | コモン枠装飾差分 | Leonardo AI |
| CARD_FRAME_RARITY_R | 300×420 | レア枠装飾差分 | Leonardo AI |
| CARD_FRAME_RARITY_SR | 300×420 | SR枠装飾差分（金縁） | Leonardo AI |
| CARD_FRAME_RARITY_SSR | 300×420 | SSR枠装飾差分（虹彩） | Leonardo AI |
| CARD_BACK | 300×420 | カード裏面（共通） | Niji Journey |
| CARD_TYPE_BADGE_MANIFEST | 80×28 | 「顕現」タイプバッジ | Leonardo AI |
| CARD_TYPE_BADGE_SPELL | 80×28 | 「詠術」タイプバッジ | Leonardo AI |
| CARD_TYPE_BADGE_LAW | 80×28 | 「界律」タイプバッジ | Leonardo AI |

---

## 2. 願相チップ・コストバッジ

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| ASPECT_CHIP_SORA | 40×40 | 願相チップ・空（#4DA3FF） | Leonardo AI |
| ASPECT_CHIP_AKEBONO | 40×40 | 願相チップ・曙（#FF5A36） | Leonardo AI |
| ASPECT_CHIP_ODAYAKA | 40×40 | 願相チップ・穏（#59C36A） | Leonardo AI |
| ASPECT_CHIP_AYAKASHI | 40×40 | 願相チップ・妖（#9A5BFF） | Leonardo AI |
| ASPECT_CHIP_ASOBI | 40×40 | 願相チップ・遊（#F4C542） | Leonardo AI |
| ASPECT_CHIP_KURO | 40×40 | 願相チップ・玄（#3A3A46） | Leonardo AI |
| COST_BADGE_TEMPLATE | 48×48 | コストバッジ枠テンプレート（願相色差分で使い回す） | Leonardo AI |

---

## 3. ボタン共通セット

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| BTN_PRIMARY_NORMAL | 280×64 | 主CTA・通常 | Leonardo AI |
| BTN_PRIMARY_HOVER | 280×64 | 主CTA・ホバー | Leonardo AI |
| BTN_PRIMARY_DISABLED | 280×64 | 主CTA・無効 | Leonardo AI |
| BTN_SECONDARY_NORMAL | 200×52 | 副CTA・通常 | Leonardo AI |
| BTN_SECONDARY_DISABLED | 200×52 | 副CTA・無効 | Leonardo AI |
| BTN_ICON_CLOSE | 44×44 | 閉じるボタン（×） | Leonardo AI |
| BTN_ICON_BACK | 44×44 | 戻るボタン（←） | Leonardo AI |
| BTN_ICON_INFO | 44×44 | 情報ボタン（ⓘ） | Leonardo AI |

---

## 4. パネル・モーダル

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| PANEL_DARK_SM | 480×320 | 暗色半透明パネル・小 | Leonardo AI |
| PANEL_DARK_MD | 720×480 | 暗色半透明パネル・中 | Leonardo AI |
| PANEL_DARK_LG | 960×640 | 暗色半透明パネル・大 | Leonardo AI |
| MODAL_OVERLAY | 1920×1080 | モーダル背景オーバーレイ（半透明黒） | Leonardo AI |
| TOAST_BG | 480×64 | トースト通知背景 | Leonardo AI |
| DIALOG_CONFIRM_BG | 560×280 | 確認ダイアログ背景 | Leonardo AI |

---

## 5. 下部ナビゲーション

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| NAV_BAR_BG | 1920×96 | 下部ナビバー背景 | Leonardo AI |
| NAV_ICON_HOME_OFF | 48×48 | ホームアイコン・非選択 | Leonardo AI |
| NAV_ICON_HOME_ON | 48×48 | ホームアイコン・選択中 | Leonardo AI |
| NAV_ICON_BATTLE_OFF | 48×48 | バトルアイコン・非選択 | Leonardo AI |
| NAV_ICON_BATTLE_ON | 48×48 | バトルアイコン・選択中 | Leonardo AI |
| NAV_ICON_CARD_OFF | 48×48 | カードアイコン・非選択 | Leonardo AI |
| NAV_ICON_CARD_ON | 48×48 | カードアイコン・選択中 | Leonardo AI |
| NAV_ICON_STORY_OFF | 48×48 | ストーリーアイコン・非選択 | Leonardo AI |
| NAV_ICON_STORY_ON | 48×48 | ストーリーアイコン・選択中 | Leonardo AI |
| NAV_ICON_SHOP_OFF | 48×48 | ショップアイコン・非選択 | Leonardo AI |
| NAV_ICON_SHOP_ON | 48×48 | ショップアイコン・選択中 | Leonardo AI |

---

## 6. ゲージ・インジケーター

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| GAUGE_BASE_HORIZONTAL | 600×24 | 汎用ゲージ背景（横） | Leonardo AI |
| GAUGE_FILL_BLUE | 600×24 | ゲージ塗り・青系 | Leonardo AI |
| GAUGE_FILL_RED | 600×24 | ゲージ塗り・赤系 | Leonardo AI |
| GAUGE_FILL_GOLD | 600×24 | ゲージ塗り・金系 | Leonardo AI |
| BADGE_NUMBER_BG | 32×32 | 数値バッジ背景（通知数・枚数等） | Leonardo AI |
| LOADING_SPINNER | 64×64 | 汎用ローディングスピナー | Leonardo AI |

---

## 7. 汎用背景・テクスチャ

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| BG_DARK_GENERIC | 1920×1080 | 汎用暗背景（霧・光の筋・奥行き感） | Niji Journey |
| BG_DARK_SUBTLE | 1920×1080 | さらに控えめな汎用暗背景（設定・規約用途） | Leonardo AI |
| DIVIDER_LINE | 可変 | セクション区切り線 | Leonardo AI |
| SCROLL_BAR | 12×400 | スクロールバー | Leonardo AI |

---

## 8. 優先度

### 最優先
- CARD_FRAME_BASE
- ASPECT_CHIP 6種
- COST_BADGE_TEMPLATE
- BTN_PRIMARY 3種
- NAV_BAR_BG + NAV_ICON 10種（ON/OFF各5）
- BG_DARK_GENERIC

### 中優先
- CARD_FRAME_RARITY_C / R / SR
- CARD_TYPE_BADGE 3種
- PANEL_DARK_MD / LG
- MODAL_OVERLAY

### 後優先
- CARD_FRAME_RARITY_SSR
- BTN_ICON セット
- GAUGE系
- CARD_BACK

---

## 9. AI生成ルール

- 願相チップは各願相カラー（`UI_STYLE_GUIDE.md §6` HEX値）を厳守
- カード枠は共通シルエット。レアリティは装飾差分のみ変化させる
- ボタン・パネルはダークトーンベースで、文字とのコントラストを確保する
- ナビアイコンは ON/OFF が明確に区別できること（輝度差 or カラー差）
- 全素材はアルファ（透過）PNG で納品
