using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nyoice.UI
{
    /// <summary>
    /// Handles navigation from the title screen to the game scene.
    /// </summary>
    public sealed class TitleSceneController : MonoBehaviour
    {
        private const string GameSceneName = "GameScene";

        private bool _isLoading;

        public void StartGame()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;
            SceneManager.LoadScene(GameSceneName);
        }
    }
}
