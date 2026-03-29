# ASSET_ONBOARDING.md
> 万願果 オンボーディング素材リスト v1

---

## 0. 目的

初回起動から最初のバトル開始までの導線に使用する素材を一覧化する。  
プレイヤーの「第一印象」を決定づける画面群。

---

## 1. ローディング・起動画面

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| ONBOARD_SPLASH_BG | 1920×1080 | 初回起動スプラッシュ背景 | Niji Journey |
| ONBOARD_SPLASH_LOGO | 640×200 | スプラッシュ用タイトルロゴ | Leonardo AI |
| ONBOARD_LOADING_BG | 1920×1080 | ローディング画面背景 | Niji Journey |
| ONBOARD_LOADING_BAR_BG | 600×20 | ローディングバー背景 | Leonardo AI |
| ONBOARD_LOADING_BAR_FILL | 600×20 | ローディングバー中身（発光） | Leonardo AI |
| ONBOARD_LOADING_ICON | 64×64 | ローディングスピナーアイコン | Leonardo AI |
| ONBOARD_LOADING_TIPS_BG | 800×80 | ローディングTips表示帯 | Leonardo AI |

---

## 2. 規約・同意画面

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| ONBOARD_TERMS_BG | 1920×1080 | 規約画面背景 | Leonardo AI |
| ONBOARD_TERMS_PANEL | 800×600 | 規約テキスト表示パネル | Leonardo AI |
| ONBOARD_SCROLL_BAR | 12×500 | スクロールバー | Leonardo AI |
| ONBOARD_BTN_AGREE | 240×56 | 同意するボタン | Leonardo AI |
| ONBOARD_BTN_DISAGREE | 240×56 | 同意しないボタン | Leonardo AI |
| ONBOARD_CHECKBOX | 32×32 | チェックボックス（未・済） | Leonardo AI |

---

## 3. 名前入力・プロフィール設定

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| ONBOARD_PROFILE_BG | 1920×1080 | プロフィール設定画面背景 | Niji Journey |
| ONBOARD_NAME_INPUT_BG | 480×64 | 名前入力フィールド背景 | Leonardo AI |
| ONBOARD_AVATAR_SELECT_GRID | 600×400 | アバター選択グリッド枠 | Leonardo AI |
| ONBOARD_AVATAR_DEFAULT | 128×128 | デフォルトアバター（7種・願主別） | Niji Journey |
| ONBOARD_AVATAR_FRAME | 128×128 | アバターフレーム（選択中） | Leonardo AI |
| ONBOARD_BTN_DECIDE | 240×56 | 決定ボタン | Leonardo AI |

> アバターは願主6キャラ + ナルのアイコン版。`--sref` + seed番号でTUTO_NARU等と同一キャラを維持する。（※ Niji v7 は `--cref` 非対応）

---

## 4. 初回ストーリー導入

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| ONBOARD_INTRO_BG_01 | 1920×1080 | 導入シーン背景①（交界の虚空） | Niji Journey |
| ONBOARD_INTRO_BG_02 | 1920×1080 | 導入シーン背景②（時代が混交する世界） | Niji Journey |
| ONBOARD_INTRO_BG_03 | 1920×1080 | 導入シーン背景③（果求者が交界に現れるシーン） | Niji Journey |
| ONBOARD_INTRO_NARU_FULL | 600×900 | 導入用・ナルフルイラスト | Niji Journey |
| ONBOARD_INTRO_TITLE_CARD | 1280×720 | タイトルカード（万願果ロゴ入り） | Leonardo AI |
| ONBOARD_INTRO_CHAPTER_LABEL | 400×80 | 「序章」ラベル | Leonardo AI |
| ONBOARD_BTN_SKIP_INTRO | 160×44 | イントロスキップボタン | Leonardo AI |

---

## 5. 初回ガチャ（スターターパック）演出

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| ONBOARD_GACHA_BG | 1920×1080 | 初回ガチャ演出背景 | Niji Journey |
| ONBOARD_GACHA_BTN | 320×80 | ガチャ引くボタン（初回） | Leonardo AI |
| ONBOARD_GACHA_RESULT_FRAME | 800×500 | 初回ガチャ結果表示枠 | Leonardo AI |

---

## 6. 優先度

### 最優先
- ONBOARD_LOADING_BG / BAR 系
- ONBOARD_SPLASH_BG / LOGO
- ONBOARD_BTN_AGREE / DECIDE

### 中優先
- ONBOARD_INTRO_BG 3種
- ONBOARD_INTRO_NARU_FULL
- ONBOARD_AVATAR_DEFAULT 7種

### 後優先
- ONBOARD_GACHA 系
- ONBOARD_INTRO_TITLE_CARD
- ONBOARD_AVATAR_SELECT_GRID

---

## 7. AI生成ルール

- 導入背景（INTRO_BG）はART_DIRECTIONの「世界の歪み」演出を積極的に使う
- ナルフルイラストはNiji Journeyで `--ar 2:3` を指定して縦長構図で生成する
- ローディング系素材は発光感のあるダークトーンで統一する
- アバターはTUTORIALの立ち絵と `--sref` + seed番号を共有してキャラを統一する（※ Niji v7 は `--cref` 非対応）
