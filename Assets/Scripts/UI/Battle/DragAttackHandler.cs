using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Banganka.UI.Battle
{
    /// <summary>
    /// ユニットやリーダーにアタッチし、ドラッグ&ドロップで攻撃を宣言する。
    /// ドラッグ中はカードが浮き上がり、矢印線を描画し、ドロップ先で攻撃対象を決定する。
    /// </summary>
    public class DragAttackHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public bool canDrag;
        public string attackerId;   // FieldUnit.instanceId or "leader"
        public bool isLeader;

        RectTransform _rt;
        Canvas _canvas;
        GameObject _arrowLine;
        RectTransform _arrowRt;
        Image _arrowImage;
        Vector2 _startPos;

        // 浮き上がり演出
        Vector3 _originalScale;
        int _originalSiblingIndex;
        GameObject _shadowObj;

        static readonly float LiftScale = 1.15f;
        static readonly Color ShadowColor = new(0, 0, 0, 0.4f);

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
        }

        Canvas GetCanvas()
        {
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();
            return _canvas;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!canDrag) { eventData.pointerDrag = null; return; }

            _startPos = _rt.position;

            _originalScale = _rt.localScale;
            _originalSiblingIndex = _rt.GetSiblingIndex();

            if (isLeader)
            {
                // リーダー: グロー枠で攻撃中を示す（UIが崩れないように）
                _shadowObj = new GameObject("AttackGlow");
                _shadowObj.transform.SetParent(_rt, false);
                var glowRt = _shadowObj.AddComponent<RectTransform>();
                glowRt.anchorMin = Vector2.zero;
                glowRt.anchorMax = Vector2.one;
                glowRt.offsetMin = new Vector2(-4, -4);
                glowRt.offsetMax = new Vector2(4, 4);
                var glowImg = _shadowObj.AddComponent<Image>();
                glowImg.color = new Color(1f, 0.4f, 0.2f, 0.5f);
                glowImg.raycastTarget = false;
                _shadowObj.transform.SetAsFirstSibling();
            }
            else
            {
                // カード: スケールアップ + 最前面 + シャドウ
                _rt.localScale = _originalScale * LiftScale;
                _rt.SetAsLastSibling();

                _shadowObj = new GameObject("DragShadow");
                _shadowObj.transform.SetParent(_rt.parent, false);
                _shadowObj.transform.SetSiblingIndex(_rt.GetSiblingIndex());
                var shadowRt = _shadowObj.AddComponent<RectTransform>();
                shadowRt.anchorMin = _rt.anchorMin;
                shadowRt.anchorMax = _rt.anchorMax;
                shadowRt.offsetMin = _rt.offsetMin + new Vector2(4, -4);
                shadowRt.offsetMax = _rt.offsetMax + new Vector2(4, -4);
                var shadowImg = _shadowObj.AddComponent<Image>();
                shadowImg.color = ShadowColor;
                shadowImg.raycastTarget = false;
            }

            // Create arrow indicator
            _arrowLine = new GameObject("AttackArrow");
            _arrowLine.transform.SetParent(GetCanvas().transform, false);
            _arrowRt = _arrowLine.AddComponent<RectTransform>();
            _arrowImage = _arrowLine.AddComponent<Image>();
            _arrowImage.color = new Color(1f, 0.3f, 0.2f, 0.7f);
            _arrowImage.raycastTarget = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_arrowLine == null) return;

            Vector2 start = _startPos;
            Vector2 end = eventData.position;
            Vector2 mid = (start + end) / 2f;
            float dist = Vector2.Distance(start, end);
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;

            _arrowRt.position = mid;
            _arrowRt.sizeDelta = new Vector2(dist, 6f);
            _arrowRt.rotation = Quaternion.Euler(0, 0, angle);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // 元に戻す
            if (_rt != null)
            {
                _rt.localScale = _originalScale;
                _rt.SetSiblingIndex(_originalSiblingIndex);
            }

            if (_shadowObj != null)
                Destroy(_shadowObj);

            if (_arrowLine != null)
                Destroy(_arrowLine);

            if (!canDrag) return;

            // Find what we dropped on
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var hit in results)
            {
                var dropTarget = hit.gameObject.GetComponent<DropAttackTarget>();
                if (dropTarget != null)
                {
                    dropTarget.OnReceiveAttack(attackerId, isLeader);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 攻撃のドロップ先（敵ユニットや敵リーダー）にアタッチする。
    /// </summary>
    public class DropAttackTarget : MonoBehaviour
    {
        public string targetId;     // FieldUnit.instanceId or "leader"
        public bool isLeader;
        public System.Action<string, bool, string, bool> onAttackReceived;
        // args: attackerId, attackerIsLeader, targetId, targetIsLeader

        public void OnReceiveAttack(string attackerId, bool attackerIsLeader)
        {
            onAttackReceived?.Invoke(attackerId, attackerIsLeader, targetId, isLeader);
        }
    }
}
