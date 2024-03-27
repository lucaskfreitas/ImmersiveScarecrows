using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.TokenizableStrings;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using xTile.Dimensions;
using xTile.Layers;
using Color = Microsoft.Xna.Framework.Color;
using Object = StardewValley.Object;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace ImmersiveScarecrows
{
    public partial class ModEntry
    {
        [HarmonyPatch(typeof(Object), nameof(Object.placementAction))]
        public class Object_placementAction_Patch
        {
            public static bool Prefix(Object __instance, GameLocation location, int x, int y,
                Farmer who, ref bool __result)
            {
                if (!Config.EnableMod || !__instance.IsScarecrow())
                    return true;

                Vector2 placementTile = new(x / 64, y / 64);
                if (!location.terrainFeatures.TryGetValue(placementTile, out TerrainFeature tf) || tf is not HoeDirt)
                    return true;

                int which = GetMouseCorner();

                SMonitor.Log($"Placing {__instance.Name} at {x},{y}:{which}");

                ReturnScarecrow(who, location, placementTile, which);
                tf.modData[scarecrowKey + which] = __instance.ItemId;
                tf.modData[guidKey + which] = Guid.NewGuid().ToString();
                tf.modData[scaredKey + which] = "0";

                if (atApi is not null)
                {
                    Object obj = (Object)__instance.getOne();
                    SetAltTextureForObject(obj);
                    foreach (KeyValuePair<string, string> kvp in obj.modData.Pairs)
                    {
                        if (kvp.Key.StartsWith(altTextureKey))
                        {
                            tf.modData[prefixKey + kvp.Key + which] = kvp.Value;
                        }
                    }
                }

                location.playSound("woodyStep");

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.checkAction))]
        public class GameLocation_checkAction_Patch
        {
            public static bool Prefix(GameLocation __instance, Location tileLocation, Farmer who, ref bool __result)
            {
                if (!Config.EnableMod)
                    return true;

                Vector2 tile = new(tileLocation.X, tileLocation.Y);
                if (!Game1.currentLocation.terrainFeatures.TryGetValue(tile, out TerrainFeature tf)
                    || tf is not HoeDirt)
                {
                    return true;
                }

                int which = GetMouseCorner();
                if (!GetScarecrowTileBool(__instance, ref tile, ref which, out _))
                    return true;

                tf = __instance.terrainFeatures[tile];
                Object scareCrow = GetScarecrow(tf, which);
                if(scareCrow is null)
                    return true;

                if(scareCrow.ParentSheetIndex == 126 && who.CurrentItem is not null && who.CurrentItem is Hat)
                {
                    if (tf.modData.TryGetValue(hatKey + which, out string hatString))
                    {
                        Game1.createItemDebris(
                            new Hat(hatString),
                            tf.Tile * 64f,
                            (who.FacingDirection + 2) % 4,
                            null,
                            -1
                        );

                        tf.modData.Remove(hatKey + which);
                    }

                    tf.modData[hatKey + which] = (who.CurrentItem as Hat).ItemId;
                    who.Items[who.CurrentToolIndex] = null;
                    who.currentLocation.playSound("dirtyHit");

                    __result = true;
                    return false;
                }

                if (Game1.didPlayerJustRightClick(true))
                {
                    if (!tf.modData.TryGetValue(scaredKey + which, out string scaredString)
                        || !int.TryParse(scaredString, out int scared))
                    {
                        tf.modData[scaredKey + which] = "0";
                        scared = 0;
                    }

                    if (scared == 0)
                    {
                        Game1.drawObjectDialogue(
                            Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12926")
                        );
                    }
                    else
                    {
                        Game1.drawObjectDialogue(
                            scared == 1
                            ? Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12927")
                            : Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12929", scared)
                        );
                    }
                }

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(GameLocation), "initNetFields")]
        public class GameLocation_initNetFields_Patch
        {
            public static void Postfix(GameLocation __instance)
            {
                if (!Config.EnableMod)
                    return;

                __instance.terrainFeatures.OnValueRemoved += delegate (Vector2 tileLocation, TerrainFeature tf)
                {
                    if (tf is not HoeDirt)
                        return;

                    for (int i = 0; i < 4; i++)
                    {
                        if (tf.modData.TryGetValue(scarecrowKey + i, out string scarecrowItemId))
                        {
                            SMonitor.Log(
                                "A scarecrow is being deleted! You can retrieve it from the Lost and Found.",
                                LogLevel.Warn
                            );

                            try
                            {
                                BigCraftableData bigCraftableData = Game1.bigCraftableData[scarecrowItemId];
                                string name = TokenParser.ParseText(bigCraftableData.DisplayName);
                                string description = TokenParser.ParseText(bigCraftableData.Description);

                                SMonitor.Log($"Scarecrow: {name} - {description}", LogLevel.Warn);
                                SMonitor.Log($"Scarecrow Tile: {tileLocation}", LogLevel.Warn);

                                Object scarecrowObject = new(Vector2.Zero, scarecrowItemId);
                                Game1.player.team.returnedDonations.Add(scarecrowObject);
                                Game1.player.team.newLostAndFoundItems.Value = true;
                            }
                            catch (Exception ex)
                            {
                                SMonitor.Log(
                                    $"Error occurred when trying to save deleted scarecrow " +
                                    $"to Lost and Found: {ex}", LogLevel.Error);
                            }
                        }
                    }
                };
            }
        }

        [HarmonyPatch(typeof(HoeDirt), nameof(HoeDirt.DrawOptimized))]
        public class HoeDirt_DrawOptimized_Patch
        {
            public static void Postfix(HoeDirt __instance, SpriteBatch dirt_batch)
            {
                if (!Config.EnableMod)
                    return;

                for (int i = 0; i < 4; i++)
                {
                    if(__instance.modData.ContainsKey(scarecrowKey + i))
                    {
                        if (!__instance.modData.TryGetValue(guidKey + i, out string guid))
                        {
                            guid = Guid.NewGuid().ToString();
                            __instance.modData[guidKey + i] = guid;
                        }

                        if (!scarecrowDict.TryGetValue(guid, out Object obj))
                        {
                            obj = GetScarecrow(__instance, i);
                        }

                        if (obj is not null)
                        {
                            Vector2 scaleFactor = obj.getScale();

                            Vector2 globalPosition = __instance.Tile * 64
                                + new Vector2(
                                    32
                                    - 8 * Config.Scale
                                    - scaleFactor.X / 2f
                                    + Config.DrawOffsetX, 32 - 8 * Config.Scale - 80
                                    - scaleFactor.Y / 2f + Config.DrawOffsetY
                                )
                                + GetScarecrowCorner(i) * 32;

                            Vector2 position = Game1.GlobalToLocal(globalPosition);
                            Texture2D texture = null;
                            Rectangle sourceRect = new();

                            if (atApi is not null && obj.modData.ContainsKey("AlternativeTextureName"))
                            {
                                texture = GetAltTextureForObject(obj, out sourceRect);
                            }

                            if (texture is null)
                            {
                                texture = Game1.bigCraftableSpriteSheet;
                                sourceRect = Object.getSourceRectForBigCraftable(obj.ParentSheetIndex);
                            }

                            float layerDepth = (globalPosition.Y + 81 + 32 + Config.DrawOffsetZ) / 10000f;

                            dirt_batch.Draw(
                                texture, position, sourceRect, Color.White * Config.Alpha, 0,
                                Vector2.Zero, Config.Scale, SpriteEffects.None, layerDepth
                            );

                            if (__instance.modData.TryGetValue(hatKey + i, out string hatString)
                                && int.TryParse(hatString, out int hat))
                            {
                                dirt_batch.Draw(
                                    FarmerRenderer.hatsTexture,
                                    position + new Vector2(-3f, -6f) * 4f,
                                    new Rectangle(
                                        hat * 20 % FarmerRenderer.hatsTexture.Width,
                                        hat * 20 / FarmerRenderer.hatsTexture.Width * 20 * 4,
                                        20,
                                        20
                                    ),
                                    Color.White * Config.Alpha,
                                    0f,
                                    Vector2.Zero,
                                    4f,
                                    SpriteEffects.None,
                                    layerDepth + 1E-05f
                                );
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Utility), nameof(Utility.playerCanPlaceItemHere))]
        public class Utility_playerCanPlaceItemHere_Patch
        {
            public static bool Prefix(GameLocation location, Item item, int x, int y, Farmer f, ref bool __result)
            {
                if (!Config.EnableMod || item is not Object || !(item as Object).IsScarecrow()
                    || !location.terrainFeatures.TryGetValue(new Vector2(x / 64, y / 64), out TerrainFeature tf)
                    || tf is not HoeDirt)
                {
                    return true;
                }

                __result = Utility.withinRadiusOfPlayer(x, y, 1, Game1.player);
                return false;
            }
        }

        [HarmonyPatch(typeof(Object), nameof(Object.drawPlacementBounds))]
        public class Object_drawPlacementBounds_Patch
        {
            public static bool Prefix(Object __instance, SpriteBatch spriteBatch, GameLocation location)
            {
                if (!Config.EnableMod || !Context.IsPlayerFree || !__instance.IsScarecrow()
                    ||  Game1.currentLocation?.terrainFeatures?.TryGetValue(
                            Game1.currentCursorTile,
                            out TerrainFeature tf
                        ) != true
                    || tf is not HoeDirt)
                {
                    return true;
                }

                int which = GetMouseCorner();
                Vector2 scarecrowTile = Game1.currentCursorTile;

                GetScarecrowTileBool(Game1.currentLocation, ref scarecrowTile, ref which, out string str);

                Vector2 pos = Game1.GlobalToLocal(scarecrowTile * 64 + GetScarecrowCorner(which) * 32f);

                spriteBatch.Draw(
                    Game1.mouseCursors,
                    pos,
                    new Rectangle(
                        Utility.withinRadiusOfPlayer(
                            (int)Game1.currentCursorTile.X * 64,
                            (int)Game1.currentCursorTile.Y * 64,
                            1,
                            Game1.player
                        ) ? 194 : 210,
                        388,
                        16,
                        16
                    ),
                    Color.White,
                    0f,
                    Vector2.Zero,
                    4f,
                    SpriteEffects.None,
                    0.01f
                );

                if (Config.ShowRangeWhenPlacing)
                {
                    foreach (Vector2 tile in
                        GetScarecrowTiles(scarecrowTile, which, __instance.GetRadiusForScarecrow()))
                    {
                        spriteBatch.Draw(Game1.mouseCursors, Game1.GlobalToLocal(tile * 64),
                            new Rectangle(194, 388, 16, 16), Color.White * 0.5f, 0f, Vector2.Zero,
                            4f, SpriteEffects.None, 0.01f);
                    }
                }

                if (__instance.bigCraftable.Value)
                    pos -= new Vector2(0, 64);

                spriteBatch.Draw(
                    __instance.bigCraftable.Value
                        ? Game1.bigCraftableSpriteSheet
                        : Game1.objectSpriteSheet,
                    pos + new Vector2(0, -16),
                    __instance.bigCraftable.Value
                        ? Object.getSourceRectForBigCraftable(__instance.ParentSheetIndex)
                        : GameLocation.getSourceRectForObject(__instance.ParentSheetIndex),
                    Color.White * Config.Alpha, 0,
                    Vector2.Zero,
                    Config.Scale,
                    __instance.Flipped
                        ? SpriteEffects.FlipHorizontally
                        : SpriteEffects.None, 0.02f
                );

                return false;
            }
        }

        [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.HandleGrassGrowth))]
        public class GameLocation_HandleGrassGrowth_Patch
        {
            public static bool Prefix(GameLocation __instance, int dayOfMonth)
            {
                if (!Config.EnableMod)
                    return true;

                if (dayOfMonth == 1)
                {
                    if (__instance is Farm || __instance.HasMapPropertyWithValue("ClearEmptyDirtOnNewMonth"))
                    {
                        __instance.terrainFeatures.RemoveWhere
                        (
                            (KeyValuePair<Vector2, TerrainFeature> pair) =>
                                pair.Value is HoeDirt hoeDirt
                                && hoeDirt.crop == null
                                && Game1.random.NextDouble() < 0.8
                                && !hoeDirt.modData.Keys.Any(t => t.StartsWith(scarecrowKey))
                        );
                    }

                    if (__instance is Farm || __instance.HasMapPropertyWithValue("SpawnDebrisOnNewMonth"))
                    {
                        __instance.spawnWeedsAndStones(20, weedsOnly: false, spawnFromOldWeeds: false);
                    }

                    if (Game1.IsSpring && Game1.stats.DaysPlayed > 1)
                    {
                        if (__instance is Farm || __instance.HasMapPropertyWithValue("SpawnDebrisOnNewYear"))
                        {
                            __instance.spawnWeedsAndStones(40, weedsOnly: false, spawnFromOldWeeds: false);
                            __instance.spawnWeedsAndStones(40, weedsOnly: true, spawnFromOldWeeds: false);
                        }

                        if (__instance is Farm || __instance.HasMapPropertyWithValue("SpawnRandomGrassOnNewYear"))
                        {
                            for (int i = 0; i < 15; i++)
                            {
                                int num = Game1.random.Next(__instance.map.DisplayWidth / 64);
                                int num2 = Game1.random.Next(__instance.map.DisplayHeight / 64);
                                Vector2 vector = new Vector2(num, num2);
                                __instance.objects.TryGetValue(vector, out var value);
                                if (value == null && __instance.doesTileHaveProperty(num, num2, "Diggable", "Back") != null && !__instance.IsNoSpawnTile(vector) && __instance.isTileLocationOpen(new Location(num, num2)) && !__instance.IsTileOccupiedBy(vector) && !__instance.isWaterTile(num, num2))
                                {
                                    int which = 1;
                                    if (Game1.whichModFarm?.Id == "MeadowlandsFarm" && Game1.random.NextDouble() < 0.2)
                                    {
                                        which = 7;
                                    }

                                    __instance.terrainFeatures.Add(vector, new Grass(which, 4));
                                }
                            }

                            __instance.growWeedGrass(40);
                        }

                        if (__instance.HasMapPropertyWithValue("SpawnGrassFromPathsOnNewYear"))
                        {
                            Layer layer = __instance.map.GetLayer("Paths");
                            if (layer != null)
                            {
                                for (int j = 0; j < layer.LayerWidth; j++)
                                {
                                    for (int k = 0; k < layer.LayerHeight; k++)
                                    {
                                        Vector2 vector2 = new Vector2(j, k);
                                        __instance.objects.TryGetValue(vector2, out var value2);
                                        if (value2 == null && __instance.getTileIndexAt(new Point(j, k), "Paths") == 22 && __instance.isTileLocationOpen(vector2) && !__instance.IsTileOccupiedBy(vector2))
                                        {
                                            __instance.terrainFeatures.Add(vector2, new Grass(1, 4));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if ((__instance is Farm || __instance.HasMapPropertyWithValue("EnableGrassSpread")) && (!__instance.IsWinterHere() || __instance.HasMapPropertyWithValue("AllowGrassGrowInWinter")))
                {
                    __instance.growWeedGrass(1);
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.GetDirtDecayChance))]
        public class GameLocation_GetDirtDecayChance_Patch
        {
            public static bool Prefix(GameLocation __instance, Vector2 tile, ref double __result)
            {
                if (Config.EnableMod)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (__instance.terrainFeatures.TryGetValue(tile, out TerrainFeature tf)
                            && tf.modData.ContainsKey(scarecrowKey + i))
                        {
                            __result = 0.0;
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Farm), nameof(Farm.addCrows))]
        public class Farm_addCrows_Patch
        {
            public static bool Prefix(Farm __instance)
            {
                if (!Config.EnableMod)
                    return true;

                int num = 0;
                foreach (KeyValuePair<Vector2, TerrainFeature> pair in __instance.terrainFeatures.Pairs)
                {
                    if (pair.Value is HoeDirt hoeDirt && hoeDirt.crop != null)
                    {
                        num++;
                    }
                }

                List<Vector2> list = new List<Vector2>();
                foreach (KeyValuePair<Vector2, Object> pair2 in __instance.objects.Pairs)
                {
                    if (pair2.Value.IsScarecrow())
                    {
                        list.Add(pair2.Key);
                    }
                }

                int num2 = Math.Min(4, num / 16);
                for (int i = 0; i < num2; i++)
                {
                    if (!(Game1.random.NextDouble() < 0.3))
                    {
                       continue;
                    }

                    for (int j = 0; j < 10; j++)
                    {
                        if (!Utility.TryGetRandom(__instance.terrainFeatures, out Vector2 key, out TerrainFeature value)
                            || !(value is HoeDirt hoeDirt2)
                            || hoeDirt2.crop?.currentPhase.Value <= 1)
                        {
                            continue;
                        }

                        bool flag = false;
                        foreach (Vector2 item in list)
                        {
                            int radiusForScarecrow = __instance.objects[item].GetRadiusForScarecrow();
                            if (Vector2.Distance(item, key) < (float)radiusForScarecrow)
                            {
                                flag = true;
                                __instance.objects[item].SpecialVariable++;
                                break;
                            }
                        }

                        if (!flag && IsNoScarecrowInRange(__instance, key))
                        {
                            hoeDirt2.destroyCrop(showAnimation: false);

                            if (__instance.critters == null && __instance.IsOutdoors)
                            {
                                __instance.critters = new List<StardewValley.BellsAndWhistles.Critter>();
                            }

                            __instance.critters.Add(new StardewValley.BellsAndWhistles.Crow((int)key.X, (int)key.Y));
                        }

                        break;
                    }
                }

                return false;
            }
        }

        public static bool Modded_Farm_AddCrows_Prefix(ref bool __result)
        {
            SMonitor.Log("Disabling addCrows prefix for Prismatic Tools and Radioactive tools");
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(Axe), nameof(Axe.DoFunction))]
        public class Axe_DoFunction_Patch
        {
            public static bool Prefix(GameLocation location, int x, int y, int power)
            {
                if (!Config.EnableMod || power > 1)
                    return true;

                return HandleAxeAndPickaxeFunction(location, x, y);
            }
        }

        [HarmonyPatch(typeof(Pickaxe), nameof(Pickaxe.DoFunction))]
        public class Pickaxe_DoFunction_Patch
        {
            public static bool Prefix(GameLocation location, int x, int y)
            {
                if (!Config.EnableMod)
                    return true;

                return HandleAxeAndPickaxeFunction(location, x, y);
            }
        }
    }
}
