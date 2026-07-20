// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Zombies.Components;

/// <summary>
///     Marker: this zombie's eyes have rotted out. Its owner sees nothing but
///     black, except for whatever their thermal vision highlights.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZombieBlindComponent : Component;
