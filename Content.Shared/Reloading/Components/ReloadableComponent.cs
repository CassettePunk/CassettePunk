using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Reloading.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReloadableComponent: Component
{
    [DataField, AutoNetworkedField]
    public string Container = "gun_magazine";

    [DataField, AutoNetworkedField]
    public ProtoId<TagPrototype> AmmoTag = string.Empty;

    [DataField, AutoNetworkedField]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(2);
}
