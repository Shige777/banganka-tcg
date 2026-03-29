# ASSET_BATTLE_UI.md
> 万願果 バトルUI素材リスト v1

---

## 0. 目的

バトル画面を構成するUIパーツを一覧化する。  
ゲームプレイの中核となる画面のため、最優先で整備する。

---

## 1. フィールド構造

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| BATTLE_FIELD_BG | 1920×1080 | バトル背景（願相別6種） | Niji Journey |
| BATTLE_FIELD_GRID_PLAYER | 可変 | 自分側フィールドグリッド枠 | Leonardo AI |
| BATTLE_FIELD_GRID_ENEMY | 可変 | 相手側フィールドグリッド枠 | Leonardo AI |
| BATTLE_FIELD_CENTER_LINE | 可変 | 中央分割ライン | Leonardo AI |

---

## 2. 願主情報・コスト表示

> ⚠️ 万願果に「LP（ライフポイント）」の概念は存在しない。
> 各プレイヤーの体力は「HPゲージ + 閾値ライフカード」で表現される（§2b参照）。
> 願主パネルには願主の立ち絵・レベル・願相・願成ストックを表示する。

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| BATTLE_LEADER_PANEL_PLAYER | 320×160 | 自分願主情報パネル（立ち絵サムネ・レベル・願相アイコン） | Leonardo AI |
| BATTLE_LEADER_PANEL_ENEMY | 320×160 | 相手願主情報パネル（同上） | Leonardo AI |
| BATTLE_LEADER_EVO_INDICATOR | 80×32 | 願成ストック表示バッジ（現在のレベル段階） | Leonardo AI |
| BATTLE_COST_ORB_ACTIVE | 40×40 | コストオーブ（使用可能） | Leonardo AI |
| BATTLE_COST_ORB_SPENT | 40×40 | コストオーブ（使用済み） | Leonardo AI |
| BATTLE_COST_ORB_LOCKED | 40×40 | コストオーブ（未解放） | Leonardo AI |

---

## 2b. HPゲージ＋閾値ライフカード

> 万願果の勝敗を決める中核UI。各プレイヤーの HP 0-100 を表示。
> 数値表示で正確な残 HP を把握でき、閾値ライフカード（85/70/55/40/25/10）で戦況判定を支援。
> HP=0 到達で即座に KO 勝利判定が発生。

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| BATTLE_HP_GAUGE_BG | 800×40 | HPゲージ背景枠（中央配置・横長） | Leonardo AI |
| BATTLE_HP_GAUGE_FILL_PLAYER | 可変 | HPゲージ・自分側の塗り（暖色系） | Leonardo AI |
| BATTLE_HP_GAUGE_FILL_ENEMY | 可変 | HPゲージ・相手側の塗り（寒色系） | Leonardo AI |
| BATTLE_HP_GAUGE_CENTER_MARK | 20×40 | ゲージ中央マーカー（基準点） | Leonardo AI |
| BATTLE_HP_GAUGE_DANGER_FLASH | 800×40 | ゲージ低 HP 域の危険域点滅演出用オーバーレイ | Leonardo AI |

---

## 3. ターン管理UI

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| BATTLE_TURN_BANNER_MY | 640×120 | 自分ターン開始バナー | Leonardo AI |
| BATTLE_TURN_BANNER_ENEMY | 640×120 | 相手ターン開始バナー | Leonardo AI |
| BATTLE_BTN_END_TURN | 200×64 | エンドターンボタン（通常） | Leonardo AI |
| BATTLE_BTN_END_TURN_HOVER | 200×64 | エンドターンボタン（ホバー） | Leonardo AI |
| BATTLE_BTN_END_TURN_DISABLED | 200×64 | エンドターンボタン（無効） | Leonardo AI |
| BATTLE_TURN_COUNTER | 80×40 | ターン数表示バッジ | Leonardo AI |

---

## 4. 手札エリア

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| BATTLE_HAND_AREA_BG | 1920×200 | 手札エリア背景（半透明） | Leonardo AI |
| BATTLE_HAND_CARD_HOVER | カード比 | 手札ホバー時の浮き上がり枠 | Leonardo AI |
| BATTLE_HAND_CARD_SELECTED | カード比 | 手札選択時のハイライト枠 | Leonardo AI |
| BATTLE_DECK_COUNT_BADGE | 60×60 | デッキ残枚数表示 | Leonardo AI |
| BATTLE_GRAVE_COUNT_BADGE | 60×60 | 墓地（捨て札）枚数表示 | Leonardo AI |

---

## 5. 攻撃・効果エフェクト

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| BATTLE_FX_ATTACK_HIT | 256×256 | 通常攻撃ヒット（汎用） | Leonardo AI |
| BATTLE_FX_ATTACK_HIT_SORA | 256×256 | 攻撃ヒット・空属性 | Leonardo AI |
| BATTLE_FX_ATTACK_HIT_AKEBONO | 256×256 | 攻撃ヒット・曙属性 | Leonardo AI |
| BATTLE_FX_ATTACK_HIT_ODAYAKA | 256×256 | 攻撃ヒット・穏属性 | Leonardo AI |
| BATTLE_FX_ATTACK_HIT_AYAKASHI | 256×256 | 攻撃ヒット・妖属性 | Leonardo AI |
| BATTLE_FX_ATTACK_HIT_ASOBI | 256×256 | 攻撃ヒット・遊属性 | Leonardo AI |
| BATTLE_FX_ATTACK_HIT_KURO | 256×256 | 攻撃ヒット・玄属性 | Leonardo AI |
| BATTLE_FX_SPELL_CAST | 512×512 | 詠術発動エフェクト（汎用） | Leonardo AI |
| BATTLE_FX_FIELD_LAW | 1280×720 | 界律発動時の全面エフェクト | Leonardo AI |
| BATTLE_FX_DIRECT_HIT | 1280×720 | 直撃（顔面攻撃）時の演出 | Leonardo AI |

---

## 6. 勝敗画面

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| BATTLE_RESULT_WIN_BG | 1920×1080 | 勝利画面背景 | Niji Journey |
| BATTLE_RESULT_LOSE_BG | 1920×1080 | 敗北画面背景 | Niji Journey |
| BATTLE_RESULT_WIN_LOGO | 480×160 | 「VICTORY」ロゴ | Leonardo AI |
| BATTLE_RESULT_LOSE_LOGO | 480×160 | 「DEFEAT」ロゴ | Leonardo AI |
| BATTLE_RESULT_REWARD_PANEL | 640×400 | 報酬表示パネル | Leonardo AI |
| BATTLE_BTN_REMATCH | 240×64 | リマッチボタン | Leonardo AI |
| BATTLE_BTN_BACK_TO_HOME | 240×64 | ホームに戻るボタン | Leonardo AI |

---

## 7. 優先度

### 最優先
- BATTLE_FIELD_BG（曙・玄の2種から着手）
- BATTLE_WANRYOKU_GAUGE_BG / FILL_PLAYER / FILL_ENEMY / CENTER_MARK
- BATTLE_LEADER_PANEL_PLAYER / ENEMY
- BATTLE_COST_ORB 3種
- BATTLE_BTN_END_TURN

### 中優先
- BATTLE_FX_ATTACK_HIT 7種
- BATTLE_TURN_BANNER 2種
- BATTLE_HAND_AREA_BG

### 後優先
- BATTLE_RESULT 系
- BATTLE_FX_FIELD_LAW
- BATTLE_FX_DIRECT_HIT

---

## 8. AI生成ルール

- エフェクト系はアルファ（透過）PNG必須
- バトル背景は願相カラーをベースに `ART_DIRECTION.md §3` を参照
- HPゲージは「自分側：暖色（橙〜金）」「相手側：寒色（青〜紫）」で色分け。中央マーカーは白またはグレー
- 願主情報パネル・コスト系UIは暗背景に映えるよう発光感を持たせる
- ツールはLeonardo AI Canvas で枠・バッジ類を統一生成する
