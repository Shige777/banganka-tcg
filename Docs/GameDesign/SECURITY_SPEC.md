# SECURITY_SPEC.md
> 万願果 セキュリティ設計 v1.0

---

## 0. 目的

オンライン対戦TCGにおけるチート対策、通信セキュリティ、アカウント保護、不正検知、プライバシー保護の技術仕様を定義する。

---

## 1. セキュリティ方針

- **サーバー権威モデル**: ゲームロジックの最終判定はすべてCloud Functionsで行う（NETWORK_SPEC.md準拠）
- **クライアント不信原則**: クライアントからのデータは常に検証する
- **最小権限**: Firestore Security Rulesで必要最小限のアクセスのみ許可
- **暗号化**: 通信はTLS 1.2以上を強制

---

## 2. チート対策

### 2.1 サーバーサイド検証（主要防御）

NETWORK_SPEC.md のサーバー権威モデルにより、以下が自動的に防御される。

| チート種類 | 防御方法 |
|-----------|---------|
| **手札改竄** | private/{playerId}/hand はCloud Functionsのみ読み書き可 |
| **CP偽装** | processCommand でCP残量をサーバーで検証 |
| **存在しないカードのプレイ** | 手札にないカードIDのコマンドは拒否 |
| **不正な戦力変更** | 戦闘計算はサーバーで実行 |
| **願力改竄** | ChannelGauge はサーバーのみ書き込み可 |
| **ターン外操作** | currentTurnPlayerId とコマンド送信者の一致確認 |

### 2.2 クライアントサイド検証（補助防御）

| 対策 | 方法 | 検知時の動作 |
|------|------|-------------|
| **メモリ改竄検知** | 重要値（CP、手札枚数）のチェックサム検証 | サーバーに不正フラグ送信 |
| **スピードハック検知** | サーバー時刻とクライアント時刻の乖離監視（±5秒以上で警告） | ログ記録 + 連続時は切断 |
| **パケット改竄検知** | コマンドにHMAC署名を付与、サーバーで検証 | コマンド拒否 |
| **リプレイ攻撃防止** | コマンドにsequence numberを付与（単調増加） | 重複コマンド破棄 |

### 2.3 Unity固有の対策

| 対策 | 方法 |
|------|------|
| **IL2CPP強制** | Mono→IL2CPPビルドで逆コンパイル難易度を上げる |
| **難読化** | コード難読化ツール（Obfuscator等）適用 |
| **Jailbreak検知** | iOSのJailbreak検知（ファイルシステムチェック） → 警告表示（BAN対象外） |
| **デバッグ検知** | デバッガアタッチ検知 → 対戦不可 |

---

## 3. 通信セキュリティ

### 3.1 暗号化

| 通信先 | プロトコル | 暗号化 |
|--------|-----------|--------|
| Firebase Authentication | HTTPS | TLS 1.2+ |
| Cloud Firestore | HTTPS | TLS 1.2+ |
| Realtime Database | WSS | TLS 1.2+ |
| Cloud Functions | HTTPS | TLS 1.2+ |
| AdMob | HTTPS | TLS 1.2+ |

追加暗号化: Firebase SDKのTLSに加えて独自暗号化は不要（Firebase SDKが標準で十分なセキュリティを提供）。

### 3.2 証明書ピニング

- Firebase SDKは証明書ピニングをデフォルトでサポート
- 独自APIエンドポイントを追加する場合はSSL Pinningを実装

---

## 4. アカウント保護

### 4.1 認証セキュリティ

| 対策 | 方法 |
|------|------|
| **Apple Sign-in** | OAuth 2.0 + PKCE。トークンの安全な管理 |
| **匿名→Apple IDリンク** | linkWithCredential で安全に移行 |
| **セッション管理** | Firebase Auth のIDトークン自動更新（1時間有効） |
| **マルチデバイスログイン** | 同一アカウントの同時ログインは**最新デバイスのみ有効** |

### 4.2 アカウント乗っ取り対策

- Apple Sign-in のトークン失効を定期チェック（Sign in with Apple サーバー通知）
- 異常ログイン検知（短時間での地理的に離れた場所からのアクセス）→ ログ記録（BAN対象ではない）
- パスワードはFirebase Authenticationが管理（アプリ側では保持しない）

### 4.3 アカウント削除（GDPR/APPI対応）

```
設定 → アカウント → アカウント削除
  → 確認ダイアログ（アカウント削除の不可逆性を説明）
  → Apple Sign-in で再認証
  → Cloud Function: deleteAccount
    - Firestore: users/{uid} 以下全データ削除
    - RTDB: 進行中マッチがあれば自動敗北処理後に削除
    - Firebase Auth: ユーザーアカウント削除
    - 処理完了 → アプリを初期画面に戻す
```

---

## 5. レート制限

### 5.1 Cloud Functions

| エンドポイント | レート制限 | 備考 |
|-------------|-----------|------|
| createRoom | 5回/分/ユーザー | ルーム乱立防止 |
| joinRoom | 10回/分/ユーザー | 連続参加試行防止 |
| processCommand | 30回/分/ユーザー | 1ターン内のコマンドスパム防止 |
| purchaseItem | 3回/分/ユーザー | 連続購入バグ防止 |
| verifyReceipt | 3回/分/ユーザー | レシート検証スパム防止 |

### 5.2 Firestore

- Security Rules 内で `request.time` を使った書き込み頻度制限
- デッキ保存: 10回/分/ユーザー
- プロフィール更新: 5回/分/ユーザー

---

## 6. 不正検知とBAN

### 6.1 不正行為分類

| レベル | 行為例 | 対処 |
|--------|--------|------|
| **Warning** | スピードハック検知1回、軽微なクライアント不整合 | ログ記録のみ |
| **Soft BAN** | スピードハック検知3回以上、レシート不正1回 | 24時間対戦停止 + 警告メール |
| **Hard BAN** | レシート偽造確認、大規模不正、RMT確認 | アカウント永久停止 |

### 6.2 BAN フロー

```
不正検知
  → Firestore: users/{uid}/violations/ にログ追加
  → Cloud Function: evaluateViolation
    - 累積違反数とレベルを評価
    - Soft BAN: users/{uid}/banUntil にタイムスタンプ設定
    - Hard BAN: users/{uid}/isBanned = true
  → クライアント:
    - Soft BAN: ログイン時に停止期間表示
    - Hard BAN: ログイン時に永久停止表示 + お問い合わせ導線
```

### 6.3 異議申し立て

- Hard BAN のプレイヤーには「お問い合わせ」からの異議申し立て手段を提供
- 対応期限: 7営業日以内

---

## 7. プライバシー保護

### 7.1 個人情報の取扱い

| データ | 収集 | 保存場所 | 第三者提供 |
|--------|------|---------|-----------|
| Apple ID（メール） | 認証時 | Firebase Auth | なし |
| プレイヤー名 | ユーザー入力 | Firestore | 対戦相手に表示 |
| 対戦履歴 | 自動 | Firestore | なし |
| デバイス情報 | 自動（Analytics） | Firebase Analytics | Google（匿名集計） |
| 課金履歴 | App Store連携 | Firestore | なし |

### 7.2 GDPR / APPI 対応

| 要件 | 対応 |
|------|------|
| **データ取得権** | 設定 → プライバシー → 「自分のデータをダウンロード」（JSON形式） |
| **データ削除権** | 設定 → アカウント → 「アカウント削除」（§4.3参照） |
| **同意管理** | 初回起動時にプライバシーポリシー同意画面 |
| **Cookie同意** | アプリのため不要（WebViewを使う場合は別途） |
| **未成年保護** | App Storeの年齢制限（9+）に準拠 |

### 7.3 プライバシーポリシー

- APPSTORE_CHECKLIST.md に記載のApp Privacy宣言に準拠
- アプリ内設定画面からプライバシーポリシーページへリンク
- データ収集項目の変更時はアプリ内通知で告知

---

## 8. インシデント対応

### 8.1 対応レベル

| レベル | 状況 | 対応時間 | 対処 |
|--------|------|---------|------|
| **P0 Critical** | データ漏洩、認証バイパス | 即時 | サービス一時停止 + 緊急修正 |
| **P1 High** | 大規模チート発覚、課金不正 | 4時間以内 | 対象アカウント停止 + 修正 |
| **P2 Medium** | 軽微なエクスプロイト | 24時間以内 | 次回メンテナンスで修正 |
| **P3 Low** | 表示バグ等のセキュリティ影響なし | 次回アップデート | 通常開発サイクルで対応 |

---

## 9. DDoS保護・負荷テスト計画

### 9.1 DDoS保護（Firebase経由）

| 対策 | 説明 |
|------|------|
| Firebase App Check | クライアント認証トークンの検証。不正クライアントからのリクエストを拒否 |
| Cloud Functions レート制限 | ユーザーごとに1秒あたり最大10リクエスト。超過時は `429 Too Many Requests` を返却 |
| Firestore Security Rules | 認証済みユーザーのみアクセス可能。匿名認証にも Firebase App Check を適用 |
| RTDB コマンド制限 | 1ユーザーあたり1秒に1コマンドまで。ターン制TCGでは十分 |
| Google Cloud Armor | Blaze プランで利用可能。MVP後にWAFルールを設定（IPレート制限、Geo制限等） |

### 9.2 負荷テスト計画

| テスト種別 | ツール | 目標 | 実施タイミング |
|-----------|--------|------|-------------|
| Cloud Functions ストレステスト | Firebase Emulator + Artillery | 同時100ユーザー × 10リクエスト/秒 を 5分間 | MVP品質保証Phase |
| RTDB 同時接続テスト | Firebase Emulator | 同時200マッチ（400接続）の安定性確認 | MVP品質保証Phase |
| Firestore 読み書きスパイク | Artillery + Custom Script | 瞬間1000リクエスト（パック発売時のスパイクを想定） | リリース前 |
| 端末メモリ・CPU負荷 | Xcode Instruments | iPhone SE(3rd) で60fps維持 + メモリ150MB以下 | MVP品質保証Phase |

### 9.3 インシデント対応連携

DDoSまたは異常負荷検知時のフロー:
1. Cloud Monitoring のアラート発火（Cloud Functions エラーレート > 10% × 5分）
2. 自動メンテナンスモード移行（BACKEND_DESIGN.md §8.4 準拠）
3. 管理者に通知（メール）
4. 原因調査 → 対象IP/ユーザーのブロック or スケールアップ

---

## 10. 関連ファイル

- `NETWORK_SPEC.md` — サーバー権威モデル、Security Rules
- `BACKEND_DESIGN.md` — Firestore/RTDB Security Rules、Cloud Functions
- `APPSTORE_CHECKLIST.md` — App Privacy宣言
- `ERROR_HANDLING.md` — エラーカテゴリ
- `ANALYTICS_SPEC.md` — 行動追跡イベント
