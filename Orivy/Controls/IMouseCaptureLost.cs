namespace Orivy.Controls;

/// <summary>Implemented by elements that must discard transient state when native mouse capture is lost.</summary>
public interface IMouseCaptureLost
{
    void OnMouseCaptureLost();
}
