// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Zombies;
using Content.Shared.Inventory.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Zombies;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.Zombies;

/// <summary>
///     Keeps a zombie's attack presentation honest: a muzzled zombie can't bite
///     (see ZombieSystem.IsMuzzled and its OnMeleeHit checks), so its swings show
///     a claw arc and slash sound instead of the bite chomp.
/// </summary>
public sealed class ZombieMuzzleSystem : EntitySystem
{
    [Dependency] private readonly ZombieSystem _zombie = default!;

    private static readonly EntProtoId ClawAnimation = "WeaponArcClaw";
    private static readonly SoundSpecifier ClawSound = new SoundPathSpecifier("/Audio/Weapons/slash.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombieComponent, DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<ZombieComponent, DidUnequipEvent>(OnDidUnequip);
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
