// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
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
    public TimeSpan Duration = TimeSpan.FromMinutes(4);

    /// <summary>
    ///     When this zombie decays into a walker. Set on map init.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan TransitionAt;

    /// <summary>
    ///     Movement speed multiplier applied on top of the base zombie speed modifier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MovementSpeedMultiplier = 1f;

    /// <summary>
    ///     Damage modifier set applied while in this stage. Null keeps the set the
    ///     zombie already has.
    /// </summary>
    [DataField]
    public string? DamageModifierSet;
}
