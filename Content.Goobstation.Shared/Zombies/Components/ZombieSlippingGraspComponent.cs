// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Shared.Zombies.Components;

/// <summary>
///     Marks an item a zombie just grabbed. Their rotten grip fails after a
///     moment and the item squirts out of their hand (see ZombieGraspSystem).
/// </summary>
[RegisterComponent]
public sealed partial class ZombieSlippingGraspComponent : Component
{
    [DataField]
    public EntityUid Holder;

    [DataField]
    public TimeSpan DropAt;
}
