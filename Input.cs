using Silk.NET.SDL;

namespace TheAdventure;

public unsafe class Input
{
    private readonly Sdl _sdl;

    public EventHandler<(int x, int y)>? OnMouseClick;
    public EventHandler<(int x, int y)>? AddBombRequested;
    public Input(Sdl sdl)
    {
        _sdl = sdl;
    }

    public bool IsWPressed()
{
    ReadOnlySpan<byte> state = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
    return state[(int)KeyCode.W] == 1;
}

public bool IsSPressed()
{
    ReadOnlySpan<byte> state = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
    return state[(int)KeyCode.S] == 1;
}

public bool IsAPressed()
{
    ReadOnlySpan<byte> state = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
    return state[(int)KeyCode.A] == 1;
}

public bool IsDPressed()
{
    ReadOnlySpan<byte> state = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
    return state[(int)KeyCode.D] == 1;
}

public bool IsSpacePressed()
{
    ReadOnlySpan<byte> keyboardState = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
    return keyboardState[(int)KeyCode.Space] == 1;
}

public bool IsLeftMouseClicked(Event ev) =>
    ev.Type == (uint)EventType.Mousebuttondown && ev.Button.Button == (byte)MouseButton.Primary;

public bool IsRightMouseClicked(Event ev) =>
    ev.Type == (uint)EventType.Mousebuttondown && ev.Button.Button == (byte)MouseButton.Secondary;

    public bool ProcessInput()
    {
        Event ev = new Event();
        while (_sdl.PollEvent(ref ev) != 0)
        {
            if (ev.Type == (uint)EventType.Quit)
            {
                return true;
            }

            switch (ev.Type)
            {
                case (uint)EventType.Windowevent:
                {
                    switch (ev.Window.Event)
                    {
                        case (byte)WindowEventID.Shown:
                        case (byte)WindowEventID.Exposed:
                        {
                            break;
                        }
                        case (byte)WindowEventID.Hidden:
                        {
                            break;
                        }
                        case (byte)WindowEventID.Moved:
                        {
                            break;
                        }
                        case (byte)WindowEventID.SizeChanged:
                        {
                            break;
                        }
                        case (byte)WindowEventID.Minimized:
                        case (byte)WindowEventID.Maximized:
                        case (byte)WindowEventID.Restored:
                            break;
                        case (byte)WindowEventID.Enter:
                        {
                            break;
                        }
                        case (byte)WindowEventID.Leave:
                        {
                            break;
                        }
                        case (byte)WindowEventID.FocusGained:
                        {
                            break;
                        }
                        case (byte)WindowEventID.FocusLost:
                        {
                            break;
                        }
                        case (byte)WindowEventID.Close:
                        {
                            break;
                        }
                        case (byte)WindowEventID.TakeFocus:
                        {
                            _sdl.SetWindowInputFocus(_sdl.GetWindowFromID(ev.Window.WindowID));
                            break;
                        }
                    }

                    break;
                }

                case (uint)EventType.Fingermotion:
                {
                    break;
                }

                case (uint)EventType.Mousemotion:
                {
                    break;
                }

                case (uint)EventType.Fingerdown:
                {
                    break;
                }
                case (uint)EventType.Mousebuttondown:
                    {
                        if (ev.Button.Button == (byte)MouseButton.Primary)
                        {
                            OnMouseClick?.Invoke(this, (ev.Button.X, ev.Button.Y)); // Left Click = Attack
                        }
                        else if (ev.Button.Button == (byte)MouseButton.Secondary)
                        {
                            AddBombRequested?.Invoke(this, (ev.Button.X, ev.Button.Y)); // Right Click = Bomb
                        }

                        break;
                    }


                case (uint)EventType.Fingerup:
                {
                    break;
                }

                case (uint)EventType.Mousebuttonup:
                {
                    break;
                }

                case (uint)EventType.Mousewheel:
                {
                    break;
                }

                case (uint)EventType.Keyup:
                {
                    break;
                }

                case (uint)EventType.Keydown:
                {
                    break;
                }
            }
        }

        return false;
    }
}