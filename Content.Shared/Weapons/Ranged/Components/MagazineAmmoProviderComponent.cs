using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged;

/// <summary>
/// Wrapper around a magazine. Passes all AmmoProvider logic onto it.
/// </summary>
[RegisterComponent, Virtual]
[Access(typeof(SharedGunSystem))]
public partial class MagazineAmmoProviderComponent : AmmoProviderComponent
{
    [DataField]
    public SoundSpecifier? SoundAutoEject = new SoundPathSpecifier("/Audio/Weapons/Guns/EmptyAlarm/smg_empty_alarm.ogg");

    /// <summary>
    /// Should the magazine automatically eject when empty.
    /// </summary>
    [DataField]
    public bool AutoEject = false;

    /// <summary>
    /// A whitelist on what tags magazines can have.
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> AmmoTagWhitelist = new();
}
