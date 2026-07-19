// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Goobstation.Shared.Zombies.Components;

/// <summary>
///     The decayed zombie stage: a slow, extremely durable shambler that a volatile
///     zombie (<see cref="VolatileZombieComponent"/>) turns into when its timer runs out.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WalkerZombieComponent : Component
{
    /// <summary>
    ///     Movement speed multiplier applied on top of the base zombie speed modifier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MovementSpeedMultiplier = 0.65f;

    /// <summary>
    ///     Damage modifier set applied while in this stage. Null keeps the set the
    ///     zombie already has.
    /// </summary>
    [DataField]
    public string? DamageModifierSet = "ZombieWalker";

    /// <summary>
    ///     How long after death the walker reanimates. Rolled between these bounds,
    ///     giving the crew a window to secure (or permanently destroy) the body.
    /// </summary>
    [DataField]
    public TimeSpan ReanimationDelayMin = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan ReanimationDelayMax = TimeSpan.FromSeconds(90);

    /// <summary>
    ///     When this dead walker gets back up. Null while alive or permanently dead.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? ReanimateAt;

    /// <summary>
    ///     Total Heat damage on the corpse at or above which it is too destroyed
    ///     to reanimate. Burning bodies is the crew's permanent disposal option.
    /// </summary>
    [DataField]
    public float HeatDestructionThreshold = 150f;

    /// <summary>
    ///     Set once a reanimation check permanently fails (burned out or headless);
    ///     the corpse stays down for good.
    /// </summary>
    [DataField]
    public bool PermanentlyDead;

    /// <summary>
    ///     Reagent that cures a downed walker when enough of it is in the corpse's
    ///     injectable solution. Checked directly by the reanimation system because
    ///     corpse metabolism is unreliable.
    /// </summary>
    [DataField]
    public string CureReagent = "Ambuzol";

    [DataField]
    public FixedPoint2 CureReagentAmount = 10;

    /// <summary>
    ///     Stronger variant that also inoculates the cured victim.
    /// </summary>
    [DataField]
    public string InoculateReagent = "AmbuzolPlus";

    [DataField]
    public FixedPoint2 InoculateReagentAmount = 5;
}
