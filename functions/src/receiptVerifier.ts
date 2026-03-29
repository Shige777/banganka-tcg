/**
 * Banganka — Server-side IAP Receipt Verification
 * MONETIZATION_DESIGN.md §2.3, SECURITY_SPEC.md
 *
 * Apple App Store レシート検証 (JWS / App Store Server API v2)
 * - JWSデコード + bundleId検証
 * - Firestoreで重複transactionId排除
 * - 願晶 (premium currency) の付与
 */

import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as crypto from "crypto";

// ====================================================================
// Types
// ====================================================================

interface VerifyReceiptRequest {
  receiptData: string;   // JWS signed transaction (from StoreKit 2)
  productId: string;     // e.g. "com.banganka.tcg.premium_60"
}

interface VerifyReceiptResponse {
  valid: boolean;
  status: "OK" | "DUPLICATE" | "INVALID";
}

interface JWSPayload {
  transactionId: string;
  originalTransactionId: string;
  bundleId: string;
  productId: string;
  purchaseDate: number;
  type: string;
  environment: string;
  [key: string]: unknown;
}

interface ReceiptRecord {
  transactionId: string;
  originalTransactionId: string;
  productId: string;
  uid: string;
  purchaseDate: number;
  environment: string;
  verifiedAt: number;
  grantedCurrency: number;
}

// ====================================================================
// Product → 願晶マッピング (MONETIZATION_DESIGN.md §2.3)
// ====================================================================

const PRODUCT_PREMIUM_MAP: Record<string, number> = {
  "com.banganka.gem60":    60,
  "com.banganka.gem300":   300,
  "com.banganka.gem650":   650,
  "com.banganka.gem1500":  1500,
  "com.banganka.gem4000":  4000,
};

const EXPECTED_BUNDLE_ID = "com.banganka.tcg";

// ====================================================================
// JWS Decoding (built-in crypto, no external deps)
// ====================================================================

/**
 * JWSトランザクションのペイロードをデコードする。
 * App Store Server API v2 の signed transaction は JWS (JSON Web Signature) 形式。
 * ヘッダーの x5c 証明書チェーンで署名検証を行う。
 */
function decodeJWSPayload(jws: string): JWSPayload | null {
  try {
    const parts = jws.split(".");
    if (parts.length !== 3) return null;

    // Decode header to extract x5c certificate chain
    const headerJson = Buffer.from(parts[0], "base64url").toString("utf-8");
    const header = JSON.parse(headerJson);

    // Decode payload
    const payloadJson = Buffer.from(parts[1], "base64url").toString("utf-8");
    const payload = JSON.parse(payloadJson) as JWSPayload;

    // Verify signature using x5c certificate chain
    if (header.x5c && Array.isArray(header.x5c) && header.x5c.length > 0) {
      const leafCertPem = convertDerToPem(header.x5c[0]);

      // Validate certificate is not expired
      if (!validateCertificateExpiry(leafCertPem)) {
        console.error("[receiptVerifier] Leaf certificate is expired or not yet valid");
        return null;
      }

      // Validate certificate chain — leaf must chain up to Apple Root CA
      if (!validateCertificateChain(header.x5c)) {
        console.error("[receiptVerifier] Certificate chain validation failed — not signed by Apple");
        return null;
      }

      const signatureValid = verifyJWSSignature(
        parts[0], parts[1], parts[2], leafCertPem, header.alg ?? "ES256",
      );
      if (!signatureValid) {
        console.warn("[receiptVerifier] JWS signature verification failed");
        return null;
      }
    } else {
      // Reject unsigned receipts in production
      // Non-production only if: emulator OR explicitly demo project
      const isNonProduction = process.env.FUNCTIONS_EMULATOR === "true" ||
        process.env.GCLOUD_PROJECT === "demo-banganka";
      if (!isNonProduction) {
        console.error("[receiptVerifier] No x5c chain — rejecting unsigned receipt in production");
        return null;
      }
      console.warn("[receiptVerifier] No x5c chain in JWS header — skipping signature verification (non-production)");
    }

    // Validate required payload fields have correct types
    if (typeof payload.transactionId !== "string" || !payload.transactionId) return null;
    if (typeof payload.bundleId !== "string" || !payload.bundleId) return null;
    if (typeof payload.productId !== "string" || !payload.productId) return null;
    if (payload.purchaseDate !== undefined && typeof payload.purchaseDate !== "number") return null;

    return payload;
  } catch (err) {
    console.error("[receiptVerifier] Failed to decode JWS:", err);
    return null;
  }
}

/**
 * DER (Base64) 証明書を PEM 形式に変換
 */
function convertDerToPem(derBase64: string): string {
  const lines: string[] = [];
  lines.push("-----BEGIN CERTIFICATE-----");
  // 64文字ごとに改行
  for (let i = 0; i < derBase64.length; i += 64) {
    lines.push(derBase64.substring(i, i + 64));
  }
  lines.push("-----END CERTIFICATE-----");
  return lines.join("\n");
}

/**
 * 証明書の有効期限を検証 (notBefore ≤ now ≤ notAfter)
 */
function validateCertificateExpiry(certPem: string): boolean {
  try {
    const cert = new crypto.X509Certificate(certPem);
    const now = new Date();
    const notBefore = new Date(cert.validFrom);
    const notAfter = new Date(cert.validTo);
    if (now < notBefore || now > notAfter) {
      console.warn(`[receiptVerifier] Certificate validity: ${cert.validFrom} — ${cert.validTo}, now: ${now.toISOString()}`);
      return false;
    }
    return true;
  } catch (err) {
    console.error("[receiptVerifier] Failed to parse certificate for expiry check:", err);
    return false;
  }
}

/**
 * x5c 証明書チェーンを検証:
 * - 各証明書が次の証明書で署名されていること
 * - ルート証明書が Apple Root CA の既知フィンガープリントと一致すること
 *
 * Apple Root CA-G3 (ECC) の SHA-256 fingerprint:
 * 63:34:3A:BF:B8:9A:6A:03:EB:B5:7E:9B:3F:5F:A7:BE:7C:4F:BE:89:53:76:A8:7E:2C:8E:7C:6A:72:E4:B4:0B
 */
const APPLE_ROOT_CA_G3_FINGERPRINT =
  "63:34:3A:BF:B8:9A:6A:03:EB:B5:7E:9B:3F:5F:A7:BE:7C:4F:BE:89:53:76:A8:7E:2C:8E:7C:6A:72:E4:B4:0B";

function validateCertificateChain(x5c: string[]): boolean {
  try {
    if (x5c.length < 2) {
      console.warn("[receiptVerifier] x5c chain too short — need at least leaf + intermediate");
      return false;
    }

    // Build X509Certificate objects
    const certs = x5c.map(der => new crypto.X509Certificate(convertDerToPem(der)));

    // Verify each cert is signed by the next one in the chain (structural + cryptographic)
    for (let i = 0; i < certs.length - 1; i++) {
      if (!certs[i].checkIssued(certs[i + 1])) {
        console.warn(`[receiptVerifier] Certificate ${i} not issued by certificate ${i + 1}`);
        return false;
      }
      // Cryptographic signature verification: cert[i] must be signed by cert[i+1]'s key
      const issuerPublicKey = certs[i + 1].publicKey;
      if (!certs[i].verify(issuerPublicKey)) {
        console.warn(`[receiptVerifier] Certificate ${i} signature not valid against issuer ${i + 1}`);
        return false;
      }
    }

    // Root certificate (last in chain) must match Apple's known fingerprint
    const rootCert = certs[certs.length - 1];
    const rootFingerprint = rootCert.fingerprint256;
    if (!rootFingerprint || rootFingerprint !== APPLE_ROOT_CA_G3_FINGERPRINT) {
      console.warn(`[receiptVerifier] Root CA fingerprint mismatch: ${rootFingerprint}`);
      return false;
    }

    return true;
  } catch (err) {
    console.error("[receiptVerifier] Certificate chain validation error:", err);
    return false;
  }
}

/**
 * JWS署名を検証 (ES256 / PS256)
 */
function verifyJWSSignature(
  headerB64: string, payloadB64: string, signatureB64url: string,
  certPem: string, algorithm: string,
): boolean {
  try {
    const signingInput = `${headerB64}.${payloadB64}`;
    const signature = Buffer.from(signatureB64url, "base64url");

    // Map JWS algorithm to Node.js crypto algorithm
    let nodeAlg: string;
    if (algorithm === "ES256") {
      nodeAlg = "SHA256";
    } else if (algorithm === "PS256") {
      nodeAlg = "SHA256";
    } else {
      console.warn(`[receiptVerifier] Unsupported algorithm: ${algorithm}`);
      return false;
    }

    const publicKey = crypto.createPublicKey(certPem);
    const verifier = crypto.createVerify(nodeAlg);
    verifier.update(signingInput);

    if (algorithm === "ES256") {
      return verifier.verify(
        { key: publicKey, dsaEncoding: "ieee-p1363" },
        signature,
      );
    } else {
      // PS256
      return verifier.verify(
        {
          key: publicKey,
          padding: crypto.constants.RSA_PKCS1_PSS_PADDING,
          saltLength: crypto.constants.RSA_PSS_SALTLEN_DIGEST,
        },
        signature,
      );
    }
  } catch (err) {
    console.error("[receiptVerifier] Signature verification error:", err);
    return false;
  }
}

// ====================================================================
// Cloud Function
// ====================================================================

export const verifyReceipt = onCall(async (request): Promise<VerifyReceiptResponse> => {
  const uid = request.auth?.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in");

  const { receiptData, productId } = request.data as VerifyReceiptRequest;
  if (!receiptData) throw new HttpsError("invalid-argument", "receiptData required");
  if (!productId) throw new HttpsError("invalid-argument", "productId required");

  // Size limit to prevent DoS via oversized JWS strings (typical receipt < 10KB)
  if (typeof receiptData !== "string" || receiptData.length > 50000) {
    throw new HttpsError("invalid-argument", "receiptData too large or invalid");
  }
  if (typeof productId !== "string" || productId.length > 200) {
    throw new HttpsError("invalid-argument", "productId too large or invalid");
  }

  // Validate product ID is known
  const grantAmount = PRODUCT_PREMIUM_MAP[productId];
  if (grantAmount === undefined) {
    throw new HttpsError("invalid-argument", `Unknown productId: ${productId}`);
  }

  // 1. Decode JWS receipt
  const payload = decodeJWSPayload(receiptData);
  if (!payload) {
    console.warn(`[verifyReceipt] Invalid receipt from ${uid} for ${productId}`);
    return { valid: false, status: "INVALID" };
  }

  // 2. Verify bundleId
  if (payload.bundleId !== EXPECTED_BUNDLE_ID) {
    console.warn(
      `[verifyReceipt] bundleId mismatch: expected ${EXPECTED_BUNDLE_ID}, got ${payload.bundleId}`
    );
    return { valid: false, status: "INVALID" };
  }

  // 3. Verify productId matches
  if (payload.productId !== productId) {
    console.warn(
      `[verifyReceipt] productId mismatch: expected ${productId}, got ${payload.productId}`
    );
    return { valid: false, status: "INVALID" };
  }

  const db = admin.firestore();
  const transactionId = payload.transactionId;

  if (!transactionId) {
    console.warn(`[verifyReceipt] Missing transactionId in receipt from ${uid}`);
    return { valid: false, status: "INVALID" };
  }

  // 4. Validate purchaseDate is not in the future (allow 5 min clock skew)
  if (payload.purchaseDate && payload.purchaseDate > Date.now() + 5 * 60 * 1000) {
    console.warn(`[verifyReceipt] Future purchaseDate from ${uid}: ${payload.purchaseDate}`);
    return { valid: false, status: "INVALID" };
  }

  // 4b. Reject Sandbox receipts in production
  const isNonProd = process.env.FUNCTIONS_EMULATOR === "true" ||
    process.env.GCLOUD_PROJECT === "demo-banganka";
  if (payload.environment === "Sandbox" && !isNonProd) {
    console.warn(`[verifyReceipt] Sandbox receipt rejected in production from ${uid}`);
    return { valid: false, status: "INVALID" };
  }

  // 5. Check for duplicate transactionId
  const receiptRef = db.collection("receipts").doc(transactionId);
  const existingReceipt = await receiptRef.get();

  if (existingReceipt.exists) {
    console.warn(`[verifyReceipt] Duplicate transactionId: ${transactionId} from ${uid}`);
    return { valid: false, status: "DUPLICATE" };
  }

  // 5. Store receipt and grant currency atomically
  const userRef = db.doc(`users/${uid}`);
  const receiptRecord: ReceiptRecord = {
    transactionId,
    originalTransactionId: payload.originalTransactionId ?? transactionId,
    productId,
    uid,
    purchaseDate: payload.purchaseDate ?? Date.now(),
    environment: payload.environment ?? "unknown",
    verifiedAt: Date.now(),
    grantedCurrency: grantAmount,
  };

  try {
    await db.runTransaction(async (tx) => {
      // Re-check duplicate inside transaction for race condition safety
      const receiptSnap = await tx.get(receiptRef);
      if (receiptSnap.exists) {
        throw new Error("DUPLICATE");
      }

      // Store receipt
      tx.set(receiptRef, receiptRecord);

      // Grant 願晶 (premium currency)
      tx.update(userRef, {
        "currency.premium": admin.firestore.FieldValue.increment(grantAmount),
      });
    });
  } catch (err) {
    if (err instanceof Error && err.message === "DUPLICATE") {
      console.warn(`[verifyReceipt] Duplicate in transaction: ${transactionId} from ${uid}`);
      return { valid: false, status: "DUPLICATE" };
    }
    // Distinguish transient Firestore errors from unexpected failures
    const errMsg = err instanceof Error ? err.message : String(err);
    console.error(`[verifyReceipt] Transaction failed for ${uid}: ${errMsg}`, err);
    if (errMsg.includes("ABORTED") || errMsg.includes("UNAVAILABLE") || errMsg.includes("DEADLINE_EXCEEDED")) {
      throw new HttpsError("unavailable", "Temporary error, please retry");
    }
    if (errMsg.includes("NOT_FOUND")) {
      throw new HttpsError("not-found", "User account not found");
    }
    throw new HttpsError("internal", "Failed to process receipt");
  }

  console.log(
    `[verifyReceipt] ${uid} verified ${productId} (tx: ${transactionId}), granted ${grantAmount} premium`
  );
  return { valid: true, status: "OK" };
});
