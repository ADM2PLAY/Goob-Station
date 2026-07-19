// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Zombies.Components;

/// <summary>
///     Marks a volatile zombie that is currently mid-pounce. Whoever it lands on
///     gets knocked down. Removed on landing or collision.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZombieLeapingComponent : Component
{
    /// <summary>
    ///     How long a victim is knocked down when pounced on.
    /// </summary>
    [DataField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(2);
}
