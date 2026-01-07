namespace Content.Server.Hands;

[RegisterComponent]
public sealed partial class HotbarHandsComponent: Component
{
    [DataField]
    public Handedness Handedness = Handedness.Right;

    [DataField(required: true)]
    public int Count;
}

public enum Handedness
{
    Left,
    Right
}
