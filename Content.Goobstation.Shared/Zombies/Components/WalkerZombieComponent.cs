// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

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
}
