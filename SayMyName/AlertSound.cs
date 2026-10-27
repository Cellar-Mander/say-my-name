using System;
using FFXIVClientStructs.FFXIV.Client.UI;
using SayMyName.Core;

namespace SayMyName;

/// <summary>The alert sound every trigger plays, with one quiet time shared between them.</summary>
public sealed class AlertSound(Configuration configuration)
{
    private readonly Cooldown cooldown = new();

    public void Play() => UIGlobals.PlayChatSoundEffect((uint)configuration.SoundEffect);

    /// <summary>Plays the sound unless sounds are off or the quiet time hasn't run out.</summary>
    public void Alert()
    {
        if (configuration.PlaySound
            && cooldown.TryFire(Environment.TickCount64, TimeSpan.FromSeconds(configuration.CooldownSeconds)))
        {
            Play();
        }
    }
}
