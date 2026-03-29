# BACKEND_DESIGN.md
> 万願果 バックエンド・データベース設計 v1.0

---

## 0. 目的

オンライン1v1 TCGを支えるバックエンド基盤を定義する。
ソロ開発者がインフラ管理を最小化しつつ、スケーラブルな構成を実現するための設計書。

---

## 1. 技術スタック

### 1.1 Firebase（Google Cloud）

| サービス | 用途 |
|----------|------|
| **Firebase Authentication** | ユーザー認証（Apple Sign-in、匿名認証） |
| **Cloud Firestore** | ユーザーデータ、デッキ、カード所持、対戦履歴、ショップ |
| **Firebase Realtime Database** | リアルタイム対戦ステート同期（NETWORK_SPEC.md参照） |
| **Cloud Functions (v2)** | ゲームロジック検証、マッチメイキング、ターンタイマー監視 |
| **Firebase Cloud Messaging** | プッシュ通知（MVP後） |
| **Firebase Analytics** | プレイヤー行動分析（ANALYTICS_SPEC.md参照） |

### 1.2 選定理由
- iOS SDK が成熟しており Unity SDK も公式サポート
- サーバーレスのためインフラ管理ゼロ
- Spark（無料）プランで開発・テスト可能、Blaze（従量課金）で本番運用
- Authentication → Firestore Security Rules の連携でセキュリティが統合的
- Realtime Database のリアルタイムリスナーがターン制TCGに最適

---

## 2. 認証設計

### 2.1 認証方式（MVP）

| 方式 | 優先度 | 備考 |
|------|--------|------|
| **Apple Sign-in** | 必須 | App Store審査要件。iOSアプリでソーシャルログインを提供する場合必須 |
| **匿名認証** | 必須 | 初回起動時に即プレイ可能にするため。後からApple IDにリンク可能 |
| **Game Center** | MVP後 | フレンド機能やリーダーボード連携時に追加 |

### 2.2 認証フロー
```
初回起動
  ├─ 匿名認証で即座にUID発行
  ├─ チュートリアル・初回バトルが可能
  └─ 設定画面から Apple Sign-in でアカウントリンク（任意）

2回目以降
  ├─ 匿名UID → 自動ログイン
  └─ Apple ID リンク済み → Apple Sign-in で復元
```

### 2.3 アカウント削除
- App Store審査要件：アカウント削除機能の提供が必須
- 設定画面に「アカウント削除」ボタンを設置
- Cloud Functionで関連データを全削除（GDPR / App Store準拠）

---

## 3. データベース設計（Cloud Firestore）

### 3.1 コレクション構造

```
firestore/
├── users/{uid}/
│   ├── displayName: string
│   ├── createdAt: timestamp
│   ├── lastLoginAt: timestamp
│   ├── tutorialCompleted: boolean
│   ├── storyProgress: {chapter: number, scene: number}
│   ├── selectedDeckId: string
│   └── currency/
│       ├── gold: number         (無償通貨)
│       └── premium: number      (有償通貨)
│
├── users/{uid}/cards/{cardId}/
│   ├── cardId: string           (GAME_DESIGN.md A2-A4 のID)
│   ├── count: number            (所持枚数)
│   ├── obtainedAt: timestamp
│   └── isNew: boolean
│
├── users/{uid}/decks/{deckId}/
│   ├── name: string
│   ├── cardIds: string[34]      (34枚固定)
│   ├── createdAt: timestamp
│   ├── updatedAt: timestamp
│   └── isPreset: boolean        (プリセットデッキか)
│
├── matches/{matchId}/
│   ├── player1Uid: string
│   ├── player2Uid: string
│   ├── winner: "P1" | "P2" | "Draw" | null
│   ├── reason: "koshoko_victory" | "nuri_victory" | "turn_limit" | "disconnect" | "timeout" | "surrender"
│   ├── turnCount: number
│   ├── duration: number         (秒)
│   ├── startedAt: timestamp
│   ├── endedAt: timestamp
│   ├── player1DeckSnapshot: string[34]
│   └── player2DeckSnapshot: string[34]
│
├── users/{uid}/stats/
│   ├── totalGames: number
│   ├── wins: number
│   ├── losses: number
│   ├── draws: number
│   ├── winStreak: number
│   ├── maxWinStreak: number
│   ├── rating: number           (MVP後。初期値1000)
│   └── favoriteAspect: string   (最も多くプレイした願相)
│
├── rooms/{roomId}/
│   ├── hostUid: string
│   ├── guestUid: string | null
│   ├── status: "waiting" | "ready" | "started" | "expired"
│   ├── createdAt: timestamp
│   └── matchId: string | null
│
├── cardMaster/{cardId}/
│   ├── (CARD_SCHEMA.md の全フィールド)
│   └── (サーバー側マスターデータ。クライアントはバンドル版を使用)
│
└── shopItems/{itemId}/
    ├── name: string
    ├── type: "pack" | "bundle" | "currency"
    ├── priceType: "gold" | "premium" | "real"
    ├── priceAmount: number
    ├── contents: [{cardId, count, guaranteed}]
    ├── isActive: boolean
    └── expiresAt: timestamp | null
```

### 3.2 セキュリティルール（Firestore）

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {

    // ユーザーデータ：本人のみ読み書き
    match /users/{uid}/{document=**} {
      allow read, write: if request.auth != null && request.auth.uid == uid;
    }

    // 対戦履歴：参加者のみ読み取り、書き込みはCloud Functionsのみ
    match /matches/{matchId} {
      allow read: if request.auth != null &&
        (request.auth.uid == resource.data.player1Uid ||
         request.auth.uid == resource.data.player2Uid);
      allow write: if false; // Cloud Functions のみ
    }

    // ルーム：認証済みユーザーが読み書き
    match /rooms/{roomId} {
      allow read: if request.auth != null;
      allow create: if request.auth != null;
      allow update: if request.auth != null;
      allow delete: if false; // Cloud Functions のみ
    }

    // カードマスター：全員読み取り可、書き込みは管理者のみ
    match /cardMaster/{cardId} {
      allow read: if true;
      allow write: if false; // 管理ツールまたはCLIから更新
    }

    // ショップ：全員読み取り可、書き込みはCloud Functionsのみ
    match /shopItems/{itemId} {
      allow read: if request.auth != null;
      allow write: if false;
    }
  }
}
```

---

## 4. Cloud Functions 設計

### 4.1 関数一覧

| 関数名 | トリガー | 処理内容 |
|--------|----------|---------|
| `createRoom` | HTTPS Callable | ルーム作成・ID生成 |
| `joinRoom` | HTTPS Callable | ルーム参加・バリデーション |
| `startMatch` | HTTPS Callable | 対戦初期化（デッキ・手札・GameState生成） |
| `processCommand` | RTDB onWrite | コマンド検証 → GameState更新 |
| `checkTurnTimeout` | Cloud Scheduler (10秒間隔) | タイムアウト監視・自動EndTurn |
| `checkDisconnect` | RTDB onDisconnect | 切断検知・猶予タイマー開始・ターンタイマー一時停止（NETWORK_SPEC.md §6.3 準拠） |
| `cleanupRooms` | Cloud Scheduler (1分間隔) | 期限切れルームの削除 |
| `onMatchEnd` | Firestore onWrite | 対戦終了処理（stats更新・報酬付与） |
| `purchaseItem` | HTTPS Callable | ショップ購入・カード付与 |
| `verifyReceipt` | HTTPS Callable | App Store レシート検証（課金） |
| `deleteAccount` | HTTPS Callable | アカウント完全削除 |

### 4.2 processCommand の処理フロー

```
1. コマンド受信（RTDB /commands/ への書き込みトリガー）
2. 認証チェック（送信者 == activePlayer?）
3. GameState読み込み（RTDB /state/）
4. コマンド検証（GAME_DESIGN.md のルールに基づく）
5. ゲームロジック実行（状態遷移）
6. 勝利判定（§8.1, §8.3）
7. GameState書き込み（RTDB /state/）
8. ログ書き込み（RTDB /log/）
9. 対戦終了なら Firestore /matches/ に結果保存
```

---

## 5. カードマスターデータ管理

### 5.1 二重管理方式
- **Firestore `cardMaster/`**：サーバー側の正本。Cloud Functionsがゲームロジック検証時に参照
- **Unity StreamingAssets**：クライアント側バンドル。表示用データ（イラスト・テキスト含む）

### 5.2 バージョニング
- カードデータにバージョン番号を持たせる
- アプリ起動時にFirestoreのバージョンとローカルバージョンを比較
- 差分がある場合はダウンロード → ローカル上書き
- バランス調整（数値変更のみ）はアプリ更新なしで反映可能

---

## 6. MVP初期データ

### 6.1 新規プレイヤー作成時の初期データ
```json
{
  "displayName": "果求者",
  "tutorialCompleted": false,
  "storyProgress": {"chapter": 0, "scene": 0},
  "currency": {"gold": 1000, "premium": 0},
  "cards": [
    // GAME_DESIGN.md A5 スターターデッキの全カードを各3枚付与
    {"cardId": "MAN_CON_01", "count": 3},
    {"cardId": "MAN_CON_02", "count": 3},
    {"cardId": "MAN_CON_03", "count": 3},
    {"cardId": "MAN_WHI_02", "count": 3},
    {"cardId": "MAN_MAN_01", "count": 3},
    {"cardId": "MAN_HUS_01", "count": 3},
    {"cardId": "SPL_CON_01", "count": 3},
    {"cardId": "SPL_CON_02", "count": 3},
    {"cardId": "SPL_WHI_01", "count": 3},
    {"cardId": "SPL_WHI_02", "count": 3},
    {"cardId": "ALG_01", "count": 1},
    {"cardId": "ALG_02", "count": 1},
    {"cardId": "ALG_03", "count": 1},
    {"cardId": "ALG_04", "count": 1}
  ],
  "decks": [
    // GAME_DESIGN.md A5 スターターデッキをプリセットとして登録
  ]
}
```

---

## 7. コスト見積もり（Firebase Blaze プラン）

### 7.1 MVP想定規模
- DAU：100人
- 1日あたりの対戦数：200試合
- 1試合あたりのRTDB読み書き：約500回

### 7.2 月間コスト概算

| サービス | 無料枠 | 想定使用量 | 月額 |
|----------|--------|-----------|------|
| Authentication | 月10,000認証 | 100 | $0 |
| Firestore 読み取り | 50,000/日 | 30,000/日 | $0 |
| Firestore 書き込み | 20,000/日 | 5,000/日 | $0 |
| Realtime Database | 100MB, 10GB転送 | 50MB, 5GB | $0 |
| Cloud Functions | 200万呼出/月 | 300,000/月 | $0 |
| **合計** | | | **$0（無料枠内）** |

DAU 1,000 を超えるまでは無料枠内で運用可能な見込み。

---

## 8. バックアップ・災害復旧（DR）計画

### 8.1 Firestore バックアップ

| 項目 | 設定 |
|------|------|
| 自動バックアップ | Cloud Firestore の「エクスポート」を Cloud Scheduler で **毎日 04:00 JST** に実行 |
| 保存先 | Cloud Storage バケット `gs://banngannka-backup/firestore/` |
| 保存期間 | 直近30日間のバックアップを保持。30日超は自動削除 |
| 対象 | 全コレクション（users, matches, cardMaster, shopItems, rooms） |
| リストア手順 | `gcloud firestore import` コマンドで指定日時のバックアップから復元 |
| リストア目標時間（RTO） | 1時間以内 |
| データ損失許容量（RPO） | 最大24時間（直近バックアップまで） |

### 8.2 Realtime Database バックアップ

| 項目 | 設定 |
|------|------|
| 自動バックアップ | Blaze プランで有効化される「自動バックアップ」機能を使用 |
| 保存先 | Cloud Storage バケット `gs://banngannka-backup/rtdb/` |
| 頻度 | 毎日（Firebase 管理コンソールで設定） |
| 備考 | RTDBは対戦中の一時データが中心。対戦終了後の結果は Firestore に保存されるため、RTDBのバックアップは補助的 |

### 8.3 障害時のフォールバック

| 障害種別 | 影響範囲 | フォールバック |
|---------|---------|-------------|
| Firestore 障害 | ユーザーデータ・デッキ読み書き不可 | 対戦開始をブロック。ホーム画面はキャッシュから表示。トースト「現在サービスに接続できません」 |
| RTDB 障害 | 対戦中のリアルタイム同期不可 | 進行中の対戦を一時停止。再接続を試行。60秒以内に復旧しなければ引き分け扱い |
| Cloud Functions 障害 | コマンド処理不可 | クライアント側でリトライ（ERROR_HANDLING.md §2.2 準拠）。5分以上続く場合はメンテナンスモードに自動切替 |
| Authentication 障害 | ログイン/認証不可 | 既存セッションは継続可能（トークンのローカルキャッシュ）。新規ログインのみブロック |

### 8.4 メンテナンスモード移行

- Firebase Remote Config の `maintenance_mode` フラグで制御（ERROR_HANDLING.md §2.4）
- Cloud Monitoring のアラートポリシーで自動検知:
  - Cloud Functions エラーレート > 10% が 5分間継続 → アラート発火
  - Firestore レイテンシ > 5秒 が 5分間継続 → アラート発火
- アラート発火時に管理者（開発者）へ通知（メール + Firebase Console）
- 手動で `maintenance_mode = true` を設定してメンテナンスモードに移行

### 8.5 データ復旧 SLA（運用目標）

| 指標 | 目標値 | 備考 |
|------|--------|------|
| 稼働率 | 99.5%（月間） | Firebase SLA（99.95%）より控えめに設定 |
| RTO（復旧時間目標） | 1時間 | Firestore バックアップからの復元 |
| RPO（復旧時点目標） | 24時間 | 日次バックアップの間隔 |
| 対戦データ完全性 | 99.9% | 進行中の対戦が障害で中断された場合は引き分け扱い |

---

## 9. MVP後の拡張

| Phase | 追加サービス | 用途 |
|-------|-------------|------|
| Phase 3 | Cloud Storage | リプレイデータ保存 |
| Phase 4 | Firebase Remote Config | A/Bテスト、バランス調整のリアルタイム配信 |
| Phase 5 | Cloud Pub/Sub | ランダムマッチキュー |
| Phase 6 | BigQuery Export | 大規模アナリティクス |
