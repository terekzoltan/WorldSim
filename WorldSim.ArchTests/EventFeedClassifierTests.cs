using WorldSim.Graphics.UI;
using Xunit;

namespace WorldSim.ArchTests;

public class EventFeedClassifierTests
{
    [Theory]
    [InlineData("[Director:MAJOR] campaign victory pressure rising")]
    [InlineData("[Director:EPIC] retreat after siege breach")]
    [InlineData("story beat grants loot after ceasefire")]
    public void Classify_PrioritizesDirectorSignals_OverBroadCampaignKeywords(string evt)
    {
        Assert.Equal(EventFeedCategory.Director, EventFeedClassifier.Classify(evt));
    }
}
