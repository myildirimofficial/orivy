namespace Orivy;

public class KeyPressEventArgs : KeyEventArgs
{
    public KeyPressEventArgs(char keyChar, Keys modifiers = Keys.None) : base((Keys)keyChar, modifiers)
    {
        KeyChar = keyChar;
    }

    public KeyPressEventArgs(Keys keyCode, Keys modifiers = Keys.None) : base(keyCode, modifiers)
    {
        KeyChar = (char)(int)keyCode;
    }

    public char KeyChar { get; }
}
