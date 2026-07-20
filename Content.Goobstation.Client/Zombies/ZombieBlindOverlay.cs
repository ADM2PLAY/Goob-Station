// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Zombies.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Client.Zombies;

/// <summary>
///     Rotted-eyes vision: the same "Noir" shader the noir-tech detective
///     glasses use (Sin City style - everything greyscale except red), drawn
///     behind the thermal-vision entity-highlight pass (ZIndex -1) so
///     heat-highlighted bodies still stand out clearly on top of it. Blood
///     and anything else red in the world stays in color too.
/// </summary>
public sealed class ZombieBlindOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> NoirShader = "Noir";

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _shader;

    public ZombieBlindOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = -10;
        _shader = _prototypeManager.Index(NoirShader).InstanceUnique();
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

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }
}
