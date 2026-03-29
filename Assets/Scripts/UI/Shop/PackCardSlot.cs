using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Banganka.Audio;
using Banganka.Core.Config;
using Banganka.Core.Data;
using Banganka.Core.Economy;
using Banganka.Core.Feedback;

namespace Banganka.UI.Shop
{
    /// <summary>
    /// パック開封時の個別カードスロット (PACK_OPENING_SPEC.md §5)
    /// 裏面→表面のフリップアニメーションとレアリティ別演出を管理
    /// </summary>
    public class PackCardSlot : MonoBehaviour
    {
        // UI参照
        RectTransform _rt;
        Image _backImage;
        CanvasGroup _cardGroup;
        GameObject _cardFront;
        Image _flashOverlay;
        Image _pillarEffect;
        TextMeshProUGUI _newBadge;

        // 状態
        PackCardEntry _entry;
        bool _isRevealed;
        bool _isInteractable;
        Action<PackCardSlot> _onRevealed;

        float DurationScale => AccessibilitySettings.ReduceMotion ? 0.25f : 1f;

        public bool IsRevealed => _isRevealed;
        public PackCardEntry Entry => _entry;

        /// <summary>スロットUIを構築</summary>
        public void Initialize(PackCardEntry entry, Action<PackCardSlot> onRevealed)
        {
            _entry = entry;
            _onRevealed = onRevealed;
            _rt = GetComponent<RectTransform>();

            // カード裏面
            var backObj = new GameObject("Back");
            backObj.transform.SetParent(transform, false);
            _backImage = backObj.AddComponent<Image>();
            _backImage.color = new Color(0.15f, 0.12f, 0.25f);
            var backRt = backObj.GetComponent<RectTransform>();
            backRt.anchorMin = Vector2.zero;
            backRt.anchorMax = Vector2.one;
            backRt.sizeDelta = Vector2.zero;

            // カード裏面の模様（中央線）
            var patternObj = new GameObject("Pattern");
            patternObj.transform.SetParent(backObj.transform, false);
            var patternImg = patternObj.AddComponent<Image>();
            patternImg.color = new Color(0.3f, 0.25f, 0.5f, 0.6f);
            var patRt = patternObj.GetComponent<RectTransform>();
            patRt.anchorMin = new Vector2(0.2f, 0.1f);
            patRt.anchorMax = new Vector2(0.8f, 0.9f);
            patRt.sizeDelta = Vector2.zero;

            // カード表面（初期非表示）
            _cardFront = new GameObject("Front");
            _cardFront.transform.SetParent(transform, false);
            var frontRt = _cardFront.AddComponent<RectTransform>();
            frontRt.anchorMin = Vector2.zero;
            frontRt.anchorMax = Vector2.one;
            frontRt.sizeDelta = Vector2.zero;
            _cardGroup = _cardFront.AddComponent<CanvasGroup>();
            _cardGroup.alpha = 0f;

            // カード表面の背景色（アスペクト色）
            var frontBg = _cardFront.AddComponent<Image>();
            frontBg.color = GetAspectColor(entry.card.aspect);

            // カード名
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(_cardFront.transform, false);
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = entry.card.cardName;
            nameTmp.fontSize = 11;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = Color.white;
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.05f, 0.7f);
            nameRt.anchorMax = new Vector2(0.95f, 0.9f);
            nameRt.sizeDelta = Vector2.zero;

            // レアリティ表示
            var rarityObj = new GameObject("Rarity");
            rarityObj.transform.SetParent(_cardFront.transform, false);
            var rarityTmp = rarityObj.AddComponent<TextMeshProUGUI>();
            rarityTmp.text = entry.rarity;
            rarityTmp.fontSize = 14;
            rarityTmp.fontStyle = FontStyles.Bold;
            rarityTmp.alignment = TextAlignmentOptions.Center;
            rarityTmp.color = GetRarityColor(entry.rarity);
            var rarRt = rarityObj.GetComponent<RectTransform>();
            rarRt.anchorMin = new Vector2(0.1f, 0.4f);
            rarRt.anchorMax = new Vector2(0.9f, 0.6f);
            rarRt.sizeDelta = Vector2.zero;

            // フラッシュオーバーレイ（演出用、初期透明）
            var flashObj = new GameObject("Flash");
            flashObj.transform.SetParent(transform, false);
            _flashOverlay = flashObj.AddComponent<Image>();
            _flashOverlay.color = new Color(1f, 1f, 1f, 0f);
            _flashOverlay.raycastTarget = false;
            var flashRt = flashObj.GetComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.sizeDelta = Vector2.zero;

            // 光柱エフェクト（R以上で使用、初期非表示）
            var pillarObj = new GameObject("Pillar");
            pillarObj.transform.SetParent(transform, false);
            _pillarEffect = pillarObj.AddComponent<Image>();
            _pillarEffect.color = new Color(0.3f, 0.64f, 1f, 0f);
            _pillarEffect.raycastTarget = false;
            var pillarRt = pillarObj.GetComponent<RectTransform>();
            pillarRt.anchorMin = new Vector2(0.3f, -0.5f);
            pillarRt.anchorMax = new Vector2(0.7f, 1.5f);
            pillarRt.sizeDelta = Vector2.zero;
            pillarObj.SetActive(false);

            // NEWバッジ（初期非表示）
            var badgeObj = new GameObject("NewBadge");
            badgeObj.transform.SetParent(transform, false);
            _newBadge = badgeObj.AddComponent<TextMeshProUGUI>();
            _newBadge.text = "NEW!";
            _newBadge.fontSize = 10;
            _newBadge.fontStyle = FontStyles.Bold;
            _newBadge.alignment = TextAlignmentOptions.Center;
            _newBadge.color = new Color(1f, 0.85f, 0.2f);
            var badgeRt = badgeObj.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.55f, 0.85f);
            badgeRt.anchorMax = new Vector2(0.95f, 0.98f);
            badgeRt.sizeDelta = Vector2.zero;
            badgeObj.SetActive(false);

            // 重複表示
            if (entry.isDuplicate)
            {
                var dupObj = new GameObject("DupGold");
                dupObj.transform.SetParent(_cardFront.transform, false);
                var dupTmp = dupObj.AddComponent<TextMeshProUGUI>();
                dupTmp.text = $"+{entry.goldConverted}G";
                dupTmp.fontSize = 10;
                dupTmp.fontStyle = FontStyles.Bold;
                dupTmp.alignment = TextAlignmentOptions.Center;
                dupTmp.color = new Color(1f, 0.82f, 0.2f);
                var dupRt = dupObj.GetComponent<RectTransform>();
                dupRt.anchorMin = new Vector2(0.1f, 0.05f);
                dupRt.anchorMax = new Vector2(0.9f, 0.2f);
                dupRt.sizeDelta = Vector2.zero;
            }

            _cardFront.SetActive(false);
        }

        /// <summary>タップ入力を有効化</summary>
        public void SetInteractable(bool interactable)
        {
            _isInteractable = interactable;
        }

        void Update()
        {
            if (!_isInteractable || _isRevealed) return;

            // タップ検出
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rt, Input.mousePosition, null, out localPoint);
                if (_rt.rect.Contains(localPoint))
                {
                    _isInteractable = false;
                    StartCoroutine(FlipAndReveal());
                }
            }
        }

        /// <summary>カードフリップ + レアリティ演出 (PACK_OPENING_SPEC.md §5.2)</summary>
        IEnumerator FlipAndReveal()
        {
            float flipDuration = 0.4f * DurationScale;

            // SE再生
            string seId = _entry.rarity == "SSR" ? "se_card_flip_ssr" : "se_card_flip";
            SoundManager.Instance?.PlaySE(seId);

            // 裏面を縮小（Xスケール0へ）
            transform.DOScaleX(0f, flipDuration * 0.5f).SetEase(Ease.InQuad);
            yield return new WaitForSeconds(flipDuration * 0.5f);

            // 裏面→表面に切り替え
            _backImage.gameObject.SetActive(false);
            _cardFront.SetActive(true);
            _cardGroup.alpha = 1f;

            // 表面を展開（Xスケール0→1）
            transform.DOScaleX(1f, flipDuration * 0.5f).SetEase(Ease.OutQuad);
            yield return new WaitForSeconds(flipDuration * 0.5f);

            // レアリティ別演出
            yield return StartCoroutine(PlayRarityEffect());

            // NEWバッジ表示
            if (_entry.isNew)
            {
                _newBadge.gameObject.SetActive(true);
                _newBadge.transform.localScale = Vector3.zero;
                _newBadge.transform.DOScale(Vector3.one, 0.2f * DurationScale)
                    .SetEase(Ease.OutBack);
            }

            _isRevealed = true;
            _onRevealed?.Invoke(this);
        }

        /// <summary>レアリティ別の視覚演出</summary>
        IEnumerator PlayRarityEffect()
        {
            switch (_entry.rarity)
            {
                case "C":
                    yield return PlayCommonEffect();
                    break;
                case "R":
                    yield return PlayRareEffect();
                    break;
                case "SR":
                    yield return PlaySuperRareEffect();
                    break;
                case "SSR":
                    // SSRは神引き演出に委譲（PackOpeningControllerが処理）
                    yield return PlaySuperRareEffect(); // SR演出をベースに再生
                    break;
            }
        }

        /// <summary>C: 白い光のフラッシュ (0.3s)</summary>
        IEnumerator PlayCommonEffect()
        {
            float d = 0.3f * DurationScale;
            _flashOverlay.color = new Color(1f, 1f, 1f, 0f);
            _flashOverlay.DOFade(0.3f, d * 0.4f);
            yield return new WaitForSeconds(d * 0.4f);
            _flashOverlay.DOFade(0f, d * 0.6f);
            yield return new WaitForSeconds(d * 0.6f);
        }

        /// <summary>R: 青い光柱 + ハプティクス (0.5s)</summary>
        IEnumerator PlayRareEffect()
        {
            float d = 0.5f * DurationScale;

            // 青い光柱
            _pillarEffect.gameObject.SetActive(true);
            _pillarEffect.color = new Color(0.3f, 0.64f, 1f, 0f); // #4DA3FF
            _pillarEffect.DOFade(0.6f, d * 0.3f);

            // フラッシュ
            _flashOverlay.color = new Color(0.3f, 0.64f, 1f, 0f);
            _flashOverlay.DOFade(0.2f, d * 0.3f);

            yield return new WaitForSeconds(d * 0.5f);

            _pillarEffect.DOFade(0f, d * 0.5f);
            _flashOverlay.DOFade(0f, d * 0.5f);

            HapticService.Trigger(HapticService.HapticType.Medium);

            yield return new WaitForSeconds(d * 0.5f);
            _pillarEffect.gameObject.SetActive(false);
        }

        /// <summary>SR: 金色爆発 + パンチスケール + ハプティクス (1.0s)</summary>
        IEnumerator PlaySuperRareEffect()
        {
            float d = 1.0f * DurationScale;

            // 金色フラッシュ
            _flashOverlay.color = new Color(0.83f, 0.66f, 0.26f, 0f); // #D4A843
            _flashOverlay.DOFade(0.5f, d * 0.2f);

            // 光柱（金色）
            _pillarEffect.gameObject.SetActive(true);
            _pillarEffect.color = new Color(0.83f, 0.66f, 0.26f, 0f);
            _pillarEffect.DOFade(0.8f, d * 0.2f);

            // パンチスケール
            transform.DOPunchScale(Vector3.one * 0.15f, d * 0.4f, 6, 0.5f);

            // 画面揺れ（親のRectTransform）
            var parentRt = transform.parent?.GetComponent<RectTransform>();
            if (parentRt != null)
                parentRt.DOShakeAnchorPos(d * 0.3f, 4f, 10, 90, false, true);

            yield return new WaitForSeconds(d * 0.4f);

            _flashOverlay.DOFade(0f, d * 0.6f);
            _pillarEffect.DOFade(0f, d * 0.6f);

            HapticService.CardRevealSR();

            yield return new WaitForSeconds(d * 0.6f);
            _pillarEffect.gameObject.SetActive(false);
        }

        static Color GetAspectColor(Aspect aspect) => aspect switch
        {
            Aspect.Contest  => new Color(1f, 0.35f, 0.21f),    // #FF5A36
            Aspect.Whisper  => new Color(0.3f, 0.64f, 1f),     // #4DA3FF
            Aspect.Weave    => new Color(0.35f, 0.76f, 0.42f),  // #59C36A
            Aspect.Verse    => new Color(0.6f, 0.36f, 1f),     // #9A5BFF
            Aspect.Manifest => new Color(0.96f, 0.77f, 0.26f), // #F4C542
            Aspect.Hush     => new Color(0.23f, 0.23f, 0.27f), // #3A3A46
            _ => Color.gray
        };

        static Color GetRarityColor(string rarity) => rarity switch
        {
            "SSR" => new Color(1f, 0.85f, 0.2f),     // 金色
            "SR"  => new Color(0.83f, 0.66f, 0.26f),  // ゴールド
            "R"   => new Color(0.3f, 0.64f, 1f),      // 青
            _     => Color.white
        };
    }
}
