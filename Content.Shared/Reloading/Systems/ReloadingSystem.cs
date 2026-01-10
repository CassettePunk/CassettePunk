using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Inventory;
using Content.Shared.Reloading.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Reloading.Systems;

public sealed class ReloadingSystem: EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BallisticAmmoProviderComponent, ScoreReloadableEvent>(OnScoreReloadableBallistic);
        SubscribeLocalEvent<ReloadableComponent, ReloadDoAfterEvent>(OnReloadDoAfterFinished);

        // magazine
        SubscribeLocalEvent<MagazineAmmoProviderComponent, GetReloadablePredicate>(OnGetPredicateMagazine);
        SubscribeLocalEvent<MagazineAmmoProviderComponent, AttemptReloadEvent>(OnAttemptReloadMagazine);
        SubscribeLocalEvent<MagazineAmmoProviderComponent, ReloadEvent>(OnReloadMagazine);

        // ballistic
        SubscribeLocalEvent<BallisticAmmoProviderComponent, GetReloadablePredicate>(OnGetPredicateBallistic);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, AttemptReloadEvent>(OnAttemptReloadBallistic);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ReloadEvent>(OnReloadBallistic);

        SubscribeLocalEvent<ActiveReloadingComponent, AttemptShootEvent>(OnAttemptShoot);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.Reload, InputCmdHandler.FromDelegate(HandleReload, handle: false, outsidePrediction: false))
            .Register<ReloadingSystem>();
    }

    private void OnScoreReloadableBallistic(Entity<BallisticAmmoProviderComponent> entity, ref ScoreReloadableEvent args)
    {
        if (args.Handled)
            return;
        args.Score = entity.Comp.Count;
        if (entity.Comp.Count == entity.Comp.Capacity)
            args.Full = true;
        args.Handled = true;
    }

    private void OnGetPredicateMagazine(Entity<MagazineAmmoProviderComponent> entity, ref GetReloadablePredicate args)
    {
        if (args.Handled)
            return;
        if (!TryComp<ItemSlotsComponent>(entity.Owner, out var itemSlots))
            return;
        var tags = itemSlots.Slots[SharedGunSystem.MagazineSlot].Whitelist?.Tags;
        if (tags is null)
            return;
        args.Handled = true;
        args.Predicate = x => _tag.HasAnyTag(x, tags);
    }

    private void OnGetPredicateBallistic(Entity<BallisticAmmoProviderComponent> entity, ref GetReloadablePredicate args)
    {
        if (args.Handled)
            return;
        var tags = entity.Comp.Whitelist?.Tags;
        if (tags is null)
            return;
        args.Handled = true;
        args.Predicate = x =>
        {
            if (!TryComp<BallisticAmmoProviderComponent>(x, out var ammos))
                return false;
            if (ammos.Whitelist?.Tags is null)
                return false;
            if (!tags.Intersect(ammos.Whitelist.Tags).Any())
                return false;
            return ammos.Count >= 1;
        };
    }

    private void OnAttemptReloadMagazine(Entity<MagazineAmmoProviderComponent> entity, ref AttemptReloadEvent args)
    {
        var ammoSlot = _container.EnsureContainer<ContainerSlot>(entity, SharedGunSystem.MagazineSlot);
        var oldAmmo = ammoSlot.ContainedEntity;
        if (oldAmmo is null)
            return;
        var ev = new ScoreReloadableEvent();
        RaiseLocalEvent(oldAmmo.Value, ref ev);
        if (ev.Full)
            args.Cancelled = true;
    }

    private void OnAttemptReloadBallistic(Entity<BallisticAmmoProviderComponent> entity, ref AttemptReloadEvent args)
    {
        if (entity.Comp.Count >= entity.Comp.Capacity)
            args.Cancelled = true;
    }

    private void OnReloadMagazine(Entity<MagazineAmmoProviderComponent> entity, ref ReloadEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var ammoSlot = _container.EnsureContainer<ContainerSlot>(entity, SharedGunSystem.MagazineSlot);
        var oldAmmo = ammoSlot.ContainedEntity;
        if (oldAmmo is not null)
            _container.Remove(oldAmmo.Value, ammoSlot);
        _container.Insert(args.Replacement, ammoSlot);
        if (oldAmmo is not null)
            _container.InsertOrDrop(oldAmmo.Value, args.ReplacementContainer);
        _gun.UpdateAmmoCount(entity.Owner);
    }

    private void OnReloadBallistic(Entity<BallisticAmmoProviderComponent> entity, ref ReloadEvent args)
    {
        if (args.Handled)
            return;

        if (entity.Comp.Count >= entity.Comp.Capacity)
            return;

        if (!TryComp<BallisticAmmoProviderComponent>(args.Replacement, out var ammos))
            return;

        var ammo = ammos.Container.ContainedEntities.FirstOrNull();
        if (ammo is not null)
        {
            args.Handled = true;
            _container.Insert(ammo.Value, entity.Comp.Container);
            _gun.UpdateAmmoCount(args.Replacement);
        }
        else if (ammos.UnspawnedCount >= 1)
        {
            if (!PredictedTrySpawnInContainer(ammos.Proto, entity, "ballistic-ammo", out _))
                return;
            _gun.SetBallisticUnspawned(entity, ammos.UnspawnedCount - 1);
            args.Handled = true;
        }

        _gun.UpdateAmmoCount(entity.Owner);
        
        if (entity.Comp.Count < entity.Comp.Capacity)
            StartReloadDoAfter(args.Reloader, args.Reloadee);
    }

    private void OnAttemptShoot(Entity<ActiveReloadingComponent> entity, ref AttemptShootEvent args)
    {
        args.Cancelled = true;
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

    public bool StartReloadDoAfter(EntityUid reloader, Entity<ReloadableComponent> reloadee)
    {
        if (!TryGetReloadingArgsStorage(reloader, out var storage))
            return false;

        return StartReloadDoAfter(reloader, reloadee, storage.Value);
    }

    public bool StartReloadDoAfter(EntityUid reloader, Entity<ReloadableComponent> reloadee, EntityUid storage)
    {
        if (!ReloadNow(reloader, reloadee, storage, suppress: true))
            return false;

        var doAfterArgs = new DoAfterArgs(EntityManager, reloader, reloadee.Comp.ReloadTime, new ReloadDoAfterEvent(), reloadee.Owner, null, reloadee.Owner)
        {
            BreakOnDamage = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            BreakOnMove = false,
            BreakOnWeightlessMove = false,
            NeedHand = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return false;

        AddComp(reloadee.Owner, new ActiveReloadingComponent());
        _audio.PlayPredicted(reloadee.Comp.ReloadStartSound, reloader, reloader);
        return true;
    }

    private void OnReloadDoAfterFinished(Entity<ReloadableComponent> entity, ref ReloadDoAfterEvent args)
    {
        RemComp<ActiveReloadingComponent>(entity.Owner);

        if (args.Cancelled)
            return;

        if (!TryGetReloadingArgsStorage(args.User, out var storage))
            return;

        ReloadNow(args.User, entity, storage.Value);
    }

    public bool ReloadNow(EntityUid reloader, Entity<ReloadableComponent> reloadee, EntityUid storage, bool suppress = false)
    {
        var attemptEv = new AttemptReloadEvent();
        RaiseLocalEvent(reloadee.Owner, ref attemptEv);
        if (attemptEv.Cancelled)
            return false;

        var replacementContainer = _container.EnsureContainer<Container>(storage, "storagebase");
        var predicateEv = new GetReloadablePredicate();
        RaiseLocalEvent(reloadee.Owner, ref predicateEv);
        if (!predicateEv.Handled)
            return false;

        var predicate = predicateEv.Predicate!;

        EntityUid? replacement = null;
        var highestScore = 0;
        var lowestNetID = int.MaxValue;
        foreach (var item in replacementContainer.ContainedEntities.Where(x => predicate(x)))
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

        var reloadEv = new ReloadEvent(replacement.Value, replacementContainer, reloader, reloadee);
        RaiseLocalEvent(reloadee.Owner, ref reloadEv);

        if (!reloadEv.Handled)
            return false;

        _audio.PlayPredicted(reloadee.Comp.ReloadEndSound, reloader, reloader);
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

/// <summary>
/// Raised on an item (like a gun) to force it to reload.
/// If Suppress is set to true it would not actually reload but still test if it is possible.
/// </summary>
/// <param name="Suppress">If it is set to true, then it won't actually reload.</param>
/// <param name="Handled">If a reload was successful or was set to </param>
[ByRefEvent]
public record struct ReloadEvent(EntityUid Replacement, Container ReplacementContainer, EntityUid Reloader, Entity<ReloadableComponent> Reloadee, bool Handled = false);

/// <summary>
/// Raised on an item to fetch a predicate to evaluate if it's valid to be used to reload with.
/// </summary>
/// <param name="Predicate"></param>
/// <param name="Handled"></param>
[ByRefEvent]
public record struct GetReloadablePredicate(Predicate<EntityUid>? Predicate, bool Handled = false);

/// <summary>
/// Cancellable event raised on an item being reloaded.
/// Cancelled if the item is already full, for example.
/// </summary>
/// <param name="Cancelled"></param>
[ByRefEvent]
public record struct AttemptReloadEvent(bool Cancelled = false);

[Serializable, NetSerializable]
public sealed partial class ReloadDoAfterEvent : SimpleDoAfterEvent;
