#nullable enable
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.IO;
using System.Reflection;
using Terraria.GameContent.UI.States;

/* If you are reading this prepare for a horror show of programming
 * It was also built with popsicle sticks so it could fall apart
 * You've been warned...
 */
/* TODO
 * Make players killable
 * Make players hair light up in ui
 */
/* We added
 * Fix GUI visual bug x
 * Tell user which player is currently selected using green overlay of player x
 * multiple player creation and deletion
 * hovering the cursor over a fake player allows you to move them or remove them
 * Add multiple players to the FakePlayer Item
 * Hold player to select Right click and Hold to delete
 * GUI to customize player that is bound to mouse3
 */
namespace FakePlayer
{
    public class FakePlayerMod : Mod
    {
        internal static ModKeybind fakePlayerUIBind;

        public override void Load()
        {
            fakePlayerUIBind = KeybindLoader.RegisterKeybind(this, "Fake Player UI", "OemPeriod");
        }
    }
    public class FakePlayerItem : ModItem
    {
        static List<Player> fakePlayer = new List<Player>();
        public override void SetDefaults()
        {
            Item.useTime = 0;
            Item.useAnimation = 0;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.noMelee = true;
            Item.autoReuse = true;
        }
        public static Player? Create()
        {
            int index = Array.FindIndex<Terraria.Player>(Main.player, playerFromArray => playerFromArray.active == false);
            if (index == -1)
                return null;

            Player player = new Player();
            player.CopyVisuals(UIPlayerPanel.selectedUIPlayerPanel.player);
            player.active = true;
            player.name = UIPlayerPanel.selectedUIPlayerPanel.player.name;
            player.whoAmI = index;
            Main.player[index] = player;
            fakePlayer.Add(player);

            return player;
        }
        public static void Destroy(ref Player player)
        {
            player.active = false;
            fakePlayer.Remove(player);
        }
        Player? IsCursorTouchingFakePlayer()
        {
            foreach (Player currentFakePlayer in fakePlayer)
            {
                Vector2 position = currentFakePlayer.position - Main.MouseScreen - Main.screenPosition;

                if (Math.Abs(position.X) < currentFakePlayer.Hitbox.Width && Math.Abs(position.Y) < currentFakePlayer.Hitbox.Height)
                {
#if DEBUG
                    Main.NewText("Touching Fake Player");
#endif
                    return currentFakePlayer;
                }
            }
            return null;
        }
        public override void UpdateInventory(Player player)
        {
            if (player.whoAmI != Main.LocalPlayer.whoAmI)
                return;
            if (FakePlayerMod.fakePlayerUIBind.JustPressed)
            {
                FakePlayerUISystem fakePlayerUISystem = ModContent.GetInstance<FakePlayerUISystem>();
                fakePlayerUISystem.userInterface.SetState(fakePlayerUISystem.userInterface.CurrentState == null ? fakePlayerUISystem.uiPlayerList : null );
#if DEBUG
                Main.NewText(fakePlayerUISystem.userInterface.CurrentState != null ? "GUI opened" : "GUI closed");
#endif
            }
        }

        public Player? selectedPlayer = null;
        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.LocalPlayer.whoAmI)
                return null;
            if (ModContent.GetInstance<FakePlayerUISystem>().userInterface.CurrentState != null)
                return null;
            if (Main.mouseLeftRelease)
                selectedPlayer = null;

            if (selectedPlayer == null)
                selectedPlayer = IsCursorTouchingFakePlayer();
            if (selectedPlayer == null)
            {
                selectedPlayer = Create();
                if (selectedPlayer == null)
                {
                    Main.NewText("All player slots are maxed out sorry...");
                    return null;
                }
            }
            selectedPlayer.velocity = new Vector2(0);
            selectedPlayer.position = Main.MouseWorld - selectedPlayer.Size / new Vector2(2, 1) + player.velocity;
            return null;
        }
        public override bool AltFunctionUse(Player player)
        {
            if (player.whoAmI != Main.LocalPlayer.whoAmI)
                return false;
            //if (ModContent.GetInstance<FakePlayerUISystem>().userInterface.CurrentState != null)
            //    return false;
            Player? currentFakePlayer = IsCursorTouchingFakePlayer();
            if (currentFakePlayer != null)
            {
                if (selectedPlayer == currentFakePlayer)
                    selectedPlayer = null;
                Destroy(ref currentFakePlayer);
                currentFakePlayer = null;
            }
            return false;
        }
    }
    public class FakePlayerUISystem : ModSystem
    {
        public UserInterface userInterface = new UserInterface();
        public UIPlayerList uiPlayerList;
        internal GameTime? lastUpdateUIGameTime;
        bool firstTime = true;
        public override void UpdateUI(GameTime gameTime)
        {
            if ((firstTime && !Main.dedServ) || false) //if we are here surely the players are loaded... Right?
            {
                uiPlayerList = new UIPlayerList();
                uiPlayerList.Activate();
                //userInterface.SetState(uiPlayerList);
                firstTime = false;
            }
            lastUpdateUIGameTime = gameTime;
            if (userInterface?.CurrentState != null)
            {
                userInterface.Update(gameTime);
            }
        }
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    nameof(FakePlayer) + ": " + nameof(uiPlayerList),
                    delegate
                    {
                        if (lastUpdateUIGameTime != null && userInterface?.CurrentState != null)
                        {
                            userInterface.Draw(Main.spriteBatch, lastUpdateUIGameTime);
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)); // puts behind mouse somehow. My guess it inserts it behind that layer.
            }
        }

    }

    class UIPlayerPanel : UIPanel
    {
        public static UIPlayerPanel selectedUIPlayerPanel;
        public UICharacter uICharacter;
        public Player player;
        public Color defaultBackgroundColor = new Color(63, 82, 151) * 0.7f;
        public Color HoverBackgroundColor = new Color(73, 94, 171);
        public Color selectedBackgroundColor = Color.Green;
        public UIPlayerPanel(PlayerFileData playerFileData)
        {
            player = playerFileData.Player;
            Player fakePlayer = new Player();
            fakePlayer.position = new Vector2(Main.maxTilesX * 16 / 2, Main.maxTilesY * 16 / 2);
            fakePlayer.CopyVisuals(playerFileData.Player);
            uICharacter = new UICharacter(fakePlayer, false, true);
            uICharacter.OnDoubleClick += DoubleClick;
            Append(uICharacter);

            UIText uIPlayerName = new UIText(playerFileData.Name);
            uIPlayerName.Left.Set(uICharacter.Width.Pixels, 0f);
            uIPlayerName.OnDoubleClick += DoubleClick;
            Append(uIPlayerName);

            Height.Set(uICharacter.Height.Pixels + 25f, 0f);
            Width.Set(0f, 1f);
            base.OnDoubleClick += DoubleClick;
            selectedUIPlayerPanel = this;
        }
        private void DoubleClick(UIMouseEvent evt, UIElement listeningElement)
        {
#if DEBUG
            Main.NewText("Selected Player");
#endif
            BackgroundColor = selectedBackgroundColor;
            selectedUIPlayerPanel = this;
        }
        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
            uICharacter.SetAnimated(animated: true);
            if (selectedUIPlayerPanel == this)
                return;
            BackgroundColor = HoverBackgroundColor;
        }
        public override void MouseOut(UIMouseEvent evt)
        {
            base.MouseOut(evt);
            uICharacter.SetAnimated(animated: false);
            if (selectedUIPlayerPanel == this)
                return;
            BackgroundColor = new Color(63, 82, 151) * 0.7f;
            //BorderColor = new Color(89, 116, 213) * 0.7f;
        }
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (selectedUIPlayerPanel == null)
                return;
            if (this == selectedUIPlayerPanel)
                return;
            if (BackgroundColor == defaultBackgroundColor)
                return;
            BackgroundColor = defaultBackgroundColor;
        }
    }
    public class UIPlayerList : UIState
    {
        public UIPanel uIPlayersTextHolder;
        public UIList uIPlayerList;
        public UIScrollbar uIScrollbar;
        public UIText uIPlayersText;
        public UIPanel uIPlayerListPanel;
        public UIPlayerList()
        {
            uIPlayersText = new UIText("Players", 1f, true);

            uIPlayerListPanel = new UIPanel();
            uIPlayerListPanel.Width.Set(0f, 0.25f);
            uIPlayerListPanel.Height.Set(0f, 0.5f);
            uIPlayerListPanel.Left.Set(0f, 0.5f - uIPlayerListPanel.Width.Precent / 2);
            uIPlayerListPanel.Top.Set(0f, 0.5f - uIPlayerListPanel.Height.Precent / 2);
            Append(uIPlayerListPanel);

            uIScrollbar = new UIScrollbar();
            uIScrollbar.Left.Set(-uIScrollbar.Width.Pixels * 0.75f, 1f);
            uIScrollbar.SetView(100f, 1000f);
            uIScrollbar.Height.Set(0f, 1f);
            uIPlayerListPanel.Append(uIScrollbar);

            uIPlayerList = new UIList();
            uIPlayerList.Height.Set(0f, 1f);
            uIPlayerList.Width.Set(-uIScrollbar.Width.Pixels, 1f);
            uIPlayerList.SetScrollbar(uIScrollbar);
            foreach (PlayerFileData playerFileData in Main.PlayerList)
            {
                uIPlayerList.Add(new UIPlayerPanel(playerFileData));
            }
            PlayerFileData fakePlayerFileData = new PlayerFileData();
            fakePlayerFileData.Player = new Player();
            fakePlayerFileData.Name = "Fake Player";
            fakePlayerFileData.Player.name = fakePlayerFileData.Name;
            UIPlayerPanel uIFakePlayerPanel = new UIPlayerPanel(fakePlayerFileData);
            uIFakePlayerPanel.BackgroundColor = uIFakePlayerPanel.selectedBackgroundColor;
            uIPlayerList.Add(uIFakePlayerPanel);

            uIPlayerListPanel.Append(uIPlayerList);

            uIPlayersTextHolder = new UIPanel();
            uIPlayersTextHolder.Width.Set(uIPlayersText.MinWidth.Pixels * (1.125f + 0.05f), 0f);
            uIPlayersTextHolder.Height.Set(uIPlayersText.MinHeight.Pixels * 2, 0f);
            uIPlayersTextHolder.Append(uIPlayersText);
            uIPlayersTextHolder.Left.Set(-uIPlayersTextHolder.Width.Pixels * 0.5f, uIPlayerListPanel.Left.Percent + uIPlayerListPanel.Width.Percent / 2);
            uIPlayersTextHolder.Top.Set(-uIPlayersTextHolder.Height.Pixels + 15f, uIPlayerListPanel.Top.Percent);
            Append(uIPlayersTextHolder);
        }
    }
    /*
    public class LightHack : GlobalWall //we need this to make sure the FakePreviewPlayer is always lit
    {
        public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
        {
            //gotta light the region around them or else the prievew will be dark
            //Wish they gave an option to disable lighting on the prev
            Player? player = FakePlayerUI.player;
            /*
             * I and J are tile coords
             * tiles are 16 by 16
            *\/
            if (player == null)
                return;
            if (FakePlayerUI.panel == null)
                return;

            Rectangle tile = new Rectangle(i * 16, j * 16, 16, 16);

            Rectangle panelHitbox = new Rectangle(((int)FakePlayerUI.panel.Left.Pixels), ((int)FakePlayerUI.panel.Top.Pixels), ((int)FakePlayerUI.panel.Width.Pixels), ((int)FakePlayerUI.panel.Height.Pixels));
            Rectangle playerInPanelHitbox = new Rectangle(((int)FakePlayerUI.panel.Left.Pixels), ((int)FakePlayerUI.panel.Top.Pixels), ((int)FakePlayerUI.panel.Width.Pixels), ((int)FakePlayerUI.panel.Height.Pixels));

            if (tile.Intersects(playerInPanelHitbox))
            {
                r = 255;
                g = 255;
                b = 255;
            }
        }
    }
*/
}