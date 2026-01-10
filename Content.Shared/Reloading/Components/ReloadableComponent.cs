using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Reloading.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReloadableComponent: Component
{
    /// <summary>
    /// The time it takes to reload this item.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The sound that plays when reloading starts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier ReloadStartSound;

    /// <summary>
    /// The sound that plays when reloading finishes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier ReloadEndSound;
}
