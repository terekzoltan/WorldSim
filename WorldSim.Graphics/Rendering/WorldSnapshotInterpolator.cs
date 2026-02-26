using Microsoft.Xna.Framework;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public static class WorldSnapshotInterpolator
{
    public static WorldRenderSnapshot Interpolate(WorldRenderSnapshot previous, WorldRenderSnapshot current, float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);

        return new WorldRenderSnapshot(
            current.Width,
            current.Height,
            current.Tiles,
            current.Houses,
            current.SpecializedBuildings,
            InterpolatePeople(previous.People, current.People, alpha),
            InterpolateAnimals(previous.Animals, current.Animals, alpha),
            current.Colonies,
            current.Ecology,
            current.CurrentSeason,
            current.IsDroughtActive,
            current.RecentEvents);
    }

    private static IReadOnlyList<PersonRenderData> InterpolatePeople(IReadOnlyList<PersonRenderData> previous, IReadOnlyList<PersonRenderData> current, float alpha)
    {
        var result = new List<PersonRenderData>(current.Count);
        int shared = Math.Min(previous.Count, current.Count);
        for (int i = 0; i < shared; i++)
        {
            var prev = previous[i];
            var cur = current[i];
            result.Add(new PersonRenderData(
                (int)MathF.Round(MathHelper.Lerp(prev.X, cur.X, alpha)),
                (int)MathF.Round(MathHelper.Lerp(prev.Y, cur.Y, alpha)),
                cur.ColonyId));
        }

        for (int i = shared; i < current.Count; i++)
            result.Add(current[i]);

        return result;
    }

    private static IReadOnlyList<AnimalRenderData> InterpolateAnimals(IReadOnlyList<AnimalRenderData> previous, IReadOnlyList<AnimalRenderData> current, float alpha)
    {
        var result = new List<AnimalRenderData>(current.Count);
        int shared = Math.Min(previous.Count, current.Count);
        for (int i = 0; i < shared; i++)
        {
            var prev = previous[i];
            var cur = current[i];
            result.Add(new AnimalRenderData(
                (int)MathF.Round(MathHelper.Lerp(prev.X, cur.X, alpha)),
                (int)MathF.Round(MathHelper.Lerp(prev.Y, cur.Y, alpha)),
                cur.Kind));
        }

        for (int i = shared; i < current.Count; i++)
            result.Add(current[i]);

        return result;
    }
}
