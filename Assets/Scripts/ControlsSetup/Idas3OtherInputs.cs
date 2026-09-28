using System;
using System.Collections.Generic;
using UnityEngine;

// Observes the provider's published frame and the mapper's already sampled keys.
// This model emits display rows only: it cannot bind, navigate or drive anything.
internal sealed class Idas3OtherInputs
{
    internal sealed class Entry
    {
        internal string Name, Display;
        internal bool Active;
        internal float Rest;
        internal double ReleasedAt = double.NegativeInfinity;
        internal bool Seen;
    }

    private readonly Dictionary<(Idas3EndpointToken, long, string), Entry> signals = new Dictionary<(Idas3EndpointToken, long, string), Entry>();
    private readonly List<(Idas3EndpointToken, long, string)> removed = new List<(Idas3EndpointToken, long, string)>();
    private readonly List<Entry> visible = new List<Entry>();
    private readonly List<KeyCode> keys = new List<KeyCode>();
    internal IReadOnlyList<Entry> Visible => visible;
    internal const int RowLimit = 6;

    internal void Clear()
    {
        signals.Clear();
        visible.Clear();
        keys.Clear();
    }

    internal void Update(Idas3DeviceFrame frame, Idas3ControlBindings bindings, double now, bool focused)
    {
        if (!focused || frame == null) { Clear(); return; }
        foreach (var entry in signals.Values) entry.Seen = false;
        foreach (var endpoint in frame.Endpoints)
        {
            if (!endpoint.CanRead || !endpoint.SampledAt.HasValue || now < endpoint.SampledAt.Value || now - endpoint.SampledAt.Value > .25) continue;
            foreach (var control in endpoint.Controls)
            {
                if (control.Validity != Idas3SampleValidity.Valid || !control.Value.HasValue) continue;
                var id = (endpoint.Token, endpoint.ConnectionGeneration, control.Path);
                if (!signals.TryGetValue(id, out var entry))
                {
                    float rest = control.Value.Value;
                    bindings.TryObservedRest(endpoint.Token, control.Path, ref rest);
                    entry = new Entry
                    {
                        Name = endpoint.Name + " #" + endpoint.Token.Number + " · " + control.Label,
                        Display = Idas3ControlBindings.ShortControlText(endpoint.Name, 17) + " #" + endpoint.Token.Number + " · " + Idas3ControlBindings.ShortControlText(control.Label, 18),
                        Rest = rest
                    };
                    signals.Add(id, entry);
                }
                float span = control.Maximum - control.Minimum;
                // Unknown unassigned axes learn their entry rest. Hysteresis
                // filters jitter; assigned axes use their existing calibration.
                bool active = control.Button ? control.Value.Value > .5f : span > 0 && Math.Abs(control.Value.Value - entry.Rest) / span >= (entry.Active ? .08f : .18f);
                Observe(entry, active, now);
            }
        }
        bindings.CopyObservedKeys(keys);
        foreach (var key in keys)
        {
            var id = (default(Idas3EndpointToken), 0L, "key/" + (int)key);
            if (!signals.TryGetValue(id, out var entry))
            {
                entry = new Entry { Name = "Keyboard · " + (key == KeyCode.Return ? "Enter" : key.ToString()) };
                entry.Display = entry.Name;
                signals.Add(id, entry);
            }
            Observe(entry, true, now);
        }
        removed.Clear();
        visible.Clear();
        foreach (var pair in signals)
        {
            var entry = pair.Value;
            if (!entry.Seen)
            {
                if (pair.Key.Item1.Number != 0) { removed.Add(pair.Key); continue; }
                Observe(entry, false, now);
                if (now - entry.ReleasedAt > .6) { removed.Add(pair.Key); continue; }
            }
            if (entry.Active || now - entry.ReleasedAt <= .6) visible.Add(entry);
        }
        foreach (var id in removed) signals.Remove(id);
        visible.Sort((a, b) => a.Active != b.Active ? (a.Active ? -1 : 1) : string.CompareOrdinal(a.Name, b.Name));
    }

    private static void Observe(Entry entry, bool active, double now)
    {
        if (entry.Active && !active) entry.ReleasedAt = now;
        entry.Active = active;
        entry.Seen = true;
    }
}
