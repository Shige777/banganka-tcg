# REPLAY_SPEC.md
> 万願果 リプレイ・観戦設計 v1.0

---

## 0. 目的

対戦リプレイの記録・再生・共有、および観戦モードの仕様を定義する。コミュニティ形成とeSports基盤の構築を目的とする。

---

## 1. リプレイシステム

### 1.1 記録方式

**コマンドログ再生方式**を採用する。

| 方式 | 採用 | 理由 |
|------|------|------|
| コマンドログ再生 | **採用** | データ量極小（1試合5〜20KB）、NETWORK_SPEC.mdのコマンド構造をそのまま利用可能 |
| 状態スナップショット | 不採用 | データ量大（1試合500KB〜）、同期コスト高い |

### 1.2 記録データ構造

```json
{
  "replayId": "rpl_20260401_abc123",
  "matchId": "match_xyz789",
  "version": "0.5.3",
  "createdAt": "2026-04-01T14:30:00+09:00",
  "players": [
    { "uid": "player1_uid", "name": "しげ", "wishMasterId": "WM_TOMONAGI", "deckHash": "a3f8..." },
    { "uid": "player2_uid", "name": "Opponent", "wishMasterId": "WM_ALDRIC", "deckHash": "b7c2..." }
  ],
  "initialState": {
    "firstPlayer": 1,
    "decks": [ ["card_id_1", "card_id_2", ...], ["card_id_1", "card_id_2", ...] ],
    "initialHands": [ [0, 3, 7, 12, 28], [1, 5, 9, 14, 22] ],
    "mulliganActions": [ [3, 12], [] ],
    "hpSnapshots": [ { "player": 1, "hp": 100, "maxHp": 100 }, { "player": 2, "hp": 100, "maxHp": 100 } ],
    "thresholdCards": [ { "player": 1, "thresholds": [85, 70, 55, 40, 25, 10], "state": [false, false, false, false, false, false] }, { "player": 2, "thresholds": [85, 70, 55, 40, 25, 10], "state": [false, false, false, false, false, false] } ]
  },
  "commands": [
    { "turn": 1, "player": 1, "type": "PlayManifest", "data": { "cardIndex": 0, "slot": "front_0" }, "timestamp": 1500 },
    { "turn": 1, "player": 1, "type": "EndTurn", "data": {}, "timestamp": 45000 },
    { "turn": 2, "player": 2, "type": "HP_DAMAGE", "data": { "target": 1, "damageType": "固定%", "amount": 10, "damagePercent": 10 }, "timestamp": 2000 },
    { "turn": 2, "player": 2, "type": "WISH_TRIGGER", "data": { "target": 1, "threshold": 85, "effect": "WT_DRAW" }, "timestamp": 2100 },
    ...
  ],
  "result": {
    "winner": 1,
    "reason": "鯱鉾勝利",
    "totalTurns": 18,
    "finalHps": { "player1": 45, "player2": 0 }
  }
}
```

### 1.3 再生フロー

```
リプレイ選択 → データ読み込み
  → initialState でゲーム状態を初期化
  → commands を順次再生
    - 各コマンドの間にアニメーション再生（ANIMATION_SPEC.md準拠）
    - 再生速度: 1x / 2x / 4x 切替可能
  → 再生コントロール:
    - ▶ 再生 / ⏸ 一時停止
    - ◀◀ 前のターンへ / ▶▶ 次のターンへ
    - スライダーでターン移動
  → 結果到達 → リザルト表示
```

### 1.4 保存と管理

| 項目 | 値 |
|------|-----|
| 自動保存 | 直近**20試合**をローカルに自動保存 |
| お気に入り保存 | プレイヤーが「保存」した試合は上限**50件**まで永続保存（Firestore） |
| 保存期間（サーバー） | 90日間（その後自動削除） |
| データサイズ | 1試合あたり5〜25KB（HP/しきい値データを含む） |

---

## 2. リプレイ共有

### 2.1 共有方法

| 方法 | 内容 | 実装時期 |
|------|------|---------|
| リプレイコード | `BNG-RPL-{replayId}` をテキストで共有 | Phase 3 |
| URLリンク | `banngannka://replay/{replayId}` ディープリンク | Phase 3 |
| SNSシェア | リザルト画像 + リプレイコード | Phase 3 |

### 2.2 閲覧権限

- リプレイは**対戦した両プレイヤー**が閲覧可能
- 共有コード/URLを知っていれば**誰でも閲覧可能**（公開リプレイ）
- 非公開設定: プレイヤーが「リプレイを非公開にする」設定可能（自分の対戦が共有されなくなる）

---

## 3. ハイライト自動生成（Phase 4）

### 3.1 検出対象

| ハイライト | 検出条件 |
|-----------|---------|
| 鯱鉾勝利 | HP が0に到達し、相手の現在 HP が「Final State」の瞬間 + 直撃で決定したターン |
| HP しきい値突破 | 相手の HP が 85/70/55/40/25/10 のしきい値を超えて WishTrigger が発動したターン |
| 大逆転 | HP が低い状態から複数ターンで回復し相手より有利になったターン |
| 連続直撃 | 1ターン内に願主への直撃が3回以上 |
| レベルアップ | 願主がLv3に到達したターン |

### 3.2 出力

- ハイライトターンに自動ブックマーク
- リプレイ再生時に「ハイライト」ボタンで該当ターンにジャンプ

---

## 4. 観戦モード（Phase 3+）

### 4.1 観戦方式

| 項目 | 値 |
|------|-----|
| 遅延 | **2ターン遅延**（情報漏洩防止） |
| 手札表示 | **非表示**（双方の手札は見えない） |
| 最大観戦者数 | **10人** |

### 4.2 観戦参加方法

- フレンドリスト → 対戦中のフレンド → 「観戦」ボタン
- 観戦URLの共有（将来）

### 4.3 観戦者UI

- バトル画面と同一レイアウト（操作不可）
- 画面上部に「観戦中」バッジ
- エモート送信不可
- 「退出」ボタンで離脱

### 4.4 プライバシー設定

```
設定 > プライバシー > 観戦
├── 観戦を許可: フレンドのみ / 全員 / 許可しない
└── デフォルト: フレンドのみ
```

---

## 5. Firestore設計

### 5.1 リプレイデータ

```
replays/{replayId}/
  ├── matchId: string
  ├── version: string
  ├── players: array
  ├── initialState: map
  ├── commands: array
  ├── result: map
  ├── createdAt: timestamp
  ├── isPublic: boolean
  └── expiresAt: timestamp (createdAt + 90days)
```

### 5.2 ユーザーのリプレイ参照

```
users/{uid}/replays/{replayId}/
  ├── isFavorite: boolean
  └── savedAt: timestamp
```

---

## 6. 関連ファイル

- `NETWORK_SPEC.md` — コマンド構造
- `GAME_DESIGN.md §10` — ターン進行
- `ANIMATION_SPEC.md` — リプレイ再生時のアニメーション
- `SOCIAL_SPEC.md` — フレンド観戦
- `NOTIFICATION_SPEC.md` — 観戦招待通知
