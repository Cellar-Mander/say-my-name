using System;
using FFXIVClientStructs.FFXIV.Client.UI;
using SayMyName.Core;

namespace SayMyName;

/// <summary>The alert sound every trigger plays, with one quiet time shared between them.</summary>
public sealed class AlertSound(Configuration configuration)
{
    /// <summary>Long enough for one test sound to finish, so clicking Test fast can't stack them.</summary>
    public static readonly TimeSpan TestCooldown = TimeSpan.FromSeconds(1);

    private Cooldown cooldown = new();
    private readonly Cooldown testCooldown = new();

    /// <summary>Dev plugin only: called instead of playing the sound, for the self-test to count alerts.</summary>
    public Action? Alerted { get; set; }

    private void Play() => UIGlobals.PlayChatSoundEffect((uint)configuration.SoundEffect);

    /// <summary>Whether the settings Test button can play the sound again yet.</summary>
    public bool CanTest => testCooldown.IsReady(Environment.TickCount64, TestCooldown);

    /// <summary>Plays the sound for the settings Test button, at most once per <see cref="TestCooldown"/>.</summary>
    public void Test()
    {
        if (testCooldown.TryFire(Environment.TickCount64, TestCooldown))
        {
            Play();
        }
    }

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
