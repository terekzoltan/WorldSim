using System.Linq;
using WorldSim.Simulation;
using WorldSim.Simulation.Effects;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class DomainModifierEngineTests
{
    [Fact]
    public void RegisterModifier_AppliesDecayAndExpires()
    {
        var engine = new DomainModifierEngine();
        engine.RegisterModifier("beat-1", RuntimeDomain.Food, modifier: 0.3, durationTicks: 4, dampeningFactor: 1.0);

        Assert.Equal(0.3, engine.GetEffectiveModifier(RuntimeDomain.Food), 6);

        engine.Tick();
        Assert.Equal(0.225, engine.GetEffectiveModifier(RuntimeDomain.Food), 6);

        engine.Tick();
        engine.Tick();
        engine.Tick();
        Assert.Equal(0d, engine.GetEffectiveModifier(RuntimeDomain.Food), 6);
        Assert.Empty(engine.GetActiveModifiers());
    }

    [Fact]
    public void RegisterModifier_UsesDampeningAndDomainCap()
    {
        var engine = new DomainModifierEngine();
        engine.RegisterModifier("a", RuntimeDomain.Economy, modifier: 0.5, durationTicks: 10, dampeningFactor: 0.5);
        engine.RegisterModifier("b", RuntimeDomain.Economy, modifier: 0.5, durationTicks: 10, dampeningFactor: 1.0);

        // 0.25 + 0.5 -> capped at 0.4
        Assert.Equal(0.4, engine.GetEffectiveModifier(RuntimeDomain.Economy), 6);
    }

    [Fact]
    public void World_MoraleDomainModifier_InfluencesColonyMorale()
    {
        var baseline = new World(width: 24, height: 16, initialPop: 8, randomSeed: 1234)
        {
            BirthRateMultiplier = 0f
        };
        var boosted = new World(width: 24, height: 16, initialPop: 8, randomSeed: 1234)
        {
            BirthRateMultiplier = 0f
        };

        boosted.RegisterDomainModifier("beat-morale", RuntimeDomain.Morale, modifier: 0.3, durationTicks: 80, dampeningFactor: 1.0);

        for (int i = 0; i < 40; i++)
        {
            baseline.Update(0.25f);
            boosted.Update(0.25f);
        }

        float baselineMorale = baseline._colonies.Average(c => c.Morale);
        float boostedMorale = boosted._colonies.Average(c => c.Morale);
        Assert.True(boostedMorale > baselineMorale);
    }
}
