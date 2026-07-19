// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Zombies.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Zombies;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Goobstation.Server.Zombies;

/// <summary>
///     Walkers don't stay dead. A downed walker gets back up after a delay unless
///     the corpse was burned past its destruction threshold, lost its head, or was
///     cured while it was down. The delay is the crew's capture-and-cure window.
/// </summary>
public sealed class ZombieReanimationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier RiseSound = new SoundPathSpecifier("/Audio/Voice/Zombie/zombie-3.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WalkerZombieComponent, MobStateChangedEvent>(OnMobState);
    }

    private void OnMobState(Entity<WalkerZombieComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && !ent.Comp.PermanentlyDead)
            ent.Comp.ReanimateAt = _timing.CurTime + _random.Next(ent.Comp.ReanimationDelayMin, ent.Comp.ReanimationDelayMax);
        else if (args.NewMobState == MobState.Alive)
            ent.Comp.ReanimateAt = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<WalkerZombieComponent, ZombieComponent, MobStateComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var walker, out _, out var mobState, out var damageable))
        {
            if (walker.ReanimateAt is not { } reanimateAt || curTime < reanimateAt)
                continue;

            walker.ReanimateAt = null;

            if (!_mobState.IsDead(uid, mobState))
                continue;

            if (!CanReanimate(uid, walker, damageable))
            {
                walker.PermanentlyDead = true;
                continue;
            }

            Reanimate(uid);
        }
    }

    private bool CanReanimate(EntityUid uid, WalkerZombieComponent walker, DamageableComponent damageable)
    {
        // Burned out: too much of the body destroyed by fire.
        if (damageable.Damage.DamageDict.TryGetValue("Heat", out var heat)
            && heat.Float() >= walker.HeatDestructionThreshold)
            return false;

        // Headless: decapitation is a permanent kill (at the cost of any cure).
        if (TryComp<BodyComponent>(uid, out var body)
            && !_body.GetBodyChildrenOfType(uid, BodyPartType.Head, body).Any())
            return false;

        return true;
    }

    private void Reanimate(EntityUid uid)
    {
        // Full restore, then stand the walker back up. Deliberately reuses the same
        // rejuvenate path zombification itself uses so Shitmed wounds reset cleanly.
        RaiseLocalEvent(uid, new RejuvenateEvent(false, false));
        _mobState.ChangeMobState(uid, MobState.Alive);

        _popup.PopupEntity(Loc.GetString("zombie-reanimation-rise", ("zombie", uid)), uid, PopupType.LargeCaution);
        _audio.PlayPvs(RiseSound, uid);
    }
}
