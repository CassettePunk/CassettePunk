using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server.Hands.Systems;

public sealed class HotbarHandSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HotbarHandsComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<HotbarHandsComponent> entity, ref MapInitEvent args)
    {
        var location = entity.Comp.Handedness switch
        {
            Handedness.Left => HandLocation.Left,
            Handedness.Right => HandLocation.Right
        };

        for (var i = 0; i < entity.Comp.Count; i++)
        {
            _hands.AddHand(entity.Owner, $"hand_{i}", location);
        }
    }
}
