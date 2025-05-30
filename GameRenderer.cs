using Silk.NET.Maths;
using Silk.NET.SDL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TheAdventure.Models;
using Point = Silk.NET.SDL.Point;

namespace TheAdventure;

public unsafe class GameRenderer
{
    private Sdl _sdl;
    private Renderer* _renderer;
    private GameWindow _window;
    private Camera _camera;

    private Dictionary<int, IntPtr> _texturePointers = new();
    private Dictionary<int, TextureData> _textureData = new();
    private int _textureId;

    public GameRenderer(Sdl sdl, GameWindow window)
    {
        var size = window.Size;
        _screenWidth = size.Width;
        _screenHeight = size.Height;

        _sdl = sdl;
        _window = window;

        _renderer = (Renderer*)window.CreateRenderer();
        _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);

        _camera = new Camera(_screenWidth, _screenHeight);
    }


    public void SetWorldBounds(Rectangle<int> bounds)
    {
        _camera.SetWorldBounds(bounds);
    }

    public void CameraLookAt(int x, int y)
    {
        _camera.LookAt(x, y);
    }

    public int LoadTexture(string fileName, out TextureData textureInfo)
    {
        using (var fStream = new FileStream(fileName, FileMode.Open))
        {
            var image = Image.Load<Rgba32>(fStream);
            textureInfo = new TextureData()
            {
                Width = image.Width,
                Height = image.Height
            };
            var imageRAWData = new byte[textureInfo.Width * textureInfo.Height * 4];
            image.CopyPixelDataTo(imageRAWData.AsSpan());
            fixed (byte* data = imageRAWData)
            {
                var imageSurface = _sdl.CreateRGBSurfaceWithFormatFrom(data, textureInfo.Width,
                    textureInfo.Height, 8, textureInfo.Width * 4, (uint)PixelFormatEnum.Rgba32);
                if (imageSurface == null)
                {
                    throw new Exception("Failed to create surface from image data.");
                }
                
                var imageTexture = _sdl.CreateTextureFromSurface(_renderer, imageSurface);
                if (imageTexture == null)
                {
                    _sdl.FreeSurface(imageSurface);
                    throw new Exception("Failed to create texture from surface.");
                }
                
                _sdl.FreeSurface(imageSurface);
                
                _textureData[_textureId] = textureInfo;
                _texturePointers[_textureId] = (IntPtr)imageTexture;
            }
        }

        return _textureId++;
    }

    public void RenderTexture(int textureId, Rectangle<int> src, Rectangle<int> dst,
        RendererFlip flip = RendererFlip.None, double angle = 0.0, Point center = default)
    {
        if (_texturePointers.TryGetValue(textureId, out var imageTexture))
        {
            var translatedDst = _camera.ToScreenCoordinates(dst);
            _sdl.RenderCopyEx(_renderer, (Texture*)imageTexture, in src,
                in translatedDst,
                angle,
                in center, flip);
        }
    }

    public Vector2D<int> ToWorldCoordinates(int x, int y)
    {
        return _camera.ToWorldCoordinates(new Vector2D<int>(x, y));
    }

    public void SetDrawColor(byte r, byte g, byte b, byte a)
    {
        _sdl.SetRenderDrawColor(_renderer, r, g, b, a);
    }

    public void ClearScreen()
    {
        _sdl.RenderClear(_renderer);
    }

    public void FillRect(Rectangle<int> rect)
    {
        var translatedRect = _camera.ToScreenCoordinates(rect);
        _sdl.RenderFillRect(_renderer, in translatedRect);
    }

    public int GetScreenWidth()
    {
        return _screenWidth;
    }

    public int GetScreenHeight()
    {
        return _screenHeight;
    }


    private int _screenWidth;
    private int _screenHeight;


    public void DrawText(string text, int x, int y, int fontSize, byte r, byte g, byte b)
    {
        Console.WriteLine($"DrawText called: '{text}' at ({x},{y}) size={fontSize}");
 
    }


    public void PresentFrame()
    {
        _sdl.RenderPresent(_renderer);
    }

    public int CreateWhiteTexture()
    {

        byte[] whitePixel = { 255, 255, 255, 255 };

        fixed (byte* data = whitePixel)
        {
            var surface = _sdl.CreateRGBSurfaceWithFormatFrom(
                data,
                1, 1, 32, 4,
                (uint)PixelFormatEnum.Rgba32
            );

            if (surface == null)
            {
                throw new Exception("Failed to create surface for white texture.");
            }

            var texture = _sdl.CreateTextureFromSurface(_renderer, surface);
            if (texture == null)
            {
                _sdl.FreeSurface(surface);
                throw new Exception("Failed to create texture from white surface.");
            }

            _sdl.FreeSurface(surface);

            _textureData[_textureId] = new TextureData { Width = 1, Height = 1 };
            _texturePointers[_textureId] = (IntPtr)texture;

            return _textureId++;
        }
    }

}
