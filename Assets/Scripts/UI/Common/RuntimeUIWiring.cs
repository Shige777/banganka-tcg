using UnityEngine;
using UnityEngine.UI;
using Banganka.Game;

namespace Banganka.UI.Common
{
    public class RuntimeUIWiring : MonoBehaviour
    {
        void Start()
        {
            // Wait one frame so ScreenManager.Awake has run
            Invoke(nameof(WireAll), 0.1f);
        }

        void WireAll()
        {
            var sm = FindFirstObjectByType<ScreenManager>();
            if (sm == null)
            {
                Debug.LogError("RuntimeUIWiring: ScreenManager not found!");
                return;
            }

            int wired = 0;

            // Wire navigation bar buttons
            wired += WireNavButton("Nav_ホーム", sm, GameManager.GameScreen.Home);
            wired += WireNavButton("Nav_バトル", sm, GameManager.GameScreen.Battle);
            wired += WireNavButton("Nav_カード", sm, GameManager.GameScreen.Cards);
            wired += WireNavButton("Nav_ストーリー", sm, GameManager.GameScreen.Story);
            wired += WireNavButton("Nav_ショップ", sm, GameManager.GameScreen.Shop);

            // Wire Home "バトルへ" button
            wired += WireScreenButton("BattleButton", sm, GameManager.GameScreen.Battle);

            // Wire Battle lobby "ローカル対戦開始"
            var startObj = FindByName("StartMatchButton");
            if (startObj != null)
            {
                var btn = startObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        GameManager.Instance.StartNewMatch();
                        sm.ShowScreen(GameManager.GameScreen.Match);
                    });
                    wired++;
                    Debug.Log("Wired: StartMatchButton");
                }
            }

            // Wire result "戻る"
            var backObj = FindByName("BackButton");
            if (backObj != null)
            {
                var btn = backObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => sm.ShowScreen(GameManager.GameScreen.Home));
                    wired++;
                }
            }

            Debug.Log($"RuntimeUIWiring: {wired} buttons wired successfully.");
        }

        int WireNavButton(string objName, ScreenManager sm, GameManager.GameScreen screen)
        {
            var obj = FindByName(objName);
            if (obj == null)
            {
                Debug.LogWarning($"RuntimeUIWiring: '{objName}' not found");
                return 0;
            }
            var btn = obj.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning($"RuntimeUIWiring: '{objName}' has no Button component");
                return 0;
            }
            btn.onClick.RemoveAllListeners();
            var target = screen; // capture
            btn.onClick.AddListener(() =>
            {
                Debug.Log($"Nav pressed: {target}");
                sm.ShowScreen(target);
            });
            return 1;
        }

        int WireScreenButton(string objName, ScreenManager sm, GameManager.GameScreen screen)
        {
            var obj = FindByName(objName);
            if (obj == null) return 0;
            var btn = obj.GetComponent<Button>();
            if (btn == null) return 0;
            btn.onClick.RemoveAllListeners();
            var target = screen;
            btn.onClick.AddListener(() => sm.ShowScreen(target));
            return 1;
        }

        GameObject FindByName(string name)
        {
            // Resources.FindObjectsOfTypeAll finds inactive objects too
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in all)
            {
                if (t.name == name && t.hideFlags == HideFlags.None &&
                    !IsInPrefabStage(t.gameObject))
                    return t.gameObject;
            }
            return null;
        }

        bool IsInPrefabStage(GameObject obj)
        {
            // Check if object belongs to a scene (not a prefab asset)
            return obj.scene.name == null || !obj.scene.isLoaded;
        }
    }
}
