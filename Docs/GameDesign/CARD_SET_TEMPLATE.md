# CARD_SET_TEMPLATE.md
> 万願果 カードセット拡張テンプレート v1.0

---

## 0. 目的

新カードセット（第2弾以降）の企画・設計・実装において、品質と整合性を担保するためのテンプレート。
本テンプレートに沿って設計することで、GAME_DESIGN.md・CARD_SCHEMA.md・BALANCE_POLICY.md との整合を自動的に維持する。

---

## 1. セット基本情報（コピーして記入）

```yaml
setId: "SET_002"                    # SET_001=MVP, SET_002=第2弾, ...
setName: "（セット名）"
setNameEn: "(English Set Name)"
seriesId: "S2"                      # S1=MVP, S2=第2弾, ...
releasePhase: "Phase 2"             # POST_MVP_ROADMAP.md 参照
targetReleaseDate: "YYYY-MM-DD"
designLead: "(担当者名)"
cardCount:
  total: 40                         # 推奨: 40〜60枚
  manifest: 24                      # 推奨: 総数の50〜60%
  spell: 12                         # 推奨: 総数の25〜30%
  algorithm: 4                      # 推奨: 総数の5〜15%
rarityDistribution:
  C: 16                             # 推奨: 40%
  R: 12                             # 推奨: 30%
  SR: 8                             # 推奨: 20%
  SSR: 4                            # 推奨: 10%
newKeywords: []                     # 新キーワード能力（あれば）
newEffectKeys: []                   # 新EffectKey（あれば）
themeDescription: "（セットのテーマ・方向性を1〜2文で）"
```

---

## 2. 設計チェックリスト

### 2.1 企画段階

- [ ] セットテーマがストーリーと連動している（STORY_BIBLE.md 参照）
- [ ] 6願相すべてにカードが追加される（偏りチェック）
- [ ] 既存6アーキタイプの強化方向性が明確（GAME_DESIGN.md 付録A0 参照）
- [ ] 新アーキタイプを追加する場合、既存6との相互作用を検討済み
- [ ] 新キーワード能力がある場合、GAME_DESIGN.md §5.2 への追記案を用意
- [ ] 新EffectKeyがある場合、GAME_DESIGN.md §11.1 への追記案を用意
- [ ] POST_MVP_ROADMAP.md の該当Phase要件を満たしている

### 2.2 カード設計段階

- [ ] 全カードが CARD_SCHEMA.md のスキーマに準拠している
- [ ] コストカーブが GAME_DESIGN.md §12b に準拠している
- [ ] キーワードペナルティが正しく適用されている
- [ ] EffectKey が GAME_DESIGN.md §11.1 に定義済み（新規は追加）
- [ ] カードテキストが CARD_TEXT_GUIDELINES.md に準拠している
- [ ] フレーバーテキストがSTORY_BIBLE.md の世界観と一致
- [ ] aspectTag が6願相のいずれか（Contest/Whisper/Weave/Verse/Manifest/Hush）
- [ ] rarity分布が §1 の配分に準拠

### 2.3 バランス検証段階

- [ ] BALANCE_POLICY.md §2 の調整プロセスを実施
- [ ] L5バランステスト実施（TEST_PLAN.md §10 参照）
  - [ ] 各アーキタイプ勝率 45%〜55%
  - [ ] 先攻/後攻勝率差 ±5%以内
  - [ ] 新カード採用率が極端に偏っていない
- [ ] 相性マトリクス（15通り）が健全範囲内
- [ ] 平均決着ターン 12〜20 を維持

### 2.4 実装段階

- [ ] CardData JSON が CARD_SCHEMA.md §9 バリデーションルール全通過
- [ ] Firestore + StreamingAssets の二重管理に対応（CARD_SCHEMA.md §7）
- [ ] イラスト156枚 + 新規分がすべて ASSET_CARD_ILLUSTRATIONS.md に追記
- [ ] SE/BGM が追加される場合、SOUND_DESIGN_SPEC.md に追記
- [ ] アニメーション追加がある場合、ANIMATION_SPEC.md に追記

### 2.5 リリース段階

- [ ] E2Eテスト E10〜E21（対戦フルループ）を新カードで再実施
- [ ] パック購入→開封→カード獲得の導線テスト（E22〜E27）
- [ ] アプリアップデート or Remote Config 配信の手順確認
- [ ] GAME_DESIGN.md 付録C にバージョン履歴追記
- [ ] README.md 更新

---

## 3. カードプール設計テンプレート

### 3.1 顕現テンプレート

各願相に最低4体を配分。以下の役割バランスを推奨する。

| 役割 | 推奨枚数/願相 | CP帯 | 説明 |
|------|-------------|------|------|
| 軽量展開 | 1〜2 | 1〜2 | 序盤の盤面構築・テンポ確保 |
| 中型主力 | 1〜2 | 3〜4 | 中盤の攻防を担う主力 |
| 大型フィニッシャー | 0〜1 | 5〜7 | 終盤の決め手 |
| ユーティリティ | 1 | 2〜4 | OnPlayEffect主体のテクニカル枠 |

```json
{
  "id": "MAN_{ASPECT}_{NN}",
  "name": "（カード名）",
  "type": "顕現",
  "cpCost": 3,
  "aspectTag": "(Contest|Whisper|Weave|Verse|Manifest|Hush)",
  "rarity": "(C|R|SR|SSR)",
  "effectKey": "(GAME_DESIGN.md §11.1 準拠)",
  "params": {
    "battlePower": 4000,
    "wishDamage": 1,
    "keywords": []
  },
  "flavorText": "（世界観準拠のフレーバーテキスト）",
  "illustrationId": "ILL_MAN_{ASPECT}_{NN}",
  "seriesId": "(S2|S3|...)",
  "archetypeHint": "(aggro|control|midrange|combo|tempo|defense)",
  "craftingCost": { "create": 200, "disenchant": 50 },
  "balanceVersion": "1.0.0"
}
```

### 3.2 詠術テンプレート

各願相に最低2枚を配分。

| カテゴリ | 推奨枚数/願相 | CP帯 | 説明 |
|---------|-------------|------|------|
| 戦闘補助 | 1 | 1〜2 | パワー/願撃バフ |
| リソース | 1 | 2〜3 | ドロー/サーチ |
| 除去/妨害 | 0〜1 | 2〜4 | 消耗/除去/バウンス |
| ゲージ操作 | 0〜1 | 1〜3 | 願力変動 |

```json
{
  "id": "SPL_{ASPECT}_{NN}",
  "name": "（カード名）",
  "type": "詠術",
  "cpCost": 2,
  "aspectTag": "(Contest|Whisper|Weave|Verse|Manifest|Hush)",
  "rarity": "(C|R|SR|SSR)",
  "effectKey": "(GAME_DESIGN.md §11.1 準拠)",
  "params": {
    "target": "(ally|enemy|any|all|all_ally|enemy_manifest)",
    "powerDelta": 1000
  },
  "flavorText": "（世界観準拠）",
  "illustrationId": "ILL_SPL_{ASPECT}_{NN}",
  "seriesId": "(S2|S3|...)",
  "craftingCost": { "create": 100, "disenchant": 25 },
  "balanceVersion": "1.0.0"
}
```

### 3.3 界律テンプレート

セットあたり2〜6枚。既存界律との上書き相互作用を必ず検証する。

```json
{
  "id": "ALG_{ASPECT_OR_XX}_{NN}",
  "name": "（カード名）",
  "type": "界律",
  "cpCost": 3,
  "aspectTag": "(Contest|...|null=汎用)",
  "rarity": "(R|SR|SSR)",
  "effectKey": "(ALGO_* 系)",
  "params": {
    "globalRule": { "kind": "(modifier_type)", "value": 1000 },
    "ownerBonus": { "kind": "(bonus_type)", "value": 500 }
  },
  "flavorText": "（世界観準拠）",
  "illustrationId": "ILL_ALG_{ASPECT}_{NN}",
  "seriesId": "(S2|S3|...)",
  "craftingCost": { "create": 400, "disenchant": 100 },
  "balanceVersion": "1.0.0"
}
```

---

## 4. コストカーブ検証表

全カードについて以下の表を作成し、GAME_DESIGN.md §12b の基準からの乖離を検証する。

| ID | 名称 | CP | 戦力 | 基準戦力 | 差分 | Keywords | KWペナルティ | OnPlay | OPペナルティ | 最終差分 | 判定 |
|----|------|--:|-----:|--------:|----:|---------|----------:|--------|----------:|---------:|------|
| (例) | 暁門の剣士 | 3 | 4000 | 5000 | -1000 | Rush | -500 | — | 0 | -500 | ⚠要検討 |

**判定基準:**
- ±0〜500: ✅ 適正
- ±500〜1000: ⚠ 要検討（効果で説明可能なら許容）
- ±1000超: ❌ 再設計

---

## 5. アーキタイプ影響分析

新カードが既存6アーキタイプにどう影響するかを事前分析する。

| アーキタイプ | 願相 | 追加カード数 | 強化方向 | 懸念事項 |
|------------|------|----------:|---------|---------|
| アグロ | 曙(Contest) | | | |
| コントロール | 空(Whisper) | | | |
| ミッドレンジ | 穏(Weave) | | | |
| コンボ | 妖(Verse) | | | |
| テンポ | 遊(Manifest) | | | |
| ディフェンス | 玄(Hush) | | | |

---

## 6. イラスト発注リスト

ASSET_CARD_ILLUSTRATIONS.md に追記する形式で記入。

| アセットID | サイズ | 説明 | 願相 | 生成ツール | 優先度 |
|-----------|--------|------|------|-----------|--------|
| ILL_MAN_{ASPECT}_{NN} | 600×840 | （カード名）| （願相） | Niji Journey | Tier 1 |

---

## 7. ローテーション影響チェック（第3弾以降）

POST_MVP_ROADMAP.md §Phase 2 補足のフォーマット方針に従い、以下を確認する。

- [ ] スタンダード落ちするカードの代替が新セットに含まれている
- [ ] エターナルの制限リスト候補を洗い出し済み
- [ ] ローテーション告知（2ヶ月前）のスケジュールを設定済み

---

## 8. リリーススケジュール目安

| フェーズ | 期間 | 成果物 |
|---------|------|--------|
| 企画・テーマ策定 | 1週 | §1 基本情報、§5 影響分析 |
| カード設計 | 2週 | §3 カードプール、§4 コストカーブ検証表 |
| バランステスト | 1〜2週 | §2.3 検証結果（L5テスト） |
| イラスト制作 | 2〜3週（並行） | §6 イラスト全点 |
| 実装・結合 | 1〜2週 | §2.4 実装チェック |
| E2Eテスト・修正 | 1週 | §2.5 リリースチェック |
| **合計** | **6〜9週** | |

---

## 9. 関連ファイル

- `GAME_DESIGN.md` — ゲームルール正本（EffectKey定義・コストカーブ・カードプール）
- `CARD_SCHEMA.md` — カードデータスキーマ（JSON構造・バリデーション）
- `CARD_TEXT_GUIDELINES.md` — カードテキスト作成ガイドライン
- `BALANCE_POLICY.md` — バランス調整ポリシー
- `ASSET_CARD_ILLUSTRATIONS.md` — イラストアセット管理
- `POST_MVP_ROADMAP.md` — 拡張ロードマップ
- `TEST_PLAN.md` — テスト計画（L5バランステスト・E2E）
- `STORY_BIBLE.md` — 世界観設定
