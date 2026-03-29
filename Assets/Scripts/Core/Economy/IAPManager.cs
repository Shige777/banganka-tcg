using System;
using UnityEngine;
using Banganka.Core.Config;
using Banganka.Core.Data;
#if UNITY_PURCHASING
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

namespace Banganka.Core.Economy
{
    /// <summary>
    /// IAP管理 (MONETIZATION_DESIGN.md §2.3, APPSTORE_CHECKLIST.md §4)
    /// Unity IAP統合。Firebase未接続時はローカルフォールバック。
    /// </summary>
    public class IAPManager : MonoBehaviour
#if UNITY_PURCHASING
        , IDetailedStoreListener
#endif
    {
        public static IAPManager Instance { get; private set; }

        public bool IsInitialized { get; private set; }
        public event Action<string> OnPurchaseCompleted;
#pragma warning disable CS0067 // IAP統合時に使用予定
        public event Action<string, string> OnPurchaseFailed;
#pragma warning restore CS0067

#if UNITY_PURCHASING
        IStoreController _storeController;
        IExtensionProvider _extensions;
#endif

        Action<bool> _pendingCallback;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeIAP();
        }

        void InitializeIAP()
        {
#if UNITY_PURCHASING
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

            // 願晶パック (AppStoreConfig.Products)
            foreach (var product in AppStoreConfig.Products)
            {
                builder.AddProduct(product.productId, ProductType.Consumable);
            }

            // スターターバンドル (1回限定 = NonConsumable)
            builder.AddProduct(AppStoreConfig.StarterBundle.productId, ProductType.NonConsumable);

            UnityPurchasing.Initialize(this, builder);
            Debug.Log("[IAPManager] Unity Purchasing initialization started");
#else
            IsInitialized = true;
            Debug.Log("[IAPManager] Unity Purchasing not available — local mode");
#endif
        }

        // ====================================================================
        // Public API
        // ====================================================================

        /// <summary>
        /// IAP購入を開始する。結果はコールバックで通知。
        /// </summary>
        public void Purchase(string productId, Action<bool> callback = null)
        {
            _pendingCallback = callback;

#if UNITY_PURCHASING
            if (!IsInitialized || _storeController == null)
            {
                Debug.LogWarning("[IAPManager] Store not initialized");
                callback?.Invoke(false);
                OnPurchaseFailed?.Invoke(productId, "Store not initialized");
                return;
            }

            var product = _storeController.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
            {
                Debug.LogWarning($"[IAPManager] Product not available: {productId}");
                callback?.Invoke(false);
                OnPurchaseFailed?.Invoke(productId, "Product not available");
                return;
            }

            Debug.Log($"[IAPManager] Initiating purchase: {productId}");
            _storeController.InitiatePurchase(product);
#else
            // ローカルフォールバック: 即座に成功として処理
            Debug.Log($"[IAPManager] Local purchase (no IAP SDK): {productId}");
            GrantProduct(productId);
            callback?.Invoke(true);
            OnPurchaseCompleted?.Invoke(productId);
#endif
        }

        /// <summary>
        /// 商品のローカライズ価格文字列を返す (例: "¥160")
        /// </summary>
        public string GetLocalizedPrice(string productId)
        {
#if UNITY_PURCHASING
            if (_storeController == null) return "";
            var product = _storeController.products.WithID(productId);
            return product?.metadata.localizedPriceString ?? "";
#else
            var iapProduct = AppStoreConfig.FindProduct(productId);
            return iapProduct != null ? $"¥{iapProduct.priceTierJPY}" : "";
#endif
        }

        /// <summary>
        /// スターターバンドルが購入済みかどうか
        /// </summary>
        public bool IsStarterBundlePurchased()
        {
#if UNITY_PURCHASING
            if (_storeController == null) return false;
            var product = _storeController.products.WithID(AppStoreConfig.StarterBundle.productId);
            return product != null && product.hasReceipt;
#else
            return PlayerData.Instance.starterBundlePurchased;
#endif
        }

        // ====================================================================
        // Product Fulfillment
        // ====================================================================

        void GrantProduct(string productId)
        {
            var iapProduct = AppStoreConfig.FindProduct(productId);
            if (iapProduct == null)
            {
                Debug.LogWarning($"[IAPManager] Unknown product: {productId}");
                return;
            }

            // 願晶付与
            CurrencyManager.AddPremium(iapProduct.gemAmount);
            Debug.Log($"[IAPManager] Granted {iapProduct.gemAmount} premium for {productId}");

            // スターターバンドル追加報酬
            if (productId == AppStoreConfig.StarterBundle.productId)
            {
                // パック×5
                for (int i = 0; i < 5; i++)
                    PackSystem.OpenPack();

                // 購入済みフラグ (Firestoreが正典、ローカルはキャッシュ)
                PlayerData.Instance.starterBundlePurchased = true;
                PlayerData.Save();

                Debug.Log("[IAPManager] Starter bundle extras granted (5 packs)");
            }

            PlayerData.Save();
        }

        // ====================================================================
        // Unity IAP Callbacks
        // ====================================================================

#if UNITY_PURCHASING
        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _storeController = controller;
            _extensions = extensions;
            IsInitialized = true;
            Debug.Log("[IAPManager] Unity Purchasing initialized successfully");
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogError($"[IAPManager] Initialization failed: {error}");
            IsInitialized = false;
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"[IAPManager] Initialization failed: {error} - {message}");
            IsInitialized = false;
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            string productId = args.purchasedProduct.definition.id;
            string receipt = args.purchasedProduct.receipt;
            Debug.Log($"[IAPManager] Purchase received: {productId}, verifying receipt...");

            // Server-side receipt validation
            var client = Banganka.Core.Network.CloudFunctionClient.Instance;
            if (client != null)
            {
                client.VerifyReceipt(receipt, productId, (valid, status) =>
                {
                    if (valid)
                    {
                        Debug.Log($"[IAPManager] Receipt verified: {productId} ({status})");
                        GrantProduct(productId);
                        _pendingCallback?.Invoke(true);
                        OnPurchaseCompleted?.Invoke(productId);
                    }
                    else
                    {
                        Debug.LogWarning($"[IAPManager] Receipt rejected: {productId} ({status})");
                        _pendingCallback?.Invoke(false);
                        OnPurchaseFailed?.Invoke(productId, $"Receipt validation: {status}");
                    }
                    _pendingCallback = null;
                });
            }
            else
            {
                // CloudFunctionClient未初期化時はフォールバック
                Debug.LogWarning("[IAPManager] No CloudFunctionClient, granting directly");
                GrantProduct(productId);
                _pendingCallback?.Invoke(true);
                _pendingCallback = null;
                OnPurchaseCompleted?.Invoke(productId);
            }

            return PurchaseProcessingResult.Pending;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
        {
            string productId = product.definition.id;
            Debug.LogWarning($"[IAPManager] Purchase failed: {productId} - {reason}");

            _pendingCallback?.Invoke(false);
            _pendingCallback = null;
            OnPurchaseFailed?.Invoke(productId, reason.ToString());
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription description)
        {
            string productId = product.definition.id;
            Debug.LogWarning($"[IAPManager] Purchase failed: {productId} - {description.message}");

            _pendingCallback?.Invoke(false);
            _pendingCallback = null;
            OnPurchaseFailed?.Invoke(productId, description.message);
        }
#endif
    }
}
