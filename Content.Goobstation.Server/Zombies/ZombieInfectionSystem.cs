// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Zombies.Components;
using Content.Server.Chat.Systems;
using Content.Server.Zombies;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Zombies;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.Zombies;

/// <summary>
///     Drives the staged (TWD-style) zombie infection: a long quiet incubation,
///     a symptomatic fever, then a terminal crash ending in conversion. The
///     upstream flat damage-per-second loop skips anyone this system manages.
/// </summary>
public sealed class ZombieInfectionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementModStatusSystem _movementModStatus = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly ZombieSystem _zombie = default!;

    public override void Initialize()
    {
        base.Initialize();

        // ComponentStartup instead of MapInit: upstream ZombieSystem already owns the
        // PendingZombie MapInit subscription, and this engine allows only one
        // directed subscriber per component/event pair.
        SubscribeLocalEvent<PendingZombieComponent, ComponentStartup>(OnPendingStartup);
        SubscribeLocalEvent<PendingZombieComponent, ComponentShutdown>(OnPendingShutdown);
    }

    private void OnPendingStartup(Entity<PendingZombieComponent> ent, ref ComponentStartup args)
    {
        // Already a zombie, or dead (upstream converts the dead instantly).
        if (HasComp<ZombieComponent>(ent) || _mobState.IsDead(ent))
            return;

        var infection = EnsureComp<ZombieInfectionComponent>(ent);
        var incubation = _random.Next(infection.IncubationMin, infection.IncubationMax);
        infection.Stage = InfectionStage.Incubation;
        infection.StageEndsAt = _timing.CurTime + incubation;
        infection.NextTick = _timing.CurTime + TimeSpan.FromSeconds(1);
        Dirty(ent.Owner, infection);
    }

    private void OnPendingShutdown(Entity<PendingZombieComponent> ent, ref ComponentShutdown args)
    {
        // Cured before turning, or converted (which removes PendingZombie).
        RemCompDeferred<ZombieInfectionComponent>(ent);
        RemCompDeferred<ZombieFeverComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ZombieInfectionComponent, PendingZombieComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var infection, out _, out var damageable, out var mobState))
        {
            // The dead don't progress; ZombifyOnDeath already handled or will handle them.
            if (_mobState.IsDead(uid, mobState))
                continue;

            // Runs every frame, not gated by NextTick: the heartbeat gets faster
            // than once a second as the infection worsens.
            TickDread(uid, infection, curTime);

            if (infection.NextTick > curTime)
                continue;

            infection.NextTick = curTime + TimeSpan.FromSeconds(1);

            if (curTime >= infection.StageEndsAt && AdvanceStage(uid, infection))
                continue;

            TickDamage(uid, infection, damageable, mobState, curTime);
            TickSymptoms(uid, infection);
            TickTerminalStim(uid, infection);
        }
    }

    /// <summary>
    ///     Moves the infection to its next stage. Returns true if the victim
    ///     converted (and the infection component is going away).
    /// </summary>
    private bool AdvanceStage(EntityUid uid, ZombieInfectionComponent infection)
    {
        switch (infection.Stage)
        {
            case InfectionStage.Incubation:
                infection.Stage = InfectionStage.Fever;
                infection.StageEndsAt = _timing.CurTime + infection.FeverDuration;
                Dirty(uid, infection);
                _popup.PopupEntity(Loc.GetString("zombie-infection-stage-fever"), uid, uid, PopupType.MediumCaution);
                return false;

            case InfectionStage.Fever:
                infection.Stage = InfectionStage.Terminal;
                infection.StageEndsAt = _timing.CurTime + infection.TerminalDuration;
                infection.TerminalEnteredAt = _timing.CurTime;
                Dirty(uid, infection);
                _popup.PopupEntity(Loc.GetString("zombie-infection-stage-terminal"), uid, uid, PopupType.LargeCaution);
                return false;

            default:
                // Terminal ran out: the virus wins no matter how healthy they still are.
                _zombie.ZombifyEntity(uid);
                return true;
        }
    }

    private void TickDamage(EntityUid uid, ZombieInfectionComponent infection, DamageableComponent damageable, MobStateComponent mobState, TimeSpan curTime)
    {
        var damage = infection.Stage switch
        {
            InfectionStage.Fever => infection.FeverDamage,
            // Grows the longer they linger in Terminal - Dylovene (or any Poison
            // healing) can outpace this early on, but not forever.
            InfectionStage.Terminal => infection.TerminalDamage * CalculateTerminalGrowth(infection, curTime),
            _ => null,
        };

        if (damage == null)
            return;

        var multiplier = _mobState.IsCritical(uid, mobState)
            ? infection.CritDamageMultiplier
            : 1f;

        _damageable.TryChangeDamage(uid,
            damage * multiplier,
            true,
            false,
            damageable,
            targetPart: TargetBodyPart.All,
            splitDamage: SplitDamageBehavior.SplitEnsureAll);
    }

    private static float CalculateTerminalGrowth(ZombieInfectionComponent infection, TimeSpan curTime)
    {
        var elapsedSeconds = Math.Max(0, (curTime - infection.TerminalEnteredAt).TotalSeconds);
        return MathF.Pow(infection.TerminalGrowthRate, (float) elapsedSeconds);
    }

    /// <summary>
    ///     While Terminal and actively metabolizing the stim reagent (Dylovene
    ///     by default), grants a temporary "riding the adrenaline" speed boost -
    ///     the small reward for engaging with the chug-to-survive loop.
    /// </summary>
    private void TickTerminalStim(EntityUid uid, ZombieInfectionComponent infection)
    {
        if (infection.Stage != InfectionStage.Terminal)
            return;

        if (!_solutionContainer.TryGetInjectableSolution(uid, out _, out var solution)
            || solution.GetTotalPrototypeQuantity(infection.StimReagent) <= FixedPoint2.Zero)
            return;

        _movementModStatus.TryAddMovementSpeedModDuration(uid,
            MovementModStatusSystem.ReagentSpeed,
            TimeSpan.FromSeconds(3),
            infection.StimWalkSpeedModifier,
            infection.StimSprintSpeedModifier);
    }

    /// <summary>
    ///     Updates the dread vignette intensity and, independently, plays the
    ///     private heartbeat sting on its own (accelerating) schedule.
    /// </summary>
    private void TickDread(EntityUid uid, ZombieInfectionComponent infection, TimeSpan curTime)
    {
        if (infection.Stage == InfectionStage.Incubation)
        {
            RemCompDeferred<ZombieFeverComponent>(uid);
            return;
        }

        var intensity = CalculateIntensity(infection, curTime);
        var fever = EnsureComp<ZombieFeverComponent>(uid);
        if (!MathHelper.CloseTo(fever.Intensity, intensity))
        {
            fever.Intensity = intensity;
            Dirty(uid, fever);
        }

        if (curTime < infection.NextHeartbeat)
            return;

        var intervalSeconds = float.Lerp((float) infection.HeartbeatIntervalMax.TotalSeconds,
            (float) infection.HeartbeatIntervalMin.TotalSeconds,
            intensity);
        infection.NextHeartbeat = curTime + TimeSpan.FromSeconds(intervalSeconds);
        _audio.PlayGlobal(infection.HeartbeatSound, uid);
    }

    private static float CalculateIntensity(ZombieInfectionComponent infection, TimeSpan curTime)
    {
        switch (infection.Stage)
        {
            case InfectionStage.Fever:
            {
                var progress = 1f - Math.Clamp((float) ((infection.StageEndsAt - curTime) / infection.FeverDuration), 0f, 1f);
                return float.Lerp(0f, 0.6f, progress);
            }
            case InfectionStage.Terminal:
            {
                var progress = 1f - Math.Clamp((float) ((infection.StageEndsAt - curTime) / infection.TerminalDuration), 0f, 1f);
                return float.Lerp(0.6f, 1f, progress);
            }
            default:
                return 0f;
        }
    }

    private void TickSymptoms(EntityUid uid, ZombieInfectionComponent infection)
    {
        var (chance, emotes, popups) = infection.Stage switch
        {
            InfectionStage.Fever => (infection.FeverSymptomChance, infection.FeverEmotes, infection.FeverPopups),
            InfectionStage.Terminal => (infection.TerminalSymptomChance, infection.TerminalEmotes, infection.TerminalPopups),
            _ => (infection.IncubationSymptomChance, infection.IncubationEmotes, infection.IncubationPopups),
        };

        if (!_random.Prob(chance))
            return;

        // Half the time an involuntary emote everyone can see, half a private popup.
        if (emotes.Count > 0 && (_random.Prob(0.5f) || popups.Count == 0))
            _chat.TryEmoteWithChat(uid, _random.Pick(emotes));
        else if (popups.Count > 0)
            _popup.PopupEntity(Loc.GetString(_random.Pick(popups)), uid, uid);
    }
}
