using UnityEngine;
using TMPro;
using Banganka.UI.Common;
using Banganka.Game;

namespace Banganka.UI.Battle
{
    public class BattleLobbyController : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI statusText;
        [SerializeField] ScreenManager screenManager;

        void OnEnable()
        {
            if (titleText) titleText.text = "バトル";
            if (statusText) statusText.text = "対戦準備完了";
        }

        public void OnStartLocalMatch()
        {
            GameManager.Instance.StartNewMatch();
            if (screenManager) screenManager.ShowScreen(GameManager.GameScreen.Match);
        }
    }
}
