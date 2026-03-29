using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Banganka.UI.Tutorial
{
    /// <summary>
    /// コンパニオン「ナル」— 案内役の浮遊猫型存在 (COMPANION_CHARACTER.md)
    /// チュートリアル + ストーリーシーンで使用
    /// </summary>
    public class CompanionNal : MonoBehaviour
    {
        [SerializeField] GameObject dialogueBubble;
        [SerializeField] TextMeshProUGUI dialogueText;
        [SerializeField] Image expressionImage;
        [SerializeField] RectTransform nalBody;
        [SerializeField] float bobAmplitude = 5f;
        [SerializeField] float bobSpeed = 2f;

        float _bobTimer;
        bool _isVisible;
        Coroutine _typewriterCoroutine;

        // Expression sprites (set in Inspector or loaded from Resources)
        // normal, explain, point, serious, smile, surprise
        [SerializeField] Sprite[] expressionSprites;

        static readonly string[] ExpressionNames =
            { "normal", "explain", "point", "serious", "smile", "surprise" };

        void Update()
        {
            if (!_isVisible || nalBody == null) return;

            // Floating bob animation
            _bobTimer += Time.deltaTime * bobSpeed;
            var pos = nalBody.anchoredPosition;
            pos.y = Mathf.Sin(_bobTimer) * bobAmplitude;
            nalBody.anchoredPosition = pos;
        }

        // ====================================================================
        // Public API
        // ====================================================================

        public void ShowDialogue(string text, string expression = "normal")
        {
            Show();
            SetExpression(expression);

            if (_typewriterCoroutine != null)
                StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = StartCoroutine(TypewriterEffect(text));
        }

        public void ShowDialogueImmediate(string text, string expression = "normal")
        {
            Show();
            SetExpression(expression);
            if (dialogueText) dialogueText.text = text;
        }

        public void Show()
        {
            _isVisible = true;
            gameObject.SetActive(true);
            if (dialogueBubble) dialogueBubble.SetActive(true);
        }

        public void Hide()
        {
            _isVisible = false;
            if (dialogueBubble) dialogueBubble.SetActive(false);
        }

        public void HideCompletely()
        {
            _isVisible = false;
            gameObject.SetActive(false);
        }

        // ====================================================================
        // Expression
        // ====================================================================

        void SetExpression(string expression)
        {
            if (expressionImage == null || expressionSprites == null) return;

            for (int i = 0; i < ExpressionNames.Length; i++)
            {
                if (ExpressionNames[i] == expression && i < expressionSprites.Length)
                {
                    expressionImage.sprite = expressionSprites[i];
                    return;
                }
            }
        }

        // ====================================================================
        // Typewriter
        // ====================================================================

        IEnumerator TypewriterEffect(string fullText)
        {
            if (dialogueText == null) yield break;

            dialogueText.text = "";
            foreach (char c in fullText)
            {
                dialogueText.text += c;
                yield return new WaitForSeconds(0.03f);
            }
        }

        // ====================================================================
        // Idle Chatter (non-tutorial)
        // ====================================================================

        static readonly string[] IdleLines =
        {
            "また鳴ってんじゃん。今回はどんな願いだろ。",
            "ねえ、ちょっと暇なんだけど。",
            "交界の霧、今日は少し薄いかも。",
            "お腹すいた…猫だけど果物がいいな。",
            "あの願主、めちゃくちゃ強そうだけど大丈夫？",
        };

        public void ShowRandomIdleLine()
        {
            string line = IdleLines[Random.Range(0, IdleLines.Length)];
            ShowDialogue(line, "normal");
        }
    }
}
