using FayteWO.Client.Entities;
using FayteWO.Client.Networking;
using FayteWO.Shared.Networking.Packets;
using FayteWO.Shared.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaPoint = Microsoft.Xna.Framework.Point;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using XnaKeys = Microsoft.Xna.Framework.Input.Keys;
using XnaButtonState = Microsoft.Xna.Framework.Input.ButtonState;

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
        _whitePixel.SetData(new[] { XnaColor.White });
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboardState = Keyboard.GetState();
        MouseState mouseState = Mouse.GetState();

        if (keyboardState.IsKeyDown(XnaKeys.Escape))
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
        GraphicsDevice.Clear(XnaColor.Black);

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
        if (WasKeyPressed(keyboardState, XnaKeys.W) || WasKeyPressed(keyboardState, XnaKeys.Up))
        {
            _client.SendMoveRequest(Direction.North);
        }

        if (WasKeyPressed(keyboardState, XnaKeys.D) || WasKeyPressed(keyboardState, XnaKeys.Right))
        {
            _client.SendMoveRequest(Direction.East);
        }

        if (WasKeyPressed(keyboardState, XnaKeys.S) || WasKeyPressed(keyboardState, XnaKeys.Down))
        {
            _client.SendMoveRequest(Direction.South);
        }

        if (WasKeyPressed(keyboardState, XnaKeys.A) || WasKeyPressed(keyboardState, XnaKeys.Left))
        {
            _client.SendMoveRequest(Direction.West);
        }

        if (WasKeyPressed(keyboardState, XnaKeys.E))
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
            mouseState.LeftButton == XnaButtonState.Pressed &&
            _previousMouseState.LeftButton == XnaButtonState.Released;

        bool rightClicked =
            mouseState.RightButton == XnaButtonState.Pressed &&
            _previousMouseState.RightButton == XnaButtonState.Released;

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

                XnaColor tileColor = GetTileColor(worldPosition);

                XnaRectangle destination = new XnaRectangle(
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
            XnaPoint? selectedScreenTile = WorldToScreenTile(selectedTile.Value);

            if (selectedScreenTile is not null)
            {
                XnaRectangle selectedRectangle = new XnaRectangle(
                    selectedScreenTile.Value.X * TileSize,
                    selectedScreenTile.Value.Y * TileSize,
                    TileSize,
                    TileSize);

                DrawRectangleOutline(selectedRectangle, XnaColor.Yellow, 3);
            }
        }
    }

    private void DrawEntities()
    {
        if (_client.Position is null || _spriteBatch is null || _whitePixel is null)
        {
            return;
        }

        XnaPoint? playerScreenTile = WorldToScreenTile(_client.Position.Value);

        if (playerScreenTile is not null)
        {
            XnaRectangle playerRectangle = new XnaRectangle(
                playerScreenTile.Value.X * TileSize + 6,
                playerScreenTile.Value.Y * TileSize + 6,
                TileSize - 12,
                TileSize - 12);

            _spriteBatch.Draw(_whitePixel, playerRectangle, XnaColor.Red);
        }

        foreach (ClientEntity entity in _client.Entities)
        {
            if (_client.PlayerId is not null && entity.EntityId == _client.PlayerId.Value)
            {
                continue;
            }

            XnaPoint? entityScreenTile = WorldToScreenTile(entity.Position);

            if (entityScreenTile is null)
            {
                continue;
            }

            XnaRectangle entityRectangle = new XnaRectangle(
                entityScreenTile.Value.X * TileSize + 8,
                entityScreenTile.Value.Y * TileSize + 8,
                TileSize - 16,
                TileSize - 16);

            _spriteBatch.Draw(_whitePixel, entityRectangle, XnaColor.Orange);
        }
    }

    private XnaColor GetTileColor(TilePosition worldPosition)
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
                return XnaColor.Magenta;
            }

            int index = (localZ * chunk.Size * chunk.Size) +
                        (localY * chunk.Size) +
                        localX;

            if (index < 0 || index >= chunk.TileIds.Length)
            {
                return XnaColor.Magenta;
            }

            int tileId = chunk.TileIds[index];
            return TileIdToColor(tileId);
        }

        return XnaColor.DarkSlateGray;
    }

    private static XnaColor TileIdToColor(int tileId)
    {
        return tileId switch
        {
            1 => XnaColor.ForestGreen,
            2 => XnaColor.Gray,
            3 => XnaColor.DodgerBlue,
            _ => XnaColor.Magenta
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

    private XnaPoint? WorldToScreenTile(TilePosition worldPosition)
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

        return new XnaPoint(screenX, screenY);
    }

    private bool WasKeyPressed(KeyboardState currentKeyboardState, XnaKeys key)
    {
        return currentKeyboardState.IsKeyDown(key) &&
               !_previousKeyboardState.IsKeyDown(key);
    }

    private void DrawGridLine(XnaRectangle rectangle)
    {
        if (_spriteBatch is null || _whitePixel is null)
        {
            return;
        }

        _spriteBatch.Draw(
            _whitePixel,
            new XnaRectangle(rectangle.X, rectangle.Y, rectangle.Width, 1),
            XnaColor.Black * 0.25f);

        _spriteBatch.Draw(
            _whitePixel,
            new XnaRectangle(rectangle.X, rectangle.Y, 1, rectangle.Height),
            XnaColor.Black * 0.25f);
    }

    private void DrawRectangleOutline(XnaRectangle rectangle, XnaColor color, int thickness)
    {
        if (_spriteBatch is null || _whitePixel is null)
        {
            return;
        }

        _spriteBatch.Draw(
            _whitePixel,
            new XnaRectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness),
            color);

        _spriteBatch.Draw(
            _whitePixel,
            new XnaRectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness),
            color);

        _spriteBatch.Draw(
            _whitePixel,
            new XnaRectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height),
            color);

        _spriteBatch.Draw(
            _whitePixel,
            new XnaRectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height),
            color);
    }
}