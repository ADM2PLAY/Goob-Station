// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Zombies.Components;

/// <summary>
///     Tracks a short cooldown after a zombie loses a piece of worn gear
///     (mask, eyes, armor, etc.) before it can equip anything into that slot
///     again. Exists so unmuzzling to bite (or losing gear in a struggle)
///     can't be chained into instantly re-gearing off a dropped item.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZombieGearLockComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan LockedUntil;

    [DataField]
    public TimeSpan LockDuration = TimeSpan.FromSeconds(2.5);
}
