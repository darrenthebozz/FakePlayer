#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameInput;
using Terraria.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent;
using Terraria.DataStructures;
using Terraria.Graphics.Renderers;
using Terraria.Graphics;
using static System.Net.Mime.MediaTypeNames;
using Newtonsoft.Json;
using ReLogic.OS;
using System.Globalization;
using Terraria.Audio;
using Terraria.GameContent.Creative;
using Terraria.Initializers;
using Terraria.IO;
using Terraria.Localization;
using Terraria.UI.Gamepad;
using Terraria.GameContent.UI.States;
using System.IO;
using System.Threading.Tasks;
using ReLogic.Content;
using System.Text;
using Terraria.ModLoader.UI;
using Terraria.Utilities;
using System.Reflection;

/* If you are reading this prepare for a horror show of programming
 * It was also built with popsicle sticks so it could fall apart
 * You've been warned...
 */
/* TODO
 */
/* We added
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
        internal static ModKeybind? fakePlayerUIBind;

        public override void Load()
        {
            fakePlayerUIBind = KeybindLoader.RegisterKeybind(this, "Fake Player UI", "OemPeriod");
        }
        public override void Unload()
        {
            fakePlayerUIBind = null;
        }
        public static Player? Create()
        {
            int index = Array.FindIndex<Terraria.Player>(Main.player, playerFromArray => playerFromArray.active == false);
            if (index == -1)
                return null;

            Player? player = (Player)typeof(Player).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Testing.UICharacterListItem.selectedPlayer, null);

            if (player == null)
                return null;

            player.active = true;
            player.name = "Player Dummy";
            player.whoAmI = index;
            player.GetModPlayer<FakePlayerCharater>().isFakePlayer = true;
            Main.player[index] = player;
            return player;
        }
        public static void Destroy(ref Player? player)
        {
            if (player == null)
                return;
            int index = Array.FindIndex<Terraria.Player>(Main.player, playerFromArray => playerFromArray.active == false);
            if (index == -1)
                return;
            player.active = false;
            (Main.player[index - 1], Main.player[player.whoAmI]) = (Main.player[player.whoAmI], Main.player[index - 1]); //swaps entries player.whoamI and index -1's player values
            (Main.player[player.whoAmI].whoAmI, Main.player[index - 1].whoAmI) = (Main.player[index - 1].whoAmI, Main.player[player.whoAmI].whoAmI); //swaps their whoami values
            Main.player[player.whoAmI].whoAmI = player.whoAmI;
            player = null;
            return;
        }
    }
    public class FakePlayerCharater : ModPlayer
    {
        public bool isFakePlayer = false;
    }
    public class FakePlayerItem : ModItem
    {
        public Player? selectedPlayer = null;
        public override void SetDefaults()
        {
            Item.useTime = 0;
            Item.useAnimation = 0;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.noMelee = true;
            Item.autoReuse = true;
        }
        Player? IsCursorTouchingFakePlayer()
        {
            foreach (Player currentPlayer in Main.player)
            {
                if (!currentPlayer.active)
                    break;
                if (!currentPlayer.GetModPlayer<FakePlayerCharater>().isFakePlayer)
                    continue;

                Vector2 position = currentPlayer.position - Main.MouseScreen - Main.screenPosition;

                if (Math.Abs(position.X) < currentPlayer.Hitbox.Width && Math.Abs(position.Y) < currentPlayer.Hitbox.Height)
                {
                    return currentPlayer;
                }
            }
            return null;
        }
        bool mouseRightWasSet = false;
        bool mouseLeftWasSet = false;
        bool statePressedUIBind = true;
        public override void UpdateInventory(Player player)
        {
            bool mouseRightJustReleased = false;

            if (FakePlayerMod.fakePlayerUIBind != null)
            {
                if (FakePlayerMod.fakePlayerUIBind.JustPressed)
                {
                    FakePlayerUISystem fakePlayerUISystem = ModContent.GetInstance<FakePlayerUISystem>();
                    if (fakePlayerUISystem.uICharacterSelect != null)
                    {
                        fakePlayerUISystem.uICharacterSelect.doNotDraw = !statePressedUIBind;
                        statePressedUIBind = !statePressedUIBind;
                    }
                }
            }
            if (Main.mouseRightRelease && mouseRightWasSet)
            {
                mouseRightJustReleased = true;
                mouseRightWasSet = false;
            }
            else
                mouseRightJustReleased = false;
            if (Main.mouseRight)
                mouseRightWasSet = true;

            bool mouseLeftJustReleased = false;
            if (Main.mouseLeftRelease && mouseLeftWasSet)
            {
                mouseLeftJustReleased = true;
                mouseLeftWasSet = false;
            }
            else
                mouseLeftJustReleased = false;
            if (Main.mouseLeft)
                mouseLeftWasSet = true;
            if (mouseLeftJustReleased)
            {
                selectedPlayer = null;
            }
            if (mouseRightJustReleased)
            {
                Player? selectedPlayer = IsCursorTouchingFakePlayer();
                if (selectedPlayer != null)
                    FakePlayerMod.Destroy(ref selectedPlayer);
            }
        }
        public override bool? UseItem(Player player)
        {
            if (selectedPlayer == null)
                selectedPlayer = IsCursorTouchingFakePlayer();

            if (selectedPlayer == null)
            {
                selectedPlayer = FakePlayerMod.Create();
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
    }
    public class FakePlayerUISystem : ModSystem
    {
        internal UserInterface? userInterface;
        internal Testing.UICharacterSelect? uICharacterSelect;
        internal GameTime? lastUpdateUIGameTime;
        public override void Load()
        {
        }
        bool firstTime = true;
        public override void UpdateUI(GameTime gameTime)
        {
            if (firstTime && !Main.dedServ)
            {
                userInterface = new UserInterface();
                uICharacterSelect = new Testing.UICharacterSelect();
                uICharacterSelect.Activate(); // Activate calls Initialize() on the UIState if not initialized and calls OnActivate, then calls Activate on every child element.
                userInterface.SetState(uICharacterSelect);
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
                    nameof(FakePlayer) + ": " + nameof(uICharacterSelect),
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
namespace Testing
{
    public class UICharacterListItem : UIPanel
    {
        public static Player selectedPlayer = new Player();
        public bool failedToGetPlayer = false;
        private PlayerFileData _data;

        private Asset<Texture2D> _dividerTexture;

        private Asset<Texture2D> _innerPanelTexture;

        private UICharacter _playerPanel;

        private UIText _buttonLabel;

        private Asset<Texture2D> _buttonPlayTexture;

        public UICharacterListItem(PlayerFileData data, int snapPointIndex)
        {
            BorderColor = new Color(89, 116, 213) * 0.7f;
            _innerPanelTexture = Main.Assets.Request<Texture2D>("Images/UI/InnerPanelBackground");
            Height.Set(96f, 0f);
            Width.Set(0f, 1f);
            SetPadding(6f);
            _data = data;
            _playerPanel = new UICharacter(data.Player);
            _playerPanel.Left.Set(0f, 0.5f - 0.125f);
            _playerPanel.Top.Set(24f, 0f);
            _playerPanel.OnDoubleClick += PlayGame;
            base.OnDoubleClick += PlayGame;
            Append(_playerPanel);
        }

        private void PlayGame(UIMouseEvent evt, UIElement listeningElement)
        {
            if (listeningElement == evt.Target && _data.Player.loadStatus == 0)
            {
                selectedPlayer = _data.Player;
            }
        }

        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
            BackgroundColor = new Color(73, 94, 171);
            BorderColor = new Color(89, 116, 213);
            _playerPanel.SetAnimated(animated: true);
        }

        public override void MouseOut(UIMouseEvent evt)
        {
            base.MouseOut(evt);
            BackgroundColor = new Color(63, 82, 151) * 0.7f;
            BorderColor = new Color(89, 116, 213) * 0.7f;
            _playerPanel.SetAnimated(animated: false);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            CalculatedStyle dimensions = _playerPanel.GetDimensions();
            float num = dimensions.X;
            Color color = Color.White;
            string text = _data.Name;
            if (_data.Player.loadStatus != 0)
            {
                color = Color.Gray;
                string name = StatusID.Search.GetName(_data.Player.loadStatus);
                text = "(" + name + ") " + text;
            }

            //Utils.DrawBorderString(spriteBatch, text, new Vector2(0, 0), color);
            Utils.DrawBorderString(spriteBatch, text, new Vector2(num + 2f, dimensions.Y - 24f), color);
        }
        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
        }
    }
    public class UICharacterSelect : UIState
    {
        internal UIList _playerList;

        private UIPanel _containerPanel;

        private UIScrollbar _scrollbar;

        private bool _isScrollbarAttached;
        public bool doNotDraw = true;

        public override void OnInitialize()
        {
            UIElement uIElement = new UIElement();
            uIElement.Width.Set(0f, 0.125f);
            uIElement.MaxWidth.Set(650f, 0f);
            uIElement.Top.Set(220f, 0f);
            uIElement.Height.Set(-220f, 1f);
            uIElement.HAlign = 0.5f;
            UIPanel uIPanel = new UIPanel();
            uIPanel.Width.Set(0f, 1f);
            uIPanel.Height.Set(-110f, 1f);
            uIPanel.BackgroundColor = new Color(33, 43, 79) * 0.8f;
            _containerPanel = uIPanel;
            uIElement.Append(uIPanel);
            _playerList = new UIList();
            _playerList.Width.Set(0f, 0.1f);
            _playerList.Height.Set(0f, 1f);
            _playerList.ListPadding = 5f;
            uIPanel.Append(_playerList);
            _scrollbar = new UIScrollbar();
            _scrollbar.SetView(100f, 1000f);
            _scrollbar.Height.Set(0f, 1f);
            _scrollbar.HAlign = 1f;
            _playerList.SetScrollbar(_scrollbar);
            UITextPanel<string> uITextPanel = new UITextPanel<string>("Players", 0.8f, large: true);
            uITextPanel.HAlign = 0.5f;
            uITextPanel.Top.Set(-40f, 0f);
            uITextPanel.SetPadding(15f);
            uITextPanel.BackgroundColor = new Color(73, 94, 171);
            uIElement.Append(uITextPanel);
            Append(uIElement);
        }

        public override void Recalculate()
        {
            if (_scrollbar != null)
            {
                if (_isScrollbarAttached && !_scrollbar.CanScroll)
                {
                    _containerPanel.RemoveChild(_scrollbar);
                    _isScrollbarAttached = false;
                    _playerList.Width.Set(0f, 1f);
                }
                else if (!_isScrollbarAttached && _scrollbar.CanScroll)
                {
                    _containerPanel.Append(_scrollbar);
                    _isScrollbarAttached = true;
                    _playerList.Width.Set(-25f, 1f);
                }
            }

            base.Recalculate();
        }

        public override void OnActivate()
        {
            Main.LoadPlayers();
            Main.ActivePlayerFileData = new PlayerFileData();
            UpdatePlayersList();
        }

        private void UpdatePlayersList()
        {
            _playerList.Clear();
            List<PlayerFileData> list = new List<PlayerFileData>(Main.PlayerList);
            int num = 0;
            PlayerFileData fakePlayerDefault = new PlayerFileData();
            fakePlayerDefault.Player = new Player();
            fakePlayerDefault.Name = "FakePlayer";
            list.Add(fakePlayerDefault);
            foreach (PlayerFileData item in list)
            {
                _playerList.Add(new UICharacterListItem(item, num++));
            }

            if (list.Count != 0)
            {
                return;
            }

            string vanillaPlayersPath = Path.Combine(Platform.Get<IPathService>().GetStoragePath("Terraria"), "Players");
            if (!Directory.Exists(vanillaPlayersPath) || !Directory.GetFiles(vanillaPlayersPath, "*.plr").Any())
            {
                return;
            }
        }
        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!doNotDraw)
            {
                base.Draw(spriteBatch);
            }
        }
    }
}