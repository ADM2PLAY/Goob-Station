// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Goobstation.Shared.Zombies.Components;

/// <summary>
///     Staged, slow-burn zombie infection. Replaces the flat damage-per-second
///     infection with a longer arc: victims incubate quietly, develop a fever,
///     then crash and turn. Attached automatically alongside
///     PendingZombieComponent by ZombieInfectionSystem.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZombieInfectionComponent : Component
{
    [DataField, AutoNetworkedField]
    public InfectionStage Stage = InfectionStage.Incubation;

    /// <summary>
    ///     When the current stage ends and the infection advances.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan StageEndsAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextTick;

    /// <summary>
    ///     Incubation length is rolled randomly between these bounds.
    /// </summary>
    [DataField]
    public TimeSpan IncubationMin = TimeSpan.FromMinutes(4);

    [DataField]
    public TimeSpan IncubationMax = TimeSpan.FromMinutes(7);

    [DataField]
    public TimeSpan FeverDuration = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan TerminalDuration = TimeSpan.FromSeconds(90);

    /// <summary>
    ///     Damage dealt each second during the fever stage. Incubation deals none.
    /// </summary>
    [DataField]
    public DamageSpecifier FeverDamage = new()
    {
        DamageDict = new()
        {
            { "Poison", 0.4 },
        }
    };

    /// <summary>
    ///     Damage dealt each second during the terminal stage.
    /// </summary>
    [DataField]
    public DamageSpecifier TerminalDamage = new()
    {
        DamageDict = new()
        {
            { "Poison", 1.5 },
        }
    };

    /// <summary>
    ///     Multiplier for stage damage while the victim is in critical condition.
    /// </summary>
    [DataField]
    public float CritDamageMultiplier = 10f;

    /// <summary>
    ///     Chance each second that a symptom (emote or popup) plays, per stage.
    /// </summary>
    [DataField]
    public float IncubationSymptomChance = 0.02f;

    [DataField]
    public float FeverSymptomChance = 0.08f;

    [DataField]
    public float TerminalSymptomChance = 0.15f;

    [DataField]
    public List<string> IncubationEmotes = new() { "Cough", "Sneeze" };

    [DataField]
    public List<string> FeverEmotes = new() { "Cough" };

    [DataField]
    public List<string> TerminalEmotes = new() { "Scream" };

    [DataField]
    public List<string> IncubationPopups = new()
    {
        "zombie-infection-symptom-chill",
        "zombie-infection-symptom-throat",
        "zombie-infection-symptom-tired",
    };

    [DataField]
    public List<string> FeverPopups = new()
    {
        "zombie-infection-warning",
        "zombie-infection-symptom-fever",
    };

    [DataField]
    public List<string> TerminalPopups = new()
    {
        "zombie-infection-underway",
        "zombie-infection-symptom-terminal",
    };
}

/// <summary>
///     Stages of a zombie infection, in progression order.
/// </summary>
public enum InfectionStage : byte
{
    /// <summary>
    ///     Long quiet phase: no damage, rare mild symptoms.
    /// </summary>
    Incubation,

    /// <summary>
    ///     Visibly sick: light damage, frequent symptoms.
    /// </summary>
    Fever,

    /// <summary>
    ///     Crashing: heavy damage until death (and conversion), or forced
    ///     conversion when the timer runs out.
    /// </summary>
    Terminal,
}
