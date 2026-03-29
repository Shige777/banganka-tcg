# ASSET_MATCHMAKING.md
> 万願果 マッチング・対戦前演出素材リスト v1

---

## 0. 目的

対戦相手とのマッチング待機〜バトル開始までの演出素材を一覧化する。
「これから戦う」緊張感と期待感を高める演出が目的。

> ⚠️ **MVP スコープ注意**
> - §1（マッチング待機画面）・§2（バトル開始演出）・§4（対戦前カットイン演出）は **MVP対象**
> - §3（ランク・段位アイコン）は **MVP対象**（ランクマッチはv0.5.4でMVP昇格。`GAME_DESIGN.md §14` 参照）

---

## 1. マッチング待機画面

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| MATCH_WAITING_BG | 1920×1080 | マッチング待機背景（ループアニメ前提） | Niji Journey |
| MATCH_SPINNER | 128×128 | マッチング中スピナー（回転アニメ） | Leonardo AI |
| MATCH_PLAYER_CARD | 400×200 | 自分のプロフィールカード表示枠 | Leonardo AI |
| MATCH_VS_LOGO | 240×120 | VS ロゴ | Leonardo AI |
| MATCH_ENEMY_CARD | 400×200 | 相手プロフィールカード（マスク版） | Leonardo AI |
| MATCH_BTN_CANCEL | 200×52 | マッチングキャンセルボタン | Leonardo AI |
| MATCH_ELAPSED_TIMER | 120×40 | 経過時間タイマー表示 | Leonardo AI |

---

## 2. 対戦相手プロフィール表示

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| MATCH_PROFILE_FOUND_BG | 1920×1080 | 対戦相手発見画面背景 | Niji Journey |
| MATCH_PROFILE_PANEL | 640×280 | 相手プロフィールパネル | Leonardo AI |
| MATCH_RANK_BADGE | 80×80 | ランクバッジ枠（ランク別差分あり） | Leonardo AI |
| MATCH_WIN_RATE_LABEL | 160×40 | 勝率ラベル | Leonardo AI |
| MATCH_FAVE_WISH_CHIP | 100×36 | よく使う願相チップ | Leonardo AI |
| MATCH_BTN_ACCEPT | 240×60 | 対戦受諾ボタン | Leonardo AI |
| MATCH_BTN_DECLINE | 240×60 | 対戦辞退ボタン | Leonardo AI |
| MATCH_ACCEPT_TIMER | 80×80 | 受諾タイムアウトタイマー（円形） | Leonardo AI |

---

## 3. ランク・段位アイコン

段位ごとに願相カラーを割り当てる。各段位3〜5ランクを想定。

| 素材名 | サイズ | 内容 | ツール |
|---|---|---|---|
| RANK_ICON_NOVICE | 80×80 | 初心者ランク（灰） | Leonardo AI |
| RANK_ICON_BRONZE | 80×80 | ブロンズ（玄カラー） | Leonardo AI |
| RANK_ICON_SILVER | 80×80 | シルバー（空カラー） | Leonardo AI |
| RANK_ICON_GOLD | 80×80 | ゴールド（遊カラー） | Leonardo AI |
| RANK_ICON_PLATINUM | 80×80 | プラチナ（穏カラー） | Leonardo AI |
| RANK_ICON_MASTER | 80×80 | マスター（妖カラー） | Leonardo AI |
| RANK_ICON_GRANDMASTER | 120×120 | グランドマスター（曙カラー・特別演出） | Niji Journey |
| RANK_UP_BANNER | 640×160 | ランクアップ演出バナー | Leonardo AI |
| RANK_DOWN_BANNER | 640×160 | ランクダウン演出バナー | Leonardo AI |

---

## 4. 対戦前カットイン演出

バトル開始直前の「名乗り合い」演出。キャラ別の短い静止画スライド。

| 素材名 | サイズ | 内容 | ツール |
|---|---|---|---|
| CUTIN_BG_PLAYER | 1920×1080 | プレイヤー側カットイン背景（願相別） | Niji Journey |
| CUTIN_BG_ENEMY | 1920×1080 | 相手側カットイン背景 | Niji Journey |
| CUTIN_WISH_LOGO_SORA | 400×120 | 願相ロゴ・空 | Leonardo AI |
| CUTIN_WISH_LOGO_AKEBONO | 400×120 | 願相ロゴ・曙 | Leonardo AI |
| CUTIN_WISH_LOGO_ODAYAKA | 400×120 | 願相ロゴ・穏 | Leonardo AI |
| CUTIN_WISH_LOGO_AYAKASHI | 400×120 | 願相ロゴ・妖 | Leonardo AI |
| CUTIN_WISH_LOGO_ASOBI | 400×120 | 願相ロゴ・遊 | Leonardo AI |
| CUTIN_WISH_LOGO_KURO | 400×120 | 願相ロゴ・玄 | Leonardo AI |
| CUTIN_PANEL_DIVIDER | 1920×8 | カットイン中央分割ライン | Leonardo AI |
| CUTIN_BATTLE_START_LOGO | 480×160 | 「BATTLE START」ロゴ | Leonardo AI |

---

## 5. 優先度

### 最優先（MVP対象）
- MATCH_WAITING_BG
- MATCH_VS_LOGO
- MATCH_BTN_ACCEPT / CANCEL
- CUTIN_BATTLE_START_LOGO

### 中優先（MVP対象）
- MATCH_PROFILE_PANEL 一式
- CUTIN_WISH_LOGO 6種
- MATCH_PLAYER_CARD / ENEMY_CARD

### 後優先（MVP対象外 — §0のスコープ注意参照）
- RANK_ICON 全種（ランクマッチ実装時に着手）
- CUTIN_BG_PLAYER / ENEMY（願相別展開）
- RANK_ICON_GRANDMASTER
- RANK_UP / DOWN_BANNER

---

## 6. AI生成ルール

- マッチング背景はループアニメを想定し、左右・上下が繋がるタイリング構図にする
- ランクアイコンは願相カラーHEX値（ART_DIRECTION §3）を厳守する
- カットイン背景はART_DIRECTIONの「世界の歪み」と願相テーマを融合させた構図にする
- カットインロゴ類はLeonardo AI Canvasで日本語・英語の両バージョンを生成する
