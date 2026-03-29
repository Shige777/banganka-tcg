using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Banganka.Core.Config;
using Banganka.Core.Data;
using Banganka.Core.Economy;
using Banganka.Core.Game;
using Banganka.Core.Network;
using Banganka.UI.Common;
using Banganka.UI.Battle;
using Banganka.UI.Shop;
using Banganka.UI.Cards;
using Banganka.UI.Effects;
using Banganka.UI.Tween;
using DG.Tweening;

namespace Banganka.Game
{
    public static class AutoBootstrap
    {
        // UI_STYLE_GUIDE colors
        static readonly Color ColBgDeep      = Hex("0A0A14");
        static readonly Color ColBgPanel     = new(0.06f, 0.06f, 0.10f, 0.92f);
        static readonly Color ColBgCard      = new(0.08f, 0.08f, 0.13f, 0.95f);
        static readonly Color ColAccentGold  = Hex("C9A84C");
        static readonly Color ColAccentBlue  = Hex("4D66E6");
        static readonly Color ColBtnPrimary  = Hex("4D66E6");
        static readonly Color ColBtnDanger   = Hex("B33A3A");
        static readonly Color ColBtnSecondary = Hex("2A2A3D");
        static readonly Color ColTextMuted   = new(0.55f, 0.55f, 0.62f);
        static readonly Color ColTextSub     = new(0.75f, 0.75f, 0.82f);
        static readonly Color ColNavBg       = new(0.05f, 0.05f, 0.09f, 0.97f);
        static readonly Color ColNavActive   = Hex("3D3566");
        static readonly Color ColNavInactive = Hex("1A1A2A");

        // Japanese font asset (loaded from Resources)
        static TMP_FontAsset _jpFont;
        public static TMP_FontAsset JapaneseFont => _jpFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            // Initialize DOTween
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly)
                .SetCapacity(200, 50);

            // Load Japanese font
            _jpFont = Resources.Load<TMP_FontAsset>("Fonts/JapaneseFont SDF");
            if (_jpFont == null)
                _jpFont = Resources.Load<TMP_FontAsset>("Fonts/HiraginoSDF");
            if (_jpFont != null)
                Debug.Log($"[Banganka] Japanese font loaded: {_jpFont.name}");
            else
                Debug.LogWarning("[Banganka] Japanese font not found in Resources/Fonts/");

            // Load accessibility settings from PlayerPrefs
            AccessibilitySettings.Load();

            var gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
            gmObj.AddComponent<RoomManager>();
            Object.DontDestroyOnLoad(gmObj);
        }

        static void ApplyFont(TextMeshProUGUI tmp)
        {
            if (_jpFont != null) tmp.font = _jpFont;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BuildUI()
        {
            if (Object.FindFirstObjectByType<Canvas>()?.name == "MainCanvas")
                return;

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            var canvasObj = new GameObject("MainCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            var root = canvasObj.transform;

            var home    = MakeScreen(root, "HomeScreen",    Hex("0D0D1A"));
            var battle  = MakeScreen(root, "BattleScreen",  Hex("100D18"));
            var cards   = MakeScreen(root, "CardsScreen",   Hex("0D1419"));
            var story   = MakeScreen(root, "StoryScreen",   Hex("120D14"));
            var shop    = MakeScreen(root, "ShopScreen",    Hex("14120D"));
            var match   = MakeScreen(root, "MatchScreen",   Hex("050510"), fullHeight: true);

            var nav = MakeNavBar(root);

            var sm = canvasObj.AddComponent<ScreenManager>();

            // Screen entrance animations
            home.AddComponent<ScreenEntranceAnimator>().entranceType = ScreenEntranceAnimator.EntranceType.FadeIn;
            battle.AddComponent<ScreenEntranceAnimator>().entranceType = ScreenEntranceAnimator.EntranceType.SlideUp;
            cards.AddComponent<ScreenEntranceAnimator>().entranceType = ScreenEntranceAnimator.EntranceType.SlideUp;
            story.AddComponent<ScreenEntranceAnimator>().entranceType = ScreenEntranceAnimator.EntranceType.SlideRight;
            shop.AddComponent<ScreenEntranceAnimator>().entranceType = ScreenEntranceAnimator.EntranceType.SlideUp;

            BuildHome(home, sm);
            BuildBattle(battle, sm);
            BuildCards(cards);
            BuildStory(story);
            BuildShop(shop);
            BuildMatch(match, sm);

            // --- Background particles on each screen ---
            AddParticles(home, new Color(0.6f, 0.5f, 1f, 0.06f), 15);
            AddParticles(battle, new Color(1f, 0.3f, 0.3f, 0.05f), 12);
            AddParticles(cards, new Color(0.3f, 0.7f, 1f, 0.05f), 10);
            AddParticles(story, new Color(0.8f, 0.5f, 1f, 0.06f), 18);
            AddParticles(shop, new Color(1f, 0.8f, 0.3f, 0.05f), 12);

            sm.Init(home, battle, cards, story, shop, match, nav);
        }

        // ==================== HOME ====================
        static void BuildHome(GameObject screen, ScreenManager sm)
        {
            var player = PlayerData.Instance;

            // Background gradient overlay
            var bgOverlay = new GameObject("BgOverlay");
            bgOverlay.transform.SetParent(screen.transform, false);
            Stretch(bgOverlay, 0, 0.5f, 1, 1);
            var bgImg = bgOverlay.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.03f, 0.08f, 0.6f);
            bgImg.raycastTarget = false;

            // Title area with glow
            var titleGlow = new GameObject("TitleGlow");
            titleGlow.transform.SetParent(screen.transform, false);
            Stretch(titleGlow, 0.1f, 0.83f, 0.9f, 0.96f);
            var tgImg = titleGlow.AddComponent<Image>();
            tgImg.color = new Color(0.6f, 0.5f, 0.3f, 0.08f);
            tgImg.raycastTarget = false;
            AddPulseGlow(titleGlow, new Color(0.6f, 0.5f, 0.3f, 0.08f), new Color(0.6f, 0.5f, 0.3f, 0.02f), 0.8f);

            var titleTmp = Txt(screen, "万願果", 76, 0.05f, 0.82f, 0.95f, 0.95f, bold: true,
                color: new Color(0.95f, 0.88f, 0.6f));
            titleTmp.outlineWidth = 0.15f;
            titleTmp.outlineColor = new Color32(60, 40, 10, 120);
            var titleFloatObj = titleTmp.gameObject;
            var titleFloat = titleFloatObj.AddComponent<FloatEffect>();
            titleFloat.amplitude = 3f;
            titleFloat.frequency = 0.6f;

            Txt(screen, "ばんがんか", 24, 0.2f, 0.77f, 0.8f, 0.82f, color: ColTextMuted);

            // World text
            Txt(screen, "交界に集いし者たちよ、\nただひとつの奇跡を求めて争え。", 20,
                0.08f, 0.62f, 0.92f, 0.75f, color: ColTextSub);

            // Player info panel
            var playerPanel = new GameObject("PlayerPanel");
            playerPanel.transform.SetParent(screen.transform, false);
            Stretch(playerPanel, 0.05f, 0.52f, 0.95f, 0.61f);
            var ppBg = playerPanel.AddComponent<Image>();
            ppBg.color = ColBgPanel;

            var pNameObj = new GameObject("PName");
            pNameObj.transform.SetParent(playerPanel.transform, false);
            Stretch(pNameObj, 0.03f, 0.1f, 0.45f, 0.9f);
            var pNameTmp = pNameObj.AddComponent<TextMeshProUGUI>();
            pNameTmp.text = $"{player.displayName}  R.{player.rating}";
            pNameTmp.fontSize = 20;
            pNameTmp.fontStyle = FontStyles.Bold;
            pNameTmp.color = Color.white;
            pNameTmp.alignment = TextAlignmentOptions.MidlineLeft;
            ApplyFont(pNameTmp);

            var pRankObj = new GameObject("PRank");
            pRankObj.transform.SetParent(playerPanel.transform, false);
            Stretch(pRankObj, 0.45f, 0.1f, 0.7f, 0.9f);
            var pRankTmp = pRankObj.AddComponent<TextMeshProUGUI>();
            pRankTmp.text = player.RankTitle;
            pRankTmp.fontSize = 16;
            pRankTmp.color = ColAccentGold;
            pRankTmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(pRankTmp);

            var pCoinObj = new GameObject("PCoin");
            pCoinObj.transform.SetParent(playerPanel.transform, false);
            Stretch(pCoinObj, 0.7f, 0.1f, 0.97f, 0.9f);
            var pCoinTmp = pCoinObj.AddComponent<TextMeshProUGUI>();
            pCoinTmp.text = $"{player.gold:N0} コイン";
            pCoinTmp.fontSize = 16;
            pCoinTmp.color = new Color(0.95f, 0.85f, 0.4f);
            pCoinTmp.alignment = TextAlignmentOptions.MidlineRight;
            ApplyFont(pCoinTmp);

            // Main CTA with glow ring
            var ctaGlow = new GameObject("CTAGlow");
            ctaGlow.transform.SetParent(screen.transform, false);
            Stretch(ctaGlow, 0.10f, 0.37f, 0.90f, 0.50f);
            var ctaGlowImg = ctaGlow.AddComponent<Image>();
            ctaGlowImg.color = new Color(0.3f, 0.4f, 0.9f, 0.15f);
            ctaGlowImg.raycastTarget = false;
            AddPulseGlow(ctaGlow, new Color(0.3f, 0.4f, 0.9f, 0.15f), new Color(0.3f, 0.4f, 0.9f, 0.04f), 1.2f);

            var btn = MakeButton(screen, "BattleButton", "バトルへ",
                0.12f, 0.38f, 0.88f, 0.49f, ColBtnPrimary);
            btn.GetComponent<Button>().onClick.AddListener(() => sm.ShowScreen(GameManager.GameScreen.Battle));

            // Banner area
            var bannerPanel = new GameObject("BannerPanel");
            bannerPanel.transform.SetParent(screen.transform, false);
            Stretch(bannerPanel, 0.05f, 0.26f, 0.95f, 0.36f);
            var bnBg = bannerPanel.AddComponent<Image>();
            bnBg.color = new Color(0.12f, 0.08f, 0.20f, 0.9f);

            var bnAccent = new GameObject("BannerAccent");
            bnAccent.transform.SetParent(bannerPanel.transform, false);
            Stretch(bnAccent, 0, 0, 0.01f, 1);
            var bnAccImg = bnAccent.AddComponent<Image>();
            bnAccImg.color = ColAccentGold;

            // Show Nal unlock message or default banner
            var nalMsg = MechanicUnlockManager.GetNalMessage();
            if (nalMsg != null && !MechanicUnlockManager.AllUnlocked)
            {
                Txt(bannerPanel, "Nal", 14, 0.02f, 0.65f, 0.15f, 0.95f, bold: true,
                    color: new Color(0.5f, 0.8f, 1f), align: TextAlignmentOptions.MidlineLeft);
                Txt(bannerPanel, nalMsg, 18,
                    0.02f, 0.1f, 0.98f, 0.65f, align: TextAlignmentOptions.MidlineLeft, color: ColTextSub);
            }
            else
            {
                Txt(bannerPanel, "注目", 14, 0.02f, 0.65f, 0.15f, 0.95f, bold: true,
                    color: ColAccentGold, align: TextAlignmentOptions.MidlineLeft);
                Txt(bannerPanel, "新章解放! ストーリー第一章「交界の目覚め」プレイ可能", 18,
                    0.02f, 0.1f, 0.98f, 0.65f, align: TextAlignmentOptions.MidlineLeft, color: ColTextSub);
            }

            // Sub CTAs
            var cardsBtn = MakeButton(screen, "HomeCardsBtn", "カード一覧",
                0.05f, 0.14f, 0.33f, 0.24f, ColBtnSecondary);
            cardsBtn.GetComponent<Button>().onClick.AddListener(() => sm.ShowScreen(GameManager.GameScreen.Cards));

            var storyBtn = MakeButton(screen, "HomeStoryBtn", "ストーリー",
                0.36f, 0.14f, 0.64f, 0.24f, ColBtnSecondary);
            storyBtn.GetComponent<Button>().onClick.AddListener(() => sm.ShowScreen(GameManager.GameScreen.Story));

            var shopBtn = MakeButton(screen, "HomeShopBtn", "ショップ",
                0.67f, 0.14f, 0.95f, 0.24f, ColBtnSecondary);
            shopBtn.GetComponent<Button>().onClick.AddListener(() => sm.ShowScreen(GameManager.GameScreen.Shop));
        }

        // ==================== BATTLE LOBBY ====================
        static void BuildBattle(GameObject screen, ScreenManager sm)
        {
            Txt(screen, "バトル", 48, 0f, 0.88f, 1f, 0.97f, bold: true,
                color: new Color(0.95f, 0.9f, 0.7f));

            // --- AI Match section ---
            var aiPanel = new GameObject("AIPanel");
            aiPanel.transform.SetParent(screen.transform, false);
            Stretch(aiPanel, 0.05f, 0.62f, 0.95f, 0.85f);
            var aiBg = aiPanel.AddComponent<Image>();
            aiBg.color = ColBgPanel;

            bool isNewPlayer = (PlayerData.Instance?.totalGames ?? 0) < 5;
            Txt(aiPanel, isNewPlayer ? "練習マッチ（おすすめ）" : "AI対戦", 26, 0.03f, 0.7f, 0.97f, 0.95f,
                bold: true, align: TextAlignmentOptions.MidlineLeft,
                color: isNewPlayer ? new Color(0.5f, 0.8f, 1f) : Color.white);
            Txt(aiPanel, isNewPlayer
                ? "まずはAIと練習しよう！\nメカニクスを段階的に習得できます"
                : "プリセットデッキでAIと対戦します\n（P1: 手動操作 / P2: AI）", 17,
                0.03f, 0.3f, 0.97f, 0.7f, align: TextAlignmentOptions.MidlineLeft, color: ColTextSub);

            var aiBtn = MakeButton(aiPanel, "StartMatchButton", "通常対戦",
                0.03f, 0.03f, 0.48f, 0.28f, ColBtnDanger);
            aiBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                MatchModeConfig.CurrentMode = MatchMode.Standard;
                GameManager.Instance.StartNewMatch();
                sm.ShowScreen(GameManager.GameScreen.Match);
            });

            var quickBtn = MakeButton(aiPanel, "QuickMatchButton", "速戦 (Quick)",
                0.52f, 0.03f, 0.97f, 0.28f, new Color(0.2f, 0.5f, 0.8f));
            quickBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                MatchModeConfig.CurrentMode = MatchMode.Quick;
                GameManager.Instance.StartNewMatch();
                sm.ShowScreen(GameManager.GameScreen.Match);
            });

            // --- Room Match section ---
            var roomPanel = new GameObject("RoomPanel");
            roomPanel.transform.SetParent(screen.transform, false);
            Stretch(roomPanel, 0.05f, 0.12f, 0.95f, 0.58f);
            var rpBg = roomPanel.AddComponent<Image>();
            rpBg.color = ColBgPanel;

            Txt(roomPanel, "ルームマッチ", 26, 0.03f, 0.85f, 0.97f, 0.98f,
                bold: true, align: TextAlignmentOptions.MidlineLeft);

            // Status text
            var statusT = TxtObj(roomPanel, "RoomStatus", "ルームに参加して対戦しましょう", 16,
                0.03f, 0.72f, 0.97f, 0.85f, TextAlignmentOptions.MidlineLeft);
            statusT.color = ColTextSub;

            // Room ID display
            var roomIdPanel = new GameObject("RoomIdPanel");
            roomIdPanel.transform.SetParent(roomPanel.transform, false);
            Stretch(roomIdPanel, 0.03f, 0.55f, 0.97f, 0.72f);
            var ridBg = roomIdPanel.AddComponent<Image>();
            ridBg.color = new Color(0.04f, 0.04f, 0.08f, 0.9f);
            var roomIdT = TxtObj(roomIdPanel, "RoomIdText", "ルームID: ---", 22,
                0.05f, 0f, 0.95f, 1f);
            roomIdT.fontStyle = FontStyles.Bold;

            // Room ID input
            var inputObj = new GameObject("RoomInput");
            inputObj.transform.SetParent(roomPanel.transform, false);
            Stretch(inputObj, 0.03f, 0.38f, 0.65f, 0.53f);
            var inputBg = inputObj.AddComponent<Image>();
            inputBg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

            var inputArea = new GameObject("Text Area");
            inputArea.transform.SetParent(inputObj.transform, false);
            var iaRt = inputArea.AddComponent<RectTransform>();
            iaRt.anchorMin = Vector2.zero; iaRt.anchorMax = Vector2.one;
            iaRt.offsetMin = new Vector2(10, 2); iaRt.offsetMax = new Vector2(-10, -2);
            inputArea.AddComponent<RectMask2D>();

            var phObj = new GameObject("Placeholder");
            phObj.transform.SetParent(inputArea.transform, false);
            var phRt = phObj.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero; phRt.offsetMax = Vector2.zero;
            var phTmp = phObj.AddComponent<TextMeshProUGUI>();
            phTmp.text = "ルームIDを入力...";
            phTmp.fontSize = 18; phTmp.fontStyle = FontStyles.Italic;
            phTmp.color = new Color(0.4f, 0.4f, 0.5f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            ApplyFont(phTmp);

            var inputTextObj = new GameObject("Text");
            inputTextObj.transform.SetParent(inputArea.transform, false);
            var itRt = inputTextObj.AddComponent<RectTransform>();
            itRt.anchorMin = Vector2.zero; itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero; itRt.offsetMax = Vector2.zero;
            var itTmp = inputTextObj.AddComponent<TextMeshProUGUI>();
            itTmp.fontSize = 18; itTmp.color = Color.white;
            itTmp.alignment = TextAlignmentOptions.MidlineLeft;
            ApplyFont(itTmp);

            var inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.textViewport = iaRt;
            inputField.textComponent = itTmp;
            inputField.placeholder = phTmp;
            inputField.pointSize = 18;
            inputField.characterLimit = 6;

            // Join button
            var joinBtn = MakeButton(roomPanel, "JoinRoomBtn", "参加",
                0.68f, 0.38f, 0.97f, 0.53f, Hex("3A6633"));
            joinBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                var rm = RoomManager.Instance;
                if (rm != null) rm.JoinRoom(inputField.text);
            });

            // Create room button
            var createBtn = MakeButton(roomPanel, "CreateRoomBtn", "ルーム作成",
                0.03f, 0.2f, 0.47f, 0.35f, ColBtnPrimary);
            createBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                var rm = RoomManager.Instance;
                if (rm != null)
                {
                    rm.CreateRoom();
                    roomIdT.text = $"ルームID: {rm.RoomId}";
                    statusT.text = "相手の参加を待っています...";
                }
            });

            // Start match button (enabled when connected)
            var startBtn = MakeButton(roomPanel, "RoomStartBtn", "対戦開始",
                0.53f, 0.2f, 0.97f, 0.35f, Hex("555555"));
            var startBtnComp = startBtn.GetComponent<Button>();
            startBtnComp.interactable = false;

            // Leave button
            var leaveBtn = MakeButton(roomPanel, "LeaveRoomBtn", "退出",
                0.03f, 0.03f, 0.47f, 0.17f, Hex("4D3333"));
            leaveBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                var rm = RoomManager.Instance;
                if (rm != null)
                {
                    rm.LeaveRoom();
                    roomIdT.text = "ルームID: ---";
                    statusT.text = "ルームに参加して対戦しましょう";
                    startBtnComp.interactable = false;
                }
            });

            // Simulate opponent join (dev only)
            var simBtn = MakeButton(roomPanel, "SimJoinBtn", "接続テスト",
                0.53f, 0.03f, 0.97f, 0.17f, Hex("333355"));
            simBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                var rm = RoomManager.Instance;
                if (rm != null)
                {
                    rm.SimulateOpponentJoin();
                    if (rm.State == RoomState.Connected)
                    {
                        statusT.text = "対戦相手が接続しました!";
                        statusT.color = new Color(0.5f, 1f, 0.5f);
                        startBtnComp.interactable = true;
                        var img = startBtn.GetComponent<Image>();
                        if (img) img.color = ColBtnDanger;
                    }
                }
            });

            startBtnComp.onClick.AddListener(() =>
            {
                GameManager.Instance.StartNewMatch();
                sm.ShowScreen(GameManager.GameScreen.Match);
            });
        }

        // ==================== CARDS ====================
        static void BuildCards(GameObject screen)
        {
            Txt(screen, "カード", 48, 0f, 0.93f, 1f, 1f, bold: true,
                color: new Color(0.95f, 0.9f, 0.7f));

            var clc = screen.AddComponent<CardListController>();

            // Search field
            var searchObj = new GameObject("SearchField");
            searchObj.transform.SetParent(screen.transform, false);
            Stretch(searchObj, 0.02f, 0.88f, 0.98f, 0.93f);
            var searchBg = searchObj.AddComponent<Image>();
            searchBg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(searchObj.transform, false);
            var taRt = textArea.AddComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero; taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(10, 2); taRt.offsetMax = new Vector2(-10, -2);
            textArea.AddComponent<RectMask2D>();

            var placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(textArea.transform, false);
            var phRt = placeholder.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero; phRt.offsetMax = Vector2.zero;
            var phTmp = placeholder.AddComponent<TextMeshProUGUI>();
            phTmp.text = "カード名で検索...";
            phTmp.fontSize = 18; phTmp.fontStyle = FontStyles.Italic;
            phTmp.color = new Color(0.4f, 0.4f, 0.5f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            ApplyFont(phTmp);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(textArea.transform, false);
            var txtRt = textObj.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
            var txtTmp = textObj.AddComponent<TextMeshProUGUI>();
            txtTmp.fontSize = 18; txtTmp.color = Color.white;
            txtTmp.alignment = TextAlignmentOptions.MidlineLeft;
            ApplyFont(txtTmp);

            var inputField = searchObj.AddComponent<TMP_InputField>();
            inputField.textViewport = taRt;
            inputField.textComponent = txtTmp;
            inputField.placeholder = phTmp;
            inputField.pointSize = 18;
            inputField.onValueChanged.AddListener(val => clc.SetSearchQuery(val));

            // Type filter bar
            var typeBar = new GameObject("TypeFilterBar");
            typeBar.transform.SetParent(screen.transform, false);
            Stretch(typeBar, 0.02f, 0.83f, 0.98f, 0.88f);
            var tbLayout = typeBar.AddComponent<HorizontalLayoutGroup>();
            tbLayout.spacing = 4;
            tbLayout.childForceExpandWidth = true;
            tbLayout.childForceExpandHeight = true;

            MakeFilterButton(typeBar, "全て", ColBtnSecondary, () => clc.SetTypeFilter(null));
            MakeFilterButton(typeBar, "顕現", Hex("2A3340"), () => clc.SetTypeFilter(CardType.Manifest));
            MakeFilterButton(typeBar, "詠術", Hex("332A40"), () => clc.SetTypeFilter(CardType.Spell));
            MakeFilterButton(typeBar, "界律", Hex("40332A"), () => clc.SetTypeFilter(CardType.Algorithm));

            // Aspect filter + Sort bar
            var aspBar = new GameObject("AspectFilterBar");
            aspBar.transform.SetParent(screen.transform, false);
            Stretch(aspBar, 0.02f, 0.78f, 0.98f, 0.83f);
            var abLayout = aspBar.AddComponent<HorizontalLayoutGroup>();
            abLayout.spacing = 3;
            abLayout.childForceExpandWidth = true;
            abLayout.childForceExpandHeight = true;

            MakeFilterButton(aspBar, "全", ColBtnSecondary, () => clc.SetAspectFilter(null));
            var aspects = new[] { Aspect.Contest, Aspect.Whisper, Aspect.Weave, Aspect.Manifest, Aspect.Verse, Aspect.Hush };
            foreach (var asp in aspects)
            {
                var a = asp;
                Color ac = AspectColors.GetColor(asp);
                var dimmed = new Color(ac.r * 0.5f, ac.g * 0.5f, ac.b * 0.5f, 0.9f);
                MakeFilterButton(aspBar, AspectColors.GetDisplayName(asp), dimmed, () => clc.SetAspectFilter(a));
            }

            // Sort bar
            var sortBar = new GameObject("SortBar");
            sortBar.transform.SetParent(screen.transform, false);
            Stretch(sortBar, 0.02f, 0.74f, 0.98f, 0.78f);
            var sbLayout = sortBar.AddComponent<HorizontalLayoutGroup>();
            sbLayout.spacing = 4;
            sbLayout.childForceExpandWidth = true;
            sbLayout.childForceExpandHeight = true;

            MakeFilterButton(sortBar, "標準", Hex("22222E"), () => clc.SetSort(CardListController.SortMode.Default));
            MakeFilterButton(sortBar, "コスト↑", Hex("22222E"), () => clc.SetSort(CardListController.SortMode.CostAsc));
            MakeFilterButton(sortBar, "コスト↓", Hex("22222E"), () => clc.SetSort(CardListController.SortMode.CostDesc));
            MakeFilterButton(sortBar, "名前順", Hex("22222E"), () => clc.SetSort(CardListController.SortMode.NameAsc));

            // Card list (scrollable)
            var scrollObj = new GameObject("CardScroll");
            scrollObj.transform.SetParent(screen.transform, false);
            Stretch(scrollObj, 0.02f, 0.02f, 0.98f, 0.74f);
            var scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.05f, 0.05f, 0.08f, 0.5f);

            var content = new GameObject("Content");
            content.transform.SetParent(scrollObj.transform, false);
            var crt = content.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = new Vector2(0, 2000);
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(210, 290);
            grid.spacing = new Vector2(12, 14);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.childAlignment = TextAnchor.UpperCenter;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var mask = scrollObj.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            foreach (var kv in CardDatabase.AllCards)
            {
                var card = kv.Value;
                var cardObj = PremiumCardFrame.Create(content.transform, card, 200, 280);

                // Make tappable
                var cardBtn = cardObj.AddComponent<Button>();
                cardBtn.transition = Selectable.Transition.None;
                var cardId = card.id;
                cardBtn.onClick.AddListener(() => clc.ShowDetail(cardId));
                cardObj.AddComponent<ButtonScaleEffect>();
            }

            clc.Init(content.transform);

            // Card Detail Panel (overlay)
            var detailPanel = new GameObject("CardDetailPanel");
            detailPanel.transform.SetParent(screen.transform, false);
            Stretch(detailPanel, 0.04f, 0.08f, 0.96f, 0.88f);
            var dpBg = detailPanel.AddComponent<Image>();
            dpBg.color = new Color(0.05f, 0.05f, 0.09f, 0.98f);

            // Detail panel border
            var borderObj = new GameObject("Border");
            borderObj.transform.SetParent(detailPanel.transform, false);
            Stretch(borderObj, 0, 0, 1, 1);
            var borderImg = borderObj.AddComponent<Outline>();

            // Accent bar
            var accentBar = new GameObject("Accent");
            accentBar.transform.SetParent(detailPanel.transform, false);
            Stretch(accentBar, 0, 0.93f, 1, 1);
            var accentImg = accentBar.AddComponent<Image>();
            accentImg.color = Color.gray;

            var dName = TxtObj(detailPanel, "DName", "", 32, 0.05f, 0.8f, 0.95f, 0.93f,
                align: TextAlignmentOptions.MidlineLeft);
            var dType = TxtObj(detailPanel, "DType", "", 18, 0.05f, 0.72f, 0.95f, 0.8f,
                align: TextAlignmentOptions.MidlineLeft);
            dType.color = ColTextSub;
            var dStats = TxtObj(detailPanel, "DStats", "", 20, 0.05f, 0.55f, 0.95f, 0.72f,
                align: TextAlignmentOptions.TopLeft);
            dStats.color = ColAccentGold;
            var dEffect = TxtObj(detailPanel, "DEffect", "", 17, 0.05f, 0.35f, 0.95f, 0.55f,
                align: TextAlignmentOptions.TopLeft);
            var dFlavor = Txt(detailPanel, "", 15, 0.05f, 0.14f, 0.95f, 0.35f,
                align: TextAlignmentOptions.TopLeft, color: new Color(0.5f, 0.5f, 0.55f));

            var closeBtn = MakeButton(detailPanel, "CloseDetail", "閉じる",
                0.3f, 0.03f, 0.7f, 0.12f, ColBtnSecondary);
            closeBtn.GetComponent<Button>().onClick.AddListener(() => clc.HideDetail());

            clc.InitDetailPanel(detailPanel, dName, dType.GetComponent<TextMeshProUGUI>(),
                dStats.GetComponent<TextMeshProUGUI>(), dEffect.GetComponent<TextMeshProUGUI>(),
                dFlavor, accentImg);
            detailPanel.SetActive(false);
        }

        // ==================== STORY ====================
        static void BuildStory(GameObject screen)
        {
            Txt(screen, "ストーリー", 48, 0f, 0.88f, 1f, 0.97f, bold: true,
                color: new Color(0.95f, 0.9f, 0.7f));

            // Subtitle
            Txt(screen, "交界に刻まれし願いの物語", 18, 0.1f, 0.84f, 0.9f, 0.89f, color: ColTextMuted);

            // Scrollable chapter list
            var scrollObj = new GameObject("StoryScroll");
            scrollObj.transform.SetParent(screen.transform, false);
            Stretch(scrollObj, 0.02f, 0.02f, 0.98f, 0.83f);
            var scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.05f, 0.04f, 0.07f, 0.4f);

            var content = new GameObject("Content");
            content.transform.SetParent(scrollObj.transform, false);
            var crt = content.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = new Vector2(0, 1500);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14;
            vlg.padding = new RectOffset(16, 16, 12, 12);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var scrollMask = scrollObj.AddComponent<Mask>();
            scrollMask.showMaskGraphic = true;

            foreach (var ch in StoryDatabase.Chapters)
            {
                var node = new GameObject($"Chapter_{ch.id}");
                node.transform.SetParent(content.transform, false);
                node.AddComponent<RectTransform>();
                var le = node.AddComponent<LayoutElement>();
                le.minHeight = 200;

                var nodeBg = node.AddComponent<Image>();
                Color aspectColor = AspectColors.GetColor(ch.themeAspect);
                nodeBg.color = ch.unlocked
                    ? new Color(aspectColor.r * 0.15f, aspectColor.g * 0.15f, aspectColor.b * 0.15f, 0.9f)
                    : new Color(0.06f, 0.06f, 0.08f, 0.7f);

                // Aspect accent (left edge)
                var accObj = new GameObject("Accent");
                accObj.transform.SetParent(node.transform, false);
                Stretch(accObj, 0, 0, 0.01f, 1);
                var accImg = accObj.AddComponent<Image>();
                accImg.color = ch.unlocked ? aspectColor : new Color(0.3f, 0.3f, 0.3f);

                // Chapter number
                var numObj = new GameObject("Num");
                numObj.transform.SetParent(node.transform, false);
                Stretch(numObj, 0.02f, 0.8f, 0.12f, 0.98f);
                var numTmp = numObj.AddComponent<TextMeshProUGUI>();
                numTmp.text = $"第{ch.number}章";
                numTmp.fontSize = 14;
                numTmp.color = ch.unlocked ? aspectColor : new Color(0.4f, 0.4f, 0.45f);
                numTmp.alignment = TextAlignmentOptions.MidlineLeft;
                ApplyFont(numTmp);

                // Title
                var titleObj = new GameObject("Title");
                titleObj.transform.SetParent(node.transform, false);
                Stretch(titleObj, 0.12f, 0.78f, 0.97f, 0.98f);
                var titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
                titleTmp.text = ch.title;
                titleTmp.fontSize = 24;
                titleTmp.fontStyle = FontStyles.Bold;
                titleTmp.color = ch.unlocked ? Color.white : new Color(0.45f, 0.45f, 0.5f);
                titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
                ApplyFont(titleTmp);

                // Key character
                var charObj = new GameObject("Character");
                charObj.transform.SetParent(node.transform, false);
                Stretch(charObj, 0.02f, 0.65f, 0.97f, 0.78f);
                var charTmp = charObj.AddComponent<TextMeshProUGUI>();
                charTmp.text = $"主要人物: {ch.keyCharacter}";
                charTmp.fontSize = 14;
                charTmp.color = ch.unlocked ? ColAccentGold : new Color(0.35f, 0.35f, 0.4f);
                charTmp.alignment = TextAlignmentOptions.MidlineLeft;
                ApplyFont(charTmp);

                // Description
                var descObj = new GameObject("Desc");
                descObj.transform.SetParent(node.transform, false);
                Stretch(descObj, 0.02f, 0.18f, 0.97f, 0.65f);
                var descTmp = descObj.AddComponent<TextMeshProUGUI>();
                descTmp.text = ch.description;
                descTmp.fontSize = 15;
                descTmp.color = ch.unlocked ? ColTextSub : new Color(0.35f, 0.35f, 0.4f);
                descTmp.alignment = TextAlignmentOptions.TopLeft;
                descTmp.textWrappingMode = TextWrappingModes.Normal;
                ApplyFont(descTmp);

                // Status badge
                var badge = new GameObject("Badge");
                badge.transform.SetParent(node.transform, false);
                Stretch(badge, 0.73f, 0.02f, 0.97f, 0.16f);
                var badgeBg = badge.AddComponent<Image>();
                badgeBg.color = ch.completed ? Hex("2A5A2A") : (ch.unlocked ? aspectColor : Hex("333340"));
                var badgeLabel = new GameObject("Label");
                badgeLabel.transform.SetParent(badge.transform, false);
                Stretch(badgeLabel, 0, 0, 1, 1);
                var bTmp = badgeLabel.AddComponent<TextMeshProUGUI>();
                bTmp.text = ch.completed ? "CLEAR" : (ch.unlocked ? "PLAY" : "LOCKED");
                bTmp.fontSize = 14;
                bTmp.fontStyle = FontStyles.Bold;
                bTmp.color = Color.white;
                bTmp.alignment = TextAlignmentOptions.Center;
                ApplyFont(bTmp);

                // Aspect chip
                var chipObj = new GameObject("AspectChip");
                chipObj.transform.SetParent(node.transform, false);
                Stretch(chipObj, 0.02f, 0.02f, 0.15f, 0.16f);
                var chipBg = chipObj.AddComponent<Image>();
                chipBg.color = ch.unlocked ? aspectColor : new Color(0.3f, 0.3f, 0.35f);
                var chipLabel = new GameObject("Label");
                chipLabel.transform.SetParent(chipObj.transform, false);
                Stretch(chipLabel, 0, 0, 1, 1);
                var cTmp = chipLabel.AddComponent<TextMeshProUGUI>();
                cTmp.text = AspectColors.GetDisplayName(ch.themeAspect);
                cTmp.fontSize = 14;
                cTmp.fontStyle = FontStyles.Bold;
                cTmp.color = Color.white;
                cTmp.alignment = TextAlignmentOptions.Center;
                ApplyFont(cTmp);
            }
        }

        // ==================== SHOP ====================
        static void BuildShop(GameObject screen)
        {
            Txt(screen, "ショップ", 48, 0f, 0.88f, 1f, 0.97f, bold: true,
                color: new Color(0.95f, 0.9f, 0.7f));

            // Currency display
            var coinBar = new GameObject("CoinBar");
            coinBar.transform.SetParent(screen.transform, false);
            Stretch(coinBar, 0.45f, 0.89f, 0.98f, 0.97f);
            var coinBg = coinBar.AddComponent<Image>();
            coinBg.color = new Color(0.10f, 0.10f, 0.15f, 0.85f);
            var coinTmp = AddTMP(coinBar, $"{PlayerData.Instance.gold:N0} コイン", 20,
                bold: true, color: new Color(0.95f, 0.85f, 0.4f));

            // Scrollable product list
            var scrollObj = new GameObject("ShopScroll");
            scrollObj.transform.SetParent(screen.transform, false);
            Stretch(scrollObj, 0.02f, 0.02f, 0.98f, 0.87f);
            var scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.05f, 0.05f, 0.07f, 0.3f);

            var content = new GameObject("Content");
            content.transform.SetParent(scrollObj.transform, false);
            var crt = content.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = new Vector2(0, 1200);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14;
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var scrollMask = scrollObj.AddComponent<Mask>();
            scrollMask.showMaskGraphic = true;

            foreach (var product in ShopDatabase.Products)
            {
                Color themeColor = AspectColors.GetColor(product.themeAspect);

                var item = new GameObject($"Product_{product.id}");
                item.transform.SetParent(content.transform, false);
                item.AddComponent<RectTransform>();
                var le = item.AddComponent<LayoutElement>();
                le.minHeight = product.featured ? 170 : 130;

                var itemBg = item.AddComponent<Image>();
                itemBg.color = product.featured
                    ? new Color(themeColor.r * 0.12f, themeColor.g * 0.12f, themeColor.b * 0.12f, 0.92f)
                    : ColBgCard;

                // Accent edge
                var accObj = new GameObject("Accent");
                accObj.transform.SetParent(item.transform, false);
                Stretch(accObj, 0, 0, 0.01f, 1);
                var accImg = accObj.AddComponent<Image>();
                accImg.color = themeColor;

                // Featured badge
                if (product.featured)
                {
                    var featBadge = new GameObject("FeatBadge");
                    featBadge.transform.SetParent(item.transform, false);
                    Stretch(featBadge, 0.02f, 0.85f, 0.16f, 0.98f);
                    var fbBg = featBadge.AddComponent<Image>();
                    fbBg.color = themeColor;
                    var fbLabel = new GameObject("Label");
                    fbLabel.transform.SetParent(featBadge.transform, false);
                    Stretch(fbLabel, 0, 0, 1, 1);
                    var fbTmp = fbLabel.AddComponent<TextMeshProUGUI>();
                    fbTmp.text = "注目";
                    fbTmp.fontSize = 12;
                    fbTmp.fontStyle = FontStyles.Bold;
                    fbTmp.color = Color.white;
                    fbTmp.alignment = TextAlignmentOptions.Center;
                    ApplyFont(fbTmp);
                }

                // Category label
                string catStr = product.category switch
                {
                    ProductCategory.CardPack => "カードパック",
                    ProductCategory.Currency => "通貨",
                    ProductCategory.Special => "特別",
                    _ => ""
                };
                var catObj = new GameObject("Category");
                catObj.transform.SetParent(item.transform, false);
                Stretch(catObj, product.featured ? 0.18f : 0.02f, 0.85f, 0.6f, 0.98f);
                var catTmp = catObj.AddComponent<TextMeshProUGUI>();
                catTmp.text = catStr;
                catTmp.fontSize = 13;
                catTmp.color = ColTextMuted;
                catTmp.alignment = TextAlignmentOptions.MidlineLeft;
                ApplyFont(catTmp);

                // Product name
                var nameObj = new GameObject("Name");
                nameObj.transform.SetParent(item.transform, false);
                Stretch(nameObj, 0.02f, 0.6f, 0.97f, 0.85f);
                var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
                nameTmp.text = product.productName;
                nameTmp.fontSize = 22;
                nameTmp.fontStyle = FontStyles.Bold;
                nameTmp.color = Color.white;
                nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
                ApplyFont(nameTmp);

                // Description
                var descObj = new GameObject("Desc");
                descObj.transform.SetParent(item.transform, false);
                Stretch(descObj, 0.02f, 0.22f, 0.6f, 0.6f);
                var descTmp = descObj.AddComponent<TextMeshProUGUI>();
                descTmp.text = product.description;
                descTmp.fontSize = 14;
                descTmp.color = ColTextSub;
                descTmp.alignment = TextAlignmentOptions.TopLeft;
                descTmp.textWrappingMode = TextWrappingModes.Normal;
                ApplyFont(descTmp);

                // Buy button
                string priceLabel = product.currency == "円"
                    ? $"¥{product.price}"
                    : $"{product.price} {product.currency}";
                var buyBtn = MakeButton(item, "Buy", priceLabel,
                    0.62f, 0.12f, 0.97f, 0.5f, themeColor);
                var capturedProduct = product;
                var capturedCoinTmp = coinTmp;
                buyBtn.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (capturedProduct.currency == "コイン")
                    {
                        if (PlayerData.Instance.TrySpend(capturedProduct.price))
                        {
                            capturedCoinTmp.text = $"{PlayerData.Instance.gold:N0} コイン";
                            Debug.Log($"Purchased: {capturedProduct.productName}");

                            // カードパック購入 → パック開封演出を起動
                            if (capturedProduct.category == ProductCategory.CardPack)
                            {
                                Aspect? pickup = capturedProduct.id == "PACK_01"
                                    ? null
                                    : capturedProduct.themeAspect;
                                PackSystem.OpenPack(pickup);
                            }
                        }
                        else
                        {
                            Debug.Log("Not enough coins!");
                        }
                    }
                });
            }

            // ── パック開封演出オーバーレイ (PACK_OPENING_SPEC.md) ──
            BuildPackOpeningOverlay(screen);
        }

        // ==================== PACK OPENING OVERLAY ====================
        static void BuildPackOpeningOverlay(GameObject shopScreen)
        {
            // フルスクリーンオーバーレイ（ショップ画面の子として最前面に配置）
            var overlay = new GameObject("PackOpeningOverlay");
            overlay.transform.SetParent(shopScreen.transform, false);
            Stretch(overlay, 0, 0, 1, 1);

            // オーバーレイ背景 (暗転用)
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0.05f, 0.05f, 0.1f, 0f);
            overlayImg.raycastTarget = false;

            var overlayGroup = overlay.AddComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = false;

            // パック画像エリア（中央上部）
            var packImg = new GameObject("PackImage");
            packImg.transform.SetParent(overlay.transform, false);
            Stretch(packImg, 0.25f, 0.55f, 0.75f, 0.85f);
            var packImgComp = packImg.AddComponent<Image>();
            packImgComp.color = new Color(0.2f, 0.15f, 0.3f, 0.9f);
            packImg.SetActive(false);
            var packRt = packImg.GetComponent<RectTransform>();

            // カード展開エリア（中央）
            var cardArea = new GameObject("CardArea");
            cardArea.transform.SetParent(overlay.transform, false);
            Stretch(cardArea, 0.05f, 0.35f, 0.95f, 0.75f);
            cardArea.AddComponent<RectTransform>();
            var cardAreaRt = cardArea.GetComponent<RectTransform>();

            // 結果パネル（下部）
            var resultPanel = new GameObject("ResultPanel");
            resultPanel.transform.SetParent(overlay.transform, false);
            Stretch(resultPanel, 0.05f, 0.05f, 0.95f, 0.32f);
            var resultBg = resultPanel.AddComponent<Image>();
            resultBg.color = new Color(0.06f, 0.06f, 0.10f, 0.92f);
            resultPanel.SetActive(false);

            // ゴールド変換テキスト
            var goldTxt = new GameObject("GoldConvertedText");
            goldTxt.transform.SetParent(resultPanel.transform, false);
            Stretch(goldTxt, 0.05f, 0.55f, 0.95f, 0.85f);
            var goldTmp = goldTxt.AddComponent<TextMeshProUGUI>();
            goldTmp.text = "";
            goldTmp.fontSize = 22;
            goldTmp.fontStyle = FontStyles.Bold;
            goldTmp.color = new Color(0.95f, 0.85f, 0.4f);
            goldTmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(goldTmp);
            goldTxt.SetActive(false);

            // 天井カウンターテキスト
            var pityTxt = new GameObject("PityCounterText");
            pityTxt.transform.SetParent(resultPanel.transform, false);
            Stretch(pityTxt, 0.05f, 0.35f, 0.95f, 0.55f);
            var pityTmp = pityTxt.AddComponent<TextMeshProUGUI>();
            pityTmp.text = "";
            pityTmp.fontSize = 14;
            pityTmp.color = ColTextSub;
            pityTmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(pityTmp);

            // 閉じるボタン
            var closeBtn = MakeButton(resultPanel, "CloseBtn", "OK",
                0.3f, 0.05f, 0.7f, 0.3f, ColBtnPrimary);
            var closeBtnComp = closeBtn.GetComponent<Button>();

            // PackOpeningController コンポーネントを追加して参照を注入
            var ctrl = overlay.AddComponent<PackOpeningController>();
            ctrl.packImage = packRt;
            ctrl.cardArea = cardAreaRt;
            ctrl.resultPanel = resultPanel.GetComponent<RectTransform>();
            ctrl.goldConvertedText = goldTmp;
            ctrl.overlayGroup = overlayGroup;
            ctrl.overlayImage = overlayImg;
            ctrl.closeButton = closeBtnComp;
            ctrl.pityCounterText = pityTmp;

            overlay.SetActive(false);

            // OnPackOpenedEx イベントで演出を自動起動
            PackSystem.OnPackOpenedEx += result =>
            {
                ctrl.StartPackOpening(result);
            };
        }

        // ==================== MATCH ====================
        static void BuildMatch(GameObject screen, ScreenManager sm)
        {
            var mc = screen.AddComponent<MatchController>();
            screen.AddComponent<AIMatchController>();

            // ============================================================
            // LANDSCAPE DQ Rivals-style layout
            // ============================================================
            //
            // X: 0.00                0.50                1.00
            //    ┌──────┬──────┬──────┬──────┬──────┬──────┬──────┐
            //    │[⚙]   │ 相手手札:5 山:22  │      │       │⏱ 78s│ Y=0.92-1.00 (info bar 8%)
            //    ├──────┤              ├──────┤              ├──────┤
            //    │      │              │ 時計  │              │      │
            //    │ 相手  │ 後列  │ 前列  │  盤   │ 前列  │ 後列  │ 自分  │
            //    │リーダー│ (3枠) │ (3枠) │──────│ (3枠) │ (3枠) │リーダー│ Y=0.25-0.92 (board 67%)
            //    │      │              │ 界律  │              │      │
            //    │      │              │      │              │      │
            //    ├──────┴──────┴──────┴──────┴──────┴──────┴──────┤
            //    │ [山24]  [手札][手札][手札][手札][手札]    [ターン終了] │ Y=0.02-0.25 (hand 23%)
            //    └────────────────────────────────────────────────────┘
            //
            // X bands: Leader=12%, Back=11%, Front=11%, Center=12%, Front=11%, Back=11%, Leader=12% + margins

            // --- Top info bar ---
            var settingsBtn = MakeButton(screen, "SettingsButton", "\u2699",
                0.005f, 0.92f, 0.04f, 1f, Hex("333344"));

            // Opponent info (hand count, deck count)
            var p2InfoT = TxtObj(screen, "P2InfoText", "手札:5 山:22", 12,
                0.05f, 0.93f, 0.30f, 1f, align: TextAlignmentOptions.MidlineLeft);

            // Turn timer (right side of top bar)
            var timerT = TxtObj(screen, "TimerText", "", 13,
                0.90f, 0.93f, 0.995f, 1f, align: TextAlignmentOptions.MidlineRight);

            // Turn text (hidden, kept for internal reference)
            var turnT = TxtObj(screen, "TurnText", "", 1, 0f, 0f, 0f, 0f);
            // Phase/ActivePlayer hidden
            var phaseT = TxtObj(screen, "PhaseText", "", 1, 0f, 0f, 0f, 0f);
            var activeT = TxtObj(screen, "ActivePlayerText", "", 1, 0f, 0f, 0f, 0f);

            // --- Turn announcement (center, fades in/out) ---
            var turnAnnounce = TxtObj(screen, "TurnAnnounceText", "", 36,
                0.25f, 0.45f, 0.75f, 0.65f, align: TextAlignmentOptions.Center);
            var taCanvasGroup = turnAnnounce.gameObject.AddComponent<CanvasGroup>();
            taCanvasGroup.alpha = 0f;

            // --- Settings panel (hidden overlay) ---
            var settingsPanel = new GameObject("SettingsPanel");
            settingsPanel.transform.SetParent(screen.transform, false);
            Stretch(settingsPanel, 0.30f, 0.25f, 0.70f, 0.85f);
            var spBg = settingsPanel.AddComponent<Image>();
            spBg.color = new Color(0.05f, 0.05f, 0.10f, 0.95f);
            settingsPanel.SetActive(false);

            Txt(settingsPanel, "設定", 22, 0.1f, 0.84f, 0.9f, 0.96f, bold: true,
                color: new Color(0.95f, 0.9f, 0.7f));
            Txt(settingsPanel, "BGM音量", 14, 0.06f, 0.70f, 0.35f, 0.80f, color: ColTextSub);
            var bgmSlider = new GameObject("BGMSlider");
            bgmSlider.transform.SetParent(settingsPanel.transform, false);
            Stretch(bgmSlider, 0.38f, 0.70f, 0.94f, 0.80f);
            bgmSlider.AddComponent<Slider>().value = 0.7f;
            Txt(settingsPanel, "SE音量", 14, 0.06f, 0.56f, 0.35f, 0.66f, color: ColTextSub);
            var seSlider = new GameObject("SESlider");
            seSlider.transform.SetParent(settingsPanel.transform, false);
            Stretch(seSlider, 0.38f, 0.56f, 0.94f, 0.66f);
            seSlider.AddComponent<Slider>().value = 0.7f;
            var logToggleBtn = MakeButton(settingsPanel, "LogToggleButton", "バトルログ",
                0.06f, 0.38f, 0.94f, 0.50f, Hex("334455"));
            var surrenderBtn = MakeButton(settingsPanel, "SurrenderButton", "降参する",
                0.06f, 0.22f, 0.94f, 0.34f, Hex("553333"));
            var settingsCloseBtn = MakeButton(settingsPanel, "SettingsCloseBtn", "閉じる",
                0.25f, 0.05f, 0.75f, 0.17f, Hex("444444"));
            settingsCloseBtn.GetComponent<Button>().onClick.AddListener(() => settingsPanel.SetActive(false));
            settingsBtn.GetComponent<Button>().onClick.AddListener(() => settingsPanel.SetActive(!settingsPanel.activeSelf));

            // --- Log panel (hidden overlay) ---
            var logPanel = new GameObject("LogPanel");
            logPanel.transform.SetParent(screen.transform, false);
            Stretch(logPanel, 0.05f, 0.30f, 0.95f, 0.90f);
            var logPanelBg = logPanel.AddComponent<Image>();
            logPanelBg.color = new Color(0.03f, 0.03f, 0.08f, 0.92f);
            logPanel.SetActive(false);
            var logT = TxtObj(logPanel, "LogText", "", 11, 0.02f, 0.02f, 0.98f, 0.98f,
                align: TextAlignmentOptions.TopLeft);

            // ============================================================
            // BOARD — Left to Right
            // ============================================================

            // ---- P2 (Opponent) Leader — LEFT ----
            var p2LeaderPanel = new GameObject("P2LeaderPanel");
            p2LeaderPanel.transform.SetParent(screen.transform, false);
            Stretch(p2LeaderPanel, 0.005f, 0.25f, 0.105f, 0.92f);
            var p2LBg = p2LeaderPanel.AddComponent<Image>();
            p2LBg.color = new Color(0.08f, 0.06f, 0.12f, 0.8f);

            // P2 Leader portrait
            var p2Portrait = new GameObject("P2Portrait");
            p2Portrait.transform.SetParent(p2LeaderPanel.transform, false);
            Stretch(p2Portrait, 0f, 0.4f, 1f, 1f);
            var p2PortImg = p2Portrait.AddComponent<Image>();
            p2PortImg.preserveAspect = true;
            p2PortImg.color = Color.white;
            p2PortImg.raycastTarget = false;
            p2Portrait.transform.SetAsFirstSibling();

            // P2 HP gauge (top of leader, spans leader+field width)
            var p2GaugeArea = new GameObject("P2GaugeArea");
            p2GaugeArea.transform.SetParent(screen.transform, false);
            Stretch(p2GaugeArea, 0.005f, 0.92f, 0.495f, 0.96f);
            var p2GaugeBg = p2GaugeArea.AddComponent<Image>();
            p2GaugeBg.color = Hex("1A1A26");

            var p2HpFillObj = new GameObject("P2HpFill");
            p2HpFillObj.transform.SetParent(p2GaugeArea.transform, false);
            var p2FillRt = p2HpFillObj.AddComponent<RectTransform>();
            p2FillRt.anchorMin = Vector2.zero;
            p2FillRt.anchorMax = Vector2.one;
            p2FillRt.offsetMin = new Vector2(2, 2);
            p2FillRt.offsetMax = new Vector2(-2, -2);
            var p2HpFillImg = p2HpFillObj.AddComponent<Image>();
            p2HpFillImg.color = new Color(0.9f, 0.3f, 0.3f);
            // anchorMax.x で幅を制御するため Type.Simple のまま

            // P2 HP — ゲージバーのみで表示（数値なし）

            // P2 リーダーパネル内: Lv(左上) + 戦力(右上) はイラスト上に重ねる
            var p2Name = TxtObj(p2LeaderPanel, "P2LeaderName", "", 13,
                0.05f, 0.85f, 0.45f, 0.98f, align: TextAlignmentOptions.TopLeft);
            var p2Pow = TxtObj(p2LeaderPanel, "P2LeaderPower", "", 14,
                0.55f, 0.85f, 0.95f, 0.98f, align: TextAlignmentOptions.TopRight);
            // P2 skill info (下部)
            var p2SkillT = TxtObj(p2LeaderPanel, "P2Skill", "[技]", 10,
                0.05f, 0.02f, 0.95f, 0.14f, align: TextAlignmentOptions.Center);

            // P2 願成 + CP — リーダーパネル下に独立エリア
            var p2StatusArea = new GameObject("P2StatusArea");
            p2StatusArea.transform.SetParent(screen.transform, false);
            Stretch(p2StatusArea, 0.005f, 0.17f, 0.105f, 0.25f);
            var p2StatusBg = p2StatusArea.AddComponent<Image>();
            p2StatusBg.color = new Color(0.06f, 0.06f, 0.1f, 0.7f);
            var p2Evo = TxtObj(p2StatusArea, "P2LeaderEvo", "", 10,
                0.05f, 0.5f, 0.95f, 1f, align: TextAlignmentOptions.Center);
            var p2Cp = TxtObj(p2StatusArea, "P2CP", "", 12,
                0.05f, 0f, 0.95f, 0.5f, align: TextAlignmentOptions.Center);

            // ---- P2 Field: Back (column of 3) ----
            var p2BackCol = new GameObject("P2BackCol");
            p2BackCol.transform.SetParent(screen.transform, false);
            Stretch(p2BackCol, 0.115f, 0.25f, 0.245f, 0.85f);
            var p2BackLayout = p2BackCol.AddComponent<VerticalLayoutGroup>();
            p2BackLayout.spacing = 4;
            p2BackLayout.childForceExpandWidth = true;
            p2BackLayout.childForceExpandHeight = true;
            var p2Back = p2BackCol; // direct reference for MakeFieldRow children

            // ---- P2 Field: Front (column of 3) ----
            var p2FrontCol = new GameObject("P2FrontCol");
            p2FrontCol.transform.SetParent(screen.transform, false);
            Stretch(p2FrontCol, 0.255f, 0.25f, 0.385f, 0.85f);
            var p2FrontLayout = p2FrontCol.AddComponent<VerticalLayoutGroup>();
            p2FrontLayout.spacing = 4;
            p2FrontLayout.childForceExpandWidth = true;
            p2FrontLayout.childForceExpandHeight = true;
            var p2Front = p2FrontCol;

            // P2 Field parent (for MatchController reference, invisible container)
            var p2Field = new GameObject("P2Field");
            p2Field.transform.SetParent(screen.transform, false);

            // ---- CENTER: Turn Clock + Algorithm ----
            // Turn Clock (upper center)
            var clockObj = new GameObject("TurnClock");
            clockObj.transform.SetParent(screen.transform, false);
            Stretch(clockObj, 0.43f, 0.60f, 0.57f, 0.85f);

            var dialImg = clockObj.AddComponent<Image>();
            dialImg.color = new Color(0.15f, 0.20f, 0.25f, 0.8f);

            for (int i = 0; i < 24; i++)
            {
                var tick = new GameObject($"Tick_{i}");
                tick.transform.SetParent(clockObj.transform, false);
                var tickRt = tick.AddComponent<RectTransform>();
                tickRt.anchorMin = new Vector2(0.5f, 0.5f);
                tickRt.anchorMax = new Vector2(0.5f, 0.5f);
                tickRt.pivot = new Vector2(0.5f, 0f);
                tickRt.localRotation = Quaternion.Euler(0, 0, -360f * i / 24f);
                bool major = (i % 6 == 0);
                tickRt.sizeDelta = new Vector2(major ? 3f : 1.5f, major ? 14f : 9f);
                tickRt.anchoredPosition = new Vector2(0, 44f);
                var tickImg = tick.AddComponent<Image>();
                tickImg.color = major ? new Color(0.9f, 0.85f, 0.6f) : new Color(0.6f, 0.6f, 0.6f);
            }

            var handObj = new GameObject("ClockHand");
            handObj.transform.SetParent(clockObj.transform, false);
            var handRt = handObj.AddComponent<RectTransform>();
            handRt.anchorMin = new Vector2(0.5f, 0.5f);
            handRt.anchorMax = new Vector2(0.5f, 0.5f);
            handRt.pivot = new Vector2(0.5f, 0f);
            handRt.sizeDelta = new Vector2(3f, 38f);
            var handImg = handObj.AddComponent<Image>();
            handImg.color = new Color(1f, 0.85f, 0.4f);

            var clockCenter = new GameObject("ClockCenterBg");
            clockCenter.transform.SetParent(clockObj.transform, false);
            var ccRt = clockCenter.AddComponent<RectTransform>();
            ccRt.anchorMin = new Vector2(0.5f, 0.5f);
            ccRt.anchorMax = new Vector2(0.5f, 0.5f);
            ccRt.sizeDelta = new Vector2(30f, 30f);
            var ccImg = clockCenter.AddComponent<Image>();
            ccImg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            var clockText = TxtObj(clockObj, "ClockTurnText", "0", 15,
                0.25f, 0.30f, 0.75f, 0.70f, align: TextAlignmentOptions.Center);

            var turnClock = clockObj.AddComponent<TurnClock>();
            SetPrivateField(turnClock, "hand", handRt);
            SetPrivateField(turnClock, "centerText", clockText);
            SetPrivateField(turnClock, "dialImage", dialImg);

            // Algorithm card (lower center, below clock)
            var algoPanel = new GameObject("AlgorithmPanel");
            algoPanel.transform.SetParent(screen.transform, false);
            Stretch(algoPanel, 0.42f, 0.25f, 0.58f, 0.58f);
            var algoBg = algoPanel.AddComponent<Image>();
            algoBg.color = new Color(0.12f, 0.10f, 0.18f, 0.85f);

            var algoT = TxtObj(algoPanel, "SharedAlgoText", "界律: なし", 14,
                0.05f, 0.05f, 0.95f, 0.95f, align: TextAlignmentOptions.Center);

            // Decline Block (contextual, center overlay)
            var declineBlockBtn = MakeButton(screen, "DeclineBlockButton", "ブロックしない",
                0.38f, 0.18f, 0.62f, 0.25f, Hex("664433"));
            declineBlockBtn.SetActive(false);

            // ---- P1 (Player) Field: Front (column of 3) ----
            var p1FrontCol = new GameObject("P1FrontCol");
            p1FrontCol.transform.SetParent(screen.transform, false);
            Stretch(p1FrontCol, 0.615f, 0.25f, 0.745f, 0.85f);
            var p1FrontLayout = p1FrontCol.AddComponent<VerticalLayoutGroup>();
            p1FrontLayout.spacing = 4;
            p1FrontLayout.childForceExpandWidth = true;
            p1FrontLayout.childForceExpandHeight = true;
            var p1Front = p1FrontCol;

            // ---- P1 Field: Back (column of 3) ----
            var p1BackCol = new GameObject("P1BackCol");
            p1BackCol.transform.SetParent(screen.transform, false);
            Stretch(p1BackCol, 0.755f, 0.25f, 0.885f, 0.85f);
            var p1BackLayout = p1BackCol.AddComponent<VerticalLayoutGroup>();
            p1BackLayout.spacing = 4;
            p1BackLayout.childForceExpandWidth = true;
            p1BackLayout.childForceExpandHeight = true;
            var p1Back = p1BackCol;

            // P1 Field parent (invisible container)
            var p1Field = new GameObject("P1Field");
            p1Field.transform.SetParent(screen.transform, false);

            // ---- P1 (Player) Leader — RIGHT ----
            var p1LeaderPanel = new GameObject("P1LeaderPanel");
            p1LeaderPanel.transform.SetParent(screen.transform, false);
            Stretch(p1LeaderPanel, 0.895f, 0.25f, 0.995f, 0.92f);
            var p1LBg = p1LeaderPanel.AddComponent<Image>();
            p1LBg.color = new Color(0.06f, 0.08f, 0.12f, 0.8f);

            // P1 Leader portrait
            var p1Portrait = new GameObject("P1Portrait");
            p1Portrait.transform.SetParent(p1LeaderPanel.transform, false);
            Stretch(p1Portrait, 0f, 0.4f, 1f, 1f);
            var p1PortImg = p1Portrait.AddComponent<Image>();
            p1PortImg.preserveAspect = true;
            p1PortImg.color = Color.white;
            p1PortImg.raycastTarget = false;
            p1Portrait.transform.SetAsFirstSibling();

            // P1 HP gauge (top of leader, spans field+leader width)
            var p1GaugeArea = new GameObject("P1GaugeArea");
            p1GaugeArea.transform.SetParent(screen.transform, false);
            Stretch(p1GaugeArea, 0.505f, 0.92f, 0.995f, 0.96f);
            var p1GaugeBg = p1GaugeArea.AddComponent<Image>();
            p1GaugeBg.color = Hex("1A1A26");

            var p1HpFillObj = new GameObject("P1HpFill");
            p1HpFillObj.transform.SetParent(p1GaugeArea.transform, false);
            var p1FillRt = p1HpFillObj.AddComponent<RectTransform>();
            p1FillRt.anchorMin = Vector2.zero;
            p1FillRt.anchorMax = Vector2.one;
            p1FillRt.offsetMin = new Vector2(2, 2);
            p1FillRt.offsetMax = new Vector2(-2, -2);
            var p1HpFillImg = p1HpFillObj.AddComponent<Image>();
            p1HpFillImg.color = new Color(0.3f, 0.8f, 0.4f);
            // anchorMax.x で幅を制御するため Type.Simple のまま

            // P1 HP — ゲージバーのみで表示（数値なし）

            // P1 リーダーパネル内: Lv(左上) + 戦力(右上) はイラスト上に重ねる
            var p1Name = TxtObj(p1LeaderPanel, "P1LeaderName", "", 13,
                0.05f, 0.85f, 0.45f, 0.98f, align: TextAlignmentOptions.TopLeft);
            var p1Pow = TxtObj(p1LeaderPanel, "P1LeaderPower", "", 14,
                0.55f, 0.85f, 0.95f, 0.98f, align: TextAlignmentOptions.TopRight);
            // P1 leader skill buttons (下部)
            var p1Sk2 = MakeButton(p1LeaderPanel, "SkillLv2Btn", "技2",
                0.05f, 0.02f, 0.48f, 0.14f, Hex("335544"));
            var p1Sk3 = MakeButton(p1LeaderPanel, "SkillLv3Btn", "技3",
                0.52f, 0.02f, 0.95f, 0.14f, Hex("335544"));

            // P1 願成 + CP — リーダーパネル下に独立エリア
            var p1StatusArea = new GameObject("P1StatusArea");
            p1StatusArea.transform.SetParent(screen.transform, false);
            Stretch(p1StatusArea, 0.895f, 0.17f, 0.995f, 0.25f);
            var p1StatusBg = p1StatusArea.AddComponent<Image>();
            p1StatusBg.color = new Color(0.06f, 0.06f, 0.1f, 0.7f);
            var p1Evo = TxtObj(p1StatusArea, "P1LeaderEvo", "", 10,
                0.05f, 0.5f, 0.95f, 1f, align: TextAlignmentOptions.Center);
            var p1Cp = TxtObj(p1StatusArea, "P1CP", "", 12,
                0.05f, 0f, 0.95f, 0.5f, align: TextAlignmentOptions.Center);

            // ---- GaugeView (HP bars for both players) ----
            var gv = p1GaugeArea.AddComponent<GaugeView>();
            SetPrivateField(gv, "p1HpBar", p1GaugeArea.GetComponent<RectTransform>());
            SetPrivateField(gv, "p1HpFill", p1HpFillImg);
            SetPrivateField(gv, "p2HpBar", p2GaugeArea.GetComponent<RectTransform>());
            SetPrivateField(gv, "p2HpFill", p2HpFillImg);

            // ---- HAND (bottom strip) ----
            var hand = new GameObject("P1Hand");
            hand.transform.SetParent(screen.transform, false);
            Stretch(hand, 0.10f, 0.02f, 0.82f, 0.24f);
            var handLayout = hand.AddComponent<HorizontalLayoutGroup>();
            handLayout.spacing = 6;
            handLayout.childForceExpandWidth = true;
            handLayout.childForceExpandHeight = true;

            // Deck count (left of hand)
            var deckCountT = TxtObj(screen, "DeckCountText", "山:24", 12,
                0.01f, 0.08f, 0.09f, 0.18f, align: TextAlignmentOptions.Center);

            // End Turn button (right of hand, thumb-friendly)
            var endTurnBtn = MakeButton(screen, "EndTurnButton", "ターン\n終了",
                0.84f, 0.02f, 0.995f, 0.24f, Hex("335999"));

            // --- Result Panel (SCREEN_SPEC.md §7c) ---
            var resultPanel = new GameObject("ResultPanel");
            resultPanel.transform.SetParent(screen.transform, false);
            Stretch(resultPanel, 0, 0, 1, 1);
            var rpBg = resultPanel.AddComponent<Image>();
            rpBg.color = new Color(0.04f, 0.04f, 0.08f, 0.96f);

            // 勝利/敗北/引き分けテキスト（大）
            var resultT = TxtObj(resultPanel, "ResultText", "", 52, 0.05f, 0.82f, 0.95f, 0.95f,
                align: TextAlignmentOptions.Center);

            // 勝利タイプ (鯱鉾勝利 / 塗り勝利)
            var victoryTypeT = TxtObj(resultPanel, "VictoryTypeText", "", 24,
                0.1f, 0.76f, 0.9f, 0.82f, align: TextAlignmentOptions.Center);

            // マッチID
            var matchIdT = TxtObj(resultPanel, "MatchIdText", "", 12,
                0.05f, 0.72f, 0.95f, 0.76f, align: TextAlignmentOptions.Center);
            matchIdT.color = ColTextSub;

            // --- 最終HP表示セクション ---
            // P1 HP label + bar
            Txt(resultPanel, "あなた", 14, 0.08f, 0.66f, 0.25f, 0.71f,
                align: TextAlignmentOptions.MidlineLeft, color: ColTextSub);
            var p1HpBarBg = new GameObject("P1HpBarBg");
            p1HpBarBg.transform.SetParent(resultPanel.transform, false);
            Stretch(p1HpBarBg, 0.26f, 0.665f, 0.92f, 0.705f);
            var p1HpBarBgImg = p1HpBarBg.AddComponent<Image>();
            p1HpBarBgImg.color = new Color(0.15f, 0.15f, 0.2f);

            var p1HpBarFill = new GameObject("P1HpBarFill");
            p1HpBarFill.transform.SetParent(p1HpBarBg.transform, false);
            Stretch(p1HpBarFill, 0, 0, 1, 1);
            var resP1HpBar = p1HpBarFill.AddComponent<Image>();
            resP1HpBar.color = new Color(0.35f, 0.76f, 0.42f);
            resP1HpBar.type = Image.Type.Filled;
            resP1HpBar.fillMethod = Image.FillMethod.Horizontal;

            // P2 HP label + bar
            Txt(resultPanel, "相手", 14, 0.08f, 0.61f, 0.25f, 0.66f,
                align: TextAlignmentOptions.MidlineLeft, color: ColTextSub);
            var p2HpBarBg = new GameObject("P2HpBarBg");
            p2HpBarBg.transform.SetParent(resultPanel.transform, false);
            Stretch(p2HpBarBg, 0.26f, 0.615f, 0.92f, 0.655f);
            var p2HpBarBgImg = p2HpBarBg.AddComponent<Image>();
            p2HpBarBgImg.color = new Color(0.15f, 0.15f, 0.2f);

            var p2HpBarFill = new GameObject("P2HpBarFill");
            p2HpBarFill.transform.SetParent(p2HpBarBg.transform, false);
            Stretch(p2HpBarFill, 0, 0, 1, 1);
            var resP2HpBar = p2HpBarFill.AddComponent<Image>();
            resP2HpBar.color = new Color(0.69f, 0.23f, 0.23f);
            resP2HpBar.type = Image.Type.Filled;
            resP2HpBar.fillMethod = Image.FillMethod.Horizontal;

            // 最終HP数値テキスト
            var finalHpT = TxtObj(resultPanel, "FinalHpText", "", 16,
                0.05f, 0.56f, 0.95f, 0.61f, align: TextAlignmentOptions.Center);
            finalHpT.color = ColTextSub;

            // --- 報酬セクション ---
            Txt(resultPanel, "報酬", 20, 0.05f, 0.48f, 0.95f, 0.54f,
                bold: true, align: TextAlignmentOptions.MidlineLeft,
                color: ColAccentGold);

            // 区切り線
            var rewardDivider = new GameObject("RewardDivider");
            rewardDivider.transform.SetParent(resultPanel.transform, false);
            Stretch(rewardDivider, 0.05f, 0.475f, 0.95f, 0.48f);
            var divImg = rewardDivider.AddComponent<Image>();
            divImg.color = new Color(0.3f, 0.28f, 0.2f, 0.6f);

            // ゴールド獲得量
            Txt(resultPanel, "ゴールド", 15, 0.08f, 0.42f, 0.35f, 0.47f,
                align: TextAlignmentOptions.MidlineLeft, color: ColTextSub);
            var rewardGoldT = TxtObj(resultPanel, "RewardGoldText", "", 18,
                0.35f, 0.42f, 0.92f, 0.47f, align: TextAlignmentOptions.MidlineRight);
            rewardGoldT.color = ColAccentGold;

            // バトルパスEXP
            Txt(resultPanel, "バトルパスEXP", 15, 0.08f, 0.37f, 0.35f, 0.42f,
                align: TextAlignmentOptions.MidlineLeft, color: ColTextSub);
            var rewardBpXpT = TxtObj(resultPanel, "RewardBpXpText", "", 18,
                0.35f, 0.37f, 0.92f, 0.42f, align: TextAlignmentOptions.MidlineRight);
            rewardBpXpT.color = ColAccentBlue;

            // ミッション進捗
            Txt(resultPanel, "ミッション", 15, 0.08f, 0.30f, 0.35f, 0.37f,
                align: TextAlignmentOptions.TopLeft, color: ColTextSub);
            var rewardMissionT = TxtObj(resultPanel, "RewardMissionText", "", 14,
                0.35f, 0.26f, 0.92f, 0.37f, align: TextAlignmentOptions.TopRight);
            rewardMissionT.color = new Color(0.75f, 0.9f, 0.75f);

            // --- ナビゲーションボタン (3つ横並び) ---
            var playAgainBtn = MakeButton(resultPanel, "PlayAgainButton", "もう一度",
                0.04f, 0.10f, 0.34f, 0.22f, ColBtnPrimary);
            var backBtn = MakeButton(resultPanel, "BackButton", "ホームへ",
                0.36f, 0.10f, 0.64f, 0.22f, ColBtnSecondary);
            backBtn.GetComponent<Button>().onClick.AddListener(() => sm.ShowScreen(GameManager.GameScreen.Home));
            var replayBtn = MakeButton(resultPanel, "ViewReplayButton", "リプレイを見る",
                0.66f, 0.10f, 0.96f, 0.22f, ColBtnSecondary);

            resultPanel.SetActive(false);

            // --- Mulligan Panel ---
            var mulliganPanel = new GameObject("MulliganPanel");
            mulliganPanel.transform.SetParent(screen.transform, false);
            Stretch(mulliganPanel, 0, 0, 1, 1);
            var mpBg = mulliganPanel.AddComponent<Image>();
            mpBg.color = new Color(0.03f, 0.03f, 0.08f, 0.95f);

            Txt(mulliganPanel, "マリガン", 40, 0.1f, 0.82f, 0.9f, 0.93f, bold: true,
                color: new Color(0.95f, 0.9f, 0.7f));
            Txt(mulliganPanel, "引き直すカードを選んでください\n選択したカードをデッキに戻し、同数を引き直します", 18,
                0.05f, 0.70f, 0.95f, 0.80f, color: ColTextSub);

            var mulliganHand = new GameObject("MulliganHand");
            mulliganHand.transform.SetParent(mulliganPanel.transform, false);
            Stretch(mulliganHand, 0.02f, 0.30f, 0.98f, 0.68f);
            var mhLayout = mulliganHand.AddComponent<HorizontalLayoutGroup>();
            mhLayout.spacing = 8;
            mhLayout.childForceExpandWidth = true;
            mhLayout.childForceExpandHeight = true;

            var mulliganConfirmBtn = MakeButton(mulliganPanel, "MulliganConfirm", "引き直す",
                0.08f, 0.12f, 0.48f, 0.24f, ColBtnDanger);
            var mulliganSkipBtn = MakeButton(mulliganPanel, "MulliganSkip", "このまま始める",
                0.52f, 0.12f, 0.92f, 0.24f, ColBtnPrimary);

            // --- Wire MatchController fields via reflection ---
            SetPrivateField(mc, "gaugeView", gv);
            // HP数値は非表示（ゲージバーのみ）
            SetPrivateField(mc, "p1LeaderNameText", p1Name);
            SetPrivateField(mc, "p1LeaderPowerText", p1Pow);
            SetPrivateField(mc, "p1LeaderEvoText", p1Evo);
            SetPrivateField(mc, "p1CpText", p1Cp);
            SetPrivateField(mc, "p1FieldParent", p1Field.transform);
            SetPrivateField(mc, "p1FieldFront", p1Front.transform);
            SetPrivateField(mc, "p1FieldBack", p1Back.transform);
            SetPrivateField(mc, "p1HandParent", hand.transform);
            // p1LeaderAttackButton removed — attacks use drag-and-drop
            SetPrivateField(mc, "p2LeaderNameText", p2Name);
            SetPrivateField(mc, "p2LeaderPowerText", p2Pow);
            SetPrivateField(mc, "p2LeaderEvoText", p2Evo);
            SetPrivateField(mc, "p2CpText", p2Cp);
            SetPrivateField(mc, "p2FieldParent", p2Field.transform);
            SetPrivateField(mc, "p2FieldFront", p2Front.transform);
            SetPrivateField(mc, "p2FieldBack", p2Back.transform);
            SetPrivateField(mc, "sharedAlgoText", algoT);
            SetPrivateField(mc, "turnText", turnT);
            SetPrivateField(mc, "activePlayerText", activeT);
            SetPrivateField(mc, "phaseText", phaseT);
            SetPrivateField(mc, "endTurnButton", endTurnBtn.GetComponent<Button>());
            SetPrivateField(mc, "surrenderButton", surrenderBtn.GetComponent<Button>());
            SetPrivateField(mc, "logPanel", logPanel);
            SetPrivateField(mc, "logToggleButton", logToggleBtn.GetComponent<Button>());
            SetPrivateField(mc, "logText", logT);
            SetPrivateField(mc, "turnAnnounceText", turnAnnounce);
            SetPrivateField(mc, "turnClock", turnClock);
            SetPrivateField(mc, "timerText", timerT);
            SetPrivateField(mc, "skillLv2Button", p1Sk2.GetComponent<Button>());
            SetPrivateField(mc, "skillLv3Button", p1Sk3.GetComponent<Button>());
            SetPrivateField(mc, "resultPanel", resultPanel);
            SetPrivateField(mc, "resultText", resultT);
            SetPrivateField(mc, "victoryTypeText", victoryTypeT);
            SetPrivateField(mc, "matchIdText", matchIdT);
            SetPrivateField(mc, "finalHpText", finalHpT);
            SetPrivateField(mc, "rewardGoldText", rewardGoldT);
            SetPrivateField(mc, "rewardBpXpText", rewardBpXpT);
            SetPrivateField(mc, "rewardMissionText", rewardMissionT);
            SetPrivateField(mc, "resultP1HpBar", resP1HpBar);
            SetPrivateField(mc, "resultP2HpBar", resP2HpBar);
            SetPrivateField(mc, "backToLobbyButton", backBtn.GetComponent<Button>());
            SetPrivateField(mc, "playAgainButton", playAgainBtn.GetComponent<Button>());
            SetPrivateField(mc, "viewReplayButton", replayBtn.GetComponent<Button>());
            SetPrivateField(mc, "screenManager", sm);
            SetPrivateField(mc, "mulliganPanel", mulliganPanel);
            SetPrivateField(mc, "mulliganHandParent", mulliganHand.transform);
            SetPrivateField(mc, "mulliganConfirmButton", mulliganConfirmBtn.GetComponent<Button>());
            SetPrivateField(mc, "mulliganSkipButton", mulliganSkipBtn.GetComponent<Button>());

            Debug.Log("Match screen built and wired.");
        }

        // ==================== NAV BAR ====================
        static GameObject MakeNavBar(Transform root)
        {
            var nav = new GameObject("NavigationBar");
            nav.transform.SetParent(root, false);
            var rt = nav.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, 80);

            var bg = nav.AddComponent<Image>();
            bg.color = ColNavBg;

            // Top separator line
            var sepObj = new GameObject("NavSeparator");
            sepObj.transform.SetParent(nav.transform, false);
            var sepRt = sepObj.AddComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(0, 1);
            sepRt.anchorMax = new Vector2(1, 1);
            sepRt.pivot = new Vector2(0.5f, 1);
            sepRt.sizeDelta = new Vector2(0, 2);
            var sepImg = sepObj.AddComponent<Image>();
            sepImg.color = new Color(0.25f, 0.20f, 0.40f, 0.6f);
            sepImg.raycastTarget = false;

            var navCanvas = nav.AddComponent<Canvas>();
            navCanvas.overrideSorting = true;
            navCanvas.sortingOrder = 10;
            nav.AddComponent<GraphicRaycaster>();

            var hlg = nav.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 6;
            hlg.padding = new RectOffset(6, 6, 4, 4);
            hlg.childForceExpandWidth = true;

            string[] labels = { "ホーム", "バトル", "カード", "ストーリー", "ショップ" };
            GameManager.GameScreen[] screens = {
                GameManager.GameScreen.Home,
                GameManager.GameScreen.Battle,
                GameManager.GameScreen.Cards,
                GameManager.GameScreen.Story,
                GameManager.GameScreen.Shop,
            };

            for (int i = 0; i < labels.Length; i++)
            {
                var btnObj = new GameObject($"Nav_{labels[i]}");
                btnObj.transform.SetParent(nav.transform, false);
                var btnBg = btnObj.AddComponent<Image>();
                btnBg.color = ColNavInactive;
                var le = btnObj.AddComponent<LayoutElement>();
                le.flexibleWidth = 1;
                le.minHeight = 60;

                var btn = btnObj.AddComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(ColNavActive.r * 1.1f, ColNavActive.g * 1.1f, ColNavActive.b * 1.1f);
                colors.pressedColor = new Color(ColNavActive.r * 0.8f, ColNavActive.g * 0.8f, ColNavActive.b * 0.8f);
                btn.colors = colors;

                AddTMP(btnObj, labels[i], 17, bold: true);

                int idx = i;
                btn.onClick.AddListener(() =>
                {
                    var mgr = Object.FindFirstObjectByType<ScreenManager>();
                    if (mgr != null) mgr.ShowScreen(screens[idx]);
                });
            }

            return nav;
        }

        // ==================== UTILITIES ====================
        static GameObject MakeScreen(Transform parent, string name, Color bgColor, bool fullHeight = false)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Stretch(obj, 0, fullHeight ? 0 : 0.042f, 1, 1);
            var bg = obj.AddComponent<Image>();
            bg.color = bgColor;
            bg.raycastTarget = false;
            return obj;
        }

        static TextMeshProUGUI Txt(GameObject parent, string text, int size,
            float xMin, float yMin, float xMax, float yMax,
            bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Center,
            Color? color = null)
        {
            var obj = new GameObject("Text");
            obj.transform.SetParent(parent.transform, false);
            Stretch(obj, xMin, yMin, xMax, yMax);
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color ?? Color.white;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            ApplyFont(tmp);
            return tmp;
        }

        static TextMeshProUGUI TxtObj(GameObject parent, string name, string text, int size,
            float xMin, float yMin, float xMax, float yMax,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            Stretch(obj, xMin, yMin, xMax, yMax);
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Color.white;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            ApplyFont(tmp);
            return tmp;
        }

        static TextMeshProUGUI AddTMP(GameObject obj, string text, int size,
            bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Center,
            Color? color = null)
        {
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var trt = textObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(4, 2);
            trt.offsetMax = new Vector2(-4, -2);
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color ?? Color.white;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            ApplyFont(tmp);
            return tmp;
        }

        static GameObject MakeFieldRow(Transform parent, string name, string label)
        {
            var row = new GameObject(name);
            row.transform.SetParent(parent, false);
            row.AddComponent<RectTransform>();
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            labelObj.AddComponent<RectTransform>().sizeDelta = new Vector2(36, 0);
            var le = labelObj.AddComponent<LayoutElement>();
            le.minWidth = 36; le.preferredWidth = 36; le.flexibleWidth = 0;
            var tmp = labelObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 10;
            tmp.color = new Color(0.5f, 0.5f, 0.6f, 0.8f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            ApplyFont(tmp);

            return row;
        }

        static GameObject MakeButton(GameObject parent, string name, string label,
            float xMin, float yMin, float xMax, float yMax, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            Stretch(obj, xMin, yMin, xMax, yMax);
            var bg = obj.AddComponent<Image>();
            bg.color = color;
            var btn = obj.AddComponent<Button>();
            var c = btn.colors;
            var hl = color * 1.2f; hl.a = 1f;
            var pr = color * 0.7f; pr.a = 1f;
            c.highlightedColor = hl;
            c.pressedColor = pr;
            btn.colors = c;
            AddTMP(obj, label, 20, bold: true);

            // Premium button feedback
            obj.AddComponent<ButtonScaleEffect>();

            return obj;
        }

        static void MakeFilterButton(GameObject parent, string label, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject($"Filter_{label}");
            obj.transform.SetParent(parent.transform, false);
            var bg = obj.AddComponent<Image>();
            bg.color = bgColor;
            var le = obj.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.minHeight = 36;
            var btn = obj.AddComponent<Button>();
            var c = btn.colors;
            var hl = bgColor * 1.3f; hl.a = 1f;
            var pr = bgColor * 0.7f; pr.a = 1f;
            c.highlightedColor = hl;
            c.pressedColor = pr;
            btn.colors = c;
            btn.onClick.AddListener(onClick);
            AddTMP(obj, label, 15, bold: true);
        }

        static void Stretch(GameObject obj, float xMin, float yMin, float xMax, float yMax)
        {
            var rt = obj.GetComponent<RectTransform>();
            if (rt == null) rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        static void AddParticles(GameObject screen, Color color, int count)
        {
            var pObj = new GameObject("Particles");
            pObj.transform.SetParent(screen.transform, false);
            Stretch(pObj, 0, 0, 1, 1);
            pObj.AddComponent<RectTransform>();
            var bp = pObj.AddComponent<BackgroundParticles>();
            bp.particleCount = count;
            bp.particleColor = color;
            bp.minSize = 2f;
            bp.maxSize = 10f;
            bp.speed = 12f;
            // Ensure particles don't block input
            var cg = pObj.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        static void AddPulseGlow(GameObject obj, Color colorA, Color colorB, float speed = 1.5f)
        {
            var cp = obj.AddComponent<ColorPulse>();
            cp.colorA = colorA;
            cp.colorB = colorB;
            cp.speed = speed;
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
            else
                Debug.LogWarning($"Field '{fieldName}' not found on {type.Name}");
        }
    }
}
