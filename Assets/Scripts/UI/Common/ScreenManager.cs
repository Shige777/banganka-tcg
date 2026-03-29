using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Banganka.Game;
using Banganka.Core.Config;
using Banganka.Audio;

namespace Banganka.UI.Common
{
    public class ScreenManager : MonoBehaviour
    {
        GameObject _homeScreen;
        GameObject _battleScreen;
        GameObject _cardsScreen;
        GameObject _storyScreen;
        GameObject _shopScreen;
        GameObject _matchScreen;
        GameObject _navigationBar;
        bool _initialized;
        bool _transitioning;

        static readonly string[] NavNames = { "Nav_ホーム", "Nav_バトル", "Nav_カード", "Nav_ストーリー", "Nav_ショップ" };
        static readonly GameManager.GameScreen[] NavScreens =
        {
            GameManager.GameScreen.Home,
            GameManager.GameScreen.Battle,
            GameManager.GameScreen.Cards,
            GameManager.GameScreen.Story,
            GameManager.GameScreen.Shop,
        };

        public void Init(GameObject home, GameObject battle, GameObject cards,
            GameObject story, GameObject shop, GameObject match, GameObject nav)
        {
            _homeScreen = home;
            _battleScreen = battle;
            _cardsScreen = cards;
            _storyScreen = story;
            _shopScreen = shop;
            _matchScreen = match;
            _navigationBar = nav;
            _initialized = true;

            EnsureCanvasGroup(_homeScreen);
            EnsureCanvasGroup(_battleScreen);
            EnsureCanvasGroup(_cardsScreen);
            EnsureCanvasGroup(_storyScreen);
            EnsureCanvasGroup(_shopScreen);
            EnsureCanvasGroup(_matchScreen);

            SwitchTo(GameManager.GameScreen.Home);
        }

        public void ShowScreen(GameManager.GameScreen screen)
        {
            if (!_initialized || _transitioning) return;

            _transitioning = true;

            // ReduceMotion: フェードアニメーションを省略
            if (AccessibilitySettings.ReduceMotion)
            {
                SwitchTo(screen);
                _transitioning = false;
                return;
            }

            var current = GetCurrentActiveScreen();
            if (current != null)
            {
                var cg = current.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.DOFade(0f, 0.15f).SetEase(Ease.InQuad).OnComplete(() =>
                    {
                        SwitchTo(screen);
                        _transitioning = false;
                    });
                    return;
                }
            }

            SwitchTo(screen);
            _transitioning = false;
        }

        public void ShowScreenImmediate(GameManager.GameScreen screen)
        {
            if (!_initialized) return;
            SwitchTo(screen);
        }

        void SwitchTo(GameManager.GameScreen screen)
        {
            HideAll();

            if (GameManager.Instance != null)
                GameManager.Instance.SetScreen(screen);

            var target = screen switch
            {
                GameManager.GameScreen.Home => _homeScreen,
                GameManager.GameScreen.Battle => _battleScreen,
                GameManager.GameScreen.Cards => _cardsScreen,
                GameManager.GameScreen.Story => _storyScreen,
                GameManager.GameScreen.Shop => _shopScreen,
                GameManager.GameScreen.Match => _matchScreen,
                _ => _homeScreen,
            };

            if (target != null)
            {
                target.SetActive(true);
                var cg = target.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    if (AccessibilitySettings.ReduceMotion)
                    {
                        cg.alpha = 1;
                    }
                    else
                    {
                        cg.alpha = 0;
                        cg.DOFade(1f, 0.25f).SetEase(Ease.OutQuad);
                    }
                }
            }

            // 画面BGM自動切替
            SoundManager.Instance?.OnScreenChanged(screen.ToString());

            if (_navigationBar != null)
            {
                _navigationBar.SetActive(screen != GameManager.GameScreen.Match);
                UpdateNavHighlight(screen);
            }
        }

        GameObject GetCurrentActiveScreen()
        {
            if (_homeScreen != null && _homeScreen.activeSelf) return _homeScreen;
            if (_battleScreen != null && _battleScreen.activeSelf) return _battleScreen;
            if (_cardsScreen != null && _cardsScreen.activeSelf) return _cardsScreen;
            if (_storyScreen != null && _storyScreen.activeSelf) return _storyScreen;
            if (_shopScreen != null && _shopScreen.activeSelf) return _shopScreen;
            if (_matchScreen != null && _matchScreen.activeSelf) return _matchScreen;
            return null;
        }

        void UpdateNavHighlight(GameManager.GameScreen screen)
        {
            if (_navigationBar == null) return;

            for (int i = 0; i < _navigationBar.transform.childCount; i++)
            {
                var child = _navigationBar.transform.GetChild(i);
                var img = child.GetComponent<Image>();
                if (img == null) continue;

                bool active = false;
                for (int j = 0; j < NavNames.Length; j++)
                {
                    if (child.name == NavNames[j] && screen == NavScreens[j])
                    {
                        active = true;
                        break;
                    }
                }

                var targetColor = active
                    ? new Color(0.24f, 0.20f, 0.40f, 1f)
                    : new Color(0.10f, 0.10f, 0.16f, 1f);

                img.DOColor(targetColor, 0.2f).SetEase(Ease.OutQuad);
            }
        }

        void HideAll()
        {
            SetInactive(_homeScreen);
            SetInactive(_battleScreen);
            SetInactive(_cardsScreen);
            SetInactive(_storyScreen);
            SetInactive(_shopScreen);
            SetInactive(_matchScreen);
        }

        void SetInactive(GameObject obj)
        {
            if (obj == null) return;
            obj.SetActive(false);
            var cg = obj.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1;
        }

        void EnsureCanvasGroup(GameObject obj)
        {
            if (obj != null && obj.GetComponent<CanvasGroup>() == null)
                obj.AddComponent<CanvasGroup>();
        }
    }
}
