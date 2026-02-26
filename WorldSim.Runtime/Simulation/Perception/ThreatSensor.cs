using System;
using System.Linq;

namespace WorldSim.Simulation;

public sealed class ThreatSensor : Sensor
{
    private const int ThreatRadius = 4;

    public override void Sense(World world, Person person, Blackboard blackboard)
    {
        var nearbyPredators = world._animals.Count(animal =>
            animal is Predator predator
            && predator.IsAlive
            && Manhattan(person.Pos, predator.Pos) <= ThreatRadius);

        var nearbyHostiles = world._people.Count(other =>
            other != person
            && other.Health > 0f
            && other.Home != person.Home
            && Manhattan(person.Pos, other.Pos) <= ThreatRadius);

        if (nearbyPredators + nearbyHostiles <= 0)
            return;

        blackboard.Add(new FactualEvent(EventTypes.Danger, new ThreatSignal(nearbyPredators, nearbyHostiles), person.Pos));
        if (nearbyHostiles > 0)
            blackboard.Add(new FactualEvent(EventTypes.Encounter, nearbyHostiles, person.Pos));
    }

    private static int Manhattan((int x, int y) a, (int x, int y) b)
        => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
}

public readonly record struct ThreatSignal(int NearbyPredators, int NearbyHostiles);
