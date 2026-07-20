// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Zombies.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Goobstation.Client.Zombies;

/// <summary>
///     Solid blackout, drawn behind the thermal-vision entity-highlight pass
///     (which sits at ZIndex -1) so a blind walker sees nothing of the
///     environment except whatever their heat-vision picks out on top of it.
/// </summary>
public sealed class ZombieBlindOverlay : Overlay
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override bool RequestScreenTexture => false;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ZombieBlindOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = -10;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _playerManager.LocalEntity is { Valid: true } player
            && _entityManager.HasComponent<ZombieBlindComponent>(player);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        args.WorldHandle.DrawRect(args.WorldBounds, Color.Black);
    }
}
