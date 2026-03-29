/**
 * Billing Guard — 予算超過時にCloud Billingを自動無効化
 *
 * 仕組み:
 * 1. Cloud Billing → 予算アラート → Pub/Sub "billing-alerts" トピック
 * 2. この関数がPub/Subメッセージを受信
 * 3. costAmount >= budgetAmount の場合、プロジェクトのBillingを無効化
 *
 * 設定手順:
 * 1. Cloud Console → Billing → 予算とアラート → 予算を作成
 * 2. Pub/Subトピック "billing-alerts" に接続
 * 3. この関数がデプロイされていれば自動で動作
 *
 * 注意: Billing無効化後はCloud Functions含む有料サービスが停止します
 */

import { onMessagePublished } from "firebase-functions/v2/pubsub";
import { CloudBillingClient } from "@google-cloud/billing";
import { PROJECT_ID } from "./billingConfig";

const billing = new CloudBillingClient();

interface BudgetNotification {
  costAmount: number;
  budgetAmount: number;
  currencyCode: string;
  alertThresholdExceeded?: number;
  costIntervalStart?: string;
}

export const billingGuard = onMessagePublished(
  { topic: "billing-alerts", region: "asia-northeast1" },
  async (event) => {
    // Validate message structure before casting
    const rawData = event.data?.message?.json;
    if (!rawData || typeof rawData !== "object") {
      console.error("[billingGuard] Invalid Pub/Sub message structure — skipping");
      return;
    }
    const data = rawData as BudgetNotification;
    if (typeof data.costAmount !== "number" || typeof data.budgetAmount !== "number" || data.budgetAmount <= 0) {
      console.error("[billingGuard] Invalid budget notification data — skipping");
      return;
    }

    console.log(
      `Budget notification: ${data.costAmount} / ${data.budgetAmount} ${data.currencyCode ?? "USD"}`
    );

    if (data.costAmount >= data.budgetAmount) {
      console.warn(
        `BUDGET EXCEEDED: ${data.costAmount} >= ${data.budgetAmount}. Disabling billing...`
      );
      try {
        await disableBilling(PROJECT_ID);
      } catch (err) {
        console.error("[billingGuard] CRITICAL: Failed to disable billing:", err);
      }
    } else {
      const pct = ((data.costAmount / data.budgetAmount) * 100).toFixed(1);
      console.log(`Budget usage: ${pct}% — no action needed.`);
    }
  }
);

async function disableBilling(projectId: string): Promise<void> {
  const projectName = `projects/${projectId}`;

  // 現在のBilling情報を取得
  const [info] = await billing.getProjectBillingInfo({ name: projectName });

  if (!info) {
    console.error("[billingGuard] Failed to get billing info — response was null");
    return;
  }

  if (!info.billingEnabled) {
    console.log("Billing is already disabled.");
    return;
  }

  // Billingを無効化
  await billing.updateProjectBillingInfo({
    name: projectName,
    projectBillingInfo: { billingAccountName: "" },
  });

  console.log(`Billing disabled for ${projectId}.`);
}
