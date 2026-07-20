// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Zombies.Components;
using Content.Server.Zombies;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Zombies;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.Zombies;

/// <summary>
///     Owns everything about a zombie's worn gear: keeps the attack
///     presentation honest (muzzled = claw arc, not a bite - see
///     ZombieSystem.IsMuzzled and its OnMeleeHit checks) and, since losing a
///     mask is what unlocks biting, enforces a short cooldown after any
///     unequip before that zombie can equip anything again - otherwise
///     "strip the muzzle to bite" chains straight into "instantly re-gear
///     off whatever's on the floor."
/// </summary>
public sealed class ZombieGearSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ZombieSystem _zombie = default!;

    private static readonly EntProtoId ClawAnimation = "WeaponArcClaw";
    private static readonly SoundSpecifier ClawSound = new SoundPathSpecifier("/Audio/Weapons/slash.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombieComponent, DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<ZombieComponent, DidUnequipEvent>(OnDidUnequip);
        SubscribeLocalEvent<ZombieComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
    }

    private void OnDidEquip(Entity<ZombieComponent> ent, ref DidEquipEvent args)
    {
        if (args.Slot == "mask")
            RefreshBiteVisuals(ent);
    }

    private void OnDidUnequip(Entity<ZombieComponent> ent, ref DidUnequipEvent args)
    {
        if (args.Slot == "mask")
            RefreshBiteVisuals(ent);

        var gearLock = EnsureComp<ZombieGearLockComponent>(ent);
        gearLock.LockedUntil = _timing.CurTime + gearLock.LockDuration;
        Dirty(ent, gearLock);
    }

    private void OnEquipAttempt(Entity<ZombieComponent> ent, ref IsEquippingAttemptEvent args)
    {
        if (!TryComp<ZombieGearLockComponent>(ent, out var gearLock) || _timing.CurTime >= gearLock.LockedUntil)
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("zombie-gear-fumble"), ent, ent);
    }

    /// <summary>
    ///     Points the zombie's melee animation and hit sound at claws or bite
    ///     depending on whether a muzzle is blocking its mouth.
    /// </summary>
    public void RefreshBiteVisuals(EntityUid uid)
    {
        if (!TryComp<ZombieComponent>(uid, out var zombie) || !TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        if (_zombie.IsMuzzled(uid))
        {
            melee.Animation = ClawAnimation;
            melee.WideAnimation = ClawAnimation;
            melee.HitSound = ClawSound;
        }
        else
        {
            melee.Animation = zombie.AttackAnimation;
            melee.WideAnimation = zombie.AttackAnimation;
            melee.HitSound = zombie.BiteSound;
        }

        Dirty(uid, melee);
    }
}
