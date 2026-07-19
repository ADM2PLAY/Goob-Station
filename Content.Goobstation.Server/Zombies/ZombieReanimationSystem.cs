// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Zombies.Components;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Zombies;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Goobstation.Server.Zombies;

/// <summary>
///     Walkers don't stay dead. A downed walker gets back up after a delay unless
///     the corpse was burned past its destruction threshold, lost its head, or was
///     cured while it was down. The delay is the crew's capture-and-cure window.
/// </summary>
public sealed class ZombieReanimationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    private readonly List<(EntityUid Uid, bool Inoculate)> _pendingCures = new();

    private static readonly SoundSpecifier RiseSound = new SoundPathSpecifier("/Audio/Voice/Zombie/zombie-3.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WalkerZombieComponent, MobStateChangedEvent>(OnMobState);
    }

    private void OnMobState(Entity<WalkerZombieComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && !ent.Comp.PermanentlyDead)
            ent.Comp.ReanimateAt = _timing.CurTime + _random.Next(ent.Comp.ReanimationDelayMin, ent.Comp.ReanimationDelayMax);
        else if (args.NewMobState == MobState.Alive)
            ent.Comp.ReanimateAt = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<WalkerZombieComponent, ZombieComponent, MobStateComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var walker, out _, out var mobState, out var damageable))
        {
            if (walker.ReanimateAt is not { } reanimateAt || !_mobState.IsDead(uid, mobState))
                continue;

            // Curing the polymorph deletes this entity, so defer it out of the query.
            if (CheckCureWhileDown(uid, walker, out var inoculate))
            {
                walker.ReanimateAt = null;
                _pendingCures.Add((uid, inoculate));
                continue;
            }

            if (curTime < reanimateAt)
                continue;

            walker.ReanimateAt = null;

            if (!CanReanimate(uid, walker, damageable))
            {
                walker.PermanentlyDead = true;
                continue;
            }

            Reanimate(uid, damageable);
        }

        foreach (var (uid, inoculate) in _pendingCures)
        {
            var ev = new EntityUnZombifiedEvent(inoculate);
            RaiseLocalEvent(uid, ref ev);
        }

        _pendingCures.Clear();
    }

    /// <summary>
    ///     Corpse metabolism is too unreliable to carry the capture-and-cure loop,
    ///     so downed walkers get their injectable solution checked directly: enough
    ///     cure reagent in the body reverts the zombie before it can rise.
    /// </summary>
    private bool CheckCureWhileDown(EntityUid uid, WalkerZombieComponent walker, out bool inoculate)
    {
        inoculate = false;

        if (!_solutionContainer.TryGetInjectableSolution(uid, out _, out var solution))
            return false;

        if (solution.GetTotalPrototypeQuantity(walker.InoculateReagent) >= walker.InoculateReagentAmount)
        {
            inoculate = true;
            return true;
        }

        return solution.GetTotalPrototypeQuantity(walker.CureReagent) >= walker.CureReagentAmount;
    }

    private bool CanReanimate(EntityUid uid, WalkerZombieComponent walker, DamageableComponent damageable)
    {
        // Burned out: too much of the body destroyed by fire.
        if (damageable.Damage.DamageDict.TryGetValue("Heat", out var heat)
            && heat.Float() >= walker.HeatDestructionThreshold)
            return false;

        // Headless: decapitation is a permanent kill (at the cost of any cure).
        if (TryComp<BodyComponent>(uid, out var body)
            && !_body.GetBodyChildrenOfType(uid, BodyPartType.Head, body).Any())
            return false;

        return true;
    }

    private void Reanimate(EntityUid uid, DamageableComponent damageable)
    {
        // Heal everything except Heat, then stand the walker back up. Keeping Heat
        // means burn progress sticks across risings, and healing via damage (not
        // Rejuvenate) means severed limbs stay lost.
        var heal = new DamageSpecifier();
        foreach (var (type, value) in damageable.Damage.DamageDict)
        {
            if (type == "Heat" || value <= FixedPoint2.Zero)
                continue;

            heal.DamageDict[type] = -value;
        }

        if (heal.DamageDict.Count > 0)
        {
            _damageable.TryChangeDamage(uid,
                heal,
                true,
                false,
                damageable,
                ignoreBlockers: true,
                targetPart: TargetBodyPart.All,
                splitDamage: SplitDamageBehavior.SplitEnsureAll);
        }

        _mobState.ChangeMobState(uid, MobState.Alive);

        _popup.PopupEntity(Loc.GetString("zombie-reanimation-rise", ("zombie", uid)), uid, PopupType.LargeCaution);
        _audio.PlayPvs(RiseSound, uid);
    }
}
