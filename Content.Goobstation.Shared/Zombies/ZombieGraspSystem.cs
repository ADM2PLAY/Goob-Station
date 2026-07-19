// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Zombies;

namespace Content.Goobstation.Shared.Zombies;

/// <summary>
///     Zombies keep their hands (so they can be cuffed and restrained), but their
///     rotten motor control can't actually hold anything.
/// </summary>
public sealed class ZombieGraspSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombieComponent, PickupAttemptEvent>(OnPickupAttempt);
    }

    private void OnPickupAttempt(EntityUid uid, ZombieComponent component, PickupAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        _popup.PopupClient(Loc.GetString("zombie-cannot-hold", ("item", args.Item)), args.Item, uid);
        args.Cancel();
    }
}
