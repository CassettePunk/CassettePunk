using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Inventory;
using Content.Shared.Reloading.Components;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.Reloading.Systems;

public sealed class ReloadingSystem: EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.Reload, InputCmdHandler.FromDelegate(HandleReload, handle: false, outsidePrediction: false))
            .Register<ReloadingSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<ReloadingSystem>();
    }

    private void HandleReload(ICommonSession? session)
    {
        if (session?.AttachedEntity is null)
            return;
        ReloadNow(session.AttachedEntity.Value);
    }

    private bool ResolveReloadingArgs(ref EntityUid reloader, [NotNullWhen(true)] ref Entity<ReloadableComponent?>? reloadee, [NotNullWhen(true)] ref EntityUid? storage)
    {
        if (reloadee is null)
        {
            if (!_hands.TryGetHeldItem(reloader, "hand", out var held))
                return false;
            reloadee = held;
        }

        var reloadableComp = reloadee.Value.Comp;
        if (!Resolve(reloadee.Value.Owner, ref reloadableComp))
            return false;
        reloadee = (reloadee.Value.Owner, reloadableComp);

        if (storage is null)
        {
            if (!_inventory.TryGetSlotEntity(reloader, "back", out var backpack))
                return false;
            storage = backpack;
        }

        return true;
    }

    public bool ReloadNow(EntityUid reloader, Entity<ReloadableComponent?>? reloadee = null, EntityUid? storage = null)
    {
        if (!ResolveReloadingArgs(ref reloader, ref reloadee, ref storage))
            return false;

        var replacementContainer = _container.EnsureContainer<Container>(storage.Value, "storagebase");
        var replacement = replacementContainer.ContainedEntities.FirstOrNull(x => _tag.HasTag(x, reloadee.Value.Comp!.AmmoTag));
        if (replacement is null)
            return false;

        var ammoContainer = _container.EnsureContainer<ContainerSlot>(reloadee.Value.Owner, reloadee.Value.Comp!.Container);
        var ammo = ammoContainer.ContainedEntity;
        if (ammo is not null)
            _container.InsertOrDrop(ammo.Value, replacementContainer);
        _container.Insert(replacement.Value, ammoContainer);

        Log.Debug("reloaded successfully");

        return true;
    }
}
