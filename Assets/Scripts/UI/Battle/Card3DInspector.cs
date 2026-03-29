using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Banganka.Core.Data;
using Banganka.Core.Config;
using Banganka.Audio;

namespace Banganka.UI.Battle
{
    /// <summary>
    /// カード3Dインスペクト表示。
    /// 長押し/タップでカードを拡大表示し、ジャイロ/ドラッグで疑似3D傾き。
    /// Pokemon TCG Pocket / Marvel Snap のカード詳細に相当。
    /// </summary>
    public class Card3DInspector : MonoBehaviour
    {
        Canvas _overlayCanvas;
        GameObject _inspectRoot;
        RectTransform _cardPanel;
        bool _isShowing;

        // Tilt state
        Vector2 _dragStart;
        Vector2 _currentTilt;       // -1 to 1
        bool _isDragging;
        bool _gyroAvailable;

        const float MaxTiltAngle = 15f;
        const float GyroSensitivity = 2f;
        const float DragSensitivity = 0.003f;
        const float TiltReturnSpeed = 5f;

        void Start()
        {
            _gyroAvailable = false; // ジャイロはタッチドラッグで代替
        }

        /// <summary>カード詳細を3D風に表示</summary>
        public void ShowCard(CardData card)
        {
            if (_isShowing) Hide();
            _isShowing = true;
            _currentTilt = Vector2.zero;

            EnsureCanvas();
            BuildInspectUI(card);
        }

        public void Hide()
        {
            if (!_isShowing) return;
            _isShowing = false;
            if (_inspectRoot != null)
                Destroy(_inspectRoot);
            _inspectRoot = null;
            _cardPanel = null;
        }

        void Update()
        {
            if (!_isShowing || _cardPanel == null) return;

            // Gyro input (mobile)
            // ジャイロ無効化済み — タッチドラッグで傾きを制御

            // Touch/mouse drag
            if (Input.GetMouseButtonDown(0))
            {
                _dragStart = Input.mousePosition;
                _isDragging = true;
            }
            else if (Input.GetMouseButton(0) && _isDragging)
            {
                Vector2 delta = (Vector2)Input.mousePosition - _dragStart;
                _currentTilt.x = Mathf.Clamp(_currentTilt.x + delta.x * DragSensitivity, -1f, 1f);
                _currentTilt.y = Mathf.Clamp(_currentTilt.y - delta.y * DragSensitivity, -1f, 1f);
                _dragStart = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }

            // Return to center when not interacting
            if (!_isDragging && !_gyroAvailable)
                _currentTilt = Vector2.Lerp(_currentTilt, Vector2.zero, Time.deltaTime * TiltReturnSpeed);

            // Apply tilt as rotation
            float rotY = _currentTilt.x * MaxTiltAngle;
            float rotX = _currentTilt.y * MaxTiltAngle;
            _cardPanel.localRotation = Quaternion.Euler(rotX, rotY, 0);

            // Holographic highlight shift based on tilt
            UpdateHighlight();
        }

        void EnsureCanvas()
        {
            if (_overlayCanvas != null) return;

            var obj = new GameObject("Card3DCanvas");
            obj.transform.SetParent(transform, false);
            _overlayCanvas = obj.AddComponent<Canvas>();
            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.sortingOrder = 110;
            obj.AddComponent<GraphicRaycaster>();
            var scaler = obj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
        }

        Image _highlightGradient;

        void BuildInspectUI(CardData card)
        {
            _inspectRoot = new GameObject("CardInspect");
            _inspectRoot.transform.SetParent(_overlayCanvas.transform, false);
            var rootRt = _inspectRoot.AddComponent<RectTransform>();
            StretchFull(rootRt);

            // Dark backdrop (tap to dismiss)
            var backdrop = CreateChild(_inspectRoot.transform, "Backdrop");
            var bdImg = backdrop.AddComponent<Image>();
            bdImg.color = new Color(0, 0, 0, 0.7f);
            StretchFull(backdrop.GetComponent<RectTransform>());

            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.onClick.AddListener(Hide);

            // Card panel (the tiltable card)
            var cardObj = CreateChild(_inspectRoot.transform, "CardPanel");
            _cardPanel = cardObj.GetComponent<RectTransform>();
            _cardPanel.anchorMin = new Vector2(0.1f, 0.2f);
            _cardPanel.anchorMax = new Vector2(0.9f, 0.8f);
            _cardPanel.offsetMin = Vector2.zero;
            _cardPanel.offsetMax = Vector2.zero;

            Color aspectColor = AspectColors.GetColor(card.aspect);

            // Card background
            var cardBg = cardObj.AddComponent<Image>();
            cardBg.color = new Color(0.12f, 0.12f, 0.18f, 1f);

            // Aspect accent bar
            var accent = CreateChild(cardObj.transform, "Accent");
            var acRt = accent.GetComponent<RectTransform>();
            acRt.anchorMin = new Vector2(0, 0.92f);
            acRt.anchorMax = Vector2.one;
            acRt.offsetMin = Vector2.zero;
            acRt.offsetMax = Vector2.zero;
            var acImg = accent.AddComponent<Image>();
            acImg.color = aspectColor;

            // Card illustration
            var illObj = CreateChild(cardObj.transform, "Illustration");
            var illRt = illObj.GetComponent<RectTransform>();
            illRt.anchorMin = new Vector2(0.05f, 0.35f);
            illRt.anchorMax = new Vector2(0.95f, 0.9f);
            illRt.offsetMin = Vector2.zero;
            illRt.offsetMax = Vector2.zero;
            var illImg = illObj.AddComponent<Image>();
            illImg.raycastTarget = false;
            if (!string.IsNullOrEmpty(card.illustrationId))
            {
                var sprite = Resources.Load<Sprite>($"CardIllustrations/{card.illustrationId}");
                if (sprite != null)
                {
                    illImg.sprite = sprite;
                    illImg.preserveAspect = true;
                }
                else
                {
                    illImg.color = new Color(aspectColor.r * 0.3f, aspectColor.g * 0.3f, aspectColor.b * 0.3f, 0.5f);
                }
            }

            // Card name
            var nameObj = CreateChild(cardObj.transform, "Name");
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.05f, 0.25f);
            nameRt.anchorMax = new Vector2(0.95f, 0.35f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = card.cardName;
            nameTmp.fontSize = 32;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.Center;

            // Stats line
            var statsObj = CreateChild(cardObj.transform, "Stats");
            var statsRt = statsObj.GetComponent<RectTransform>();
            statsRt.anchorMin = new Vector2(0.05f, 0.15f);
            statsRt.anchorMax = new Vector2(0.95f, 0.25f);
            statsRt.offsetMin = Vector2.zero;
            statsRt.offsetMax = Vector2.zero;
            var statsTmp = statsObj.AddComponent<TextMeshProUGUI>();
            string statsLine = card.type switch
            {
                CardType.Manifest => $"CP:{card.cpCost}  戦力:{card.battlePower}  願力:{card.wishDamage}%",
                CardType.Spell => $"CP:{card.cpCost}  詠術",
                CardType.Algorithm => $"CP:{card.cpCost}  界律",
                _ => $"CP:{card.cpCost}"
            };
            statsTmp.text = statsLine;
            statsTmp.fontSize = 20;
            statsTmp.color = new Color(0.8f, 0.8f, 0.8f);
            statsTmp.alignment = TextAlignmentOptions.Center;

            // Flavor text
            if (!string.IsNullOrEmpty(card.flavorText))
            {
                var flavorObj = CreateChild(cardObj.transform, "Flavor");
                var flavorRt = flavorObj.GetComponent<RectTransform>();
                flavorRt.anchorMin = new Vector2(0.08f, 0.03f);
                flavorRt.anchorMax = new Vector2(0.92f, 0.15f);
                flavorRt.offsetMin = Vector2.zero;
                flavorRt.offsetMax = Vector2.zero;
                var flavorTmp = flavorObj.AddComponent<TextMeshProUGUI>();
                flavorTmp.text = card.flavorText;
                flavorTmp.fontSize = 14;
                flavorTmp.fontStyle = FontStyles.Italic;
                flavorTmp.color = new Color(0.6f, 0.6f, 0.65f);
                flavorTmp.alignment = TextAlignmentOptions.Center;
            }

            // Holographic highlight overlay (shifts with tilt)
            var hlObj = CreateChild(cardObj.transform, "Highlight");
            var hlRt = hlObj.GetComponent<RectTransform>();
            StretchFull(hlRt);
            _highlightGradient = hlObj.AddComponent<Image>();
            _highlightGradient.color = new Color(1f, 1f, 1f, 0f);
            _highlightGradient.raycastTarget = false;

            SoundManager.Instance?.PlayUISE("se_card_lift");
        }

        void UpdateHighlight()
        {
            if (_highlightGradient == null) return;

            // Simulate holographic sheen: alpha shifts based on tilt magnitude
            float tiltMag = _currentTilt.magnitude;
            float alpha = tiltMag * 0.12f; // Subtle
            float hue = (_currentTilt.x + 1f) * 0.5f; // 0-1 mapped from tilt

            // Rainbow-ish tint shift
            Color hlColor = Color.HSVToRGB(hue * 0.3f + 0.1f, 0.3f, 1f);
            hlColor.a = alpha;
            _highlightGradient.color = hlColor;
        }

        static GameObject CreateChild(Transform parent, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
