// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Zombies.Components;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Movement.Systems;

namespace Content.Goobstation.Shared.Zombies;

/// <summary>
///     Applies the per-stage modifiers (movement speed, damage modifier set) for
///     zombie lifecycle stages. Stage transitions themselves are driven serverside.
/// </summary>
public abstract class SharedZombieStageSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VolatileZombieComponent, RefreshMovementSpeedModifiersEvent>(OnVolatileRefreshSpeed);
        SubscribeLocalEvent<WalkerZombieComponent, RefreshMovementSpeedModifiersEvent>(OnWalkerRefreshSpeed);

        SubscribeLocalEvent<VolatileZombieComponent, ComponentStartup>(OnVolatileStartup);
        SubscribeLocalEvent<WalkerZombieComponent, ComponentStartup>(OnWalkerStartup);

        SubscribeLocalEvent<VolatileZombieComponent, ComponentShutdown>(OnVolatileShutdown);
        SubscribeLocalEvent<WalkerZombieComponent, ComponentShutdown>(OnWalkerShutdown);
    }

    private void OnVolatileRefreshSpeed(Entity<VolatileZombieComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.MovementSpeedMultiplier, ent.Comp.MovementSpeedMultiplier);
    }

    private void OnWalkerRefreshSpeed(Entity<WalkerZombieComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.MovementSpeedMultiplier, ent.Comp.MovementSpeedMultiplier);
    }

    private void OnVolatileStartup(Entity<VolatileZombieComponent> ent, ref ComponentStartup args)
    {
        ApplyStageModifiers(ent, ent.Comp.DamageModifierSet);
    }

    private void OnWalkerStartup(Entity<WalkerZombieComponent> ent, ref ComponentStartup args)
    {
        ApplyStageModifiers(ent, ent.Comp.DamageModifierSet);
    }

    private void OnVolatileShutdown(Entity<VolatileZombieComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.LeapActionEntity);
        RefreshAfterStageRemoved(ent);
    }

    private void OnWalkerShutdown(Entity<WalkerZombieComponent> ent, ref ComponentShutdown args)
    {
        RefreshAfterStageRemoved(ent);
    }

    private void RefreshAfterStageRemoved(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void ApplyStageModifiers(EntityUid uid, string? damageModifierSet)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);

        if (damageModifierSet != null)
            _damageable.SetDamageModifierSetId(uid, damageModifierSet);
    }
}
