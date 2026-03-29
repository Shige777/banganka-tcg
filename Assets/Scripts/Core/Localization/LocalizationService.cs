using System;
using System.Collections.Generic;
using UnityEngine;

namespace Banganka.Core.Localization
{
    /// <summary>
    /// ローカライゼーション基盤 (LOCALIZATION_SPEC.md)
    /// MVP: 日本語のみ — Phase 2+: en, zh-TW, ko, zh-CN
    /// StringTable方式 — カテゴリ別キー管理
    /// </summary>
    public static class LocalizationService
    {
        // ====================================================================
        // Supported Locales
        // ====================================================================

        public enum Locale { Ja, En, ZhTW, Ko, ZhCN }

        static Locale _currentLocale = Locale.Ja;
        public static Locale CurrentLocale
        {
            get => _currentLocale;
            set
            {
                _currentLocale = value;
                LoadStringTable(value);
                OnLocaleChanged?.Invoke(value);
            }
        }

        public static event Action<Locale> OnLocaleChanged;

        // ====================================================================
        // String Tables (6 categories per LOCALIZATION_SPEC)
        // ====================================================================

        // Key format: {Category}_{Screen}_{Element}
        // e.g. UI_HOME_BTN_BATTLE, CARD_MAN_CON_01_NAME, STORY_CH01_LINE_001

        static readonly Dictionary<string, string> _strings = new();
        static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Detect system locale
            _currentLocale = DetectLocale();
            LoadStringTable(_currentLocale);
        }

        static Locale DetectLocale()
        {
            var sysLang = Application.systemLanguage;
            return sysLang switch
            {
                SystemLanguage.Japanese => Locale.Ja,
                SystemLanguage.English  => Locale.En,
                SystemLanguage.Korean   => Locale.Ko,
                SystemLanguage.ChineseTraditional => Locale.ZhTW,
                SystemLanguage.ChineseSimplified  => Locale.ZhCN,
                _ => Locale.Ja, // Default to Japanese
            };
        }

        // ====================================================================
        // Get Localized String
        // ====================================================================

        /// <summary>
        /// Get localized string by key. Returns key if not found.
        /// </summary>
        public static string Get(string key)
        {
            if (!_initialized) Initialize();
            return _strings.TryGetValue(key, out string val) ? val : key;
        }

        /// <summary>
        /// Get with format parameters: Get("UI_BATTLE_HP", hp, maxHp) → "HP 75/100"
        /// </summary>
        public static string GetFormat(string key, params object[] args)
        {
            string template = Get(key);
            try { return string.Format(template, args); }
            catch { return template; }
        }

        // ====================================================================
        // Load String Table (MVP: embedded Japanese)
        // ====================================================================

        static void LoadStringTable(Locale locale)
        {
            _strings.Clear();

            // Try load from Resources
            var asset = Resources.Load<TextAsset>($"Localization/{locale}");
            if (asset != null)
            {
                ParseStringTable(asset.text);
                return;
            }

            // Fallback: embedded Japanese strings
            if (locale == Locale.Ja || _strings.Count == 0)
                LoadEmbeddedJapanese();
        }

        static void ParseStringTable(string text)
        {
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                int eqIdx = trimmed.IndexOf('=');
                if (eqIdx <= 0) continue;

                string key = trimmed.Substring(0, eqIdx).Trim();
                string val = trimmed.Substring(eqIdx + 1).Trim();
                // Unescape \n
                val = val.Replace("\\n", "\n");
                _strings[key] = val;
            }
        }

        // ====================================================================
        // Embedded Japanese (MVP)
        // ====================================================================

        static void LoadEmbeddedJapanese()
        {
            // ---- UI: Home ----
            Set("UI_HOME_TITLE", "万願果");
            Set("UI_HOME_SUBTITLE", "ばんがんか");
            Set("UI_HOME_FLAVOR", "交界に集いし者たちよ、\nただひとつの奇跡を求めて争え。");
            Set("UI_HOME_BTN_BATTLE", "バトル");

            // ---- UI: Navigation ----
            Set("UI_NAV_HOME", "ホーム");
            Set("UI_NAV_BATTLE", "バトル");
            Set("UI_NAV_CARDS", "カード");
            Set("UI_NAV_STORY", "ストーリー");
            Set("UI_NAV_SHOP", "ショップ");

            // ---- UI: Battle ----
            Set("UI_BATTLE_HP", "HP {0}/{1}");
            Set("UI_BATTLE_TURN", "ターン {0}");
            Set("UI_BATTLE_YOUR_TURN", "あなたのターン");
            Set("UI_BATTLE_OPP_TURN", "相手のターン");
            Set("UI_BATTLE_END_TURN", "ターン終了");
            Set("UI_BATTLE_SURRENDER", "降参");
            Set("UI_BATTLE_VICTORY", "勝利！");
            Set("UI_BATTLE_DEFEAT", "敗北…");
            Set("UI_BATTLE_DRAW", "引き分け");
            Set("UI_BATTLE_WISH_TRIGGER", "願力発動 [{0}%] {1}");
            Set("UI_BATTLE_FINAL_STATE", "瀕死");
            Set("UI_BATTLE_KO_WIN", "鯱鉾勝利！");
            Set("UI_BATTLE_HP_WIN", "塗り勝利");
            Set("UI_BATTLE_SUMMON_SICK", "召喚酔い");
            Set("UI_BATTLE_EXHAUSTED", "消耗");
            Set("UI_BATTLE_READY", "待機");

            // ---- UI: Cards ----
            Set("UI_CARDS_TITLE", "カード");
            Set("UI_CARDS_COLLECTION", "コレクション: {0}/{1} ({2}%)");
            Set("UI_CARDS_OWNED", "所持: {0}枚");
            Set("UI_CARDS_CRAFT", "生成: {0}ゴールド");
            Set("UI_CARDS_DISMANTLE", "分解: +{0}ゴールド");

            // ---- UI: Shop ----
            Set("UI_SHOP_TITLE", "ショップ");
            Set("UI_SHOP_GOLD", "ゴールド: {0}");
            Set("UI_SHOP_PREMIUM", "願晶: {0}");
            Set("UI_SHOP_CONFIRM", "{0}を消費します");
            Set("UI_SHOP_SOLD_OUT", "売切");
            Set("UI_SHOP_TAB_PACKS", "パック");
            Set("UI_SHOP_TAB_CURRENCY", "通貨");
            Set("UI_SHOP_TAB_BUNDLE", "バンドル");
            Set("UI_SHOP_TAB_DAILY", "デイリー");

            // ---- UI: Story ----
            Set("UI_STORY_TITLE", "ストーリー");
            Set("UI_STORY_PROGRESS", "進行度: {0}/{1}章完了");
            Set("UI_STORY_LOCKED", "未解放");
            Set("UI_STORY_COMPLETED", "完了済み");
            Set("UI_STORY_START", "開始する");

            // ---- UI: Deck Builder ----
            Set("UI_DECK_NEW", "新しいデッキ");
            Set("UI_DECK_COUNT", "{0}/34枚");
            Set("UI_DECK_SAVE", "保存");
            Set("UI_DECK_IMPORT", "インポート");
            Set("UI_DECK_EXPORT", "エクスポート");

            // ---- UI: Mission ----
            Set("UI_MISSION_DAILY", "デイリーミッション");
            Set("UI_MISSION_WEEKLY", "ウィークリーミッション");
            Set("UI_MISSION_CLAIM", "受取");
            Set("UI_MISSION_COMPLETE", "完了");

            // ---- UI: Battle Pass ----
            Set("UI_BP_TITLE", "願道パス");
            Set("UI_BP_LEVEL", "Lv.{0}/{1}");
            Set("UI_BP_PREMIUM", "プレミアム");

            // ---- UI: Login Bonus ----
            Set("UI_LOGIN_TITLE", "ログインボーナス！");
            Set("UI_LOGIN_STREAK", "連続{0}日目");
            Set("UI_LOGIN_REWARD", "ゴールド獲得！");

            // ---- UI: Settings ----
            Set("UI_SETTINGS_TITLE", "設定");
            Set("UI_SETTINGS_SOUND", "サウンド");
            Set("UI_SETTINGS_BGM", "BGM音量");
            Set("UI_SETTINGS_SE", "SE音量");
            Set("UI_SETTINGS_NOTIFICATIONS", "通知");
            Set("UI_SETTINGS_ACCESSIBILITY", "アクセシビリティ");
            Set("UI_SETTINGS_ACCOUNT", "アカウント");
            Set("UI_SETTINGS_DELETE_ACCOUNT", "アカウント削除");

            // ---- System ----
            Set("SYS_NETWORK_ERROR", "通信エラーが発生しました。接続を確認してください。");
            Set("SYS_AUTH_ERROR", "認証に失敗しました。再ログインしてください。");
            Set("SYS_RETRY", "リトライ");
            Set("SYS_CANCEL", "キャンセル");
            Set("SYS_CONFIRM", "確認");
            Set("SYS_OK", "OK");
            Set("SYS_YES", "はい");
            Set("SYS_NO", "いいえ");

            // ---- Card Types ----
            Set("CARD_TYPE_MANIFEST", "顕現");
            Set("CARD_TYPE_SPELL", "詠術");
            Set("CARD_TYPE_ALGORITHM", "界律");

            // ---- Aspects ----
            Set("ASPECT_CONTEST", "曙赤");
            Set("ASPECT_WHISPER", "空青");
            Set("ASPECT_WEAVE", "穏緑");
            Set("ASPECT_VERSE", "妖紫");
            Set("ASPECT_MANIFEST", "遊黄");
            Set("ASPECT_HUSH", "玄白");

            // ---- Ranks ----
            Set("RANK_NEWCOMER", "新参の果求者");
            Set("RANK_BEGINNER", "見習い果求者");
            Set("RANK_SEEKER", "果求者");
            Set("RANK_VETERAN", "熟達の果求者");
            Set("RANK_MASTER", "交界の覇者");
        }

        static void Set(string key, string value) => _strings[key] = value;

        // ====================================================================
        // Font Assets
        // ====================================================================

        public static string GetFontFamily(Locale locale) => locale switch
        {
            Locale.Ja   => "Noto Sans JP",
            Locale.En   => "Roboto",
            Locale.Ko   => "Noto Sans KR",
            Locale.ZhTW => "Noto Sans TC",
            Locale.ZhCN => "Noto Sans SC",
            _ => "Noto Sans JP",
        };

        public const string NumberFont = "Roboto Mono"; // Cost/Power display
    }
}
