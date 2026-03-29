/**
 * Billing Guard 設定
 * PROJECT_ID は環境変数から取得（必須）
 */
const projectId = process.env.GCLOUD_PROJECT || process.env.GCP_PROJECT;
if (!projectId) {
  console.error("[billingConfig] GCLOUD_PROJECT or GCP_PROJECT env var is required");
}
export const PROJECT_ID = projectId ?? "MISSING_PROJECT_ID";
