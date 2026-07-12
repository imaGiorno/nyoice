using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nyoice.UI
{
    /// <summary>
    /// Draws the minimal title screen and starts the game from a click or tap.
    /// </summary>
    public sealed class TitleSceneController : MonoBehaviour
    {
        private const string GameSceneName = "GameScene";
        private const string TitleText = "尿意's（Nyoice）";

        private GUIStyle _titleStyle;
        private bool _isLoading;

        private void OnGUI()
        {
            EnsureStyles();

            GUI.Label(
                new Rect(0f, Screen.height * 0.3f, Screen.width, 80f),
                TitleText,
                _titleStyle);

            GUI.Label(
                new Rect(0f, Screen.height * 0.65f, Screen.width, 40f),
                "クリック / タップでスタート",
                _titleStyle);

            if (!_isLoading && GUI.Button(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, GUIStyle.none))
            {
                _isLoading = true;
                SceneManager.LoadScene(GameSceneName);
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                normal = { textColor = Color.white }
            };
        }
    }
}
