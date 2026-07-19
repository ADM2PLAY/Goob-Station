// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Shared.Zombies;

/// <summary>
///     Lifecycle stages a zombie moves through after conversion.
/// </summary>
public enum ZombieStage : byte
{
    /// <summary>
    ///     Freshly converted: fast, aggressive, short-lived form.
    /// </summary>
    Volatile,

    /// <summary>
    ///     Decayed form: slow, extremely durable shambler.
    /// </summary>
    Walker,
}

/// <summary>
///     Raised directed on a zombie when it transitions between lifecycle stages.
///     OldStage is null when the zombie enters its first stage on conversion.
/// </summary>
[ByRefEvent]
public readonly record struct ZombieStageChangedEvent(EntityUid Zombie, ZombieStage? OldStage, ZombieStage NewStage);

/// <summary>
///     Raised (directed and broadcast) after an entity is successfully cured of
///     zombification. Broadcast so multiple systems can react - this engine allows
///     only one directed subscriber per component/event pair.
/// </summary>
[ByRefEvent]
public readonly record struct ZombieCuredEvent(EntityUid Target);
