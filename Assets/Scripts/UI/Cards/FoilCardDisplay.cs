using UnityEngine;
using UnityEngine.UI;

namespace Banganka.UI.Cards
{
    /// <summary>
    /// Manages foil/premium card visual effects.
    /// Shows shimmer overlay on foil cards, plus a foil icon badge.
    /// </summary>
    public class FoilCardDisplay : MonoBehaviour
    {
        [SerializeField] Image shimmerOverlay;
        [SerializeField] GameObject foilBadge;
        [SerializeField] float shimmerSpeed = 1.5f;
        [SerializeField] float shimmerAngle = 45f;

        bool _isFoil;
        Material _shimmerMaterial;

        static readonly int OffsetId = Shader.PropertyToID("_MainTex_ST");

        void OnDestroy()
        {
            if (_shimmerMaterial != null)
            {
                Destroy(_shimmerMaterial);
                _shimmerMaterial = null;
            }
        }

        /// <summary>
        /// Enable or disable foil visual effects.
        /// </summary>
        public void SetFoil(bool isFoil)
        {
            _isFoil = isFoil;

            if (foilBadge != null)
                foilBadge.SetActive(_isFoil);

            if (shimmerOverlay != null)
            {
                shimmerOverlay.gameObject.SetActive(_isFoil);

                if (_isFoil && _shimmerMaterial == null)
                    CreateShimmerMaterial();
            }
        }

        void Update()
        {
            if (!_isFoil || _shimmerMaterial == null)
                return;

            // Scroll UV offset over time to create moving shimmer
            float offset = Time.time * shimmerSpeed;
            float rad = shimmerAngle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            _shimmerMaterial.mainTextureOffset = direction * offset;
        }

        void CreateShimmerMaterial()
        {
            // Create a simple additive-blend material for the shimmer overlay
            var shader = Shader.Find("UI/Default");
            if (shader == null)
                return;

            _shimmerMaterial = new Material(shader);
            _shimmerMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _shimmerMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);

            // Semi-transparent white to let the shimmer show through
            _shimmerMaterial.color = new Color(1f, 1f, 1f, 0.25f);

            shimmerOverlay.material = _shimmerMaterial;
        }
    }
}
