using System.Reflection;
using System.Text.Json;
using Silk.NET.Maths;
using TheAdventure.Models;
using TheAdventure.Models.Data;

using TheAdventure.Scripting;

namespace TheAdventure;

public class Engine
{
    private readonly GameRenderer _renderer;
    private readonly Input _input;
    private readonly ScriptEngine _scriptEngine = new();

    private readonly Dictionary<int, GameObject> _gameObjects = new();
    private readonly Dictionary<string, TileSet> _loadedTileSets = new();
    private readonly Dictionary<int, Tile> _tileIdMap = new();

    private Level _currentLevel = new();
    private PlayerObject? _player;

    private DateTimeOffset _lastUpdate = DateTimeOffset.Now;



    public void AddEnemy(int x, int y)
    {
        SpriteSheet enemySprite = SpriteSheet.Load(_renderer, "Minotaur.json", "Assets");
        EnemyObject enemy = new EnemyObject(enemySprite, new Vector2D<int>(x, y));

        _gameObjects.Add(enemy.Id, enemy);
    }



    public Engine(GameRenderer renderer, Input input)
    {
        _renderer = renderer;
        _input = input;

        _input.OnMouseClick += (_, _) =>
        {
            _player?.Attack();
        };

        _input.AddBombRequested += (_, coords) =>
        {
            AddBomb(coords.x, coords.y);
        };
    }


    public void SetupWorld()
    {
        _player = new(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 100, 100);

        var levelContent = File.ReadAllText(Path.Combine("Assets", "untitled.tmj"));
        var level = JsonSerializer.Deserialize<Level>(levelContent);
        if (level == null)
        {
            throw new Exception("Failed to load level");
        }

        foreach (var tileSetRef in level.TileSets)
        {
            var tileSetContent = File.ReadAllText(Path.Combine("Assets", tileSetRef.Source));
            var tileSet = JsonSerializer.Deserialize<TileSet>(tileSetContent);
            if (tileSet == null)
            {
                throw new Exception("Failed to load tile set");
            }

            foreach (var tile in tileSet.Tiles)
            {
                tile.TextureId = _renderer.LoadTexture(Path.Combine("Assets", tile.Image), out _);
                _tileIdMap.Add(tile.Id!.Value, tile);
            }

            _loadedTileSets.Add(tileSet.Name, tileSet);
        }

        if (level.Width == null || level.Height == null)
        {
            throw new Exception("Invalid level dimensions");
        }

        if (level.TileWidth == null || level.TileHeight == null)
        {
            throw new Exception("Invalid tile dimensions");
        }

        _renderer.SetWorldBounds(new Rectangle<int>(0, 0, level.Width.Value * level.TileWidth.Value,
            level.Height.Value * level.TileHeight.Value));

        _currentLevel = level;

        _scriptEngine.LoadAll(Path.Combine("Assets", "Scripts"));

        AddEnemy(200, 200);
    }

    public void ProcessFrame()
    {
        var currentTime = DateTimeOffset.Now;
        var msSinceLastFrame = (currentTime - _lastUpdate).TotalMilliseconds;
        _lastUpdate = currentTime;

        if (_player == null)
        {
            return;
        }

        // Update player position and input
        double up = _input.IsWPressed() ? 1.0 : 0.0;
        double down = _input.IsSPressed() ? 1.0 : 0.0;
        double left = _input.IsAPressed() ? 1.0 : 0.0;
        double right = _input.IsDPressed() ? 1.0 : 0.0;

        _player.UpdatePosition(up, down, left, right, 48, 48, msSinceLastFrame);

        if (_input.IsSpacePressed())
        {
            AddBomb(_player.Position.X, _player.Position.Y, false);
        }

        _scriptEngine.ExecuteAll(this);

        
        foreach (var obj in _gameObjects.Values)
        {
            if (obj is EnemyObject enemy && _player != null)
            {
                enemy.Update(msSinceLastFrame, new Vector2D<int>(_player.Position.X, _player.Position.Y));
            }
        }

    }

    public void RenderFrame()
    {
        _renderer.SetDrawColor(0, 0, 0, 255);
        _renderer.ClearScreen();

        var playerPosition = _player!.Position;
        _renderer.CameraLookAt(playerPosition.X, playerPosition.Y);

        RenderTerrain();
        RenderAllObjects();

        _renderer.PresentFrame();
    }

    public void RenderAllObjects()
    {
        var toRemove = new List<int>();

        foreach (var gameObject in GetRenderables())
        {
            gameObject.Render(_renderer);

            if (gameObject is TemporaryGameObject { IsExpired: true } tempGameObject)
            {
                toRemove.Add(tempGameObject.Id);

                // Check if enemy is in blast range of this bomb
                foreach (var obj in _gameObjects.Values)
                {
                    if (obj is EnemyObject enemy && !enemy.IsExpired)
                    {
                        var deltaX = Math.Abs(enemy.Position.X - tempGameObject.Position.X);
                        var deltaY = Math.Abs(enemy.Position.Y - tempGameObject.Position.Y);

                        // 32px blast radius in both directions (can be tweaked)
                        if (deltaX < 32 && deltaY < 32)
                        {
                            enemy.TakeDamage(999); // Instantly kill enemy
                        }
                    }
                }

                
                if (_player != null)
                {
                    var deltaX = Math.Abs(_player.Position.X - tempGameObject.Position.X);
                    var deltaY = Math.Abs(_player.Position.Y - tempGameObject.Position.Y);
                    if (deltaX < 32 && deltaY < 32)
                    {
                        _player.GameOver();
                    }
                }
            }
        }

        
        foreach (var id in toRemove)
        {
            _gameObjects.Remove(id);
        }

        _player?.Render(_renderer);
    }


    public void RenderTerrain()
    {
        foreach (var currentLayer in _currentLevel.Layers)
        {
            for (int i = 0; i < _currentLevel.Width; ++i)
            {
                for (int j = 0; j < _currentLevel.Height; ++j)
                {
                    int? dataIndex = j * currentLayer.Width + i;
                    if (dataIndex == null)
                    {
                        continue;
                    }

                    var currentTileId = currentLayer.Data[dataIndex.Value] - 1;
                    if (currentTileId == null)
                    {
                        continue;
                    }

                    var currentTile = _tileIdMap[currentTileId.Value];

                    var tileWidth = currentTile.ImageWidth ?? 0;
                    var tileHeight = currentTile.ImageHeight ?? 0;

                    var sourceRect = new Rectangle<int>(0, 0, tileWidth, tileHeight);
                    var destRect = new Rectangle<int>(i * tileWidth, j * tileHeight, tileWidth, tileHeight);
                    _renderer.RenderTexture(currentTile.TextureId, sourceRect, destRect);
                }
            }
        }
    }

    public IEnumerable<RenderableGameObject> GetRenderables()
    {
        foreach (var gameObject in _gameObjects.Values)
        {
            if (gameObject is RenderableGameObject renderableGameObject)
            {
                yield return renderableGameObject;
            }
        }
    }

    public (int X, int Y) GetPlayerPosition()
    {
        return _player!.Position;
    }

    public void AddBomb(int X, int Y, bool translateCoordinates = true)
    {
        var worldCoords = translateCoordinates ? _renderer.ToWorldCoordinates(X, Y) : new Vector2D<int>(X, Y);

        SpriteSheet spriteSheet = SpriteSheet.Load(_renderer, "BombExploding.json", "Assets");
        spriteSheet.ActivateAnimation("Explode");

        TemporaryGameObject bomb = new(spriteSheet, 2.1, (worldCoords.X, worldCoords.Y));
        _gameObjects.Add(bomb.Id, bomb);
    }
}