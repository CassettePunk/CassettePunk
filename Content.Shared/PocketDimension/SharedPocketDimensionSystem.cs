using Content.Shared.GridPreloader.Systems;

namespace Content.Shared.PocketDimension;

public abstract class SharedPocketDimensionSystem : EntitySystem
{
    [Dependency] protected readonly SharedMapSystem _map = default!;
    [Dependency] protected readonly SharedTransformSystem _transform = default!;

}
