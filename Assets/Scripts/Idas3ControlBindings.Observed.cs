using System.Collections.Generic;
using UnityEngine;

public sealed partial class Idas3ControlBindings
{
    internal void CopyObservedKeys(List<KeyCode> target)
    {
        target.Clear();
        foreach (var key in PollKeys)
            if ((int)key < (int)KeyCode.Mouse0 && Held(key)) target.Add(key);
    }

    internal void TryObservedRest(Idas3EndpointToken token, string path, ref float rest)
    {
        foreach (var assignment in experimentalDraft.actions)
            if (assignment.assigned && assignment.endpoint.Equals(token) && assignment.binding.controlPath == path)
            { rest = assignment.binding.controlRest; return; }
        foreach (var assignment in experimentalDraft.menuActions)
            if (assignment.assigned && assignment.endpoint.Equals(token) && assignment.binding.controlPath == path)
            { rest = assignment.binding.controlRest; return; }
    }
}
