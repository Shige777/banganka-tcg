# SOUND_DESIGN_SPEC.md
> 万願果 サウンド演出設計 v2.0（ASSET_AUDIO.md 統合済み）

---

## 0. 目的

BGM遷移ルール、SE優先度、願相別サウンドモチーフ、ダイナミックBGM設計、および全オーディオ素材リストを一元管理する。演出設計（いつ・どう鳴らすか）と素材リスト（何を用意するか）を統合した唯一のサウンド仕様書。

---

## 1. 設計原則

- **世界観の一貫性**: 異時代・異時空交錯ファンタジーの重厚な雰囲気を音で表現
- **情報伝達**: SE で盤面の変化をプレイヤーに伝える（視覚だけに頼らない）
- **テンポ維持**: BGM がゲームのテンポを支える（終盤の緊張感、勝利の解放感）
- **疲労防止**: ループBGMは最低2分の長さ。同一SEの連続再生は避ける

---

## 2. BGM設計

### 2.1 BGMリスト

| ID | 曲名（仮） | 使用場面 | BPM | 調 | 長さ |
|----|-----------|---------|-----|-----|------|
| BGM_TITLE | 万願果のテーマ | タイトル/スプラッシュ | 80 | Dm | 30s（ループ） |
| BGM_HOME | 交界の息吹 | ホーム画面 | 90 | Am | 2:30（ループ） |
| BGM_BATTLE_PREP | 願いの衝突 | マッチメイキング〜マリガン | 100 | Em | 1:30（ループ） |
| BGM_BATTLE_MAIN | 盤上の攻防 | バトル中（通常） | 120 | Cm | 3:00（ループ） |
| BGM_BATTLE_CLIMAX | 運命の瀬戸際 | バトル中（願力危険域） | 140 | Cm | 2:00（ループ） |
| BGM_VICTORY | 願い、実る | 勝利演出 | 130 | C | 15s（ワンショット） |
| BGM_DEFEAT | 散りゆく願い | 敗北演出 | 70 | Fm | 10s（ワンショット） |
| BGM_SHOP | 商人の語り | ショップ画面 | 85 | G | 2:00（ループ） |
| BGM_STORY | 語り部の声 | ストーリー画面 | 75 | Dm | 3:00（ループ） |
| BGM_TUTORIAL | ナルの導き | チュートリアル | 95 | F | 2:00（ループ） |
| BGM_PACK | 封印解放 | パック開封 | 110 | Am | 1:00（ループ） |

### 2.2 BGM遷移ルール

| 遷移元 → 遷移先 | 方式 | 時間 |
|----------------|------|------|
| HOME → BATTLE_PREP | クロスフェード | 1.5s |
| BATTLE_PREP → BATTLE_MAIN | クロスフェード | 1.0s（マリガン完了時） |
| BATTLE_MAIN → BATTLE_CLIMAX | クロスフェード | 2.0s（願力が危険域突入時） |
| BATTLE_CLIMAX → BATTLE_MAIN | クロスフェード | 2.0s（危険域脱出時） |
| BATTLE_MAIN/CLIMAX → VICTORY | カットイン | 0.5s（BGM即停止→VICTORY再生） |
| BATTLE_MAIN/CLIMAX → DEFEAT | フェードアウト→DEFEAT | 1.0s |
| 任意画面 → HOME | クロスフェード | 1.0s |

### 2.3 ダイナミックBGM（HP残量連動）

バトル中BGMをHP残量の状態で動的に変化させる。

| HP 状態 | BGM変化 |
|-----------|---------|
| 中央付近（HP 40-60） | BATTLE_MAIN 通常再生 |
| やや有利/不利（HP 20-80） | BATTLE_MAIN + テンションレイヤー追加（パーカッション強化） |
| 危険域（HP < 20） | BATTLE_CLIMAX に遷移 |
| 超危険域（HP < 10） | BATTLE_CLIMAX + ストリングス上昇 |

実装方式: Suno AI で通常版とテンションレイヤーの2トラックを生成し、Unity AudioMixerで動的ミキシング（HP残量の状態で動的に変化）。

---

## 3. SE設計

### 3.1 SEリスト

| ID | 内容 | 再生タイミング | 優先度 |
|----|------|-------------|--------|
| SE_CARD_LIFT | カードを手札から持ち上げ | カードタップ時 | 中 |
| SE_CARD_PLAY | カードプレイ確定 | CP支払い時 | 高 |
| SE_SUMMON_SORA | 空の召喚音 | 召喚エフェクト時 | 高 |
| SE_SUMMON_AKEBONO | 曙の召喚音 | 召喚エフェクト時 | 高 |
| SE_SUMMON_ODAYAKA | 穏の召喚音 | 召喚エフェクト時 | 高 |
| SE_SUMMON_AYAKASHI | 妖の召喚音 | 召喚エフェクト時 | 高 |
| SE_SUMMON_ASOBI | 遊の召喚音 | 召喚エフェクト時 | 高 |
| SE_SUMMON_KURO | 玄の召喚音 | 召喚エフェクト時 | 高 |
| SE_SPELL_CAST | 詠術発動 | 魔法陣表示時 | 高 |
| SE_LAW_SET | 界律設置（表出し） | 界律展開時 | 最高 |
| SE_LAW_SET_FACEDOWN | 界律セット（裏出し） | 裏向き着地時 | 高 |
| SE_LAW_OPEN | 界律オープン（裏返し） | 封印解除→表返し時 | 最高 |
| SE_LAW_OVERWRITE | 界律上書き（破壊音+設置音） | 上書き時 | 最高 |
| SE_ATTACK_DECLARE | アタック宣言 | 攻撃選択時 | 高 |
| SE_ATTACK_HIT | 攻撃衝突 | 戦闘解決時 | 最高 |
| SE_BLOCK | ブロック成立 | ブロッカー前進時 | 高 |
| SE_DESTROY | 顕現退場 | 消滅エフェクト時 | 高 |
| SE_DIRECT_HIT | 願主への直撃 | 直撃エフェクト時 | 最高 |
| SE_HP_DAMAGE | HP ダメージ | ダメージ適用時 | 高 |
| SE_THRESHOLD_CROSS | 閾値越過 | HP 閾値到達時（85/70/55/40/25/10） | 高 |
| SE_LEVEL_UP | 願主レベルアップ | カットイン時 | 最高 |
| SE_LEADER_SKILL_LV2 | 願主Lv2スキル発動 | スキルカットイン時 | 高 |
| SE_LEADER_SKILL_LV3 | 願主Lv3スキル発動（必殺技） | スキルカットイン時 | 最高 |
| SE_SKILL_UNLOCK | スキル解禁 | レベルアップ後スキルアイコン点灯時 | 高 |
| SE_DRAW | カードドロー | ドロー時 | 低 |
| SE_TURN_START | ターン開始 | ターン表示時 | 中 |
| SE_TURN_END | ターン終了 | ボタンタップ時 | 中 |
| SE_SHACHIHOKO_WIN | 鯱鉾勝利ジングル（KO） | KO（HP=0）テキスト表示 | 最高 |
| SE_NURI_WIN | 塗り勝利ジングル（HP比較） | ターン24終了時勝利判定 | 最高 |
| SE_WISH_TRIGGER | WishTrigger 発動音 | 閾値カード起動時 | 高 |
| SE_FINAL_STATE | ファイナルステート音 | 相手 HP=0 判定時 | 最高 |
| SE_DEFEAT | 敗北ジングル | 敗北テキスト表示 | 最高 |
| SE_BUTTON_TAP | UIボタンタップ | 全ボタン | 低 |
| SE_BUTTON_CANCEL | キャンセル/戻る | 戻るボタン | 低 |
| SE_PACK_OPEN | パック破裂 | パック開封時 | 高 |
| SE_CARD_FLIP | カードフリップ | 開封カードめくり | 中 |
| SE_CARD_FLIP_SR | SRカード出現 | SR以上のフリップ | 高 |
| SE_CARD_FLIP_SSR | SSRカード出現 | SSRフリップ | 最高 |
| SE_EMOTE | エモート送信 | エモート表示時 | 低 |
| SE_TIMER_WARNING | ターンタイマー残り10秒 | タイマー10秒時 | 高 |
| SE_NAVIGATION | ナビタブ切替 | タブタップ時 | 低 |

### 3.2 願相別サウンドモチーフ

| 願相 | 楽器/音色イメージ | SE特徴 |
|------|-----------------|--------|
| **空** | 笛・フルート・風の音 | 柔らかく流れるような音 |
| **曙** | 太鼓・ブラス・炎のパチパチ | 力強く衝撃的な音 |
| **穏** | ハープ・木琴・葉擦れ | 穏やかで暖かい音 |
| **妖** | シンセ・グロッケン・鈴 | 神秘的で幻想的な音 |
| **遊** | マリンバ・弦楽器ピッツィカート | 軽快で楽しげな音 |
| **玄** | 低音弦・パイプオルガン・鎖の音 | 重厚で威圧的な音 |

---

## 4. ミキシングルール

### 4.1 音量レイヤー

| レイヤー | デフォルト音量 | ユーザー設定 |
|---------|-------------|-------------|
| BGM | 70% | 0〜100% |
| SE | 100% | 0〜100% |
| ボイス（MVP後） | 80% | 0〜100% |

### 4.2 SE同時再生制限

| 制限 | 値 |
|------|-----|
| 最大同時SE数 | **8チャンネル** |
| 同一SE同時再生 | **2回まで**（3回目はキャンセル） |

### 4.3 優先度ルール

同時再生上限に達した場合、優先度の低いSEを停止して高いSEを再生する。

最高 > 高 > 中 > 低

---

## 5. 音声フォーマット

| 種類 | フォーマット | ビットレート | 備考 |
|------|-----------|------------|------|
| BGM | Ogg Vorbis | 128kbps | ストリーミング再生 |
| SE | WAV 16bit | — | メモリ常駐（即時再生） |
| ジングル（勝利/敗北） | Ogg Vorbis | 192kbps | 高品質 |

---

## 6. 設定画面

```
設定 > サウンド
├── BGM音量: [スライダー 0〜100]
├── SE音量: [スライダー 0〜100]
├── ボイス音量: [スライダー 0〜100]（MVP後）
└── バトル中BGM: 通常 / ダイナミック / OFF
```

---

## 7. ストーリー章別BGM（素材リスト）

§2.1 のBGMリストに加え、ストーリーモードでは各章専用BGMを用意する。

| 素材名 | 尺目安 | 内容 | ツール |
|---|---|---|---|
| BGM_STORY_CHAPTER_01 | 2分（ループ） | 第一章 灯凪・古代BGM | Suno AI |
| BGM_STORY_CHAPTER_02 | 2分（ループ） | 第二章 Aldric・中世BGM | Suno AI |
| BGM_STORY_CHAPTER_03 | 2分（ループ） | 第三章 崔鋒・近世BGM | Suno AI |
| BGM_STORY_CHAPTER_04 | 2分（ループ） | 第四章 Rahim・近代BGM | Suno AI |
| BGM_STORY_CHAPTER_05 | 2分（ループ） | 第五章 Amara・近未来BGM | Suno AI |
| BGM_STORY_CHAPTER_06 | 2分（ループ） | 終章 Vael・交界BGM | Suno AI |

---

## 8. ジングル素材リスト

| 素材名 | 尺目安 | 内容 | ツール |
|---|---|---|---|
| JINGLE_SHACHIHOKO_VICTORY | 5〜10秒 | 鯱鉾勝利ジングル（KO/HP=0） | Suno AI |
| JINGLE_NURI_VICTORY | 5〜10秒 | 塗り勝利ジングル（HP比較/ターン24） | Suno AI |
| JINGLE_DEFEAT | 5〜10秒 | 敗北ジングル | Suno AI |
| JINGLE_LEVEL_UP | 3〜5秒 | 願主レベルアップ演出音 | Suno AI |
| JINGLE_GACHA_SSR | 8〜15秒 | SSRカード排出ジングル | Suno AI |
| JINGLE_CHAPTER_CLEAR | 5〜10秒 | 章クリアジングル | Suno AI |

---

## 9. SE素材リスト（詳細分類）

§3.1 のSE設計に対応する、カテゴリ別の素材リスト。

### 9.1 UI操作SE

| 素材名 | 内容 | ツール |
|---|---|---|
| SE_BTN_TAP | ボタンタップ | Suno AI / 素材DB |
| SE_BTN_CONFIRM | 決定・確認 | Suno AI / 素材DB |
| SE_BTN_CANCEL | キャンセル・戻る | Suno AI / 素材DB |
| SE_SCREEN_TRANSITION | 画面遷移 | Suno AI / 素材DB |
| SE_TAB_SWITCH | タブ切り替え | Suno AI / 素材DB |
| SE_PANEL_OPEN | パネル展開 | Suno AI / 素材DB |
| SE_PANEL_CLOSE | パネル閉じる | Suno AI / 素材DB |

### 9.2 カード操作SE

| 素材名 | 内容 | ツール |
|---|---|---|
| SE_CARD_DRAW | カードドロー | Suno AI |
| SE_CARD_HOVER | カードホバー（手札） | Suno AI |
| SE_CARD_SELECT | カード選択 | Suno AI |
| SE_CARD_PLAY_MANIFEST | 顕現プレイ | Suno AI |
| SE_CARD_PLAY_SPELL | 詠術プレイ | Suno AI |
| SE_CARD_PLAY_LAW | 界律プレイ | Suno AI |
| SE_CARD_FIRST_HAND | 初期手札配布 | Suno AI |

### 9.3 戦闘SE

| 素材名 | 内容 | ツール |
|---|---|---|
| SE_ATTACK_DECLARE | アタック宣言 | Suno AI |
| SE_ATTACK_HIT_GENERIC | 攻撃ヒット（汎用） | Suno AI |
| SE_ATTACK_HIT_SORA | 攻撃ヒット・空属性 | Suno AI |
| SE_ATTACK_HIT_AKEBONO | 攻撃ヒット・曙属性 | Suno AI |
| SE_ATTACK_HIT_ODAYAKA | 攻撃ヒット・穏属性 | Suno AI |
| SE_ATTACK_HIT_AYAKASHI | 攻撃ヒット・妖属性 | Suno AI |
| SE_ATTACK_HIT_ASOBI | 攻撃ヒット・遊属性 | Suno AI |
| SE_ATTACK_HIT_KURO | 攻撃ヒット・玄属性 | Suno AI |
| SE_DIRECT_HIT | 直撃（願主へ通った） | Suno AI |
| SE_BLOCK_DECLARE | ブロック宣言 | Suno AI |
| SE_UNIT_DESTROY | ユニット退場 | Suno AI |
| SE_HP_DAMAGE | HP ダメージ効果音 | Suno AI |
| SE_THRESHOLD_CROSS | 閾値越過効果音（特定 HP に到達） | Suno AI |
| SE_WISH_TRIGGER | WishTrigger 発動効果音 | Suno AI |
| SE_FINAL_STATE | ファイナルステート突入音（相手 HP=0 判定） | Suno AI |

### 9.4 願主・進化SE

| 素材名 | 内容 | ツール |
|---|---|---|
| SE_LEADER_EVO_GAIN | 願成獲得 | Suno AI |
| SE_LEADER_LEVEL_UP | 願主レベルアップ | Suno AI |
| SE_LEADER_SKILL_LV2 | 願主Lv2スキル発動 | Suno AI |
| SE_LEADER_SKILL_LV3 | 願主Lv3スキル発動（必殺技） | Suno AI |
| SE_SKILL_UNLOCK | スキル解禁通知 | Suno AI |
| SE_TURN_START_MY | 自分ターン開始 | Suno AI |
| SE_TURN_START_ENEMY | 相手ターン開始 | Suno AI |
| SE_END_TURN_BTN | エンドターンボタン押下 | Suno AI |

---

## 10. ボイス計画（MVP後対応）

> MVP では未実装。MVP後フェーズで順次追加する。
> ツールは ElevenLabs を推奨（チュートリアルでのナルのボイス実績を活用）。

| 素材名 | 内容 | ツール |
|---|---|---|
| VOICE_NARU_INTRO | ナル：ゲーム開始時の挨拶 | ElevenLabs |
| VOICE_NARU_BATTLE_START | ナル：バトル開始コメント | ElevenLabs |
| VOICE_NARU_WIN | ナル：勝利コメント | ElevenLabs |
| VOICE_NARU_LOSE | ナル：敗北コメント | ElevenLabs |
| VOICE_LEADER_SUMMON_* | 各願主：召喚ボイス（6種） | ElevenLabs |
| VOICE_LEADER_ATTACK_* | 各願主：アタックボイス（6種） | ElevenLabs |
| VOICE_LEADER_LEVELUP_* | 各願主：レベルアップボイス（6種） | ElevenLabs |

---

## 11. 素材制作優先度

### 最優先
- SE_CARD_DRAW / PLAY_MANIFEST / PLAY_SPELL / PLAY_LAW
- SE_ATTACK_HIT_GENERIC / DIRECT_HIT
- SE_BTN_TAP / CONFIRM / CANCEL
- BGM_BATTLE_NORMAL（BGM_BATTLE_MAIN）
- SE_SHACHIHOKO_WIN / SE_NURI_WIN / SE_DEFEAT
- SE_HP_DAMAGE / SE_THRESHOLD_CROSS / SE_WISH_TRIGGER

### 中優先
- BGM_TITLE / HOME
- SE_FINAL_STATE
- SE_LEADER_LEVEL_UP / EVO_GAIN
- SE_TURN_START 2種

### 後優先
- BGM_STORY 6種
- BGM_BATTLE_CLIMAX
- JINGLE_GACHA_SSR
- VOICE系（MVP後）

---

## 12. 制作ルール

- BGMはSuno AIで生成後、適切な音量で書き出し（-14 LUFS目安）
- ループ点を明確に設定する（シームレスループ必須）
- SEは短く（0.1〜1秒）、過剰な残響を避ける
- 属性別SEは同じ基本SE形にHEX値対応のピッチ・音色変化を付けて差別化する
- ボイスはキャラクターの性格（`COMPANION_CHARACTER.md`・`STORY_BIBLE.md`）に沿ったトーン指定で生成
- 納品形式：BGM は OGG/MP3（ループタグ付き）、SE は OGG/WAV（短尺）

---

## 13. 関連ファイル

- `ANIMATION_SPEC.md §8` — SE同期ポイント
- `ACCESSIBILITY_SPEC.md §5` — 聴覚アクセシビリティ
- `PERFORMANCE_SPEC.md §3` — オーディオメモリバジェット
- `ASSET_LIST.md §2` — オーディオセクション概要（§2 オーディオ→SOUND_DESIGN_SPEC.md §7-12）
