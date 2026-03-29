# ASSET_SHOP.md
> 万願果 ショップ画面素材リスト v1

---

## 0. 目的

ショップ画面（商品一覧・通貨・バナー等）に必要な素材を一覧化する。
MVPは収益化の完全実装より「見た目の骨格」を先行して整備する。

---

## 1. 背景

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| SHOP_BG_MAIN | 1920×1080 | ショップ背景（市場・交界の境界感） | Niji Journey |
| SHOP_BG_OVERLAY | 1920×1080 | グラデーションオーバーレイ | Leonardo AI |

---

## 2. 商品バナー・カード

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| SHOP_PRODUCT_BANNER_FEATURED | 800×300 | 特集商品バナー（大） | Niji Journey |
| SHOP_PRODUCT_CARD_BG | 320×400 | 商品カード背景（汎用） | Leonardo AI |
| SHOP_PRODUCT_CARD_HIGHLIGHT | 320×400 | 商品カード・おすすめ強調枠 | Leonardo AI |
| SHOP_PACK_ILLUSTRATION | 400×400 | カードパック外観イラスト（汎用） | Niji Journey |
| SHOP_PACK_ILLUSTRATION_PREMIUM | 400×400 | プレミアムパック外観 | Niji Journey |
| SHOP_BUNDLE_RIBBON | 200×60 | セット商品リボンラベル | Leonardo AI |

---

## 3. 通貨アイコン

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| SHOP_CURRENCY_PAID_ICON | 48×48 | 有償通貨アイコン（金色系） | Leonardo AI |
| SHOP_CURRENCY_FREE_ICON | 48×48 | 無償通貨アイコン（銀色系） | Leonardo AI |
| SHOP_CURRENCY_TICKET_ICON | 48×48 | ガチャチケットアイコン | Leonardo AI |
| SHOP_CURRENCY_DISPLAY_BG | 200×44 | 通貨残高表示バー背景 | Leonardo AI |

---

## 4. セールラベル・バッジ

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| SHOP_LABEL_SALE | 80×32 | 「SALE」ラベル | Leonardo AI |
| SHOP_LABEL_NEW | 80×32 | 「NEW」ラベル | Leonardo AI |
| SHOP_LABEL_LIMITED | 80×32 | 「期間限定」ラベル | Leonardo AI |
| SHOP_LABEL_BESTSELLER | 80×32 | 「人気」ラベル | Leonardo AI |
| SHOP_PRICE_TAG_BG | 120×40 | 価格タグ背景 | Leonardo AI |
| SHOP_DISCOUNT_BADGE | 64×64 | 割引率バッジ（〇〇%OFF） | Leonardo AI |

---

## 5. ショップUI

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| SHOP_TAB_BAR_BG | 800×48 | ショップタブバー背景 | Leonardo AI |
| SHOP_BTN_BUY_NORMAL | 200×52 | 購入ボタン・通常 | Leonardo AI |
| SHOP_BTN_BUY_PREMIUM | 200×52 | 購入ボタン・有償 | Leonardo AI |
| SHOP_BTN_BUY_FREE | 200×52 | 購入ボタン・無料 | Leonardo AI |
| SHOP_CONFIRMATION_PANEL | 640×360 | 購入確認ダイアログ背景 | Leonardo AI |

---

## 6. 優先度

### 最優先
- SHOP_BG_MAIN
- SHOP_PRODUCT_CARD_BG
- SHOP_CURRENCY_PAID_ICON / FREE_ICON
- SHOP_BTN_BUY_NORMAL / FREE

### 中優先
- SHOP_PRODUCT_BANNER_FEATURED
- SHOP_PACK_ILLUSTRATION
- SHOP_LABEL_SALE / NEW
- SHOP_PRICE_TAG_BG

### 後優先
- SHOP_PACK_ILLUSTRATION_PREMIUM
- SHOP_LABEL_LIMITED / BESTSELLER
- SHOP_DISCOUNT_BADGE
- SHOP_CONFIRMATION_PANEL

---

## 7. AI生成ルール

- ショップ背景は交界の「市場・取引の場」感を持たせる（商業的すぎず、世界観を維持）
- 通貨アイコンは TCG らしい「輝き・価値」を感じさせるデザイン（金・銀・宝石系）
- ラベル・バッジは視認性優先。小さくても読めること
- カードパックは「万願果を封じ込めた器」として世界観に合うビジュアルにする
- 価格タグは有償/無償で色分け（有償：金色系、無償：銀色系）
