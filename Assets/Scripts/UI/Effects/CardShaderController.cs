using UnityEngine;
using UnityEngine.UI;
using Banganka.Core.Config;
using Banganka.Core.Data;

namespace Banganka.UI.Effects
{
    /// <summary>
    /// カードのホログラフィック/フォイルシェーダーを制御する。
    /// レアリティに応じてR=微光沢、SR=ホロ、SSR=フルプリズムを切り替え。
    /// タッチ位置に連動した光の移動も管理する。
    /// </summary>
    public class CardShaderController : MonoBehaviour
    {
        [SerializeField] Image targetImage;
        [SerializeField] bool enableTouchInteraction = true;

        Material _material;
        bool _isHolographic;

        // シェーダープロパティID（キャッシュ）
        static readonly int PropHoloIntensity = Shader.PropertyToID("_HoloIntensity");
        static readonly int PropFoilIntensity = Shader.PropertyToID("_FoilIntensity");
        static readonly int PropGlowColor = Shader.PropertyToID("_GlowColor");
        static readonly int PropGlowIntensity = Shader.PropertyToID("_GlowIntensity");
        static readonly int PropTouchPos = Shader.PropertyToID("_TouchPos");
        static readonly int PropTouchIntensity = Shader.PropertyToID("_TouchIntensity");

        // レアリティキーワード
        const string KW_NONE = "_RARITY_NONE";
        const string KW_R = "_RARITY_R";
        const string KW_SR = "_RARITY_SR";
        const string KW_SSR = "_RARITY_SSR";

        static Shader _holoShader;

        static Shader HoloShader
        {
            get
            {
                if (_holoShader == null)
                    _holoShader = Shader.Find("Banganka/HolographicCard");
                return _holoShader;
            }
        }

        void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        /// <summary>
        /// レアリティに応じたシェーダーを適用する。
        /// C=通常マテリアル、R以上=ホログラフィック。
        /// </summary>
        public void ApplyRarity(string rarity, Aspect aspect = (Aspect)(-1))
        {
            if (targetImage == null)
                targetImage = GetComponent<Image>();
            if (targetImage == null) return;

            // C (コモン) は通常レンダリング
            if (rarity == "C" || string.IsNullOrEmpty(rarity))
            {
                ResetToDefault();
                return;
            }

            // ホロシェーダーが見つからない場合はフォールバック
            if (HoloShader == null)
            {
                Debug.LogWarning("[CardShaderController] HolographicCard shader not found");
                return;
            }

            // マテリアルインスタンス生成
            if (_material == null || _material.shader != HoloShader)
            {
                if (_material != null) Destroy(_material);
                _material = new Material(HoloShader);
            }

            // レアリティキーワード設定
            _material.DisableKeyword(KW_NONE);
            _material.DisableKeyword(KW_R);
            _material.DisableKeyword(KW_SR);
            _material.DisableKeyword(KW_SSR);

            switch (rarity)
            {
                case "R":
                    _material.EnableKeyword(KW_R);
                    _material.SetFloat(PropHoloIntensity, 0.3f);
                    _material.SetFloat(PropFoilIntensity, 0f);
                    break;
                case "SR":
                    _material.EnableKeyword(KW_SR);
                    _material.SetFloat(PropHoloIntensity, 0.6f);
                    _material.SetFloat(PropFoilIntensity, 0.3f);
                    break;
                case "SSR":
                    _material.EnableKeyword(KW_SSR);
                    _material.SetFloat(PropHoloIntensity, 0.8f);
                    _material.SetFloat(PropFoilIntensity, 0.6f);
                    break;
                default:
                    _material.EnableKeyword(KW_NONE);
                    break;
            }

            // アスペクト発光色の設定
            if ((int)aspect >= 0)
            {
                Color glowColor = AspectColors.GetColor(aspect);
                _material.SetColor(PropGlowColor, glowColor);
                _material.SetFloat(PropGlowIntensity, rarity == "SSR" ? 1.5f : rarity == "SR" ? 0.8f : 0.3f);
            }

            // イラストのテクスチャをコピー
            if (targetImage.sprite != null)
                _material.mainTexture = targetImage.sprite.texture;

            targetImage.material = _material;
            _isHolographic = true;
        }

        /// <summary>通常マテリアルに戻す</summary>
        public void ResetToDefault()
        {
            if (targetImage != null)
                targetImage.material = null; // デフォルトUI/Defaultに戻す
            _isHolographic = false;
        }

        /// <summary>タッチ/マウス位置を更新（UIイベントから呼ぶ）</summary>
        public void UpdateTouchPosition(Vector2 normalizedPos)
        {
            if (!_isHolographic || _material == null || !enableTouchInteraction) return;
            _material.SetVector(PropTouchPos, new Vector4(normalizedPos.x, normalizedPos.y, 0, 0));
            _material.SetFloat(PropTouchIntensity, 1f);
        }

        /// <summary>タッチ終了</summary>
        public void ClearTouch()
        {
            if (_material == null) return;
            _material.SetFloat(PropTouchIntensity, 0f);
        }

        /// <summary>召喚時の強発光→通常への遷移</summary>
        public void PlaySummonGlow(float duration = 0.5f)
        {
            if (!_isHolographic || _material == null) return;
            StartCoroutine(SummonGlowCoroutine(duration));
        }

        System.Collections.IEnumerator SummonGlowCoroutine(float duration)
        {
            float originalGlow = _material.GetFloat(PropGlowIntensity);
            float peakGlow = 3f;
            float elapsed = 0f;

            // 急速に最大輝度へ
            while (elapsed < duration * 0.2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.2f);
                _material.SetFloat(PropGlowIntensity, Mathf.Lerp(originalGlow, peakGlow, t));
                yield return null;
            }

            // ゆっくり通常輝度へ
            elapsed = 0f;
            while (elapsed < duration * 0.8f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.8f);
                t = 1f - (1f - t) * (1f - t); // EaseOutQuad
                _material.SetFloat(PropGlowIntensity, Mathf.Lerp(peakGlow, originalGlow, t));
                yield return null;
            }

            _material.SetFloat(PropGlowIntensity, originalGlow);
        }
    }
}
