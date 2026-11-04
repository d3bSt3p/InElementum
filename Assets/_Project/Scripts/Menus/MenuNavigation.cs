using System.Collections.Generic;
using UnityEngine;

namespace InElementum.Menus
{
    /// <summary>Cursor movement helpers shared by the menu screens. Pure functions, so they are tested in EditMode.</summary>
    public static class MenuNavigation
    {
        /// <summary>
        /// The index of the point nearest to <paramref name="positions"/>[<paramref name="from"/>] in
        /// <paramref name="direction"/> (screen space, y down), favouring points in line with it. Returns
        /// <paramref name="from"/> when nothing lies that way.
        /// </summary>
        public static int Nearest(IReadOnlyList<Vector2> positions, int from, Vector2Int direction)
        {
            if (positions == null || from < 0 || from >= positions.Count || direction == Vector2Int.zero) return from;

            var origin = positions[from];
            var axis = ((Vector2)direction).normalized;
            var best = from;
            var bestScore = float.MaxValue;

            for (var i = 0; i < positions.Count; i++)
            {
                if (i == from) continue;

                var delta = positions[i] - origin;
                var along = Vector2.Dot(delta, axis);
                if (along <= 0.01f) continue;

                var across = Mathf.Abs(delta.x * axis.y - delta.y * axis.x);
                // Off-axis distance counts double, so the cursor follows rows and columns before cutting diagonally.
                var score = along + across * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>Moves a list cursor by <paramref name="step"/>, wrapping around. -1 for an empty list.</summary>
        public static int Wrap(int index, int step, int count)
        {
            if (count <= 0) return -1;
            return ((index + step) % count + count) % count;
        }
    }
}
