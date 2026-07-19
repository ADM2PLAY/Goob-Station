// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Radio;
using Content.Shared.Zombies;

namespace Content.Goobstation.Server.Zombies;

/// <summary>
///     Zombies keep their headsets but the virus ate the part of the brain that
///     parses comms: all radio reception is cancelled for them. Replaces the old
///     behavior of stripping the headset on conversion.
/// </summary>
public sealed class ZombieRadioSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Broadcast subscription: the radio system raises this both broadcast and
        // directed, and the directed pair is taken by IntrinsicRadioReceiver.
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // The receiver is either the radio itself (intrinsic/borg) or a headset
        // worn by something.
        var receiver = args.RadioReceiver;
        if (HasComp<ZombieComponent>(receiver)
            || HasComp<ZombieComponent>(Transform(receiver).ParentUid))
        {
            args.Cancelled = true;
        }
    }
}
