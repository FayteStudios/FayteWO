using FayteWO.Client.Entities;
using FayteWO.Client.Networking;
using FayteWO.Shared.Networking.Packets;
using FayteWO.Shared.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FayteWO.Client.Rendering;

public sealed class FayteGame : Game
{
    private const int TileSize = 32;
    private const int ViewportTileWidth = 25;
    private const int ViewportTileHeight = 18;

    private readonly GameClient _client;
    private readonly GraphicsDeviceManager _graphics;

    private SpriteBatch? _spriteBatch;
    private Texture2D? _whitePixel;

    private MouseState _previousMouseState;
    private KeyboardState _previousKeyboardState;

    public FayteGame(GameClient client)
    {
        _client = client;

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = ViewportTileWidth * TileSize,
            PreferredBackBufferHeight = ViewportTileHeight * TileSize,
            SynchronizeWithVerticalRetrace = true
        };

        IsMouseVisible = true;
        Window.Title = "FayteWO";
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _whitePixel = new Texture2D(GraphicsDevice, 1, 1);
        _whitePixel.SetData(new[] { Color.White });
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboardState = Keyboard.GetState();
        MouseState mouseState = Mouse.GetState();

        if (keyboardState.IsKeyDown(Keys.Escape))
        {
            Exit();
            return;
        }

        HandleKeyboardInput(keyboardState);
        HandleMouseInput(mouseState);

        _previousKeyboardState = keyboardState;
        _previousMouseState = mouseState;

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        if (_spriteBatch is null || _whitePixel is null)
        {
            return;
        }

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        DrawWorld();
        DrawEntities();

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void HandleKeyboardInput(KeyboardState keyboardState)
    {
        if (WasKeyPressed(keyboardState, Keys.W) || WasKeyPressed(keyboardState, Keys.Up))
        {
            _client.SendMoveRequest(Direction.North);
        }

        if (WasKeyPressed(keyboardState, Keys.D) || WasKeyPressed(keyboardState, Keys.Right))
        {
            _client.SendMoveRequest(Direction.East);
        }

        if (WasKeyPressed(keyboardState, Keys.S) || WasKeyPressed(keyboardState, Keys.Down))
        {
            _client.SendMoveRequest(Direction.South);
        }

        if (WasKeyPressed(keyboardState, Keys.A) || WasKeyPressed(keyboardState, Keys.Left))
        {
            _client.SendMoveRequest(Direction.West);
        }

        if (WasKeyPressed(keyboardState, Keys.E))
        {
            _client.InteractWithSelectedTile();
        }
    }

    private void HandleMouseInput(MouseState mouseState)
    {
        if (_client.Position is null)
        {
            return;
        }

        bool leftClicked =
            mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released;

        bool rightClicked =
            mouseState.RightButton == ButtonState.Pressed &&
            _previousMouseState.RightButton == ButtonState.Released;

        if (!leftClicked && !rightClicked)
        {
            return;
        }

        TilePosition? clickedTile = ScreenToWorldTile(mouseState.X, mouseState.Y);

        if (clickedTile is null)
        {
            return;
        }

        _client.SetSelectedTilePosition(clickedTile.Value);

        if (rightClicked)
        {
            _client.SendTileInteractionRequest(clickedTile.Value);
        }
    }

    private void DrawWorld()
    {
        if (_client.Position is null || _spriteBatch is null || _whitePixel is null)
        {
            return;
        }

        TilePosition playerPosition = _client.Position.Value;
        int startX = playerPosition.X - ViewportTileWidth / 2;
        int startY = playerPosition.Y - ViewportTileHeight / 2;

        for (int screenY = 0; screenY < ViewportTileHeight; screenY++)
        {
            for (int screenX = 0; screenX < ViewportTileWidth; screenX++)
            {
                int worldX = startX + screenX;
                int worldY = startY + screenY;

                TilePosition worldPosition = new TilePosition(worldX, worldY, playerPosition.Z);

                Color tileColor = GetTileColor(worldPosition);

                Rectangle destination = new Rectangle(
                    screenX * TileSize,
                    screenY * TileSize,
                    TileSize,
                    TileSize);

                _spriteBatch.Draw(_whitePixel, destination, tileColor);

                DrawGridLine(destination);
            }
        }

        TilePosition? selectedTile = _client.GetSelectedTilePosition();

        if (selectedTile is not null && selectedTile.Value.Z == playerPosition.Z)
        {
            Point? selectedScreenTile = WorldToScreenTile(selectedTile.Value);

            if (selectedScreenTile is not null)
            {
                Rectangle selectedRectangle = new Rectangle(
                    selectedScreenTile.Value.X * TileSize,
                    selectedScreenTile.Value.Y * TileSize,
                    TileSize,
                    TileSize);

                DrawRectangleOutline(selectedRectangle, Color.Yellow, 3);
            }
        }
    }

    private void DrawEntities()
    {
        if (_client.Position is null || _spriteBatch is null || _whitePixel is null)
        {
            return;
        }

        Point? playerScreenTile = WorldToScreenTile(_client.Position.Value);

        if (playerScreenTile is not null)
        {
            Rectangle playerRectangle = new Rectangle(
                playerScreenTile.Value.X * TileSize + 6,
                playerScreenTile.Value.Y * TileSize + 6,
                TileSize - 12,
                TileSize - 12);

            _spriteBatch.Draw(_whitePixel, playerRectangle, Color.Red);
        }

        foreach (ClientEntity entity in _client.Entities)
        {
            if (_client.PlayerId is not null && entity.EntityId == _client.PlayerId.Value)
            {
                continue;
            }

            Point? entityScreenTile = WorldToScreenTile(entity.Position);

            if (entityScreenTile is null)
            {
                continue;
            }

            Rectangle entityRectangle = new Rectangle(
                entityScreenTile.Value.X * TileSize + 8,
                entityScreenTile.Value.Y * TileSize + 8,
                TileSize - 16,
                TileSize - 16);

            _spriteBatch.Draw(_whitePixel, entityRectangle, Color.Orange);
        }
    }

    private Color GetTileColor(TilePosition worldPosition)
    {
        foreach (ChunkDataPacket chunk in _client.GetLoadedChunksSnapshot())
        {
            if (chunk.ChunkPosition != ChunkPosition.FromWorldPosition(worldPosition))
            {
                continue;
            }

            int localX = Chunk.WorldToLocalCoordinate(worldPosition.X);
            int localY = Chunk.WorldToLocalCoordinate(worldPosition.Y);
            int localZ = worldPosition.Z - chunk.ChunkPosition.Z;

            if (localX < 0 ||
                localX >= chunk.Size ||
                localY < 0 ||
                localY >= chunk.Size ||
                localZ < 0 ||
                localZ >= chunk.Height)
            {
                return Color.Magenta;
            }

            int index = (localZ * chunk.Size * chunk.Size) +
                        (localY * chunk.Size) +
                        localX;

            if (index < 0 || index >= chunk.TileIds.Length)
            {
                return Color.Magenta;
            }

            int tileId = chunk.TileIds[index];
            return TileIdToColor(tileId);
        }

        return Color.DarkSlateGray;
    }

    private static Color TileIdToColor(int tileId)
    {
        return tileId switch
        {
            1 => Color.ForestGreen,
            2 => Color.Gray,
            3 => Color.DodgerBlue,
            _ => Color.Magenta
        };
    }

    private TilePosition? ScreenToWorldTile(int mouseX, int mouseY)
    {
        if (_client.Position is null)
        {
            return null;
        }

        int screenTileX = mouseX / TileSize;
        int screenTileY = mouseY / TileSize;

        if (screenTileX < 0 ||
            screenTileX >= ViewportTileWidth ||
            screenTileY < 0 ||
            screenTileY >= ViewportTileHeight)
        {
            return null;
        }

        TilePosition playerPosition = _client.Position.Value;

        int startX = playerPosition.X - ViewportTileWidth / 2;
        int startY = playerPosition.Y - ViewportTileHeight / 2;

        return new TilePosition(
            startX + screenTileX,
            startY + screenTileY,
            playerPosition.Z);
    }

    private Point? WorldToScreenTile(TilePosition worldPosition)
    {
        if (_client.Position is null)
        {
            return null;
        }

        TilePosition playerPosition = _client.Position.Value;

        int startX = playerPosition.X - ViewportTileWidth / 2;
        int startY = playerPosition.Y - ViewportTileHeight / 2;

        int screenX = worldPosition.X - startX;
        int screenY = worldPosition.Y - startY;

        if (screenX < 0 ||
            screenX >= ViewportTileWidth ||
            screenY < 0 ||
            screenY >= ViewportTileHeight)
        {
            return null;
        }

        return new Point(screenX, screenY);
    }

    private bool WasKeyPressed(KeyboardState currentKeyboardState, Keys key)
    {
        return currentKeyboardState.IsKeyDown(key) &&
               !_previousKeyboardState.IsKeyDown(key);
    }

    private void DrawGridLine(Rectangle rectangle)
    {
        if (_spriteBatch is null || _whitePixel is null)
        {
            return;
        }

        _spriteBatch.Draw(
            _whitePixel,
            new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 1),
            Color.Black * 0.25f);

        _spriteBatch.Draw(
            _whitePixel,
            new Rectangle(rectangle.X, rectangle.Y, 1, rectangle.Height),
            Color.Black * 0.25f);
    }

    private void DrawRectangleOutline(Rectangle rectangle, Color color, int thickness)
    {
        if (_spriteBatch is null || _whitePixel is null)
        {
            return;
        }

        _spriteBatch.Draw(
            _whitePixel,
            new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness),
            color);

        _spriteBatch.Draw(
            _whitePixel,
            new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness),
            color);

        _spriteBatch.Draw(
            _whitePixel,
            new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height),
            color);

        _spriteBatch.Draw(
            _whitePixel,
            new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height),
            color);
    }
}