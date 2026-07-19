// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Zombies.Components;
using Content.Server.Chat.Systems;
using Content.Server.Zombies;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Zombies;
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
    [Dependency] private readonly SharedPopupSystem _popup = default!;
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
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ZombieInfectionComponent, PendingZombieComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var infection, out _, out var damageable, out var mobState))
        {
            if (infection.NextTick > curTime)
                continue;

            infection.NextTick = curTime + TimeSpan.FromSeconds(1);

            // The dead don't progress; ZombifyOnDeath already handled or will handle them.
            if (_mobState.IsDead(uid, mobState))
                continue;

            if (curTime >= infection.StageEndsAt && AdvanceStage(uid, infection))
                continue;

            TickDamage(uid, infection, damageable, mobState);
            TickSymptoms(uid, infection);
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
                Dirty(uid, infection);
                _popup.PopupEntity(Loc.GetString("zombie-infection-stage-terminal"), uid, uid, PopupType.LargeCaution);
                return false;

            default:
                // Terminal ran out: the virus wins no matter how healthy they still are.
                _zombie.ZombifyEntity(uid);
                return true;
        }
    }

    private void TickDamage(EntityUid uid, ZombieInfectionComponent infection, DamageableComponent damageable, MobStateComponent mobState)
    {
        var damage = infection.Stage switch
        {
            InfectionStage.Fever => infection.FeverDamage,
            InfectionStage.Terminal => infection.TerminalDamage,
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
