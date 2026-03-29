using UnityEngine;
using UnityEngine.UI;
using Banganka.Game;

namespace Banganka.UI.Common
{
    [RequireComponent(typeof(Button))]
    public class NavigationButton : MonoBehaviour
    {
        [SerializeField] GameManager.GameScreen targetScreen;

        void Start()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        void OnClick()
        {
            var sm = FindFirstObjectByType<ScreenManager>();
            if (sm != null)
                sm.ShowScreen(targetScreen);
        }
    }
}
