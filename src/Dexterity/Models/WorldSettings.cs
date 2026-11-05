namespace Dexterity;

public class WorldSettings
{
    /// <summary>Rounds each stat's combined immediate effect amount with Math.Round after incoming mitigation and before application, updating preview and applied result contributions to whole amounts with the same total. Defaults to false.</summary>
    public bool RoundEffectAmounts { get; set; }
}
