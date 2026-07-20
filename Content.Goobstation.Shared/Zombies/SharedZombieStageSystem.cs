// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Overlays;
using Content.Goobstation.Shared.Zombies.Components;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Zombies;

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
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VolatileZombieComponent, RefreshMovementSpeedModifiersEvent>(OnVolatileRefreshSpeed);
        SubscribeLocalEvent<WalkerZombieComponent, RefreshMovementSpeedModifiersEvent>(OnWalkerRefreshSpeed);

        SubscribeLocalEvent<VolatileZombieComponent, ComponentStartup>(OnVolatileStartup);
        SubscribeLocalEvent<WalkerZombieComponent, ComponentStartup>(OnWalkerStartup);

        SubscribeLocalEvent<VolatileZombieComponent, ComponentShutdown>(OnVolatileShutdown);
        SubscribeLocalEvent<WalkerZombieComponent, ComponentShutdown>(OnWalkerShutdown);

        // Classic class event: must use the non-ref handler form.
        SubscribeLocalEvent<VolatileZombieComponent, MeleeHitEvent>(OnVolatileMeleeHit);
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

        // Freshly turned: claws over teeth. Restored on stage shutdown.
        if (TryComp<MeleeWeaponComponent>(ent, out var melee))
        {
            melee.Damage = ent.Comp.AttackDamage;
            Dirty(ent.Owner, melee);
        }

        // Still sharp-eyed, but already hunting by body heat.
        GrantHeatVision(ent.Owner, drawOverlay: false, color: Color.FromHex("#d06764"), lightRadius: 0f);
    }

    private void OnWalkerStartup(Entity<WalkerZombieComponent> ent, ref ComponentStartup args)
    {
        ApplyStageModifiers(ent, ent.Comp.DamageModifierSet);

        // Rotted eyes: genuinely blind to the environment, but warm bodies
        // still stand out through their heat-vision.
        GrantHeatVision(ent.Owner, drawOverlay: false, color: Color.FromHex("#d06764"), lightRadius: 0f);
        EnsureComp<ZombieBlindComponent>(ent.Owner);
    }

    private void GrantHeatVision(EntityUid uid, bool drawOverlay, Color color, float lightRadius, float overlayOpacity = 0.5f)
    {
        var thermal = EnsureComp<ThermalVisionComponent>(uid);
        thermal.IsEquipment = false;
        thermal.ToggleAction = null;
        thermal.IsActive = true;
        thermal.DrawOverlay = drawOverlay;
        thermal.OverlayOpacity = overlayOpacity;
        thermal.Color = color;
        thermal.LightRadius = lightRadius;
        Dirty(uid, thermal);
    }

    private void OnVolatileShutdown(Entity<VolatileZombieComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.LeapActionEntity);
        RefreshAfterStageRemoved(ent);

        if (TerminatingOrDeleted(ent))
            return;

        // Back to the base zombie bite (walker transition or cure).
        if (TryComp<MeleeWeaponComponent>(ent, out var melee) && TryComp<ZombieComponent>(ent, out var zombie))
        {
            melee.Damage = zombie.DamageOnBite;
            Dirty(ent.Owner, melee);
        }
    }

    private void OnWalkerShutdown(Entity<WalkerZombieComponent> ent, ref ComponentShutdown args)
    {
        RefreshAfterStageRemoved(ent);
    }

    private void OnVolatileMeleeHit(EntityUid uid, VolatileZombieComponent comp, MeleeHitEvent args)
    {
        // Examine raises this event too, with IsHit false.
        if (!args.IsHit)
            return;

        var origin = _transform.GetMapCoordinates(uid);

        foreach (var target in args.HitEntities)
        {
            // Only shove the living - not fellow zeds, not furniture.
            if (!HasComp<MobStateComponent>(target) || HasComp<ZombieComponent>(target))
                continue;

            var dir = _transform.GetMapCoordinates(target).Position - origin.Position;
            if (dir.LengthSquared() < 0.01f)
                continue;

            _throwing.TryThrow(target, dir.Normalized() * comp.KnockbackDistance, comp.KnockbackSpeed, animated: false, playSound: false, doSpin: false);
        }
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
