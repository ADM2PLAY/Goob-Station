// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Zombies;
using Content.Goobstation.Shared.Zombies.Components;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Zombies;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.Zombies;

/// <summary>
///     Drives zombie lifecycle stage transitions: fresh conversions start volatile,
///     then decay into walkers when their timer runs out.
/// </summary>
public sealed class ZombieStageSystem : SharedZombieStageSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ZombieMuzzleSystem _muzzle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombieComponent, EntityZombifiedEvent>(OnZombified);
        SubscribeLocalEvent<VolatileZombieComponent, MapInitEvent>(OnVolatileMapInit);
        SubscribeLocalEvent<ZombieCuredEvent>(OnZombieCured);
    }

    private void OnZombified(Entity<ZombieComponent> ent, ref EntityZombifiedEvent args)
    {
        // Converted while already muzzled (the cuff-and-muzzle capture flow):
        // start with claw visuals instead of the bite.
        _muzzle.RefreshBiteVisuals(ent);

        // Every fresh conversion starts in the volatile stage, unless something
        // (e.g. a prototype or an admin) already made this zombie a walker.
        if (HasComp<WalkerZombieComponent>(ent) || HasComp<VolatileZombieComponent>(ent))
            return;

        EnsureComp<VolatileZombieComponent>(ent);

        var ev = new ZombieStageChangedEvent(ent, null, ZombieStage.Volatile);
        RaiseLocalEvent(ent, ref ev);
    }

    private void OnZombieCured(ref ZombieCuredEvent args)
    {
        // Cured: drop any stage state with the zombie.
        RemCompDeferred<VolatileZombieComponent>(args.Target);
        RemCompDeferred<WalkerZombieComponent>(args.Target);
    }

    private void OnVolatileMapInit(Entity<VolatileZombieComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.TransitionAt = _timing.CurTime + ent.Comp.Duration;
        _actions.AddAction(ent, ref ent.Comp.LeapActionEntity, ent.Comp.LeapAction);
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<VolatileZombieComponent, ZombieComponent>();
        while (query.MoveNext(out var uid, out var volatileComp, out _))
        {
            if (curTime < volatileComp.TransitionAt)
                continue;

            TransitionToWalker(uid);
        }
    }

    /// <summary>
    ///     Decays a zombie into the walker stage.
    /// </summary>
    public void TransitionToWalker(EntityUid uid)
    {
        if (!HasComp<ZombieComponent>(uid) || HasComp<WalkerZombieComponent>(uid))
            return;

        var wasVolatile = RemComp<VolatileZombieComponent>(uid);
        EnsureComp<WalkerZombieComponent>(uid);

        _popup.PopupEntity(Loc.GetString("zombie-stage-walker-transition", ("zombie", uid)), uid, PopupType.LargeCaution);

        var ev = new ZombieStageChangedEvent(uid, wasVolatile ? ZombieStage.Volatile : null, ZombieStage.Walker);
        RaiseLocalEvent(uid, ref ev);
    }
}
