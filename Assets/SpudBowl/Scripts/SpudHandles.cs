#if UNITY_EDITOR && SATURA_SPACE
using System.Collections;
using SaturaSpace;
using UnityEngine;

public static class SpudHandles
{
    static SpudGame G => SpudGame.I;

    [SaturaHandle("Clears the bowl and starts a fresh run for a human player (bot off)")]
    public static string StartRun() { G.bot = false; G.Restart(); return G.State(); }

    [SaturaHandle("Starts a fresh run played by the bot: it holds pour and sweeps the spout across the neck")]
    public static string DemoRun() { G.Restart(); G.bot = true; return G.State(); }

    [SaturaHandle("Stops the bot")]
    public static string BotOff() { G.bot = false; return G.State(); }

    [SaturaHandle("Score, spud count, merges, highest tier, found tiers, overflow and game over")]
    public static string GetState() => G.State();

    [SaturaHandle("Clears the bowl, drops two equal spuds of a tier (1-8) toward each other and yields the state after 1.5 s")]
    public static IEnumerator MergeTest(int tier)
    {
        G.bot = false;
        G.Restart();
        G.DropPair(Mathf.Clamp(tier - 1, 0, 7));
        float end = Time.time + 1.5f;
        while (Time.time < end) yield return null;
        yield return G.State();
    }

    [SaturaHandle("Waits until a tier (1-8) has appeared or the game time runs out; yields the state")]
    public static IEnumerator WaitForTier(int tier, float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end && !G.Found(tier - 1)) yield return null;
        yield return G.State();
    }
}
#endif
