// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Zombies.Components;
using Content.Shared.Actions;
using Content.Shared.Gravity;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Zombies;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Goobstation.Shared.Zombies;

/// <summary>
///     Gives volatile zombies a pounce: a forward leap that knocks down the
///     first living victim the zombie lands on.
/// </summary>
public sealed class ZombieLeapSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Action grant/removal lives in the stage systems - this engine allows only
        // one directed subscriber per component/event pair.
        SubscribeLocalEvent<VolatileZombieComponent, ZombieLeapActionEvent>(OnLeap);

        SubscribeLocalEvent<ZombieLeapingComponent, StartCollideEvent>(OnLeapingCollide);
        SubscribeLocalEvent<ZombieLeapingComponent, LandEvent>(OnLeapingLand);
        SubscribeLocalEvent<ZombieLeapingComponent, StopThrowEvent>(OnLeapingStopThrow);
    }

    private void OnLeap(Entity<VolatileZombieComponent> ent, ref ZombieLeapActionEvent args)
    {
        if (args.Handled)
            return;

        if (_gravity.IsWeightless(ent.Owner) || _standing.IsDown(ent.Owner))
        {
            _popup.PopupClient(Loc.GetString("jump-ability-failure"), ent, ent);
            return;
        }

        var xform = Transform(ent);
        var throwing = xform.LocalRotation.ToWorldVec() * ent.Comp.LeapDistance;
        var direction = xform.Coordinates.Offset(throwing);

        _throwing.TryThrow(ent.Owner, direction, ent.Comp.LeapSpeed);
        _audio.PlayPredicted(ent.Comp.LeapSound, ent, ent);

        var leaping = EnsureComp<ZombieLeapingComponent>(ent);
        leaping.KnockdownDuration = ent.Comp.LeapKnockdownDuration;
        Dirty(ent.Owner, leaping);

        args.Handled = true;
    }

    private void OnLeapingCollide(Entity<ZombieLeapingComponent> ent, ref StartCollideEvent args)
    {
        RemCompDeferred<ZombieLeapingComponent>(ent);

        var target = args.OtherEntity;

        // Pouncing only floors the living, not fellow zeds or furniture.
        if (!HasComp<MobStateComponent>(target) || HasComp<ZombieComponent>(target))
            return;

        _stun.TryKnockdown(target, ent.Comp.KnockdownDuration, force: true);
    }

    private void OnLeapingLand(Entity<ZombieLeapingComponent> ent, ref LandEvent args)
    {
        RemCompDeferred<ZombieLeapingComponent>(ent);
    }

    private void OnLeapingStopThrow(Entity<ZombieLeapingComponent> ent, ref StopThrowEvent args)
    {
        RemCompDeferred<ZombieLeapingComponent>(ent);
    }
}

public sealed partial class ZombieLeapActionEvent : InstantActionEvent;
