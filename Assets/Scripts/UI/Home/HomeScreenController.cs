using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Core.Config;
using Banganka.Core.Data;
using Banganka.Core.Economy;
using Banganka.Core.Network;
using Banganka.UI.Common;
using Banganka.UI.Replay;
using Banganka.UI.Spectator;
using Banganka.UI.PvE;

namespace Banganka.UI.Home
{
    /// <summary>
    /// ホーム画面 (SCREEN_SPEC.md)
    /// プレイヤー情報 / ミッション進捗 / ログインボーナス / バトルパス
    /// </summary>
    public class HomeScreenController : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI subtitleText;
        [SerializeField] TextMeshProUGUI worldText;

        [Header("Player Info")]
        [SerializeField] TextMeshProUGUI playerNameText;
        [SerializeField] TextMeshProUGUI rankText;
        [SerializeField] TextMeshProUGUI ratingText;
        [SerializeField] TextMeshProUGUI recordText;

        [Header("Currency")]
        [SerializeField] TextMeshProUGUI goldText;
        [SerializeField] TextMeshProUGUI premiumText;

        [Header("Mission Summary")]
        [SerializeField] TextMeshProUGUI missionText;
        [SerializeField] Image missionProgressFill;

        [Header("Battle Pass")]
        [SerializeField] TextMeshProUGUI battlePassText;
        [SerializeField] Image battlePassFill;

        [Header("Login Bonus")]
        [SerializeField] GameObject loginBonusPopup;
        [SerializeField] TextMeshProUGUI loginBonusText;

        [Header("Settings Panel")]
        [SerializeField] GameObject settingsPanel;
        [SerializeField] Button settingsButton;
        [SerializeField] Button settingsCloseButton;
        [SerializeField] TextMeshProUGUI accountTypeText;
        [SerializeField] Button appleSignInButton;
        [SerializeField] Button deleteAccountButton;
        [SerializeField] GameObject deleteConfirmDialog;
        [SerializeField] Button deleteConfirmYes;
        [SerializeField] Button deleteConfirmNo;
        [SerializeField] Button logoutButton;

        [Header("Free Pack")]
        [SerializeField] TextMeshProUGUI freePackTimerText;
        [SerializeField] Button freePackButton;
        [SerializeField] TextMeshProUGUI freePackStockText;

        [Header("Replay")]
        [SerializeField] Button replayButton;

        [Header("Spectator")]
        [SerializeField] Button spectatorButton;

        [Header("PvE")]
        [SerializeField] Button roguelikeButton;

        [Header("Navigation")]
        [SerializeField] ScreenManager screenManager;

        void OnEnable()
        {
            if (titleText) titleText.text = "万願果";
            if (subtitleText) subtitleText.text = "ばんがんか";
            if (worldText)
                worldText.text = "交界に集いし者たちよ、\nただひとつの奇跡を求めて争え。";

            RefreshPlayerInfo();
            RefreshCurrency();
            RefreshMissions();
            RefreshBattlePass();
            RefreshFreePack();
            CheckLoginBonus();

            // Settings panel defaults
            if (settingsPanel) settingsPanel.SetActive(false);
            if (deleteConfirmDialog) deleteConfirmDialog.SetActive(false);

            // Button listeners
            if (settingsButton) settingsButton.onClick.AddListener(OnSettingsButton);
            if (settingsCloseButton) settingsCloseButton.onClick.AddListener(OnCloseSettings);
            if (appleSignInButton) appleSignInButton.onClick.AddListener(OnAppleSignIn);
            if (deleteAccountButton) deleteAccountButton.onClick.AddListener(OnDeleteAccount);
            if (deleteConfirmYes) deleteConfirmYes.onClick.AddListener(OnConfirmDelete);
            if (deleteConfirmNo) deleteConfirmNo.onClick.AddListener(OnCancelDelete);
            if (logoutButton) logoutButton.onClick.AddListener(OnLogout);
            if (freePackButton) freePackButton.onClick.AddListener(OnFreePackButton);
            if (replayButton) replayButton.onClick.AddListener(OnReplayButton);
            if (spectatorButton) spectatorButton.onClick.AddListener(OnSpectatorButton);
            if (roguelikeButton) roguelikeButton.onClick.AddListener(OnRoguelikeButton);

            CurrencyManager.OnCurrencyChanged += RefreshCurrency;
            MissionSystem.OnMissionUpdated += RefreshMissions;
        }

        void OnDisable()
        {
            if (settingsButton) settingsButton.onClick.RemoveListener(OnSettingsButton);
            if (settingsCloseButton) settingsCloseButton.onClick.RemoveListener(OnCloseSettings);
            if (appleSignInButton) appleSignInButton.onClick.RemoveListener(OnAppleSignIn);
            if (deleteAccountButton) deleteAccountButton.onClick.RemoveListener(OnDeleteAccount);
            if (deleteConfirmYes) deleteConfirmYes.onClick.RemoveListener(OnConfirmDelete);
            if (deleteConfirmNo) deleteConfirmNo.onClick.RemoveListener(OnCancelDelete);
            if (logoutButton) logoutButton.onClick.RemoveListener(OnLogout);
            if (freePackButton) freePackButton.onClick.RemoveListener(OnFreePackButton);
            if (replayButton) replayButton.onClick.RemoveListener(OnReplayButton);
            if (spectatorButton) spectatorButton.onClick.RemoveListener(OnSpectatorButton);
            if (roguelikeButton) roguelikeButton.onClick.RemoveListener(OnRoguelikeButton);

            CurrencyManager.OnCurrencyChanged -= RefreshCurrency;
            MissionSystem.OnMissionUpdated -= RefreshMissions;
        }

        void RefreshPlayerInfo()
        {
            var pd = PlayerData.Instance;
            if (playerNameText) playerNameText.text = pd.displayName;
            if (rankText) rankText.text = pd.RankTitle;
            if (ratingText) ratingText.text = $"レート: {pd.rating}";
            if (recordText)
                recordText.text = $"{pd.wins}勝 {pd.losses}敗 {pd.draws}引分";
        }

        void RefreshCurrency()
        {
            if (goldText) goldText.text = $"{CurrencyManager.Gold:#,0}";
            if (premiumText) premiumText.text = $"{CurrencyManager.Premium:#,0}";
        }

        void RefreshMissions()
        {
            var missions = MissionSystem.ActiveMissions;
            if (missions.Count == 0)
            {
                MissionSystem.GenerateDailyMissions();
                MissionSystem.GenerateWeeklyMissions();
                missions = MissionSystem.ActiveMissions;
            }

            int completed = 0;
            int total = missions.Count;
            foreach (var m in missions)
                if (m.IsComplete) completed++;

            int unclaimed = MissionSystem.UnclaimedCount;

            if (missionText)
            {
                string badge = unclaimed > 0 ? $" ({unclaimed}件受取可)" : "";
                missionText.text = $"ミッション: {completed}/{total} 完了{badge}";
            }

            if (missionProgressFill)
                missionProgressFill.fillAmount = total > 0 ? (float)completed / total : 0;
        }

        void RefreshBattlePass()
        {
            int level = BattlePassSystem.Level;
            int xp = BattlePassSystem.Xp;
            bool premium = BattlePassSystem.IsPremium;

            if (battlePassText)
                battlePassText.text = $"願道パス Lv.{level}/{BattlePassSystem.MaxLevel}" +
                                      (premium ? " [Premium]" : "") +
                                      $"  XP: {xp}/{BattlePassSystem.XpPerLevel}";

            if (battlePassFill)
                battlePassFill.fillAmount = (float)xp / BattlePassSystem.XpPerLevel;
        }

        void CheckLoginBonus()
        {
            bool claimed = LoginBonusSystem.CheckAndClaim();
            if (claimed && loginBonusPopup)
            {
                loginBonusPopup.SetActive(true);
                int day = LoginBonusSystem.CurrentStreak % 7 + 1;
                int nextReward = LoginBonusSystem.NextReward;
                if (loginBonusText)
                    loginBonusText.text = $"ログインボーナス!\n" +
                                          $"連続{LoginBonusSystem.CurrentStreak}日目\n" +
                                          $"ゴールド獲得!\n" +
                                          $"次回報酬: {nextReward}ゴールド";
            }
        }

        public void OnDismissLoginBonus()
        {
            if (loginBonusPopup) loginBonusPopup.SetActive(false);
        }

        public void OnBattleButton()
        {
            if (screenManager) screenManager.ShowScreen(Game.GameManager.GameScreen.Battle);
        }

        // ============================================================
        // Free Pack Timer
        // ============================================================

        float _freePackRefreshTimer;

        void Update()
        {
            // 1秒ごとにタイマー表示を更新
            _freePackRefreshTimer -= Time.deltaTime;
            if (_freePackRefreshTimer <= 0f)
            {
                _freePackRefreshTimer = 1f;
                RefreshFreePack();
            }
        }

        void RefreshFreePack()
        {
            int stock = PackSystem.RefreshFreePackStock();

            if (freePackStockText)
                freePackStockText.text = $"無料パック: {stock}/{PackSystem.FreePackMaxStock}";

            if (freePackButton)
                freePackButton.interactable = stock > 0;

            if (freePackTimerText)
            {
                if (stock >= PackSystem.FreePackMaxStock)
                {
                    freePackTimerText.text = "受取可能!";
                }
                else
                {
                    TimeSpan remaining = PackSystem.GetTimeUntilNextFreePack();
                    if (remaining <= TimeSpan.Zero)
                        freePackTimerText.text = "受取可能!";
                    else
                        freePackTimerText.text = $"次のパック: {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                }
            }
        }

        void OnFreePackButton()
        {
            var result = PackSystem.ClaimFreePack();
            if (result == null) return;

            RefreshFreePack();
            RefreshCurrency();
            // OpenPack内でOnPackOpenedExイベントが発火済み → 演出はリスナー側で処理
            Debug.Log($"[HomeScreen] Free pack opened: {result.cards.Count} cards, +{result.totalGoldConverted}G converted");
        }

        // ============================================================
        // Replay
        // ============================================================

        ReplayListController _replayList;

        void OnReplayButton()
        {
            if (_replayList == null)
                _replayList = gameObject.AddComponent<ReplayListController>();

            if (_replayList.IsOpen)
                _replayList.Hide();
            else
                _replayList.Show();
        }

        // ============================================================
        // Spectator
        // ============================================================

        SpectatorListController _spectatorList;

        void OnSpectatorButton()
        {
            if (_spectatorList == null)
                _spectatorList = gameObject.AddComponent<SpectatorListController>();

            if (_spectatorList.IsOpen)
                _spectatorList.Hide();
            else
                _spectatorList.Show();
        }

        // ============================================================
        // PvE Roguelike
        // ============================================================

        RoguelikeScreenController _roguelikeScreen;

        void OnRoguelikeButton()
        {
            if (_roguelikeScreen == null)
                _roguelikeScreen = gameObject.AddComponent<RoguelikeScreenController>();

            if (_roguelikeScreen.IsOpen)
                _roguelikeScreen.Hide();
            else
                _roguelikeScreen.Show();
        }

        // ============================================================
        // Settings Panel
        // ============================================================

        public void OnSettingsButton()
        {
            if (settingsPanel) settingsPanel.SetActive(true);
            RefreshSettingsInfo();
        }

        public void OnCloseSettings()
        {
            if (settingsPanel) settingsPanel.SetActive(false);
            if (deleteConfirmDialog) deleteConfirmDialog.SetActive(false);
        }

        void RefreshSettingsInfo()
        {
            var auth = AuthService.Instance;
            if (playerNameText) playerNameText.text = auth != null && !string.IsNullOrEmpty(auth.DisplayName)
                ? auth.DisplayName
                : PlayerData.Instance.displayName;

            if (accountTypeText)
            {
                if (auth == null || !auth.IsAuthenticated)
                    accountTypeText.text = "アカウント: 未認証";
                else if (auth.IsAnonymous)
                    accountTypeText.text = "アカウント: 匿名";
                else
                    accountTypeText.text = $"アカウント: {auth.ProviderId ?? "Apple"}";
            }

            // Apple Sign-In button is only shown for anonymous users
            if (appleSignInButton)
                appleSignInButton.gameObject.SetActive(auth != null && auth.IsAnonymous);
        }

        // ============================================================
        // Account Actions
        // ============================================================

        public void OnAppleSignIn()
        {
            // Apple Sign-In requires native plugin (Sign in with Apple Unity Plugin).
            // This is a placeholder that logs the intent; actual implementation
            // will call AuthService.LinkWithApple() once the native plugin is integrated.
            Debug.Log("[HomeScreen] Apple Sign-In requested — native plugin required");
        }

        public void OnDeleteAccount()
        {
            if (deleteConfirmDialog) deleteConfirmDialog.SetActive(true);
        }

        void OnCancelDelete()
        {
            if (deleteConfirmDialog) deleteConfirmDialog.SetActive(false);
        }

        public void OnConfirmDelete()
        {
            if (deleteConfirmDialog) deleteConfirmDialog.SetActive(false);

            var auth = AuthService.Instance;
            if (auth == null)
            {
                Debug.LogWarning("[HomeScreen] AuthService not available");
                return;
            }

            // Disable button to prevent double-tap
            if (deleteAccountButton) deleteAccountButton.interactable = false;

            auth.DeleteAccount(success =>
            {
                if (deleteAccountButton) deleteAccountButton.interactable = true;

                if (success)
                {
                    Debug.Log("[HomeScreen] Account deleted successfully");
                    // Return to boot scene
                    UnityEngine.SceneManagement.SceneManager.LoadScene(0);
                }
                else
                {
                    Debug.LogError("[HomeScreen] Account deletion failed");
                }
            });
        }

        public void OnLogout()
        {
            var auth = AuthService.Instance;
            if (auth == null)
            {
                Debug.LogWarning("[HomeScreen] AuthService not available");
                return;
            }

            auth.SignOut();
            Debug.Log("[HomeScreen] Signed out");
            // Return to boot scene
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
}
