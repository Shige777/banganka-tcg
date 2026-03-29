# NETWORK_SPEC.md
> 万願果 ネットワーク・オンライン対戦仕様 v1.1

---

## 0. 目的

1v1リアルタイム対戦を成立させるための通信仕様を定義する。
本書は `GAME_DESIGN.md` のバトルロジックを前提とし、SCREEN_SPEC.md のバトル画面・マッチメイキング画面と連携する。

---

## 1. 基本方針

### 1.1 アーキテクチャ：サーバー権威型（Server-Authoritative）
- ゲームロジックの最終判定はすべてサーバー（Cloud Functions）が行う
- クライアントは **コマンド送信 + 結果受信** のみ
- クライアントが不正なコマンドを送信しても、サーバーが拒否する
- チート対策の根幹であり、これを崩さない

### 1.2 通信プロトコル
- **Firebase Realtime Database** をリアルタイム同期に使用
- WebSocketは不要（Realtime Databaseが双方向リスニングを提供）
- ターン制TCGのため低レイテンシ要件は緩い（200ms以下であれば快適）

### 1.3 通信モデル
```
Client A                 Firebase RTDB               Cloud Functions              Client B
   |                         |                            |                         |
   |-- Command(PlayCard) --> |                            |                         |
   |                         |-- onWrite trigger -------> |                         |
   |                         |                     [validate & resolve]             |
   |                         |<-- write GameState ------- |                         |
   |<-- onValue(GameState) - |                            | - onValue(GameState) --> |
   |                         |                            |                         |
```

---

## 2. マッチメイキング

### 2.1 MVP方式：ルームベースマッチング
SCREEN_SPEC.md §3 のバトル画面仕様に準拠。

**ルーム作成フロー**：
1. Player Aが「ルーム作成」をタップ
2. Cloud Functionが6桁の英数字ルームIDを生成
3. Firestore `rooms/{roomId}` にドキュメント作成
4. Player Aにルームid表示 → 相手に共有

**ルーム参加フロー**：
1. Player BがルームIDを入力して「参加」
2. Cloud Functionがルームの存在と空き状態を確認
3. `rooms/{roomId}.player2` にPlayer Bを登録
4. 両プレイヤーの接続が確認されたら「対戦開始」ボタンを活性化

**ルーム有効期限**：
- 作成から5分間参加がなければ自動削除
- 対戦終了後は即座に削除

### 2.2 MVP後：ランダムマッチ（POST_MVP Phase 5）
- マッチキューにエントリー → Cloud Functionが近似レーティングのペアを組む
- MVP後の実装のため本書では詳細を定義しない

---

## 3. ゲームセッション管理

### 3.1 データ構造（Realtime Database）

```
/matches/{matchId}/
  ├── meta/
  │   ├── player1Uid: string
  │   ├── player2Uid: string
  │   ├── status: "mulligan" | "active" | "finished"
  │   ├── createdAt: timestamp
  │   └── winner: null | "P1" | "P2" | "Draw"
  │
  ├── state/
  │   ├── turnTotal: number
  │   ├── activePlayer: 1 | 2
  │   ├── hpP1: number  (0-100)
  │   ├── hpP2: number  (0-100)
  │   ├── finalStateP1: boolean
  │   ├── finalStateP2: boolean
  │   ├── cpP1: number
  │   ├── cpP2: number
  │   ├── maxCpP1: number
  │   ├── maxCpP2: number
  │   ├── manifestRowP1: [{cardId, exhausted, power, wishDamage, keywords, row}]
  │   ├── manifestRowP2: [{cardId, exhausted, power, wishDamage, keywords, row}]
  │   ├── sharedAlgo: {cardId, owner, faceDown: bool} | null  (faceDown=true: 裏向きセット状態。相手にはcardId非公開)
  │   ├── leaderP1: {level, evoGauge, evoMax, keyAspect, power, wishDamage, exhausted, keywords, skillUsed: {lv2: bool, lv3: bool}}
  │   ├── leaderP2: {level, evoGauge, evoMax, keyAspect, power, wishDamage, exhausted, keywords, skillUsed: {lv2: bool, lv3: bool}}
  │   ├── handCountP1: number  (相手には枚数のみ公開)
  │   ├── handCountP2: number
  │   └── isGameOver: boolean
  │
  ├── private/
  │   ├── deckP1: [cardIds...]  (サーバーのみ読み取り可)
  │   ├── deckP2: [cardIds...]
  │   ├── handP1: [cardIds...]
  │   └── handP2: [cardIds...]
  │
  ├── commands/
  │   └── {pushId}: {type, payload, playerUid, timestamp}
  │
  ├── timers/
  │   ├── turnStartedAt: timestamp
  │   ├── turnTimeLimit: 90  (秒)
  │   └── disconnectDeadline: timestamp | null
  │
  └── log/
      └── [{event, data, timestamp}]
```

### 3.2 セキュリティルール
- `/matches/{matchId}/state/` : 両プレイヤー読み取り可、書き込みはCloud Functionsのみ
- `/matches/{matchId}/private/` : 読み取り・書き込みともCloud Functionsのみ
- `/matches/{matchId}/commands/` : 該当プレイヤーのみ書き込み可（自分のコマンドのみ）
- `/matches/{matchId}/log/` : 両プレイヤー読み取り可、書き込みはCloud Functionsのみ

---

## 4. コマンド仕様

### 4.1 コマンド一覧（GAME_DESIGN.md B6 準拠）

| コマンド | payload | タイミング |
|----------|---------|-----------|
| `Mulligan` | `{selectedCardIds: string[]}` | マリガンフェーズのみ |
| `PlayManifest` | `{cardId: string, row: "front" \| "back", position: 0-2}` | メインフェーズ |
| `PlaySpell` | `{cardId: string, targets?: string[]}` | メインフェーズ |
| `PlayAlgorithm` | `{cardId: string}` | メインフェーズ |
| `DeclareAttack` | `{attackerId: string, targetId: string}` | 戦闘フェーズ |
| `DeclareBlock` | `{blockerId: string, attackerId: string}` | 戦闘フェーズ（防御側） |
| `EndTurn` | `{}` | いつでも（自分のターン中） |

### 4.2 コマンド検証（Cloud Functions）
すべてのコマンドは以下の順で検証される：
1. **認証チェック**：送信者が現在のアクティブプレイヤーか
2. **フェーズチェック**：現在のゲームフェーズでそのコマンドが有効か
3. **リソースチェック**：CP・手札・盤面スロットなどの条件を満たすか
4. **ルールチェック**：GAME_DESIGN.md §10 の処理順に従い合法か
5. **HP変更・願いトリガー**：ダメージ計算（固定%または現在%）、HP閾値に基づくWishTrigger発動
6. **状態更新**：検証通過後、GameStateを更新（hpP1/hpP2、lifeCards、finalState含む）して両クライアントに配信

不正コマンドは **拒否レスポンス**（`{error: "INVALID_COMMAND", reason: "..."}`) を返し、GameStateは変更しない。

> HP閾値は GAME_DESIGN.md §4.1 に定義（85/70/55/40/25/10）。WishTrigger発動判定はサーバー側で実行し、閾値到達イベントをクライアントへ通知する。

---

## 5. ターンタイマー

### 5.1 基本ルール
- 1ターンの制限時間：**90秒**
- ターン開始時に `timers/turnStartedAt` を更新
- Cloud Functionsのスケジュールタスクで残時間を監視

### 5.2 タイムアウト処理
- 90秒経過してもコマンドがない場合、サーバーが自動で `EndTurn` を実行
- **連続タイムアウトペナルティ**：
  - 2回連続タイムアウト → クライアントに警告表示
  - 3回連続タイムアウト → 自動敗北（`MATCH_END(result=opponent_win, reason=timeout)`)

### 5.3 クライアント表示
- 画面上部（またはターン表示近く）にカウントダウンタイマーを表示
- 残り15秒で赤色 + アニメーション警告
- 残り5秒で画面フラッシュ

---

## 6. 切断・再接続

### 6.1 切断検知
- Firebase Realtime Databaseの **presence** 機能を使用
- `onDisconnect()` でプレイヤーの切断を検知
- 切断時にサーバーが `timers/disconnectDeadline` を設定

### 6.2 再接続フロー
1. クライアントがアプリ復帰 → Firebase再接続
2. `/matches/{matchId}/state/` を再リスニング
3. 最新GameStateを受信して画面を復元
4. `timers/disconnectDeadline` をクリア
5. 通常のターン進行に復帰

### 6.3 タイムアウト
- **再接続猶予時間：60秒**
- 60秒以内に再接続 → 通常続行（**ターンタイマーは切断中一時停止し、再接続後に残り時間から再開する**）
- 60秒超過 → 自動敗北（`MATCH_END(result=opponent_win, reason=disconnect)`)

### 6.4 両者切断
- 両者が同時に切断した場合、先に再接続した側のみ猶予リセット
- 両者とも60秒以内に再接続しない場合 → 引き分け

### 6.5 クライアントUI
- 切断検知時：「接続が切れました。再接続中...」オーバーレイ表示
- 相手切断時：「相手の接続を待っています...（残り XX秒）」表示
- タイムアウト時：結果画面に遷移

---

## 7. マリガンフェーズの同期

### 7.1 フロー
1. 両プレイヤーに初期手札5枚を配布（private領域に格納）
2. 各プレイヤーがマリガン選択 → `Mulligan` コマンド送信
3. **両者のMulliganコマンドが揃うまで待機**（タイムアウト：30秒）
4. 30秒以内に未送信のプレイヤーは「引き直しなし」として処理
5. Cloud Functionsがマリガン処理を実行し、新しい手札を配布
6. `meta/status` を "active" に変更
7. Player1のターン開始

---

## 8. 対戦中の情報公開ルール

### 8.1 公開情報（両プレイヤーが見える）
- 盤面の全顕現（カード名、戦力、願撃、キーワード、消耗/待機状態）
- 共有界律
- 両願主の状態（レベル、願成ゲージ、戦力、キーワード、スキル使用済みフラグ）
- 各プレイヤーのHP値（0-100）と閾値ライフカード状態
- 現在ターン数
- 相手の手札枚数
- 対戦ログ

### 8.2 非公開情報（サーバーのみ保持）
- 相手の手札内容
- 相手のデッキ残り内容・順番

---

## 9. レイテンシ・パフォーマンス要件

| 項目 | 目標値 |
|------|--------|
| コマンド送信→GameState反映 | 500ms以下 |
| マッチメイキング（ルーム参加） | 2秒以下 |
| 再接続→画面復元 | 3秒以下 |
| Firebase RTDB リスナー遅延 | 200ms以下（国内） |

---

## 10. エラーコード一覧

| コード | 意味 | クライアント表示 |
|--------|------|-----------------|
| `ERR_NOT_YOUR_TURN` | 自分のターンではない | — (無視) |
| `ERR_INVALID_PHASE` | 現在のフェーズでは実行不可 | — (無視) |
| `ERR_INSUFFICIENT_CP` | CP不足 | 「CPが足りません」 |
| `ERR_FIELD_FULL` | 顕現列が満員 | 「配置スロットがありません」 |
| `ERR_CARD_NOT_IN_HAND` | 手札にないカード | — (不正検知) |
| `ERR_EXHAUSTED` | 消耗中のカードで攻撃 | — (UI側で制御) |
| `ERR_SUMMON_SICK` | 召喚酔い中のカードで攻撃 | — (UI側で制御) |
| `ERR_INVALID_BLOCK` | ブロッカーでないカードでブロック | — (UI側で制御) |
| `ERR_MATCH_NOT_FOUND` | 対戦が見つからない | 「対戦が終了しました」 |
| `ERR_ROOM_FULL` | ルームが満員 | 「ルームが満員です」 |
| `ERR_ROOM_NOT_FOUND` | ルームが存在しない | 「ルームが見つかりません」 |
| `ERR_TIMEOUT` | ターンタイムアウト | 「制限時間を超過しました」 |

---

## 11. MVP後の拡張予定

- ランダムマッチキュー + レーティングベースマッチング（Phase 5）
- 観戦モード（GameState の読み取り専用リスナー）
- リプレイ機能（log を再生してGameStateを再構築）
- 決定論エンジン（乱数seedの共有でクライアント並列シミュレーション）
- ハッシュチェーンによる改ざん検知
