using Robust.Shared.GameStates;

namespace Content.Shared.PocketDimension;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class PocketDimensionLinkComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity Parent;
}
