using System;
using UnityEngine;

namespace Nyoice.Managers
{
    public enum GameState
    {
        Playing,
        GameOver
    }

    [DisallowMultipleComponent]
    public sealed class GameStateManager : MonoBehaviour
    {
        [SerializeField]
        private GameState currentState = GameState.Playing;

        [SerializeField]
        private bool enableDebugLogs = true;

        public event Action GameOver;

        public GameState CurrentState => currentState;
        public bool IsGameOver => currentState == GameState.GameOver;

        public bool TriggerGameOver()
        {
            if (IsGameOver)
            {
                return false;
            }

            GameState previousState = currentState;
            currentState = GameState.GameOver;
            Log($"Game state: {previousState} -> {currentState}");
            GameOver?.Invoke();
            return true;
        }

        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log(message, this);
            }
        }
    }
}
