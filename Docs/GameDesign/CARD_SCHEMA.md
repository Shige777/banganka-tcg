# CARD_SCHEMA.md
> 万願果 カードデータスキーマ v2.0

---

## 0. 目的

このファイルは、カードデータを AI と実装者が同じ構造で扱うためのスキーマ定義です。
`GAME_DESIGN.md` 付録B のデータ設計に準拠し、Firestore `cardMaster/` と Unity StreamingAssets の双方で使用する正式なスキーマとする。

> **v1.0 → v2.0 変更点**: GAME_DESIGN.md v0.5.0 付録B との完全整合。`params` オブジェクト追加、EffectKey パラメータ構造定義、クラフト・アナリティクスフィールド追加。

---

## 1. 共通 CardData（全カード型共通）

### 1.1 必須項目

| フィールド | 型 | 説明 |
|-----------|------|------|
| `id` | `string` | 一意識別子。命名規則: `{TYPE}_{ASPECT}_{NUMBER}` （例: `MAN_AKE_01`, `SPL_SOR_03`, `ALG_YOU_01`） |
| `name` | `string` | カード名（日本語） |
| `type` | `"顕現" \| "詠術" \| "界律"` | カード種別 |
| `cpCost` | `int` (0〜10) | コスト。GAME_DESIGN.md §12b のコストカーブに準拠 |
| `aspectTag` | `string` | 願相タグ。`"Contest"(曙)`, `"Whisper"(空)`, `"Weave"(穏)`, `"Verse"(妖)`, `"Manifest"(遊)`, `"Hush"(玄)` のいずれか |
| `rarity` | `"C" \| "R" \| "SR" \| "SSR"` | レアリティ。MVP分布: 78C / 48R / 24SR / 12SSR = 162種 |
| `effectKey` | `string` | 効果識別子。GAME_DESIGN.md §11.1 の EffectKey 一覧に準拠 |
| `params` | `object` | effectKey に対応するパラメータ群。型ごとに§2〜§4で定義 |
| `emotionTag` | `string` | 情相タグ。`"Thirst"(渇望)`, `"Atonement"(贖罪)`, `"Grace"(慈愛)`, `"Cling"(執着)`, `"Resign"(諦観)`, `"Grudge"(怨念)`, `"None"(無相)` のいずれか。界律の設置者ボーナス条件・情相シナジーに使用（GAME_DESIGN.md §5.4b参照） |

### 1.2 任意項目

| フィールド | 型 | 説明 |
|-----------|------|------|
| `flavorText` | `string` | フレーバーテキスト。世界観に沿わせる |
| `tags` | `string[]` | 検索・フィルタ用タグ（例: `["dragon","fire"]`） |
| `craftingCost` | `object` | クラフトコスト（§7参照） |
| `illustrationId` | `string` | ASSET_CARD_ILLUSTRATIONS.md のイラストID参照 |
| `hasPremium` | `boolean` | プレミアム（Foil）バリアント有無。デフォルト: `false` |

---

## 2. 顕現（Manifest）

### 2.1 必須項目（`params` 内）

| フィールド | 型 | 説明 |
|-----------|------|------|
| `battlePower` | `int` | 戦力値。コストカーブ: CP×2000+1000（バニラ基準） |
| `wishDamage` | `int` (1〜4) | 願撃値 |
| `keywords` | `string[]` | キーワード能力リスト（§2.3参照） |
| `wishTrigger` | `string` | 願力カード発動時のトリガー効果。値は `"-"`, `"WT_DRAW"`, `"WT_BOUNCE"`, `"WT_POWER_PLUS"`, `"WT_BLOCKER"` のいずれか。`"-"` は発動効果なし |

### 2.2 任意項目（`params` 内）

| フィールド | 型 | 説明 |
|-----------|------|------|
| `onPlayEffect` | `object` | 登場時効果。`{ effectKey: string, params: object }` 形式 |
| `onDestroyEffect` | `object` | 退場時効果。同形式 |
| `artDirection` | `string` | アート指示（AI生成用） |
| `characterLore` | `string` | キャラクター背景（世界観用） |

### 2.3 WishTrigger（願力カード発動効果）

願力カードが発動時に実行される効果タイプ。GAME_DESIGN.md §5.5 参照。

| トリガータイプ | 効果内容 |
|-----------|------|
| `"-"` | 発動効果なし |
| `"WT_DRAW"` | ドロー1を実行 |
| `"WT_BOUNCE"` | 敵ユニット1体を手札に戻す |
| `"WT_POWER_PLUS"` | 味方ユニット1体に+2000の戦力を付与（ターン終了時まで） |
| `"WT_BLOCKER"` | 味方ユニット1体にBlockerを付与（ターン終了時まで） |

分布目安（顕現・詠術のみ）: 30-40% のカードに WishTrigger を付与。アーキタイプごとの傾向は GAME_DESIGN.md §付録A 参照。

### 2.4 キーワード能力一覧

| キーワード | 効果 | コストカーブ影響 |
|-----------|------|---------------|
| `Blocker` | 攻撃対象の強制的引き受け | −500 戦力 |
| `Rush` | 召喚酔いなし | −1000 戦力 |
| `GuardBreak` | Blocker を無視して直撃可能 | −500 戦力 |
| `Ambush` | 相手ターン中に手札から割り込みプレイ可能（手札1枚追加コスト） | −1500 戦力 |

> コストカーブ影響値は GAME_DESIGN.md §12b キーワードペナルティに準拠。MVP では上記4種。将来拡張候補（Stealth, DoubleStrike, Regenerate 等）は POST_MVP_ROADMAP.md にて管理。
> Ambush の詳細ルールは GAME_DESIGN.md §5.7 を参照。

### 2.5 顕現 JSON 例

```json
{
  "id": "MAN_AKE_01",
  "name": "暁門の剣士",
  "type": "顕現",
  "cpCost": 3,
  "aspectTag": "Contest",
  "rarity": "R",
  "effectKey": "SUMMON_RUSH",
  "params": {
    "battlePower": 4000,
    "wishDamage": 2,
    "keywords": ["Rush"],
    "wishTrigger": "WT_DRAW"
  },
  "flavorText": "曙の門をくぐりし者に、迷いはない。",
  "illustrationId": "ILL_MAN_AKE_01",
  "craftingCost": { "create": 200, "disenchant": 50 }
}
```

---

## 3. 詠術（Spell）

### 3.1 必須項目（`params` 内）

effectKey の種類に応じて以下を組み合わせる。

| フィールド | 型 | 対応 effectKey | 説明 |
|-----------|------|-------------|------|
| `target` | `"ally" \| "enemy" \| "any" \| "all" \| "all_ally" \| "enemy_manifest"` | ほぼ全詠術 | 効果対象 |
| `wishTrigger` | `string` | 詠術 | 願力カード発動時のトリガー効果（オプション）。値は `"-"`, `"WT_DRAW"`, `"WT_BOUNCE"`, `"WT_POWER_PLUS"`, `"WT_BLOCKER"` のいずれか |

### 3.2 任意項目（`params` 内 — effectKey に応じて使用）

| フィールド | 型 | 対応 effectKey | 説明 |
|-----------|------|-------------|------|
| `hpDamageDelta` | `int` | `SPELL_HP_DAMAGE_FIXED`, `SPELL_HP_DAMAGE_CURRENT` | HP ダメージ量 |
| `wishDamageDelta` | `int` | `SPELL_WISHDMG_PLUS` | 願撃変動量 |
| `powerDelta` | `int` | `SPELL_POWER_PLUS`, `SPELL_POWER_MINUS` | 戦力変動量 |
| `restTargets` | `int` | `SPELL_REST` | 消耗させる対象数 |
| `removeCondition` | `string` | `SPELL_REMOVE_CONDITIONAL` | 除去条件（例: `"power<=3000"`） |
| `drawCount` | `int` | `SPELL_DRAW`, `SUMMON_ON_PLAY_DRAW` | ドロー枚数 |
| `searchAspect` | `string` | `SPELL_SEARCH_ASPECT` | サーチ対象の願相 |
| `searchType` | `string` | `SPELL_SEARCH_TYPE` | サーチ対象のカード型（`"顕現"`, `"詠術"`, `"界律"`） |
| `count` | `int` | `SPELL_SEARCH_*`, `SPELL_REMOVE_MULTI` | 効果の適用回数・枚数 |
| `healAmount` | `int` | `SPELL_HEAL_GAUGE` | ゲージ回復量 |
| `summonId` | `string` | `SPELL_SUMMON_TOKEN` | 生成するトークンのカードID |
| `conditionAspect` | `string` | `SPELL_CONDITIONAL_*` | 条件付き効果の参照願相 |
| `duration` | `int` | `SPELL_BUFF_*_TEMP` | 効果持続ターン数（0=永続） |

### 3.3 effectKey → params マッピング一覧

| effectKey | 必須 params | 任意 params |
|-----------|-----------|-----------|
| `SPELL_DRAW` | `drawCount` | — |
| `SPELL_SEARCH_ASPECT` | `searchAspect`, `count` | `searchType` |
| `SPELL_SEARCH_TYPE` | `searchType`, `count` | `searchAspect` |
| `SPELL_POWER_PLUS` | `target`, `powerDelta` | `duration`, `count` |
| `SPELL_POWER_MINUS` | `target`, `powerDelta` | `count` |
| `SPELL_WISHDMG_PLUS` | `target`, `wishDamageDelta` | `duration` |
| `SPELL_REMOVE_CONDITIONAL` | `target`, `removeCondition` | `count` |
| `SPELL_REMOVE_MULTI` | `target`, `count` | — |
| `SPELL_REST` | `target`, `restTargets` | — |
| `SPELL_HP_DAMAGE_FIXED` | `hpDamageDelta` | — |
| `SPELL_HP_DAMAGE_CURRENT` | `hpDamageDelta` | — |
| `SPELL_HEAL_GAUGE` | `healAmount` | — |
| `SPELL_SUMMON_TOKEN` | `summonId` | `count` |

### 3.4 詠術 JSON 例

```json
{
  "id": "SPL_SOR_03",
  "name": "蒼穹の叡智",
  "type": "詠術",
  "cpCost": 2,
  "aspectTag": "Whisper",
  "rarity": "C",
  "effectKey": "SPELL_DRAW",
  "params": {
    "drawCount": 2,
    "target": "ally",
    "wishTrigger": "WT_BOUNCE"
  },
  "flavorText": "空の願主は、静寂のなかに答えを聴く。",
  "illustrationId": "ILL_SPL_SOR_03",
  "craftingCost": { "create": 100, "disenchant": 25 }
}
```

---

## 4. 界律（Algorithm）

### 4.1 必須項目（`params` 内）

| フィールド | 型 | 説明 |
|-----------|------|------|
| `globalRule` | `object` | 全プレイヤーに適用される効果。`{ kind: string, value: int, condition?: object }` |
| `ownerBonus` | `object` | 設置者のみに適用されるボーナス。同形式 |

### 4.2 globalRule / ownerBonus の `kind` 一覧

| kind | 説明 | value の意味 |
|------|------|------------|
| `"cp_modify"` | CP最大値/獲得量変動 | 変動量（+/-） |
| `"power_modify"` | 全顕現の戦力変動 | 変動量（+/-） |
| `"draw_modify"` | ドローフェーズ追加ドロー | 追加枚数 |
| `"cost_modify"` | 特定カード型のコスト変動 | 変動量。`condition.type` で対象指定 |
| `"damage_modify"` | 願撃ダメージ変動 | 変動量（+/-） |
| `"keyword_grant"` | キーワード付与 | — 。`condition.keyword` で指定 |
| `"heal_per_turn"` | ターン開始時ゲージ回復 | 回復量 |

### 4.3 condition オブジェクト（任意）

| フィールド | 型 | 説明 |
|-----------|------|------|
| `aspectMatch` | `string` | 対象の願相が一致する場合のみ適用 |
| `emotionMatch` | `string` | 対象の情相が一致する場合のみ適用（v0.5.4） |
| `emotionThreshold` | `int` | ownerBonus発動に必要な場の指定情相顕現数（v0.5.4） |
| `type` | `string` | 対象のカード型が一致する場合のみ適用 |
| `keyword` | `string` | 付与するキーワード（`keyword_grant` 時） |
| `minCost` | `int` | コストがこの値以上のカードのみ対象 |
| `maxCost` | `int` | コストがこの値以下のカードのみ対象 |

### 4.4 界律 JSON 例

```json
{
  "id": "ALG_ODY_01",
  "name": "穏やかなる均衡",
  "type": "界律",
  "cpCost": 4,
  "aspectTag": "Weave",
  "rarity": "SR",
  "effectKey": "ALGO_GLOBAL_RULE",
  "params": {
    "globalRule": {
      "kind": "power_modify",
      "value": -1000,
      "condition": { "minCost": 5 }
    },
    "ownerBonus": {
      "kind": "draw_modify",
      "value": 1
    }
  },
  "flavorText": "穏の法は、すべてを秤にかける。",
  "illustrationId": "ILL_ALG_ODY_01",
  "craftingCost": { "create": 400, "disenchant": 100 }
}
```

---

## 5. 願主（Leader）

願主は通常のカード束外に置かれる特別存在。デッキに含まず、ゲーム開始時に自動配置される。

### 5.1 必須項目

| フィールド | 型 | 説明 |
|-----------|------|------|
| `id` | `string` | 願主ID（例: `LDR_CON_01`）。命名規則: `LDR_{ASPECT}_{NUMBER}` |
| `name` | `string` | 願主名（例: `"Aldric（アルドリック）"`） |
| `basePower` | `int` | Lv1初期戦力（基本5000） |
| `baseWishDamage` | `string` | Lv1初期願撃（例: `"固定3%"`, `"現在5%"`） |
| `keyAspect` | `string` | 主願相（aspectTag と同じ値域） |
| `levelCap` | `int` | 最大レベル（MVP: 3固定） |

### 5.2 成長項目

| フィールド | 型 | 説明 |
|-----------|------|------|
| `evoGaugeMaxByLevel` | `int[]` | レベルごとの願成ゲージ必要値。配列長 = `levelCap - 1` |
| `levelUpPowerGain` | `int` | レベルアップごとの戦力増加量 |
| `levelUpWishDamageGain` | `string` | レベルアップごとの願撃増加量（例: `"+2%Fixed"`, `"+3%Current"`） |
| `grantedKeywordsByLevel` | `object[]` | レベル到達時に獲得するキーワード。`[{ level: int, keyword: string }]` |
| `leaderSkills` | `object[]` | 願主スキル定義。配列長 = 2（Lv2スキル＋Lv3スキル）。`[{ unlockLevel: int, name: string, effectKey: string, params: object, description: string }]` |

### 5.3 願主 JSON 例（Aldric）

```json
{
  "id": "LDR_CON_01",
  "name": "Aldric（アルドリック）",
  "keyAspect": "Contest",
  "basePower": 5000,
  "baseWishDamage": "固定3%",
  "levelCap": 3,
  "evoGaugeMaxByLevel": [3, 4],
  "levelUpPowerGain": 1000,
  "levelUpWishDamageGain": "+2%Fixed",
  "grantedKeywordsByLevel": [
    { "level": 3, "keyword": "GuardBreak" }
  ],
  "leaderSkills": [
    {
      "unlockLevel": 2,
      "name": "突撃令",
      "effectKey": "LEADER_SKILL_RUSH_ALL",
      "params": {},
      "description": "自軍顕現全体にRush付与（ターン終了まで）"
    },
    {
      "unlockLevel": 3,
      "name": "覇王の一喝",
      "effectKey": "LEADER_SKILL_POWER_BUFF_GUARDBREAK",
      "params": { "powerDelta": 3000, "grantKeyword": "GuardBreak" },
      "description": "自軍顕現全体に戦力+3000＋GuardBreak付与（ターン終了まで）"
    }
  ]
}
```

### 5.4 MVP願主一覧（6体）

> 全データは `GAME_DESIGN.md` 付録A6 を正式参照。

| ID | 名前 | KeyAspect | 願撃タイプ | Lv3キーワード | Lv2スキル | Lv3スキル |
|---|---|---|---|---|---|---|
| LDR_CON_01 | Aldric | Contest（曙） | 固定% | GuardBreak | 突撃令 | 覇王の一喝 |
| LDR_WHI_01 | Vael | Whisper（空） | 固定% | — | 静寂の檻 | 虚無の問い |
| LDR_WEA_01 | 灯凪 | Weave（穏） | 固定% | Blocker | 響き渡る声 | 万語の祝福 |
| LDR_VER_01 | Amara | Verse（妖） | 現在% | — | 呪縛の糸 | 世界消去宣言 |
| LDR_MAN_01 | Rahim | Manifest（遊） | 固定% | Rush | 姉の教え | 蘇りの祈り |
| LDR_HUS_01 | 崔鋒 | Hush（玄） | 固定% | Blocker | 不退転の構え | 殉の遺言 |

---

## 6. レアリティとクラフトコスト

### 6.1 MVP レアリティ分布（162種）

| レアリティ | 枚数 | 生成コスト（ゴールド） | 分解還元 | パック排出率 |
|-----------|------|-------------|---------|-----------|
| C（コモン） | 72 | 100 | 25 | 70% |
| R（レア） | 48 | 200 | 50 | 20% |
| SR（スーパーレア） | 24 | 400 | 100 | 8% |
| SSR（ウルトラレア） | 12 | 800 | 200 | 2% |

> MONETIZATION_DESIGN.md §3, §9 に準拠。天井50パックの pity system あり。

### 6.2 craftingCost オブジェクト

```json
{
  "create": 200,
  "disenchant": 50
}
```

- `create`: 生成に必要なゴールド
- `disenchant`: 分解で得られるゴールド
- プレミアム版は `create` × 2, `disenchant` × 2

---

## 7. Firestore / StreamingAssets 二重管理

### 7.1 保存先

| 保存先 | パス | 用途 |
|--------|------|------|
| Firestore | `cardMaster/{id}` | サーバー権威データ。Cloud Functions がカード効果解決に参照 |
| StreamingAssets | `Cards/{id}.json` | クライアントローカル。UI表示・オフラインキャッシュ用 |

### 7.2 同期ルール

- Firestore が正本。StreamingAssets はビルド時に Firestore から生成する
- クライアント起動時にバージョンハッシュを比較し、差分があればダウンロード
- カードバランス調整（ホットフィックス）は Firestore のみ更新 → クライアントが次回起動時に自動取得

> BACKEND_DESIGN.md §5 に準拠。

---

## 8. アナリティクスフィールド（実装補助）

以下のフィールドはゲームロジックには影響しないが、バランス分析とメタゲーム追跡に使用する。

| フィールド | 型 | 説明 |
|-----------|------|------|
| `seriesId` | `string` | 所属シリーズ（例: `"S1_BASE"`）。ローテーション管理に使用 |
| `archetypeHint` | `string` | 想定アーキタイプ（例: `"aggro"`, `"control"`）。ANALYTICS_SPEC.md §5.1 のデッキ分類に使用 |
| `designerNote` | `string` | 設計意図メモ（内部用。クライアントには配信しない） |
| `balanceVersion` | `string` | 最終バランス調整バージョン（例: `"1.0.0"`） |

---

## 9. バリデーションルール

カードデータ入力時に以下を自動チェックする（IMPLEMENTATION_PLAN.md Phase 11 タスク89）。

| ルール | 対象 | 条件 |
|--------|------|------|
| ID一意性 | 全カード | 同一IDが存在しないこと |
| aspectTag値域 | 全カード | 6種のいずれかであること |
| cpCost範囲 | 全カード | 0 ≤ cpCost ≤ 10 |
| コストカーブ準拠 | 顕現 | battlePower ≤ cpCost×2000+1000+keyword補正 |
| effectKey存在 | 全カード | GAME_DESIGN.md §11.1 の一覧に含まれること |
| params整合 | 全カード | §3.3 マッピング表の必須 params がすべて存在すること |
| レアリティ分布 | 全体 | MVP 162種: 78C/48R/24SR/12SSR を満たすこと |
| 願主levelCap | 願主 | evoGaugeMaxByLevel の配列長 = levelCap - 1 |
| craftingCost整合 | 全カード | §6.1 のレアリティ別コスト表に一致すること |

---

## 10. AI 生成ルール

- 顕現、詠術、界律の境界を混同しない
- `aspectTag` を必ず設定する（内部タグ名で。表示名は UI_STYLE_GUIDE.md §6 を参照）
- `effectKey` は無制限に増やさず、GAME_DESIGN.md §11.1 の既存カテゴリに寄せる
- 新しい effectKey を追加する場合は、先に GAME_DESIGN.md §11.1 に追記してからカードデータを作る
- `flavorText` は STORY_CHAPTERS.md / COMPANION_CHARACTER.md の世界観に沿わせる
- コストカーブ（§12b）からの逸脱がある場合は `designerNote` にその理由を記載する
- JSON配列 または Markdown表 のどちらかで出力する

---

## 11. 関連ファイル

- `GAME_DESIGN.md` 付録B — データ設計の正本
- `GAME_DESIGN.md` §11.1 — EffectKey 一覧
- `GAME_DESIGN.md` §12b — コストカーブ指針
- `CARD_TEXT_GUIDELINES.md` — テキスト文法ルール
- `MONETIZATION_DESIGN.md` §3, §9 — レアリティ・クラフトシステム
- `BACKEND_DESIGN.md` §5 — Firestore カードマスターデータ管理
- `ANALYTICS_SPEC.md` §5 — メタゲーム分析
- `ASSET_CARD_ILLUSTRATIONS.md` — イラスト素材リスト
