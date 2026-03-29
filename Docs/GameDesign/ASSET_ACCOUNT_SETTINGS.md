# ASSET_ACCOUNT_SETTINGS.md
> 万願果 アカウント・設定画面素材リスト v1

---

## 0. 目的

設定・プロフィール・実績・エラー画面など、ゲームプレイ外のシステム画面素材を一覧化する。

---

## 1. 設定画面

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| SETTINGS_BG | 1920×1080 | 設定画面背景 | Leonardo AI |
| SETTINGS_PANEL | 800×700 | 設定項目パネル | Leonardo AI |
| SETTINGS_SECTION_DIVIDER | 700×2 | セクション区切り線 | Leonardo AI |
| SETTINGS_SLIDER_TRACK | 400×12 | スライダー背景（音量等） | Leonardo AI |
| SETTINGS_SLIDER_THUMB | 28×28 | スライダーつまみ | Leonardo AI |
| SETTINGS_TOGGLE_ON | 64×32 | トグルスイッチ（ON） | Leonardo AI |
| SETTINGS_TOGGLE_OFF | 64×32 | トグルスイッチ（OFF） | Leonardo AI |
| SETTINGS_BTN_BACK | 48×48 | 戻るボタン | Leonardo AI |
| SETTINGS_LANG_SELECT_BG | 280×48 | 言語選択ドロップダウン | Leonardo AI |

---

## 2. プロフィール画面

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| PROFILE_BG | 1920×1080 | プロフィール画面背景 | Niji Journey |
| PROFILE_CARD | 640×360 | プロフィールカード枠 | Leonardo AI |
| PROFILE_AVATAR_FRAME_LG | 160×160 | アバター大枠（プロフィール用） | Leonardo AI |
| PROFILE_WIN_RATE_BADGE | 160×60 | 勝率バッジ | Leonardo AI |
| PROFILE_TOTAL_GAMES_BADGE | 160×60 | 総対戦数バッジ | Leonardo AI |
| PROFILE_FAVE_WISH_BADGE | 120×44 | よく使う願相バッジ | Leonardo AI |
| PROFILE_TITLE_SELECT_BG | 480×56 | 称号選択フィールド | Leonardo AI |

---

## 3. 実績画面

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| ACHIEVE_BG | 1920×1080 | 実績画面背景 | Leonardo AI |
| ACHIEVE_ITEM_BG_LOCKED | 480×80 | 実績アイテム枠（未達成） | Leonardo AI |
| ACHIEVE_ITEM_BG_UNLOCKED | 480×80 | 実績アイテム枠（達成済み） | Leonardo AI |
| ACHIEVE_ICON_LOCKED | 64×64 | 実績アイコン（未達成・グレー） | Leonardo AI |
| ACHIEVE_PROGRESS_BAR | 320×12 | 実績進捗バー | Leonardo AI |
| ACHIEVE_UNLOCK_POPUP | 640×160 | 実績解除ポップアップ | Leonardo AI |

---

## 4. Steam実績アイコン

Steam規格（64×64px PNG）。世界観に沿ったアイコンデザインで統一する。

| 素材名 | サイズ | 内容（想定実績） | ツール |
|---|---|---|---|
| STEAM_ACH_FIRST_WIN | 64×64 | 初勝利 | Leonardo AI |
| STEAM_ACH_FIRST_DECK | 64×64 | 初デッキ作成 | Leonardo AI |
| STEAM_ACH_WIN_10 | 64×64 | 10勝達成 | Leonardo AI |
| STEAM_ACH_WIN_100 | 64×64 | 100勝達成 | Leonardo AI |
| STEAM_ACH_ALL_WISH | 64×64 | 全願相デッキ作成 | Leonardo AI |
| STEAM_ACH_COLLECTION_50 | 64×64 | カード50種収集 | Leonardo AI |
| STEAM_ACH_COLLECTION_FULL | 64×64 | コレクションコンプリート | Leonardo AI |
| STEAM_ACH_STORY_CLEAR | 64×64 | ストーリークリア | Leonardo AI |
| STEAM_ACH_VAEL | 64×64 | Vael関連の隠し実績（存在証明） | Niji Journey |

---

## 5. エラー・システム通知

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| ERROR_PANEL_BG | 640×320 | 汎用エラーパネル | Leonardo AI |
| ERROR_ICON_NETWORK | 64×64 | 通信エラーアイコン | Leonardo AI |
| ERROR_ICON_MAINTENANCE | 64×64 | メンテナンスアイコン | Leonardo AI |
| ERROR_BTN_RETRY | 200×52 | リトライボタン | Leonardo AI |
| ERROR_BTN_CLOSE | 200×52 | 閉じるボタン | Leonardo AI |
| NOTIFY_POPUP_BG | 480×120 | 通知ポップアップ背景 | Leonardo AI |
| NOTIFY_ICON_INFO | 32×32 | インフォアイコン | Leonardo AI |
| NOTIFY_ICON_WARN | 32×32 | 警告アイコン | Leonardo AI |

---

## 6. 優先度

### 最優先
- SETTINGS_TOGGLE ON/OFF
- SETTINGS_SLIDER 系
- ERROR_PANEL_BG / BTN_RETRY

### 中優先
- PROFILE_CARD / AVATAR_FRAME_LG
- ACHIEVE_ITEM_BG 2種
- STEAM_ACH 主要9種

### 後優先
- PROFILE_TITLE_SELECT_BG
- ACHIEVE_UNLOCK_POPUP
- NOTIFY 系

---

## 7. AI生成ルール

- 設定・エラー系UIは派手さより「暗背景での視認性」を優先する
- Steam実績アイコンは64×64px必須・PNG形式・透過なし（Steamの仕様）
- 実績アイコンは願相カラーやキャラモチーフを1アイコン1テーマで表現する
- エラーアイコンは世界観を壊さないようにシンプルな紋様デザインにする
