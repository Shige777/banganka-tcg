# ANALYTICS_SPEC.md
> 万願果 アナリティクス・KPI設計 v1.1

---

## 0. 目的

プレイヤー行動の追跡・KPI計測に必要なイベント設計を定義する。
データに基づくバランス調整・リテンション改善・マネタイゼーション最適化の基盤。

---

## 1. ツール

| ツール | 用途 | コスト |
|--------|------|--------|
| **Firebase Analytics** | 基本イベント追跡・ファネル分析 | 無料 |
| **Firebase Crashlytics** | クラッシュ・エラー追跡 | 無料 |
| **BigQuery Export**（MVP後） | 大規模分析・カスタムクエリ | 従量課金 |

---

## 2. 追跡イベント

### 2.1 オンボーディング

| イベント名 | パラメータ | トリガー |
|-----------|-----------|---------|
| `app_first_open` | — | 初回起動 |
| `tutorial_begin` | — | チュートリアル開始 |
| `tutorial_step` | `step_number` | 各ステップ到達 |
| `tutorial_complete` | `duration_sec` | チュートリアル完了 |
| `tutorial_skip` | `step_number` | スキップ時の到達ステップ |

### 2.2 バトル

| イベント名 | パラメータ | トリガー |
|-----------|-----------|---------|
| `match_start` | `match_id`, `is_tutorial` | 対戦開始 |
| `match_end` | `match_id`, `result`, `turn_count`, `duration_sec`, `reason` | 対戦終了 |
| `card_played` | `card_id`, `card_type`, `aspect`, `turn` | カードプレイ |
| `attack_declared` | `attacker_id`, `target_type` | 攻撃宣言 |
| `direct_hit` | `wish_damage`, `turn` | 願主直撃成功 |
| `surrender` | `turn` | 降参 |
| `timeout` | `consecutive_count` | タイムアウト |
| `disconnect` | `reconnected` | 切断（再接続有無） |

### 2.3 ショップ・課金

| イベント名 | パラメータ | トリガー |
|-----------|-----------|---------|
| `shop_view` | — | ショップ画面表示 |
| `pack_purchase` | `pack_type`, `currency_type`, `amount` | パック購入 |
| `pack_open` | `pack_type`, `cards_obtained` | パック開封 |
| `iap_begin` | `product_id`, `price` | IAP開始 |
| `iap_complete` | `product_id`, `price`, `transaction_id` | IAP完了 |
| `iap_fail` | `product_id`, `error` | IAP失敗 |

### 2.4 画面・ナビゲーション

| イベント名 | パラメータ | トリガー |
|-----------|-----------|---------|
| `screen_view` | `screen_name` | 画面表示 |
| `session_start` | — | アプリ起動 |
| `session_end` | `duration_sec` | アプリ終了 |

### 2.5 ストーリー

| イベント名 | パラメータ | トリガー |
|-----------|-----------|---------|
| `story_chapter_start` | `chapter` | 章開始 |
| `story_chapter_complete` | `chapter`, `duration_sec` | 章完了 |

---

## 3. KPI定義

### 3.1 コアKPI

| KPI | 定義 | 目標（MVP） |
|-----|------|------------|
| **DAU** | 日間アクティブユーザー数 | 50+ |
| **D1 リテンション** | 初日翌日復帰率 | 40%+ |
| **D7 リテンション** | 初日7日後復帰率 | 20%+ |
| **D30 リテンション** | 初日30日後復帰率 | 10%+ |
| **チュートリアル完了率** | tutorial_complete / app_first_open | 70%+ |
| **初回対戦到達率** | 初回match_start / app_first_open | 60%+ |
| **平均セッション時間** | session_duration の中央値 | 15分+ |
| **日間対戦数/ユーザー** | match_start / DAU | 3回+ |

### 3.2 バランスKPI

| KPI | 定義 | 健全範囲 |
|-----|------|---------|
| **先攻勝率** | 先攻勝利数 / 総対戦数 | 45%〜55% |
| **平均決着ターン** | turn_count の中央値 | 12〜20ターン |
| **24ターン判定率** | reason=turn_limit / 総対戦数 | 30%以下 |
| **引き分け率** | result=Draw / 総対戦数 | 5%以下 |
| **降参率** | reason=surrender / 総対戦数 | 15%以下 |
| **カード採用率** | 各カードのデッキ内出現率 | 偏りなし |

### 3.3 課金KPI（MVP後）

| KPI | 定義 |
|-----|------|
| **課金率** | IAP完了ユーザー / DAU |
| **ARPU** | 総収益 / DAU |
| **ARPPU** | 総収益 / 課金ユーザー数 |

---

## 4. ファネル分析

### 4.1 新規ユーザーファネル
```
app_first_open
  → tutorial_begin
    → tutorial_complete
      → 初回 match_start
        → 初回 match_end (result=win)
          → D1 復帰
```

### 4.2 バトルファネル
```
screen_view (battle)
  → room_create or room_join
    → match_start
      → match_end
        → 次の match_start (同セッション内)
```

---

## 5. メタゲーム分析（v1.1追加）

### 5.1 デッキ・アーキタイプ追跡

| イベント名 | パラメータ | トリガー |
|-----------|-----------|---------|
| `deck_created` | `deck_id`, `leader_aspect`, `aspect_distribution` | デッキ保存時 |
| `deck_modified` | `deck_id`, `cards_added`, `cards_removed` | デッキ編集時 |
| `deck_used` | `deck_id`, `leader_aspect`, `match_id` | 対戦にデッキを使用 |

### 5.2 アーキタイプ分布KPI

| KPI | 定義 | 健全範囲 |
|-----|------|---------|
| **アーキタイプ使用率** | 各願相のデッキ使用比率 | 各10%〜25%（6願相均等に近い） |
| **アーキタイプ勝率** | 各願相デッキの勝率 | 45%〜55% |
| **トップデッキ占有率** | 最も使用率の高いデッキの割合 | 30%以下 |
| **デッキ多様性指数** | シャノンエントロピー（願相分布） | 1.5以上（最大≒1.79） |

### 5.3 カードシナジー分析

| イベント名 | パラメータ | トリガー |
|-----------|-----------|---------|
| `card_combo` | `card_id_1`, `card_id_2`, `turn`, `effect_type` | 同一ターン内に2枚以上プレイ |
| `card_synergy_win` | `card_ids`, `match_id`, `result` | 対戦終了時のデッキ内組み合わせ |

### 5.4 メタシフト検出

日次バッチ処理（BigQuery）で以下を集計：

| 分析項目 | 検出基準 | アクション |
|---------|---------|-----------|
| 急激な使用率変化 | 1願相の使用率が前日比±5%以上 | BALANCE_POLICY.md の監視リストに追加 |
| 勝率異常 | 1願相の勝率が55%超を3日連続 | 緊急バランスレビュー対象 |
| カード採用率スパイク | 1枚のカードの採用率が前日比+20%以上 | シナジーバグの可能性を調査 |

### 5.5 クライアントパフォーマンス追跡

| イベント名 | パラメータ | トリガー |
|-----------|-----------|---------|
| `perf_fps_drop` | `scene`, `min_fps`, `device_model` | FPSが30以下に低下 |
| `perf_load_time` | `screen_name`, `load_ms`, `device_model` | 画面ロード完了時 |
| `perf_memory_warning` | `used_mb`, `device_model` | メモリ警告発生時 |
| `perf_battery_session` | `drain_percent`, `session_min`, `device_model` | セッション終了時 |
| `perf_thermal_throttle` | `thermal_state`, `device_model` | サーマルスロットリング検知 |

---

## 6. 実装注意事項

- Firebase Analytics は自動でscreen_viewとsession_startを取得する
- カスタムイベントは上記リストに従い手動でログ
- ユーザープロパティとして `tutorial_completed`, `total_matches`, `favorite_aspect` を設定
- デバッグモードではFirebase DebugViewでリアルタイム確認
- §5 メタゲーム分析は BigQuery Export が有効な場合に日次バッチで集計
- `perf_*` イベントはサンプリング率10%で送信（全ユーザーからの送信は不要）
