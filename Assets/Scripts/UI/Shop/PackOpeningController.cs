using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Banganka.Audio;
using Banganka.Core.Config;
using Banganka.Core.Data;
using Banganka.Core.Economy;
using Banganka.Core.Feedback;
using Banganka.UI.Tween;

namespace Banganka.UI.Shop
{
    /// <summary>
    /// パック開封演出コントローラー (PACK_OPENING_SPEC.md §4-§6, §10)
    /// 全フェーズ: 予兆 → 開封 → 扇形展開 → カードめくり → (SSR神引き) → 結果画面
    /// </summary>
    public class PackOpeningController : MonoBehaviour
    {
        // UI要素（AutoBootstrapから注入）
        [NonSerialized] public RectTransform packImage;
        [NonSerialized] public RectTransform cardArea;
        [NonSerialized] public RectTransform resultPanel;
        [NonSerialized] public TextMeshProUGUI goldConvertedText;
        [NonSerialized] public CanvasGroup overlayGroup;
        [NonSerialized] public Image overlayImage;
        [NonSerialized] public Button closeButton;
        [NonSerialized] public TextMeshProUGUI pityCounterText;

        // 状態
        PackOpenResult _currentResult;
        List<PackCardSlot> _slots = new();
        int _revealedCount;
        bool _isPlaying;
        Action _onComplete;

        float DurationScale => AccessibilitySettings.ReduceMotion ? 0.25f : 1f;

        /// <summary>パック開封演出を開始</summary>
        public void StartPackOpening(PackOpenResult result, Action onComplete = null)
        {
            if (_isPlaying) return;
            _isPlaying = true;
            _currentResult = result;
            _onComplete = onComplete;
            _revealedCount = 0;

            gameObject.SetActive(true);
            overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = true;
            resultPanel.gameObject.SetActive(false);

            StartCoroutine(PackOpenSequence());
        }

        IEnumerator PackOpenSequence()
        {
            // フェーズ1: 画面フェードイン
            overlayGroup.DOFade(1f, 0.2f * DurationScale);
            yield return new WaitForSeconds(0.2f * DurationScale);

            // フェーズ2: パック予兆 (PACK_OPENING_SPEC.md §3 — 簡略版)
            yield return StartCoroutine(PlayOmenPhase());

            // フェーズ3: パック開封 (§4)
            yield return StartCoroutine(PlayOpeningPhase());

            // フェーズ4: カードめくり待ち (§5)
            // ユーザーのタップで1枚ずつめくる — Update内のPackCardSlotが処理
            EnableCardTapping();

            // めくり完了を待つ
            while (_revealedCount < _currentResult.cards.Count)
                yield return null;

            // 全カード開示後、少し間を置く
            yield return new WaitForSeconds(0.5f * DurationScale);

            // フェーズ5: 結果画面 (§10)
            ShowResultScreen();
        }

        /// <summary>パック予兆 — 最高レアリティに応じた背景グロー (§3簡略版)</summary>
        IEnumerator PlayOmenPhase()
        {
            packImage.gameObject.SetActive(true);
            packImage.localScale = Vector3.one;

            string maxRarity = _currentResult.MaxRarity;
            Color glowColor = maxRarity switch
            {
                "SSR" => new Color(1f, 0.85f, 0.2f, 0.3f),    // 虹色（金で代替）
                "SR"  => new Color(0.83f, 0.66f, 0.26f, 0.2f), // 金
                "R"   => new Color(0.3f, 0.64f, 1f, 0.15f),    // 青
                _     => new Color(1f, 1f, 1f, 0.1f)            // 白
            };

            // パック画像の背景グロー
            overlayImage.color = new Color(0.05f, 0.05f, 0.1f, 1f);

            // SSR/SR: 予兆演出が長め + パルス強い
            float omenDuration = maxRarity switch
            {
                "SSR" => 1.6f * DurationScale,
                "SR" => 1.2f * DurationScale,
                _ => 0.8f * DurationScale
            };

            int pulseCount = maxRarity switch { "SSR" => 3, "SR" => 2, _ => 1 };

            // パルスアニメーション + グロー
            for (int p = 0; p < pulseCount; p++)
            {
                float pulseDur = omenDuration / pulseCount;
                float intensity = (float)(p + 1) / pulseCount; // 段階的に強く

                overlayImage.DOColor(new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * intensity),
                    pulseDur * 0.4f);
                packImage.DOScale(1f + 0.05f * intensity, pulseDur * 0.5f).SetEase(Ease.InOutSine);

                if (p == 0)
                    HapticService.Trigger(HapticService.HapticType.Light);
                else if (p == pulseCount - 1 && maxRarity is "SSR" or "SR")
                    HapticService.Trigger(HapticService.HapticType.Medium);

                yield return new WaitForSeconds(pulseDur * 0.5f);

                overlayImage.DOColor(new Color(0.05f, 0.05f, 0.1f, 1f), pulseDur * 0.5f);
                packImage.DOScale(1.0f, pulseDur * 0.5f).SetEase(Ease.InOutSine);
                yield return new WaitForSeconds(pulseDur * 0.5f);
            }

            overlayImage.color = new Color(0.05f, 0.05f, 0.1f, 1f);
        }

        /// <summary>パック開封 — スワイプで引き裂き + 5枚扇形展開 (§4)</summary>
        IEnumerator PlayOpeningPhase()
        {
            float d = 1.0f * DurationScale;

            // スワイプ待ち: 上方向スワイプでパックを引き裂く
            yield return StartCoroutine(WaitForSwipeTear());

            // パック画像フェードアウト
            var packImg = packImage.GetComponent<Image>();
            if (packImg != null)
                packImg.DOFade(0f, d * 0.2f);
            packImage.DOScale(1.3f, d * 0.2f);
            HapticService.PackTear();
            yield return new WaitForSeconds(d * 0.2f);
            packImage.gameObject.SetActive(false);

            // 5枚のカードスロットを生成して扇形に展開
            CreateCardSlots();

            // 扇形展開アニメーション
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                var rt = slot.GetComponent<RectTransform>();

                // 扇形の位置計算 (-45° ~ +45°)
                float angle = -45f + (90f / (_slots.Count - 1)) * i;
                float rad = angle * Mathf.Deg2Rad;
                float radius = 120f;
                Vector2 targetPos = new Vector2(
                    Mathf.Sin(rad) * radius,
                    Mathf.Cos(rad) * radius * 0.3f - 20f
                );

                // 初期状態: 中央に重ねて配置、スケール0
                rt.anchoredPosition = Vector2.zero;
                slot.transform.localScale = Vector3.zero;
                slot.transform.localRotation = Quaternion.Euler(0, 0, -angle * 0.5f);

                // 展開アニメーション（スタガー）
                float delay = i * 0.06f * DurationScale;
                rt.DOAnchorPos(targetPos, d * 0.4f)
                    .SetEase(Ease.OutBack)
                    .SetDelay(delay);
                slot.transform.DOScale(Vector3.one, d * 0.4f)
                    .SetEase(Ease.OutBack)
                    .SetDelay(delay);
            }

            yield return new WaitForSeconds(d * 0.5f + _slots.Count * 0.06f * DurationScale);
        }

        /// <summary>
        /// スワイプで引き裂きジェスチャー。
        /// 上方向に一定距離スワイプするとパックが裂ける。
        /// ReduceMotion時は即座にスキップ。
        /// </summary>
        IEnumerator WaitForSwipeTear()
        {
            if (AccessibilitySettings.ReduceMotion)
            {
                // ReduceMotion: スワイプ不要、即開封
                SoundManager.Instance?.PlaySE("se_pack_open");
                yield break;
            }

            // ヒントテキスト
            var hintObj = new GameObject("SwipeHint");
            hintObj.transform.SetParent(packImage, false);
            var hintRt = hintObj.AddComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.1f, -0.2f);
            hintRt.anchorMax = new Vector2(0.9f, -0.05f);
            hintRt.offsetMin = Vector2.zero;
            hintRt.offsetMax = Vector2.zero;
            var hintTmp = hintObj.AddComponent<TextMeshProUGUI>();
            hintTmp.text = "↑ スワイプで開封";
            hintTmp.fontSize = 16;
            hintTmp.color = new Color(1f, 1f, 1f, 0.6f);
            hintTmp.alignment = TextAlignmentOptions.Center;

            const float tearThreshold = 120f; // スワイプ距離の閾値
            bool tornOpen = false;
            Vector2 swipeStart = Vector2.zero;
            bool tracking = false;
            float totalSwipeY = 0f;
            float autoTearTimer = 5f; // 5秒後に自動開封

            while (!tornOpen)
            {
                autoTearTimer -= Time.deltaTime;
                if (autoTearTimer <= 0f)
                {
                    tornOpen = true;
                    break;
                }

                if (Input.GetMouseButtonDown(0))
                {
                    swipeStart = Input.mousePosition;
                    tracking = true;
                    totalSwipeY = 0f;
                }
                else if (Input.GetMouseButton(0) && tracking)
                {
                    Vector2 current = Input.mousePosition;
                    float deltaY = current.y - swipeStart.y;
                    totalSwipeY = Mathf.Max(totalSwipeY, deltaY);

                    // パックの傾き/変形フィードバック
                    float progress = Mathf.Clamp01(totalSwipeY / tearThreshold);
                    packImage.localRotation = Quaternion.Euler(0, 0, progress * -5f);
                    packImage.localScale = Vector3.one * (1f + progress * 0.1f);

                    // 段階的ハプティクス
                    if (progress > 0.3f && progress < 0.35f)
                        HapticService.Trigger(HapticService.HapticType.Light);
                    else if (progress > 0.6f && progress < 0.65f)
                        HapticService.Trigger(HapticService.HapticType.Medium);

                    if (totalSwipeY >= tearThreshold)
                    {
                        tornOpen = true;
                        HapticService.PackTear();
                    }
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    tracking = false;
                    // スプリングバック
                    packImage.DOLocalRotate(Vector3.zero, 0.2f);
                    packImage.DOScale(1f, 0.2f);
                }

                yield return null;
            }

            Destroy(hintObj);

            // 裂け演出: シェイク + SE
            SoundManager.Instance?.PlaySE("se_pack_open");
            packImage.DOShakeAnchorPos(0.3f * DurationScale, 12f, 18, 90, false, true);
            packImage.localRotation = Quaternion.identity;
            yield return new WaitForSeconds(0.3f * DurationScale);
        }

        void CreateCardSlots()
        {
            // 既存スロットをクリア
            foreach (var s in _slots)
                if (s != null) Destroy(s.gameObject);
            _slots.Clear();

            foreach (var entry in _currentResult.cards)
            {
                var slotObj = new GameObject($"CardSlot_{entry.slotIndex}");
                slotObj.transform.SetParent(cardArea, false);

                var rt = slotObj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(77f, 108f); // カードサイズ (64×90 の 1.2倍)

                var slot = slotObj.AddComponent<PackCardSlot>();
                slot.Initialize(entry, OnCardRevealed);
                _slots.Add(slot);
            }
        }

        void EnableCardTapping()
        {
            // 左から順に1枚ずつタップ可能にする
            if (_slots.Count > 0)
                _slots[0].SetInteractable(true);
        }

        void OnCardRevealed(PackCardSlot slot)
        {
            _revealedCount++;

            // SSR: 神引き演出を割り込み (§6)
            if (slot.Entry.rarity == "SSR")
            {
                StartCoroutine(PlaySSRDivineAnimation(slot));
                return;
            }

            // 次のカードをタップ可能に
            ActivateNextSlot();
        }

        void ActivateNextSlot()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsRevealed)
                {
                    _slots[i].SetInteractable(true);
                    return;
                }
            }
        }

        /// <summary>SSR神引き演出 (PACK_OPENING_SPEC.md §6)</summary>
        IEnumerator PlaySSRDivineAnimation(PackCardSlot slot)
        {
            // フェーズ1: 画面暗転 (0.3s)
            var darkOverlay = CreateTempOverlay(new Color(0, 0, 0, 0));
            var darkCg = darkOverlay.AddComponent<CanvasGroup>();
            darkCg.DOFade(1f, 0.3f * DurationScale);
            yield return new WaitForSeconds(0.3f * DurationScale);

            // フェーズ2: 虹色オーラ爆発 (0.6s)
            var auraObj = CreateTempOverlay(new Color(1f, 0.85f, 0.2f, 0f));
            var auraImg = auraObj.GetComponent<Image>();
            auraObj.transform.localScale = Vector3.zero;

            auraImg.DOFade(0.7f, 0.2f * DurationScale);
            auraObj.transform.DOScale(1.5f, 0.6f * DurationScale).SetEase(Ease.OutBack);

            // 画面揺れ
            if (cardArea != null)
                cardArea.DOShakeAnchorPos(0.4f * DurationScale, 8f, 12, 90, false, true);

            SoundManager.Instance?.PlaySE("se_card_flip_ssr");

            // ハプティクス連打 (4パルス)
            HapticService.CardRevealSSR();
            yield return new WaitForSeconds(0.4f * DurationScale);

            yield return new WaitForSeconds(0.2f * DurationScale);

            // フェーズ3: カードイラスト全画面表示 (2.0s)
            // MVP: 静止画 + アスペクトカラー背景
            var cardData = slot.Entry.card;
            Color aspectBg = GetAspectBgColor(cardData.aspect);
            auraImg.DOColor(new Color(aspectBg.r, aspectBg.g, aspectBg.b, 0.8f), 0.5f * DurationScale);

            // カード名を中央に大きく表示
            var nameObj = new GameObject("SSRName");
            nameObj.transform.SetParent(overlayGroup.transform, false);
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = cardData.cardName;
            nameTmp.fontSize = 32;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = new Color(1f, 0.92f, 0.6f);
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.1f, 0.35f);
            nameRt.anchorMax = new Vector2(0.9f, 0.55f);
            nameRt.sizeDelta = Vector2.zero;
            nameObj.transform.localScale = Vector3.zero;
            nameObj.transform.DOScale(1f, 0.5f * DurationScale).SetEase(Ease.OutBack);

            // レアリティ表示
            var rarObj = new GameObject("SSRRarity");
            rarObj.transform.SetParent(overlayGroup.transform, false);
            var rarTmp = rarObj.AddComponent<TextMeshProUGUI>();
            rarTmp.text = "- SSR -";
            rarTmp.fontSize = 18;
            rarTmp.alignment = TextAlignmentOptions.Center;
            rarTmp.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            var rarRt = rarObj.GetComponent<RectTransform>();
            rarRt.anchorMin = new Vector2(0.2f, 0.28f);
            rarRt.anchorMax = new Vector2(0.8f, 0.35f);
            rarRt.sizeDelta = Vector2.zero;

            yield return new WaitForSeconds(2.0f * DurationScale);

            // フェーズ5: スクリーンショットタイム (2.0s — ReduceMotionなら0.5s)
            float screenshotTime = AccessibilitySettings.ReduceMotion ? 0.5f : 2.0f;
            yield return new WaitForSeconds(screenshotTime);

            // フェーズ6: クリーンアップ + フェードアウト (0.5s)
            darkCg.DOFade(0f, 0.5f * DurationScale);
            auraImg.DOFade(0f, 0.3f * DurationScale);
            nameTmp.DOFade(0f, 0.3f * DurationScale);
            rarTmp.DOFade(0f, 0.3f * DurationScale);

            yield return new WaitForSeconds(0.5f * DurationScale);

            Destroy(darkOverlay);
            Destroy(auraObj);
            Destroy(nameObj);
            Destroy(rarObj);

            // 次のカードへ
            ActivateNextSlot();
        }

        /// <summary>結果画面を表示 (§10)</summary>
        void ShowResultScreen()
        {
            resultPanel.gameObject.SetActive(true);
            resultPanel.localScale = Vector3.zero;
            resultPanel.DOScale(1f, 0.3f * DurationScale).SetEase(Ease.OutBack);

            // ゴールド変換表示
            if (_currentResult.totalGoldConverted > 0 && goldConvertedText != null)
            {
                goldConvertedText.gameObject.SetActive(true);
                goldConvertedText.text = "0";
                // NumberTickアニメーション
                UITweenAnimations.NumberTick(
                    goldConvertedText,
                    0,
                    _currentResult.totalGoldConverted,
                    0.5f * DurationScale
                );
            }
            else if (goldConvertedText != null)
            {
                goldConvertedText.gameObject.SetActive(false);
            }

            // 天井カウンター更新
            if (pityCounterText != null)
            {
                int remaining = PackSystem.PacksUntilPity;
                pityCounterText.text = $"SSR確定まで あと{remaining}パック";
            }

            // 閉じるボタン
            if (closeButton != null)
            {
                closeButton.gameObject.SetActive(true);
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
        }

        /// <summary>演出を閉じて完了</summary>
        public void Close()
        {
            overlayGroup.DOFade(0f, 0.2f * DurationScale).OnComplete(() =>
            {
                // スロットをクリーンアップ
                foreach (var s in _slots)
                    if (s != null) Destroy(s.gameObject);
                _slots.Clear();

                resultPanel.gameObject.SetActive(false);
                overlayGroup.blocksRaycasts = false;
                gameObject.SetActive(false);
                _isPlaying = false;
                _onComplete?.Invoke();
            });
        }

        GameObject CreateTempOverlay(Color color)
        {
            var obj = new GameObject("TempOverlay");
            obj.transform.SetParent(overlayGroup.transform, false);
            var img = obj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            return obj;
        }

        static Color GetAspectBgColor(Aspect aspect) => aspect switch
        {
            Aspect.Contest  => new Color(0.4f, 0.1f, 0.05f),
            Aspect.Whisper  => new Color(0.05f, 0.15f, 0.4f),
            Aspect.Weave    => new Color(0.05f, 0.25f, 0.1f),
            Aspect.Verse    => new Color(0.2f, 0.05f, 0.35f),
            Aspect.Manifest => new Color(0.35f, 0.25f, 0.05f),
            Aspect.Hush     => new Color(0.1f, 0.1f, 0.12f),
            _ => new Color(0.05f, 0.05f, 0.1f)
        };
    }
}
