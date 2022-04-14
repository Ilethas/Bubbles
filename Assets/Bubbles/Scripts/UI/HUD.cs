using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bubbles.Scripts.UI
{
    public class HUD : MonoBehaviour
    {
        private Board _gameBoard;
        public Board GameBoard
        {
            get => _gameBoard;
            set
            {
                if (_gameBoard != null)
                {
                    _gameBoard.turnChanged -= OnTurnChanged;
                    _gameBoard.scoreChanged -= OnScoreChanged;
                    _gameBoard.gameFinished -= OnGameFinished;
                    restartButton.onClick.RemoveListener(RestartGame);
                }
                _gameBoard = value;
                _gameBoard.turnChanged += OnTurnChanged;
                _gameBoard.scoreChanged += OnScoreChanged;
                _gameBoard.gameFinished += OnGameFinished;
                restartButton.onClick.AddListener(RestartGame);
                
                OnTurnChanged(_gameBoard.CurrentTurn);
                OnScoreChanged(_gameBoard.PlayerScore);
            }
        }

        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private GameObject gameFinishedObject;
        [SerializeField] private Button restartButton;

        void OnTurnChanged(int newTurnNumber)
        {
            turnText.text = $"Turn: {newTurnNumber}";
        }

        void OnScoreChanged(int newScore)
        {
            scoreText.text = $"Score: {newScore}";
        }

        void OnGameFinished()
        {
            gameFinishedObject.SetActive(true);
        }

        void RestartGame()
        {
            if (_gameBoard != null)
            {
                _gameBoard.Restart();
            }
            gameFinishedObject.SetActive(false);
        }
    }
}
