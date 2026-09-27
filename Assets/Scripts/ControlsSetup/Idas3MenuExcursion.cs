// Shared by the legacy menu packet and explicit assignments' host routing.
// A direction (including a diagonal) is one excursion until all directions
// release. Entry, focus loss and reconnect require a neutral sample first.
internal sealed class Idas3MenuExcursion
{
    private bool armed;
    internal void Reset()
    {
        armed = false;
    }

    internal int Evaluate(int directions)
    {
        if (directions == 0)
        {
            armed = true;
            return 0;
        }

        if (!armed)
            return 0;
        armed = false;
        int vertical = directions & 3;
        int horizontal = directions & 12;
        if (vertical == 3)
            return 0;
        if (vertical != 0)
            return vertical;
        return horizontal == 12 ? 0 : horizontal;
    }
}
