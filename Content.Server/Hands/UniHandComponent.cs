namespace Content.Server.Hands;

[RegisterComponent]
public sealed partial class UniHandComponent: Component
{
    [DataField]
    public Handedness Handedness = Handedness.Right;
}

public enum Handedness
{
    Left,
    Right
}
