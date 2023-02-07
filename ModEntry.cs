﻿using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;
using System.Globalization;
using Object = StardewValley.Object;

namespace ImmersiveScarecrows
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {

        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        public static ModConfig Config;

        public static ModEntry context;

        public static object atApi;

        public static string prefixKey = "aedenthorn.ImmersiveScarecrows/";
        public static string scarecrowKey = "aedenthorn.ImmersiveScarecrows/scarecrow";
        public static string scaredKey = "aedenthorn.ImmersiveScarecrows/scared";
        public static string hatKey = "aedenthorn.ImmersiveScarecrows/hat";
        public static string guidKey = "aedenthorn.ImmersiveScarecrows/guid";
        public static string altTexturePrefix = "aedenthorn.ImmersiveScarecrows/AlternativeTexture";
        public static string altTextureKey = "AlternativeTexture";

        public static Dictionary<string, Object> scarecrowDict = new();

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();

            context = this;

            SMonitor = Monitor;
            SHelper = helper;

            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.Input.ButtonPressed += Input_ButtonPressed;
            Helper.Events.Display.RenderedWorld += Display_RenderedWorld;

            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.PatchAll();

        }

        private void Display_RenderedWorld(object sender, StardewModdingAPI.Events.RenderedWorldEventArgs e)
        {
            if (!Config.EnableMod || !Context.IsPlayerFree || !Helper.Input.IsDown(Config.ShowRangeButton) || Game1.currentLocation?.terrainFeatures?.TryGetValue(Game1.currentCursorTile, out var tf) != true || tf is not HoeDirt)
                return;
            var which = GetMouseCorner();
            var scarecrowTile = Game1.currentCursorTile;
            if (!GetScarecrowTileBool(Game1.currentLocation, ref scarecrowTile, ref which, out string str))
                return;
            if (!Game1.currentLocation.terrainFeatures.TryGetValue(scarecrowTile, out tf))
                return;
            var obj = GetScarecrow(tf, which);
            if(obj is not null)
            {
                var r = obj.GetRadiusForScarecrow();
                Vector2 pos = scarecrowTile + GetScarecrowCorner(which) * 0.5f;
                for(int x = 0; x < r * 2 + 1; x ++)
                {
                    for (int y = 0; y < r * 2 + 1; y++)
                    {
                        Vector2 tile = scarecrowTile + new Vector2(x - r , y - r);
                        if (Vector2.Distance(tile, pos) < r)
                            e.SpriteBatch.Draw(Game1.mouseCursors, Game1.GlobalToLocal(new Vector2((float)((int)tile.X * 64), (float)((int)tile.Y * 64))), new Rectangle?(new Rectangle(194, 388, 16, 16)), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.01f);

                    }
                }
            }
        }

        private void Input_ButtonPressed(object sender, StardewModdingAPI.Events.ButtonPressedEventArgs e)
        {
            if (!Config.EnableMod)
                return;

            if(Context.IsPlayerFree && Config.Debug && e.Button == SButton.X)
            {

                Game1.getFarm().addCrows();
                Monitor.Log("Adding crows");
            }
            if (e.Button == Config.PickupButton && Context.CanPlayerMove && Game1.currentLocation.terrainFeatures.TryGetValue(Game1.currentCursorTile, out var tf) && tf is HoeDirt)
            {
                int which = GetMouseCorner();
                if (ReturnScarecrow(Game1.player, Game1.currentLocation, tf, Game1.currentCursorTile, which))
                {
                    Helper.Input.Suppress(e.Button);
                }
            }
        }

        private void GameLoop_SaveLoaded(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            scarecrowDict.Clear();
        }

        private void GameLoop_GameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            atApi = Helper.ModRegistry.GetApi("PeacefulEnd.AlternativeTextures");

            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Mod Enabled",
                getValue: () => Config.EnableMod,
                setValue: value => Config.EnableMod = value
            );
            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => "Pickup Key",
                getValue: () => Config.PickupButton,
                setValue: value => Config.PickupButton = value
            );
            
            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => "Show Range Key",
                getValue: () => Config.ShowRangeButton,
                setValue: value => Config.ShowRangeButton = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Scale",
                getValue: () => Config.Scale + "",
                setValue: delegate (string value) { if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float f)) { Config.Scale = f; } }
            );
            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Alpha",
                getValue: () => Config.Alpha + "",
                setValue: delegate (string value) { if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float f)) { Config.Alpha = f; } }
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Offset X",
                getValue: () => Config.DrawOffsetX,
                setValue: value => Config.DrawOffsetX = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Offset Y",
                getValue: () => Config.DrawOffsetY,
                setValue: value => Config.DrawOffsetY = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Offset Z",
                getValue: () => Config.DrawOffsetZ,
                setValue: value => Config.DrawOffsetZ = value
            );

        }

    }
}
