// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Shared.Zombies.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.Zombies;

/// <summary>
///     Pulsing red vignette that closes in as an infection worsens through
///     Fever and Terminal - the "your heart is racing and it's getting
///     worse" tell. Mirrors the vanilla pain-overlay's visual language
///     (same GradientCircleMask shader), driven by ZombieFeverComponent's
///     Intensity instead of pain.
/// </summary>
public sealed class ZombieFeverOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _shader;

    public ZombieFeverOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return _playerManager.LocalEntity is { Valid: true } player
            && _entityManager.TryGetComponent(player, out ZombieFeverComponent? fever)
            && fever.Intensity > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { Valid: true } player
            || !_entityManager.TryGetComponent(player, out ZombieFeverComponent? fever))
            return;

        var intensity = fever.Intensity;
        var viewport = args.WorldAABB;
        var handle = args.WorldHandle;
        var distance = args.ViewportBounds.Width;

        // Pulse gets faster as the infection worsens - a racing heartbeat.
        var pulseRate = float.Lerp(1.5f, 5f, intensity);
        var time = (float) _timing.RealTime.TotalSeconds * pulseRate;
        var pulse = MathF.Max(0f, MathF.Sin(time));

        var outerMax = 2.0f * distance;
        var outerMin = 0.75f * distance;
        var innerMax = 0.6f * distance;
        var innerMin = 0.25f * distance;

        var outerRadius = outerMax - intensity * (outerMax - outerMin);
        var innerRadius = innerMax - intensity * (innerMax - innerMin);

        _shader.SetParameter("time", pulse);
        _shader.SetParameter("color", new Vector3(0.7f, 0.05f, 0.05f));
        _shader.SetParameter("darknessAlphaOuter", float.Lerp(0.1f, 0.65f, intensity));
        _shader.SetParameter("outerCircleRadius", outerRadius);
        _shader.SetParameter("outerCircleMaxRadius", outerRadius + 0.2f * distance);
        _shader.SetParameter("innerCircleRadius", innerRadius);
        _shader.SetParameter("innerCircleMaxRadius", innerRadius + 0.02f * distance);

        handle.UseShader(_shader);
        handle.DrawRect(viewport, Color.White);
        handle.UseShader(null);
    }
}
