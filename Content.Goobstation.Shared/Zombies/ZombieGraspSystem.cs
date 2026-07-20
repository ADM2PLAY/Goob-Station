// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Zombies.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Zombies;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.Zombies;

/// <summary>
///     Zombies keep their hands (so they can be cuffed and restrained) and can
///     grab things, but their rotten grip fails almost immediately: whatever
///     they pick up squirts out of their hand like it's lubed.
/// </summary>
public sealed class ZombieGraspSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombieComponent, DidEquipHandEvent>(OnDidEquipHand);
        SubscribeLocalEvent<ZombieComponent, UserActivateInWorldEvent>(OnActivateInWorld);
    }

    private void OnActivateInWorld(EntityUid uid, ZombieComponent component, UserActivateInWorldEvent args)
    {
        // Zombies lack complex interaction, so their clicks never reach the
        // normal pickup path - clicking an item lands here instead. Grab it
        // with the paw; the slipping grasp below flings it right back out.
        if (args.Handled || !HasComp<ItemComponent>(args.Target))
            return;

        if (_container.IsEntityInContainer(args.Target))
            return;

        args.Handled = true;
        _hands.TryPickup(uid, args.Target, checkActionBlocker: false);
    }

    private void OnDidEquipHand(EntityUid uid, ZombieComponent component, DidEquipHandEvent args)
    {
        // Server-authoritative: the slip is randomized and thrown, not predictable.
        if (_net.IsClient)
            return;

        var item = args.Equipped;

        // Virtual items are hand-fillers from cuffs/pulling - dropping those
        // would break restraints. Unremoveable speaks for itself.
        if (HasComp<VirtualItemComponent>(item) || HasComp<UnremoveableComponent>(item))
            return;

        var slipping = EnsureComp<ZombieSlippingGraspComponent>(item);
        slipping.Holder = uid;
        slipping.DropAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0.2f, 0.6f));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ZombieSlippingGraspComponent>();
        while (query.MoveNext(out var item, out var slipping))
        {
            if (curTime < slipping.DropAt)
                continue;

            RemCompDeferred<ZombieSlippingGraspComponent>(item);

            var holder = slipping.Holder;
            if (TerminatingOrDeleted(holder) || !_hands.IsHolding(holder, item))
                continue;

            if (!_hands.TryDrop(holder, item, checkActionBlocker: false))
                continue;

            _throwing.TryThrow(item, _random.NextAngle().ToVec() * 1.5f, 4f, playSound: false);
            _popup.PopupEntity(Loc.GetString("zombie-cannot-hold", ("item", item)), holder, holder);
        }
    }
}
