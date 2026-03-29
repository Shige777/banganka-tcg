using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Banganka.Audio;

namespace Banganka.UI.Battle
{
    /// <summary>
    /// エモート6種 (SOCIAL_SPEC.md P0)
    /// </summary>
    public class EmoteSystem : MonoBehaviour
    {
        [SerializeField] RectTransform emoteDisplayArea;
        [SerializeField] GameObject emoteButtonPanel;

        public static readonly string[] EmoteIds =
        {
            "emote_good",     // よくやった
            "emote_think",    // 考え中…
            "emote_sorry",    // ごめん
            "emote_hurry",    // 急いで
            "emote_respect",  // 敬意
            "emote_fun",      // 楽しい
        };

        static readonly string[] EmoteTexts =
        {
            "よくやった！",
            "考え中…",
            "ごめんなさい",
            "急いで！",
            "お見事！",
            "楽しい！",
        };

        float _cooldown;
        const float EmoteCooldown = 5f;

        public event Action<int> OnEmoteSent; // emote index

        public void ShowEmotePanel()
        {
            if (emoteButtonPanel) emoteButtonPanel.SetActive(true);
        }

        public void HideEmotePanel()
        {
            if (emoteButtonPanel) emoteButtonPanel.SetActive(false);
        }

        public void SendEmote(int index)
        {
            if (_cooldown > 0) return;
            if (index < 0 || index >= EmoteIds.Length) return;

            _cooldown = EmoteCooldown;
            OnEmoteSent?.Invoke(index);
            DisplayEmote(index, true);

            SoundManager.Instance?.PlaySE($"se_emote_{index + 1}");
            HideEmotePanel();
        }

        public void ReceiveEmote(int index)
        {
            DisplayEmote(index, false);
        }

        void DisplayEmote(int index, bool isSelf)
        {
            if (emoteDisplayArea == null) return;
            StartCoroutine(ShowEmoteRoutine(EmoteTexts[index], isSelf));
        }

        IEnumerator ShowEmoteRoutine(string text, bool isSelf)
        {
            var obj = new GameObject("Emote");
            obj.transform.SetParent(emoteDisplayArea, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 50);
            rt.anchoredPosition = isSelf ? new Vector2(0, -30) : new Vector2(0, 30);

            var bg = obj.AddComponent<Image>();
            bg.color = isSelf
                ? new Color(0.2f, 0.3f, 0.5f, 0.9f)
                : new Color(0.5f, 0.2f, 0.2f, 0.9f);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var trt = textObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            // Animate
            obj.transform.localScale = Vector3.zero;
            float duration = 2.5f;
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (t < 0.15f)
                    obj.transform.localScale = Vector3.one * (t / 0.15f);
                else if (t > 0.75f)
                {
                    float fadeT = (t - 0.75f) / 0.25f;
                    bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0.9f * (1 - fadeT));
                    tmp.color = new Color(1, 1, 1, 1 - fadeT);
                }

                yield return null;
            }

            Destroy(obj);
        }

        void Update()
        {
            if (_cooldown > 0) _cooldown -= Time.deltaTime;
        }
    }
}
