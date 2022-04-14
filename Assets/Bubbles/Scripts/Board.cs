using System;
using System.Collections.Generic;
using System.Linq;
using QPathFinder;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using Random = UnityEngine.Random;

namespace Bubbles.Scripts
{
    public delegate void TurnChanged(int newTurnNumber);
    public delegate void ScoreChanged(int newScore);
    public delegate void GameFinished();
    
    // Cached cell state
    public struct BoardCell
    {
        public GameObject tileObject;
        public GameObject bubbleObject;

        public Bubble bubble;
        public Tile tile;
    }

    // Stores information about bubbles of a particular color that are about to spawn in some location
    struct PendingBubble
    {
        public Vector2Int position;
        public Color color;

        public PendingBubble(Vector2Int position, Color color)
        {
            this.position = position;
            this.color = color;
        }
    }
    
    public class Board : MonoBehaviour
    {
        [SerializeField] [MinAttribute(1)] private int width = 1;
        [SerializeField] [MinAttribute(1)] private int height = 1;

        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private GameObject bubblePrefab;
    
        private BoardCell[,] _cellsPool;
        private int _activeBubblesAmount = 0;
        private int scoreToNextColor;
        private List<PendingBubble> _pendingBubbles = new List<PendingBubble>();
        private List<Vector2Int> _scoredBubbles = new List<Vector2Int>();
        private List<Color> _bubbleColors = new List<Color>();
        private List<Color> _additionalColors = new List<Color>();
        private Vector2Int? _lastMoveDestination = null;
        private bool _isGameFinished = false;
        private Bubble _selectedBubble = null;
        
        public List<Color> defaultBubbleColors = new List<Color>();
        public List<Color> defaultAdditionalColors = new List<Color>();
        [MinAttribute(1)] public int bubblesSpawnedPerTurn = 3;
        [MinAttribute(2)] public int amountOfBubblesPerLine = 5;
        [MinAttribute(2)] public int maxBubbleColors = 9;
        [MinAttribute(1)] public int pointsAmountPerAdditionalColor = 50;
        
        private int _playerScore = 0;
        public int PlayerScore
        {
            get => _playerScore;
            set
            {
                // Add a new possible bubble color, if the player reached the score threshold
                scoreToNextColor -= value - _playerScore;
                if (scoreToNextColor <= 0)
                {
                    scoreToNextColor = amountOfBubblesPerLine;
                    if (_bubbleColors.Count < maxBubbleColors)
                    {
                        // Get first predefined color and add it to the color list
                        if (_additionalColors.Count > 0)
                        {
                            _bubbleColors.Add(_additionalColors.First());
                            _additionalColors.RemoveAt(0);
                        }
                        else
                        {
                            // Generate a random color otherwise
                            while (true)
                            {
                                Color newColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
                                if (!_bubbleColors.Contains(newColor))
                                {
                                    _bubbleColors.Add(newColor);
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // Update the actual score value and notify subscribers
                _playerScore = value;
                scoreChanged?.Invoke(value);
            }
        }
        
        private int _currentTurn = 1;
        public int CurrentTurn
        {
            get => _currentTurn;
            set
            {
                _currentTurn = value;
                turnChanged?.Invoke(value);
            }
        }

        public TurnChanged turnChanged;
        public ScoreChanged scoreChanged;
        public GameFinished gameFinished;

        void Start()
        {
            // Initialize the board
            _cellsPool = new BoardCell[height, width];
            SpawnBoardPrefabs();
            Restart();
        }

        public void Restart()
        {
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    SetBubbleActive(new Vector2Int(j, i), Color.clear, false);
                }
            }

            _selectedBubble = null;
            _isGameFinished = false;
            _bubbleColors = new List<Color>(defaultBubbleColors);
            _additionalColors = new List<Color>(defaultAdditionalColors);
            scoreToNextColor = pointsAmountPerAdditionalColor;
            PlayerScore = 0;
            CurrentTurn = 1;
            
            MarkNewBubblesPositions();
            SpawnAdditionalBubbles();
            MarkNewBubblesPositions();
        }

        private void SpawnBoardPrefabs()
        {
            if (tilePrefab == null)
            {
                Debug.LogWarning("Board: attempted to instantiate tilePrefab with null value");
                return;
            }
            if (bubblePrefab == null)
            {
                Debug.LogWarning("Board: attempted to instantiate bubblePrefab with null value");
                return;
            }
            
            // Find "Bubbles" and "Tiles" empty subobjects to parent tiles and bubbles for more readable game object
            // hierarchy
            Transform tilesTransform = gameObject.transform.Find("Tiles");
            Transform bubblesTransform = gameObject.transform.Find("Bubbles");
            
            // Create tiles and bubbles objects and cache them
            Vector3 origin = transform.position;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    Vector3 offset = Vector3.back * i + Vector3.right * j;
                    
                    // Spawn tile object
                    GameObject tileObject = Instantiate(tilePrefab, origin + offset, Quaternion.identity, tilesTransform);
                    InitializeBoardElement(tileObject, "Tile", new Vector2Int(j, i));
                    _cellsPool[i, j].tileObject = tileObject;
                    _cellsPool[i, j].tile = tileObject.GetComponent<Tile>();
                    
                    // Spawn bubble object
                    GameObject bubbleObject = Instantiate(bubblePrefab, origin + offset, Quaternion.identity, bubblesTransform);
                    InitializeBoardElement(bubbleObject, "Bubble", new Vector2Int(j, i));
                    _cellsPool[i, j].bubbleObject = bubbleObject;
                    _cellsPool[i, j].bubble = bubbleObject.GetComponent<Bubble>();
                    bubbleObject.SetActive(false);
                }
            }
        }

        void InitializeBoardElement(GameObject boardElementObject, string elementName, Vector2Int position)
        {
            Element element = boardElementObject.GetComponent<Element>();
            if (element != null)
            {
                boardElementObject.name = $"{elementName} ({position.x}, {position.y})";
                element.boardPosition = position;
                element.parentBoard = this;
            }
        }

        private int GetNodeIndex(int rowNumber, int columnNumber)
        {
            return rowNumber * width + columnNumber + 1;
        }

        private Vector2Int GetNodePosition(Node node)
        {
            int y = (node.autoGeneratedID - 1) / width;
            int x = (node.autoGeneratedID - 1) % width;
            return new Vector2Int(x, y);
        }

        private void CreatePathfindingGraph()
        {
            // This method produces a grid graph. After nodes are generated for each grid cell, node connections are
            // generated from left to right, top to bottom. A valid path should exist for every two adjacent empty cells
            // or an empty cell adjacent to the selected bubble
            GraphData graph = new GraphData();
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    // Spawn a note for each board cell
                    Vector3 position = Vector3.back * i + Vector3.right * j;
                    graph.nodes.Add(new Node(position));

                    bool isCurrentNodeInactive = !_cellsPool[i, j].bubbleObject.activeSelf;
                    bool isCurrentNodeSelected = _cellsPool[i, j].bubble == _selectedBubble;
                    if (j < width - 1)
                    {
                        bool isRightNodeInactive = !_cellsPool[i, j + 1].bubbleObject.activeSelf;
                        bool isRightNodeSelected = _cellsPool[i, j + 1].bubble == _selectedBubble;
                        
                        if (isCurrentNodeInactive && isRightNodeInactive ||
                            isCurrentNodeSelected && isRightNodeInactive || 
                            isCurrentNodeInactive && isRightNodeSelected)
                        {
                            // Create horizontal connections between nodes
                            graph.paths.Add(new Path(GetNodeIndex(i, j), GetNodeIndex(i, j + 1)));
                            graph.paths.Last().cost = 1;
                        }
                    }
                    if (i != 0)
                    {
                        bool isTopNodeInactive = !_cellsPool[i - 1, j].bubbleObject.activeSelf;
                        bool isTopNodeSelected = _cellsPool[i - 1, j].bubble == _selectedBubble;
                        
                        if (isCurrentNodeInactive && isTopNodeInactive ||
                            isCurrentNodeSelected && isTopNodeInactive ||
                            isCurrentNodeInactive && isTopNodeSelected)
                        {
                            // Create vertical connections between nodes
                            graph.paths.Add(new Path(GetNodeIndex(i, j), GetNodeIndex(i - 1, j)));
                            graph.paths.Last().cost = 1;
                        }
                    }
                }
            }
            graph.ReGenerateIDs();
            PathFinder.instance.graphData = graph;
        }

        public void OnTileClicked(Vector2Int tilePosition)
        {
            if (_isGameFinished)
            {
                return;
            }
            
            if (_selectedBubble != null)
            {
                if (TryMoveSelectedBubbleToCell(tilePosition))
                {
                    _lastMoveDestination = tilePosition;
                    CheckIfBubblesMakeLines(tilePosition);
                    OnNextTurn();
                };
            }
        }

        bool TryMoveSelectedBubbleToCell(Vector2Int destination)
        {
            if (_selectedBubble == null)
            {
                return false;
            }
            
            // Regenerate the pathfinding graph and attempt to find a path from the selected bubble to the destination
            CreatePathfindingGraph();
            List<Vector2Int> resultPath = new List<Vector2Int>(); 

            int startNode = GetNodeIndex(_selectedBubble.boardPosition.y, _selectedBubble.boardPosition.x);
            int endNode = GetNodeIndex(destination.y, destination.x);
            PathFinder.instance.FindShortestPathOfNodes(startNode, endNode, Execution.Synchronous,
            delegate (List<Node> nodes)
            {
                if (nodes != null)
                {
                    foreach (Node node in nodes)
                    {
                        resultPath.Add(GetNodePosition(node));
                    }
                }
            });

            // Path was not found, so don't do anything
            if (resultPath.Count == 0)
            {
                return false;
            }
            
            // Path was found, so move the bubble, update board state and deselect the bubble
            SetBubbleActive(destination, _selectedBubble.BubbleColor, true);
            SetBubbleActive(_selectedBubble.boardPosition, Color.black, false);
            _selectedBubble = null;
            
            return true;
        }

        bool IsPositionValid(Vector2Int position)
        {
            // Check if position corresponds to a cell in the board
            return position.x >= 0 && position.y >= 0 && position.x < width && position.y < height;
        }

        public void CheckIfBubblesMakeLines(Vector2Int destination)
        {
            // Inspect the destination position and see if there are any lines of bubbles emanating out of it
            Vector2Int pos = destination;
            Color movedBubbleColor = Color.clear;
            List<Vector2Int> bubblesToRemove = new List<Vector2Int>();
            
            Vector2Int[] stepDirections = { Vector2Int.right, Vector2Int.up, Vector2Int.one, new Vector2Int(1, -1) };
            foreach (Vector2Int direction in stepDirections)
            {
                Vector2Int step = direction;
                pos = destination;
                if (IsPositionValid(pos))
                {
                    movedBubbleColor = _cellsPool[pos.y, pos.x].bubble.BubbleColor;
                    pos += step;
                }

                bool flippedStepDirection = false;
                while (true)
                {
                    if (IsPositionValid(pos) && _cellsPool[pos.y, pos.x].bubble.BubbleColor == movedBubbleColor)
                    {
                        bubblesToRemove.Add(pos);
                    }
                    else
                    {
                        if (!flippedStepDirection)
                        {
                            step *= -1;
                            pos = destination - step;
                            flippedStepDirection = true;
                        }
                        else
                        {
                            break;
                        }
                    }
                    pos += step;
                }

                if (bubblesToRemove.Count >= amountOfBubblesPerLine)
                {
                    _scoredBubbles.AddRange(bubblesToRemove);
                }
                bubblesToRemove.Clear();
            }
        }

        private void ScoreBubbles()
        {
            // Despawn all bubbles that were in lines of appropriate lengths and increase the player's score
            foreach (Vector2Int position in _scoredBubbles)
            {
                SetBubbleActive(position, Color.clear, false);
            }
            PlayerScore += _scoredBubbles.Count;
            _scoredBubbles.Clear();
        }

        private void OnNextTurn()
        {
            SpawnAdditionalBubbles();
            ScoreBubbles();
            MarkNewBubblesPositions();
            HandleGameFinishedConditions();

            CurrentTurn++;
        }

        void HandleGameFinishedConditions()
        {
            if (_activeBubblesAmount == width * height - 1)
            {
                _isGameFinished = true;
                gameFinished?.Invoke();
            }
        }

        public void OnBubbleClicked(Vector2Int bubblePosition)
        {
            _selectedBubble = _cellsPool[bubblePosition.y, bubblePosition.x].bubble;
        }

        private void MarkNewBubblesPositions()
        {
            for (int i = 0; i < bubblesSpawnedPerTurn; i++)
            {
                // Randomize a tile that doesn't yet have an active bubble
                Vector2Int position;
                while (true)
                {
                    if (_activeBubblesAmount == width * height)
                    {
                        Debug.Log("Board: attempted to activate additional bubbles while the board is full");
                        return;
                    }
                    
                    position = new Vector2Int(Random.Range(0, width), Random.Range(0, height));
                    if (!_cellsPool[position.y, position.x].bubbleObject.activeSelf)
                    {
                        break;
                    }
                }
                
                // Add the tile to the list of tiles that are about to create a bubble
                Color color = _bubbleColors[Random.Range(0, _bubbleColors.Count)];
                
                _pendingBubbles.Add(new PendingBubble(position, color));
                _cellsPool[position.y, position.x].tile.TileColor = color;
            }
            
        }

        private void SpawnAdditionalBubbles()
        {
            // Activate the randomized bubbles
            foreach (PendingBubble pendingBubble in _pendingBubbles)
            {
                if (pendingBubble.position != _lastMoveDestination)
                {
                    SetBubbleActive(pendingBubble.position, pendingBubble.color, true);
                    CheckIfBubblesMakeLines(pendingBubble.position);
                }
            }
            _pendingBubbles.Clear();
        }

        private void SetBubbleActive(Vector2Int position, Color color, bool active)
        {
            GameObject bubbleObject = _cellsPool[position.y, position.x].bubbleObject;
            if (bubbleObject.activeSelf != active)
            {
                _activeBubblesAmount += active ? 1 : -1;
            }
            _cellsPool[position.y, position.x].bubble.BubbleColor = color;
            bubbleObject.SetActive(active);
            
            _cellsPool[position.y, position.x].tile.StopIndicating();
        }
    }
}
