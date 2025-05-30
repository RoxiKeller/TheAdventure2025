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

    private int _whiteTextureId;

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

    private void DrawHealthBar(int x, int y, int width, int height, int health, int maxHealth)
    {
        float hpPercent = Math.Clamp((float)health / maxHealth, 0f, 1f);

        // Red background
        DrawFilledRect(new Rectangle<int>(x, y, width, height), 255, 0, 0);

        // Green foreground (current health)
        int greenWidth = (int)(width * hpPercent);
        if (greenWidth > 0)
        {
            DrawFilledRect(new Rectangle<int>(x, y, greenWidth, height), 0, 255, 0);
        }
    }

    public void SetupWorld()
    {
        _whiteTextureId = _renderer.CreateWhiteTexture();

        _deathTextureId = _renderer.LoadTexture("Assets/ulost.png", out var textureSize);
        _deathImageWidth = textureSize.Width;  
        _deathImageHeight = textureSize.Height; 


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
                // Move enemy
                enemy.Update(msSinceLastFrame, new Vector2D<int>(_player.Position.X, _player.Position.Y));

                double currentTimeMs = currentTime.ToUnixTimeMilliseconds();

                // Enemy attacks player if close and enough time has passed
                if (!enemy.IsDead &&
                    Math.Abs(enemy.Position.X - _player.Position.X) < 32 &&
                    Math.Abs(enemy.Position.Y - _player.Position.Y) < 32 &&
                    enemy.CanAttack(currentTimeMs))
                {
                    _player.TakeDamage(enemy.GetDamage());
                    enemy.Attack(currentTimeMs);
                }

                // Player attacks enemy
                if (_player.State.State == PlayerObject.PlayerState.Attack &&
                    !enemy.IsDead &&
                    Math.Abs(enemy.Position.X - _player.Position.X) < 32 &&
                    Math.Abs(enemy.Position.Y - _player.Position.Y) < 32)
                {
                    enemy.TakeDamage(_player.GetDamage());
                }
            }
        }

    }

    private void DrawDeathScreen()
    {
        var screenWidth = _renderer.GetScreenWidth();
        var screenHeight = _renderer.GetScreenHeight();

        _renderer.SetDrawColor(0, 0, 0, 255);
        _renderer.ClearScreen();

        if (_deathTextureId == -1)
            return;

        var sourceRect = new Rectangle<int>(0, 0, _deathImageWidth, _deathImageHeight);
        var destRect = new Rectangle<int>(
            (screenWidth - _deathImageWidth) / 2,
            (screenHeight - _deathImageHeight) / 2,
            _deathImageWidth,
            _deathImageHeight);

        _renderer.RenderTexture(_deathTextureId, sourceRect, destRect);

        _renderer.PresentFrame();
    }

    public void RenderFrame()
    {
        _renderer.SetDrawColor(0, 0, 0, 255);
        _renderer.ClearScreen();

        if (_player == null)
            return;

        var playerPosition = _player.Position;
        _renderer.CameraLookAt(playerPosition.X, playerPosition.Y);

        RenderTerrain();
        RenderAllObjects();

        // Death screen if player is dead
        if (_player.IsDead)
        {
            DrawDeathScreen();
        }

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

                        // 32px blast radius in both directions
                        if (deltaX < 32 && deltaY < 32)
                        {
                            enemy.TakeDamage(100); // Instantly kill enemy
                        }
                    }


                }

                if (_player != null)
                {
                    var deltaX = Math.Abs(_player.Position.X - tempGameObject.Position.X);
                    var deltaY = Math.Abs(_player.Position.Y - tempGameObject.Position.Y);
                    if (deltaX < 32 && deltaY < 32)
                    {
                        _player.TakeDamage(35); 
                    }
                }

            }

        }

        foreach (var id in toRemove)
        {
            _gameObjects.Remove(id);
        }

        _player?.Render(_renderer);

        //  Player health bar
        if (_player != null)
        {
            int healthBarX = _player.Position.X - 25;
            int spriteHeight = 48;
            int healthBarY = _player.Position.Y - spriteHeight - 10;

            DrawHealthBar(healthBarX, healthBarY, 50, 6, _player.GetHealth(), _player.GetMaxHealth());
        }

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

    private void DrawFilledRect(Rectangle<int> rect, byte r, byte g, byte b, byte a = 255)
    {
        _renderer.SetDrawColor(r, g, b, a);
        _renderer.FillRect(rect);
    }

    private int _deathTextureId = -1;
    private int _deathImageWidth = 0;
    private int _deathImageHeight = 0;

    public void AddBomb(int X, int Y, bool translateCoordinates = true)
    {
        var worldCoords = translateCoordinates ? _renderer.ToWorldCoordinates(X, Y) : new Vector2D<int>(X, Y);

        SpriteSheet spriteSheet = SpriteSheet.Load(_renderer, "BombExploding.json", "Assets");
        spriteSheet.ActivateAnimation("Explode");

        TemporaryGameObject bomb = new(spriteSheet, 2.1, (worldCoords.X, worldCoords.Y));
        _gameObjects.Add(bomb.Id, bomb);
    }
}