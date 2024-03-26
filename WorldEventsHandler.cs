using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.TerrainFeatures;
using StardewValley.TokenizableStrings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmersiveScarecrows;

public class WorldEventsHandler
{
    public static void TerrainFeatureListChanged(object sender, StardewModdingAPI.Events.TerrainFeatureListChangedEventArgs e)
    {
        if (!ModEntry.Config.EnableMod)
            return;

        foreach(KeyValuePair<Vector2, TerrainFeature> removed in e.Removed.Where(x => x.Value is HoeDirt))
        {
            for (int mouseCorner = 0; mouseCorner < 4; mouseCorner++)
            {
                if (removed.Value.modData.TryGetValue(ModEntry.scarecrowKey + mouseCorner, out string scarecrowItemId))
                {
                    try
                    {
                        e.Location.terrainFeatures.Add(removed.Key, removed.Value);
                    }
                    catch (Exception ex)
                    {
                        ModEntry.SMonitor.Log(
                            $"A scarecrow could not be restored! " +
                            $"You can retrieve it from the Lost and Found. Error: {ex}",
                            LogLevel.Error
                        );

                        ModEntry.SMonitor.Log($"Scarecrow Tile: {removed.Key}", LogLevel.Debug);

                        SendScarecrowToLostAndFound(scarecrowItemId);
                    }
                }
            }
        }
    }

    private static void SendScarecrowToLostAndFound(string scarecrowItemId)
    {
        try
        {
            if (ModEntry.SMonitor.IsVerbose)
            {
                BigCraftableData bigCraftableData = Game1.bigCraftableData[scarecrowItemId];
                string name = TokenParser.ParseText(bigCraftableData.DisplayName);
                string description = TokenParser.ParseText(bigCraftableData.Description);

                ModEntry.SMonitor.Log($"Scarecrow: {name} - {description}", LogLevel.Debug);
            }

            StardewValley.Object scarecrowObject = new(Vector2.Zero, scarecrowItemId);
            Game1.player.team.returnedDonations.Add(scarecrowObject);
            Game1.player.team.newLostAndFoundItems.Value = true;
        }
        catch (Exception ex)
        {
            ModEntry.SMonitor.Log(
                $"Error occurred when trying to save deleted scarecrow " +
                $"to Lost and Found: {ex}", LogLevel.Error);
        }
    }
}
