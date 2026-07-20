// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Zombies.Components;

/// <summary>
///     Present on an infected victim once symptoms turn visible (Fever
///     onward). Drives the client-side dread vignette; heartbeat audio is
///     played directly by the server. Removed on cure or conversion.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZombieFeverComponent : Component
{
    /// <summary>
    ///     How far gone the infection is, 0 (just turned feverish) to 1
    ///     (about to convert). Drives vignette strength/pulse speed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Intensity;
}
