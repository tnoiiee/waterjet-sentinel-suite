using Wjss.Contracts;

namespace Wjss.Runtime.Core;

/// <summary>
/// Recalculation of the four wall summaries from the Sensor composition. The
/// summaries are derived state: the Runtime recomputes them whenever the Sensor
/// set changes, and <see cref="RuntimeStateInvariants"/> refuses a state whose
/// published summaries disagree with its Sensors, so a stale summary can never
/// be committed.
///
/// Canonical wall order is Left, Rear, Right, Front (DOMAIN_MODEL.md section 1,
/// rule 2). NON_SENSOR_GAP positions are not Sensors and contribute nothing.
/// </summary>
public static class RuntimeWallSummaries
{
    /// <summary>Canonical wall enumeration order used by every wall list in the Runtime.</summary>
    public static IReadOnlyList<Wall> CanonicalWallOrder { get; } =
        Array.AsReadOnly(new[] { Wall.LEFT, Wall.REAR, Wall.RIGHT, Wall.FRONT });

    /// <summary>
    /// Recomputes the four summaries from the given Sensor projections. Counters
    /// count classification for Dirty/Cleaner/NotClassified and quality for
    /// Uncertain, matching the accepted presentation baseline. MaxScore is the
    /// highest current Dirty Score on the wall, or null when no Sensor on that
    /// wall carries a score.
    /// </summary>
    public static IReadOnlyList<WallSummary> Recalculate(IReadOnlyList<SensorPresentationState> sensors)
    {
        var summaries = new WallSummary[CanonicalWallOrder.Count];
        for (var wallIndex = 0; wallIndex < CanonicalWallOrder.Count; wallIndex++)
        {
            var wall = CanonicalWallOrder[wallIndex];
            var total = 0;
            var dirty = 0;
            var cleaner = 0;
            var notClassified = 0;
            var uncertain = 0;
            double? maxScore = null;

            for (var index = 0; index < sensors.Count; index++)
            {
                var sensor = sensors[index];
                if (sensor.Wall != wall)
                {
                    continue;
                }

                total++;
                switch (sensor.Classification)
                {
                    case Classification.DIRTY:
                        dirty++;
                        break;
                    case Classification.CLEANER:
                        cleaner++;
                        break;
                    case Classification.NOT_CLASSIFIED:
                        notClassified++;
                        break;
                    default:
                        break;
                }

                if (sensor.Quality == Quality.UNCERTAIN)
                {
                    uncertain++;
                }

                if (sensor.DirtyScore is { } score && (maxScore is null || score > maxScore.Value))
                {
                    maxScore = score;
                }
            }

            summaries[wallIndex] = new WallSummary
            {
                Wall = wall,
                Total = total,
                Dirty = dirty,
                Cleaner = cleaner,
                NotClassified = notClassified,
                Uncertain = uncertain,
                MaxScore = maxScore,
            };
        }

        return Array.AsReadOnly(summaries);
    }
}
