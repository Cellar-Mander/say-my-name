namespace SayMyName.Core;

/// <summary>
/// Splits chat text around the matches a trigger found, so the matched words can be coloured.
/// Chat lines are split into payloads, so each text payload is split on its own.
/// </summary>
public static class Highlights
{
    /// <summary>Sorts the matches and joins overlapping or touching ones, in place.</summary>
    public static void Merge(List<(int Start, int Length)> matches)
    {
        matches.Sort();
        var kept = 0;
        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            if (kept > 0 && m.Start <= End(matches[kept - 1]))
            {
                var last = matches[kept - 1];
                matches[kept - 1] = (last.Start, Math.Max(End(last), End(m)) - last.Start);
            }
            else
            {
                matches[kept++] = m;
            }
        }
        matches.RemoveRange(kept, matches.Count - kept);
    }

    /// <summary>
    /// Adds the pieces of the text from <paramref name="start"/> for <paramref name="length"/> characters
    /// to <paramref name="pieces"/>, in order, with starts relative to <paramref name="start"/>.
    /// </summary>
    /// <param name="merged">Matches in the whole text, as left by <see cref="Merge"/>.</param>
    public static void Split(int start, int length, IReadOnlyList<(int Start, int Length)> merged,
        List<(int Start, int Length, bool Highlighted)> pieces)
    {
        var cursor = start;
        var end = start + length;
        foreach (var m in merged)
        {
            // Also stops for an empty segment, which gets no pieces
            if (m.Start >= end || cursor == end)
            {
                break;
            }
            if (End(m) <= cursor)
            {
                continue;
            }

            var from = Math.Max(m.Start, cursor);
            var to = Math.Min(End(m), end);
            if (from > cursor)
            {
                pieces.Add((cursor - start, from - cursor, false));
            }
            pieces.Add((from - start, to - from, true));
            cursor = to;
        }

        if (cursor < end)
        {
            pieces.Add((cursor - start, end - cursor, false));
        }
    }

    private static int End((int Start, int Length) m) => m.Start + m.Length;
}
