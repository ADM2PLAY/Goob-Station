// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Zombies.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Client.Zombies;

/// <summary>
///     Rotted-eyes vision, drawn behind the thermal-vision entity-highlight
///     pass (which sits at ZIndex -1) so a blind walker's environment reads
///     as dim, greyscale, and hard to parse - like the game's Blind trait -
///     rather than a total void, while whatever their heat-vision picks out
///     still stands out clearly on top of it.
/// </summary>
public sealed class ZombieBlindOverlay : Overlay
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _greyscaleShader;

    public ZombieBlindOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = -10;
        _greyscaleShader = _prototypeManager.Index<ShaderPrototype>("GreyscaleFullscreen").InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _playerManager.LocalEntity is { Valid: true } player
            && _entityManager.HasComponent<ZombieBlindComponent>(player);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null)
            return;

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;

        _greyscaleShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        worldHandle.UseShader(_greyscaleShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);

        // Dim heavily on top of the desaturation, tinted blood-red - vague
        // shapes survive, detail doesn't.
        worldHandle.DrawRect(viewport, Color.FromHex("#3a0000").WithAlpha(0.8f));
    }
}
