using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server.Hands.Systems;

public sealed class UniHandSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UniHandComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<UniHandComponent> entity, ref MapInitEvent args)
    {
        var location = entity.Comp.Handedness switch
        {
            Handedness.Left => HandLocation.Left,
            Handedness.Right => HandLocation.Right
        };

        _hands.AddHand(entity.Owner, "hand", location);
    }
}
