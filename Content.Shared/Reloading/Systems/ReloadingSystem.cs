using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Inventory;
using Content.Shared.Reloading.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared.Reloading.Systems;

public sealed class ReloadingSystem: EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BallisticAmmoProviderComponent, ScoreReloadableEvent>(OnReloadBallistic);
        SubscribeLocalEvent<ReloadableComponent, ReloadDoAfterEvent>(OnReloadDoAfterFinished);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.Reload, InputCmdHandler.FromDelegate(HandleReload, handle: false, outsidePrediction: false))
            .Register<ReloadingSystem>();
    }

    private void OnReloadBallistic(Entity<BallisticAmmoProviderComponent> entity, ref ScoreReloadableEvent args)
    {
        if (args.Handled)
            return;
        args.Score = entity.Comp.Count;
        if (entity.Comp.Count == entity.Comp.Capacity)
            args.Full = true;
        args.Handled = true;
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
        StartReloadDoAfter(session.AttachedEntity.Value);
    }

    private bool TryGetReloadingArgsReloadee(EntityUid reloader, [NotNullWhen(true)] out Entity<ReloadableComponent>? reloadee)
    {
        reloadee = null;

        if (!_hands.TryGetActiveItem(reloader, out var reloadeeEntity))
            return false;

        if (!TryComp<ReloadableComponent>(reloadeeEntity, out var reloadable))
            return false;

        reloadee = (reloadeeEntity.Value, reloadable);
        return true;
    }

    private bool TryGetReloadingArgsStorage(EntityUid reloader, [NotNullWhen(true)] out EntityUid? storage)
    {
        storage = null;

        if (!_inventory.TryGetSlotEntity(reloader, "back", out storage))
            return false;

        return true;
    }

    public bool StartReloadDoAfter(EntityUid reloader)
    {
        if (!TryGetReloadingArgsReloadee(reloader, out var reloadee))
            return false;

        if (!TryGetReloadingArgsStorage(reloader, out var storage))
            return false;

        return StartReloadDoAfter(reloader, reloadee.Value, storage.Value);
    }

    public bool StartReloadDoAfter(EntityUid reloader, Entity<ReloadableComponent> reloadee, EntityUid storage)
    {
        if (!ReloadNow(reloader, reloadee, storage, suppress: true))
            return false;

        var doAfterArgs = new DoAfterArgs(EntityManager, reloader, reloadee.Comp.ReloadTime, new ReloadDoAfterEvent(), reloadee.Owner, null, reloadee.Owner)
        {
            BreakOnDamage = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            BreakOnMove = false,
            BreakOnWeightlessMove = false
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnReloadDoAfterFinished(Entity<ReloadableComponent> entity, ref ReloadDoAfterEvent args)
    {
        if (!TryGetReloadingArgsStorage(args.User, out var storage))
            return;

        ReloadNow(args.User, entity, storage.Value);
    }

    public bool ReloadNow(EntityUid reloader, Entity<ReloadableComponent> reloadee, EntityUid storage, bool suppress = false)
    {
        var replacementContainer = _container.EnsureContainer<Container>(storage, "storagebase");

        EntityUid? replacement = null;
        var highestScore = 0;
        var lowestNetID = int.MaxValue;
        foreach (var item in replacementContainer.ContainedEntities.Where(x => _tag.HasTag(x, reloadee.Comp.AmmoTag)))
        {
            var ev = new ScoreReloadableEvent();
            RaiseLocalEvent(item, ref ev);
            if (ev.Score <= 0)
                continue;
            if (ev.Score > highestScore)
            {
                highestScore = ev.Score;
                replacement = item;
                lowestNetID = GetNetEntity(item).Id;
            }
            else if (ev.Score == highestScore && GetNetEntity(item).Id < lowestNetID)
            {
                lowestNetID = GetNetEntity(item).Id;
                replacement = item;
            }
        }

        if (replacement is null)
            return false;

        if (suppress)
            return true;

        var ammoContainer = _container.EnsureContainer<ContainerSlot>(reloadee.Owner, reloadee.Comp.Container);
        var ammo = ammoContainer.ContainedEntity;
        if (ammo is not null)
            _container.InsertOrDrop(ammo.Value, replacementContainer);
        _container.Insert(replacement.Value, ammoContainer);

        Log.Debug("reloaded successfully");

        return true;
    }
}

/// <summary>
/// Raised on an ammo to score how good it is to be reloaded with.
/// For ballistic ammo for example, it will just count the number of bullets.
/// </summary>
/// <param name="Score"></param>
[ByRefEvent]
public record struct ScoreReloadableEvent(int Score = 0, bool Full = false, bool Handled = false);

[Serializable, NetSerializable]
public sealed partial class ReloadDoAfterEvent : SimpleDoAfterEvent;
