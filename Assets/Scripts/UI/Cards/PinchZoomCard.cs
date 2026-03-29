using UnityEngine;
using DG.Tweening;

namespace Banganka.UI.Cards
{
    /// <summary>
    /// Handles pinch-to-zoom gesture on card detail image.
    /// Zooms from 1x to 3x, with pan when zoomed.
    /// </summary>
    public class PinchZoomCard : MonoBehaviour
    {
        [SerializeField] RectTransform cardImage;
        [SerializeField] float minScale = 1f;
        [SerializeField] float maxScale = 3f;
        [SerializeField] float zoomSpeed = 0.01f;
        [SerializeField] float resetDuration = 0.3f;

        float _currentScale = 1f;
        Vector2 _lastPinchDistance;
        bool _isPinching;
        Vector2 _panOffset;
        Vector2 _lastSingleTouchPos;
        bool _isDragging;

        void Update()
        {
            if (cardImage == null)
                return;

            int touchCount = Input.touchCount;

            if (touchCount == 2)
            {
                HandlePinchZoom();
                _isDragging = false;
            }
            else if (touchCount == 1 && _currentScale > minScale)
            {
                HandlePan();
                _isPinching = false;
            }
            else
            {
                _isPinching = false;
                _isDragging = false;
            }
        }

        void HandlePinchZoom()
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            Vector2 currentDistance = touch0.position - touch1.position;
            float currentMagnitude = currentDistance.magnitude;

            if (!_isPinching)
            {
                _isPinching = true;
                _lastPinchDistance = currentDistance;
                return;
            }

            float lastMagnitude = _lastPinchDistance.magnitude;
            float deltaMagnitude = currentMagnitude - lastMagnitude;

            _currentScale += deltaMagnitude * zoomSpeed;
            _currentScale = Mathf.Clamp(_currentScale, minScale, maxScale);

            cardImage.localScale = Vector3.one * _currentScale;

            // If zoomed back to minimum, reset pan offset
            if (Mathf.Approximately(_currentScale, minScale))
            {
                _panOffset = Vector2.zero;
                cardImage.anchoredPosition = Vector2.zero;
            }

            ClampPosition();
            _lastPinchDistance = currentDistance;
        }

        void HandlePan()
        {
            Touch touch = Input.GetTouch(0);

            if (!_isDragging)
            {
                _isDragging = true;
                _lastSingleTouchPos = touch.position;
                return;
            }

            Vector2 delta = touch.position - _lastSingleTouchPos;
            _panOffset += delta;
            cardImage.anchoredPosition = _panOffset;

            ClampPosition();
            _lastSingleTouchPos = touch.position;
        }

        /// <summary>
        /// Animate back to 1x scale and centered position.
        /// </summary>
        public void ResetZoom()
        {
            _currentScale = minScale;
            _panOffset = Vector2.zero;
            _isPinching = false;
            _isDragging = false;

            if (cardImage != null)
            {
                cardImage.DOScale(Vector3.one * minScale, resetDuration)
                    .SetEase(Ease.OutCubic);
                cardImage.DOAnchorPos(Vector2.zero, resetDuration)
                    .SetEase(Ease.OutCubic);
            }
        }

        void ClampPosition()
        {
            if (cardImage == null || cardImage.parent == null)
                return;

            // Calculate how far outside the parent bounds we can pan
            RectTransform parent = cardImage.parent as RectTransform;
            if (parent == null)
                return;

            Vector2 parentSize = parent.rect.size;
            Vector2 cardSize = cardImage.rect.size * _currentScale;

            // Maximum offset is half the overflow in each direction
            float maxX = Mathf.Max(0f, (cardSize.x - parentSize.x) * 0.5f);
            float maxY = Mathf.Max(0f, (cardSize.y - parentSize.y) * 0.5f);

            _panOffset.x = Mathf.Clamp(_panOffset.x, -maxX, maxX);
            _panOffset.y = Mathf.Clamp(_panOffset.y, -maxY, maxY);

            cardImage.anchoredPosition = _panOffset;
        }
    }
}
