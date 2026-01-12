using Content.Shared.GridPreloader.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.PocketDimension;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class PocketDimensionComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? Map;

    [DataField, AutoNetworkedField]
    public NetEntity? Link;

    [DataField, AutoNetworkedField]
    public ProtoId<PreloadedGridPrototype>? StartingGrid;
}
