using System.Diagnostics;
using Content.Server.GridPreloader;
using Content.Shared.PocketDimension;

namespace Content.Server.PocketDimension;

public sealed class PocketDimensionSystem : SharedPocketDimensionSystem
{
    [Dependency] private readonly GridPreloaderSystem _gridPreloader = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PocketDimensionComponent, MapInitEvent>(OnInit);
    }

    private void OnInit(Entity<PocketDimensionComponent> entity, ref MapInitEvent args)
    {
        var map = _map.CreateMap();
        entity.Comp.Map = GetNetEntity(map);
        if (entity.Comp.StartingGrid is null)
            return;
        if (!_gridPreloader.TryGetPreloadedGrid(entity.Comp.StartingGrid.Value, out var grid))
        {
            Log.Error($"Failed to get StartingGrid for entity with PocketDimension: {entity.Owner}");
            return;
        }
        _transform.SetParent(grid.Value, map);



        DirtyField(entity.AsNullable(), nameof(PocketDimensionComponent.Map));
    }
}

/// <summary>
/// Raised on every entity in the pocket dimension when it is created.
/// </summary>
/// <param name="Parent">The entity which the pocket dimension is inside.</param>
[ByRefEvent]
public record struct PocketDimensionInit(EntityUid Parent);
