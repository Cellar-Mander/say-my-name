using System;
using FFXIVClientStructs.FFXIV.Client.UI;
using SayMyName.Core;

namespace SayMyName;

/// <summary>The alert sound every trigger plays, with one quiet time shared between them.</summary>
public sealed class AlertSound(Configuration configuration)
{
    private Cooldown cooldown = new();

    /// <summary>Dev plugin only: called instead of playing the sound, for the self-test to count alerts.</summary>
    public Action? Alerted { get; set; }

    public void Play() => UIGlobals.PlayChatSoundEffect((uint)configuration.SoundEffect);

    /// <summary>Plays the sound unless sounds are off or the quiet time hasn't run out.</summary>
    public void Alert()
    {
        if (configuration.PlaySound
            && cooldown.TryFire(Environment.TickCount64, TimeSpan.FromSeconds(configuration.CooldownSeconds)))
        {
            if (Alerted != null)
            {
                Alerted();
            }
            else
            {
                Play();
            }
        }
    }

    /// <summary>Dev plugin only: lets the next alert sound whatever the quiet time.</summary>
    public void ResetQuietTime() => cooldown = new();
}
