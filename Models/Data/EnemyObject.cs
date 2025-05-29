using Silk.NET.Maths;

namespace TheAdventure.Models.Data;

public class EnemyObject : RenderableGameObject
{
    private int _health = 50;
    private double _speed = 100.0; 
    private Vector2D<int> _direction = new(1, 0);
    private bool _isDead = false;

    public bool IsExpired { get; set; } = false;

    public EnemyObject(SpriteSheet spriteSheet, Vector2D<int> startPosition)
        : base(spriteSheet, (startPosition.X, startPosition.Y))
    {
        SpriteSheet.ActivateAnimation("MoveDown");
    }

    public void Update(double deltaTime, Vector2D<int> playerPosition)
    {
        if (_isDead)
            return;

        // Direction to player
        var directionToPlayer = new Vector2D<float>(
            playerPosition.X - Position.X,
            playerPosition.Y - Position.Y
        );

        // Normalize direction
        if (directionToPlayer.Length > 0)
        {
            directionToPlayer = Vector2D.Normalize(directionToPlayer);
        }

        Position = (
            Position.X + (int)(directionToPlayer.X * _speed * deltaTime / 1000),
            Position.Y + (int)(directionToPlayer.Y * _speed * deltaTime / 1000)
        );
    }

    public void TakeDamage(int damage)
    {
        if (_isDead)
            return;

        _health -= damage;
        if (_health <= 0)
        {
            _isDead = true;
            IsExpired = true;

            // Activate GameOver animation for enemy
            SpriteSheet.ActivateAnimation("GameOver");
        }
    }
}
