// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Goobstation.Shared.Zombies.Components;

/// <summary>
///     The freshly-converted zombie stage. Fast and dangerous, but only for a short
///     window, after which the zombie decays into a walker (<see cref="WalkerZombieComponent"/>).
///     Volatiles that die stay dead - only walkers reanimate.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VolatileZombieComponent : Component
{
    /// <summary>
    ///     How long the volatile stage lasts before decaying into a walker.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     When this zombie decays into a walker. Set on map init.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan TransitionAt;

    /// <summary>
    ///     Movement speed multiplier applied on top of the base zombie speed modifier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MovementSpeedMultiplier = 1.3f;

    /// <summary>
    ///     Damage modifier set applied while in this stage. Null keeps the set the
    ///     zombie already has.
    /// </summary>
    [DataField]
    public string? DamageModifierSet = "ZombieVolatile";

    /// <summary>
    ///     The pounce action granted while volatile.
    /// </summary>
    [DataField]
    public EntProtoId LeapAction = "ActionZombieLeap";

    [DataField, AutoNetworkedField]
    public EntityUid? LeapActionEntity;

    /// <summary>
    ///     How far the pounce carries the zombie, in tiles.
    /// </summary>
    [DataField]
    public float LeapDistance = 5f;

    /// <summary>
    ///     Throw speed of the pounce.
    /// </summary>
    [DataField]
    public float LeapSpeed = 12f;

    /// <summary>
    ///     How long a victim the zombie lands on is knocked down.
    /// </summary>
    [DataField]
    public TimeSpan LeapKnockdownDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public SoundSpecifier? LeapSound = new SoundPathSpecifier("/Audio/Voice/Zombie/zombie-2.ogg");

    /// <summary>
    ///     Melee damage while volatile, replacing the base zombie bite. Restored to
    ///     the zombie's normal bite damage when the stage ends.
    /// </summary>
    [DataField]
    public DamageSpecifier AttackDamage = new()
    {
        DamageDict = new()
        {
            { "Slash", 18 },
            { "Structural", 10 }
        }
    };

    /// <summary>
    ///     How far a clawed victim gets shoved, in tiles.
    /// </summary>
    [DataField]
    public float KnockbackDistance = 1.5f;

    /// <summary>
    ///     Throw speed of the knockback shove.
    /// </summary>
    [DataField]
    public float KnockbackSpeed = 6f;
}
