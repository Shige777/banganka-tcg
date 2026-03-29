# ASSET_TUTORIAL.md
> 万願果 チュートリアル素材リスト v1

---

## 0. 目的

新規プレイヤー向けチュートリアル画面で使用する素材を一覧化する。  
ナルが「案内役」として案内する形式を前提とする。

---

## 1. ナルチュートリアル立ち絵

`ART_DIRECTION.md §4` のナルビジュアルテーマに準拠。
Niji Journey の `--sref`（スタイルリファレンス）+ seed番号でベース画像を固定して差分展開する。（※ Niji v7 は `--cref` 非対応。`--sref` で代替）

| 素材名 | サイズ | 表情・ポーズ | ツール |
|---|---|---|---|
| TUTO_NARU_NEUTRAL | 400×700 | 通常（浮遊・口元が動く） | Niji Journey |
| TUTO_NARU_EXPLAIN | 400×700 | 説明（前のめりで指を立てる） | Niji Journey |
| TUTO_NARU_SMILE | 400×700 | 笑顔（目が丸く大きく開く） | Niji Journey |
| TUTO_NARU_SURPRISED | 400×700 | 驚き（大げさに飛び上がる） | Niji Journey |
| TUTO_NARU_SERIOUS | 400×700 | 真剣（戦闘説明時） | Niji Journey |
| TUTO_NARU_POINT | 400×700 | 指差しポーズ（誘導時） | Niji Journey |

> 全差分で交界の霧色 + うっすら発光の配色を維持すること。

---

## 2. 吹き出し・テキストUI

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| TUTO_BALLOON_LEFT | 640×160 | 吹き出し（左向き・ナル発言） | Leonardo AI |
| TUTO_BALLOON_RIGHT | 640×160 | 吹き出し（右向き・相手発言） | Leonardo AI |
| TUTO_BALLOON_NARRATION | 800×100 | ナレーション帯（中央） | Leonardo AI |
| TUTO_TEXT_BG | 1280×200 | テキスト表示帯（下部固定） | Leonardo AI |
| TUTO_NAME_PLATE | 200×44 | キャラ名プレート | Leonardo AI |

---

## 3. ガイドオーバーレイ

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| TUTO_OVERLAY_DIM | 1920×1080 | 暗幕オーバーレイ（スポットライト用） | Leonardo AI |
| TUTO_HIGHLIGHT_BOX | 可変 | 強調枠（点滅・角丸） | Leonardo AI |
| TUTO_ARROW_POINT | 80×120 | 指示矢印（下向き） | Leonardo AI |
| TUTO_ARROW_POINT_RIGHT | 120×80 | 指示矢印（右向き） | Leonardo AI |
| TUTO_HAND_CURSOR | 64×80 | 手のカーソル（タップ誘導） | Leonardo AI |
| TUTO_PULSE_RING | 128×128 | タップ誘導パルスリング | Leonardo AI |

---

## 4. 進行UI

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| TUTO_BTN_NEXT | 200×56 | 次へボタン | Leonardo AI |
| TUTO_BTN_SKIP | 140×44 | スキップボタン | Leonardo AI |
| TUTO_BTN_REPLAY | 140×44 | もう一度見るボタン | Leonardo AI |
| TUTO_STEP_DOTS | 可変 | ステップ進捗ドット | Leonardo AI |
| TUTO_STEP_BAR | 480×8 | ステップ進捗バー | Leonardo AI |
| TUTO_COMPLETE_BANNER | 640×160 | チュートリアル完了バナー | Leonardo AI |

---

## 5. チュートリアルバトル専用

| 素材名 | サイズ目安 | 内容 | ツール |
|---|---|---|---|
| TUTO_ENEMY_DUMMY | 400×700 | チュートリアル用ダミー敵キャラ立ち絵 | Niji Journey |
| TUTO_ENEMY_FIELD | 1920×1080 | チュートリアル専用バトル背景 | Niji Journey |
| TUTO_HAND_PRESET | カード比 × 3 | チュートリアル初期手札（固定3種） | — |

---

## 6. 優先度

### 最優先
- TUTO_NARU_NEUTRAL / EXPLAIN / SMILE（3差分）
- TUTO_BALLOON_LEFT
- TUTO_HIGHLIGHT_BOX
- TUTO_ARROW_POINT（2種）
- TUTO_BTN_NEXT / SKIP

### 中優先
- TUTO_NARU 残り3差分
- TUTO_OVERLAY_DIM
- TUTO_HAND_CURSOR / PULSE_RING

### 後優先
- TUTO_ENEMY_DUMMY / FIELD
- TUTO_COMPLETE_BANNER
- TUTO_BTN_REPLAY

---

## 7. AI生成ルール

- ナル立ち絵はNiji Journeyで1枚ベース画像を確定させてから差分生成する
- `--sref` のスタイルリファレンス値 + seed番号を `/assets/reference/naru_ref.txt` に記録・共有する（※ Niji v7 は `--cref` 非対応）
- 吹き出し・オーバーレイ類はLeonardo AI Canvasで世界観トーンに合わせる
- アルファPNG必須（UI素材はすべて透過前提）
