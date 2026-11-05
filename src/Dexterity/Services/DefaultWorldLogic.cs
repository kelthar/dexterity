using System.Globalization;

namespace Dexterity;

// Games can replace these defaults through IWorldLogic. Coordinates live in MetaData.
public class DefaultWorldLogic : IWorldLogic
{
    private readonly World _world;
    public DefaultWorldLogic(World world) => _world = world;
    public double GetDistance(int firstId, int secondId)
    {
        if (!_world.Repository.Items.TryGetValue(firstId, out var first) || !_world.Repository.Items.TryGetValue(secondId, out var second))
            return double.PositiveInfinity;
        if (firstId == secondId)
            return 0;
        double Coordinate(Component component, string key) => component.MetaData.TryGetValue(key, out var text) &&
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && EffectRules.Finite(value) ? value : 0;
        double dx = Coordinate(first, "x") - Coordinate(second, "x"), dy = Coordinate(first, "y") - Coordinate(second, "y");
        return Math.Sqrt(dx * dx + dy * dy);
    }
    public RelationKind GetRelationKind(int firstId, int secondId)
    {
        if (!_world.Repository.Items.TryGetValue(firstId, out var first) || !_world.Repository.Items.TryGetValue(secondId, out var second))
            return RelationKind.None;
        if (firstId == secondId)
            return RelationKind.Self;
        return first.MetaData.TryGetValue("faction", out var faction) && !string.IsNullOrWhiteSpace(faction) && second.MetaData.TryGetValue("faction", out var other) &&
            string.Equals(faction, other, StringComparison.OrdinalIgnoreCase) ? RelationKind.Friends : RelationKind.Enemies;
    }
}
