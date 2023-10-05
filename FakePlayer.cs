#nullable enable
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Achievements;
using Terraria.GameContent.Events;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using System.Collections;
using Terraria.GameContent.Creative;
using System.IO;
using ReLogic.Content;
using System.Xml.Linq;
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
    public class FakePlayer : ModPlayer
    {
        public bool isFakePlayerKillable = false;
    }

    public class FakePlayerMod : Mod
    {
        internal static ModKeybind fakePlayerUIBind;

        public override void Load()
        {
            fakePlayerUIBind = KeybindLoader.RegisterKeybind(this, "Fake Player UI", "OemPeriod");

            Terraria.On_Player.Update += On_Player_Update;
            Terraria.On_Player.CheckDrowning += On_Player_CheckDrowning;
            Terraria.On_Player.Update_NPCCollision += On_Player_Update_NPCCollision;
            Terraria.On_Player.UpdateDead += On_Player_UpdateDead;
            Terraria.On_Player.Spawn += On_Player_Spawn;
            Terraria.On_Player.Hurt_HurtInfo_bool += On_Player_Hurt_HurtInfo_bool;
        }

        private void On_Player_Hurt_HurtInfo_bool(On_Player.orig_Hurt_HurtInfo_bool orig, Player self, Player.HurtInfo info, bool quiet)
        {
            FakePlayer fakePlayer = self.GetModPlayer<FakePlayer>();

            if (!fakePlayer.isFakePlayerKillable)
            {
                orig.Invoke(self, info, quiet);
                return;
            }
            Player clientPlayerTmp = Main.clientPlayer;

            Main.clientPlayer = self;
            Main.myPlayer = self.whoAmI;

            orig.Invoke(self, info, quiet);

            Main.clientPlayer = clientPlayerTmp;
            Main.myPlayer = clientPlayerTmp.whoAmI;
        }
        private void On_Player_Spawn(On_Player.orig_Spawn orig, Player self, PlayerSpawnContext context)
        {
            FakePlayer fakePlayer = self.GetModPlayer<FakePlayer>();

            if (!fakePlayer.isFakePlayerKillable)
            {
                orig.Invoke(self, context);
                return;
            }
            bool flag = false;
            if (context == PlayerSpawnContext.SpawningIntoWorld)
            {
                if (self.dead)
                {
                    typeof(Player).InvokeMember("AdjustRespawnTimerForWorldJoining", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, new object[] { self });
                    if (self.dead)
                    {
                        flag = true;
                    }
                }
            }
            self.StopVanityActions();
            Main.NotifyOfEvent(GameNotificationType.SpawnOrDeath);

            if (Main.netMode == 1)
            {
                NetMessage.SendData(12, -1, -1, null, Main.myPlayer, (int)(byte)context);
            }
            self.headPosition = Vector2.Zero;
            self.bodyPosition = Vector2.Zero;
            self.legPosition = Vector2.Zero;
            self.headRotation = 0f;
            self.bodyRotation = 0f;
            self.legRotation = 0f;
            self.rabbitOrderFrame.Reset();
            self.lavaTime = self.lavaMax;
            if (!flag)
            {
                if (self.statLife <= 0)
                {
                    int num = self.statLifeMax2 / 2;
                    self.statLife = 100;
                    if (num > self.statLife)
                    {
                        self.statLife = num;
                    }
                    self.breath = self.breathMax;
                    if (self.spawnMax)
                    {
                        self.statLife = self.statLifeMax2;
                        self.statMana = self.statManaMax2;
                    }
                }
                self.immune = true;
                if (self.dead)
                {
                    PlayerLoader.OnRespawn(self);
                }
                self.dead = false;
                self.immuneTime = 0;
            }
            self.active = true;
            Vector2 value = self.position;
            if (self.SpawnX >= 0 && self.SpawnY >= 0)
            {
                _ = self.SpawnX;
                _ = self.SpawnY;
                typeof(Player).InvokeMember("Spawn_SetPosition", BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic, null, self, new object[]{ self.SpawnX, self.SpawnY });
            }
            else
            {
                typeof(Player).InvokeMember("Spawn_SetPositionAtWorldSpawn", BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic, null, self, null);
            }
            self.wet = false;
            self.wetCount = 0;
            self.lavaWet = false;
            self.fallStart = (int)(self.position.Y / 16f);
            self.fallStart2 = self.fallStart;
            self.velocity.X = 0f;
            self.velocity.Y = 0f;
            self.ResetAdvancedShadows();
            for (int i = 0; i < 3; i++)
            {
                self.UpdateSocialShadow();
            }
            self.oldPosition = self.position + self.BlehOldPositionFixer;
            if (!flag)
            {
                if (self.pvpDeath)
                {
                    self.pvpDeath = false;
                    self.immuneTime = 300;
                    self.statLife = self.statLifeMax;
                }
                else if (context == PlayerSpawnContext.ReviveFromDeath)
                {
                    self.immuneTime = 180;
                }
                else
                {
                    self.immuneTime = 60;
                }
                if (self.immuneTime > 0 && !self.hostile)
                {
                    self.immuneNoBlink = true;
                }
            }
            if (flag)
            {
                self.immuneAlpha = 255;
            }
            typeof(Player).InvokeMember("UpdateGraveyard", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod, null, self, new object[] { true });
            if (context == PlayerSpawnContext.ReviveFromDeath && self.difficulty == 3)
            {
                self.AutoFinchStaff();
            }
            if (context == PlayerSpawnContext.SpawningIntoWorld)
            {
                Player.Hooks.EnterWorld(self.whoAmI);
            }
        }
        private void On_Player_UpdateDead(On_Player.orig_UpdateDead orig, Player self)
        {
            FakePlayer fakePlayer = self.GetModPlayer<FakePlayer>();

            orig.Invoke(self);
            if (!fakePlayer.isFakePlayerKillable)
            {
                return;
            }

            if (self.difficulty != 2 && self.respawnTimer <= 0)
                self.Spawn(PlayerSpawnContext.ReviveFromDeath);
        }
        private void On_Player_Update_NPCCollision(On_Player.orig_Update_NPCCollision orig, Player self)
        {
            FakePlayer fakePlayer = self.GetModPlayer<FakePlayer>();

            if (!fakePlayer.isFakePlayerKillable)
            {
                orig.Invoke(self);
                return;
            }
            Player clientPlayerTmp = Main.clientPlayer;

            Main.clientPlayer = self;
            Main.myPlayer = self.whoAmI;

            orig.Invoke(self);

            Main.clientPlayer = clientPlayerTmp;
            Main.myPlayer = clientPlayerTmp.whoAmI;
        }
        private void On_Player_CheckDrowning(On_Player.orig_CheckDrowning orig, Player self)
        {
            FakePlayer fakePlayer = self.GetModPlayer<FakePlayer>();

            if (!fakePlayer.isFakePlayerKillable)
            {
                orig.Invoke(self);
                return;
            }
            Player clientPlayerTmp = Main.clientPlayer;

            Main.clientPlayer = self;
            Main.myPlayer = self.whoAmI;

            orig.Invoke(self);

            Main.clientPlayer = clientPlayerTmp;
            Main.myPlayer = clientPlayerTmp.whoAmI;
        }
        public void On_Player_Update(On_Player.orig_Update orig, Player self, int i)
        {
            FakePlayer fakePlayer = self.GetModPlayer<FakePlayer>();
            orig.Invoke(self, i);

            FakePlayerUISystem fakePlayerUISystem = ModContent.GetInstance<FakePlayerUISystem>();

            if (!fakePlayer.isFakePlayerKillable)
                return;

            if (self.velocity.Y <= 0f)
            {
                self.fallStart2 = (int)(self.position.Y / 16f);
            }
            if (self.velocity.Y == 0f)
            {
                int num25 = 25;
                num25 += self.extraFall;
                int num26 = (int)(self.position.Y / 16f) - self.fallStart;
                if (self.mount.CanFly())
                {
                    num26 = 0;
                }
                if (self.mount.Cart && Minecart.OnTrack(self.position, self.width, self.height))
                {
                    num26 = 0;
                }
                if (self.mount.Type == 1)
                {
                    num26 = 0;
                }
                if (num26 > 0 || (self.gravDir == -1f && num26 < 0))
                {
                    int num112 = (int)(self.position.X / 16f);
                    int num28 = (int)((self.position.X + (float)self.width) / 16f);
                    int num29 = (int)((self.position.Y + (float)self.height + 1f) / 16f);
                    if (self.gravDir == -1f)
                        num29 = (int)((self.position.Y - 1f) / 16f);

                    for (int num30 = num112; num30 <= num28; num30++)
                    {
                        if (Main.tile[num30, num29] != null && Main.tile[num30, num29].IsActuated && (Main.tile[num30, num29].TileType == 189 || Main.tile[num30, num29].TileType == 196 || Main.tile[num30, num29].TileType == 460 || Main.tile[num30, num29].TileType == 666))
                        {
                            num26 = 0;
                            break;
                        }
                    }
                }
                if (self.stoned)
                {
                    int num31 = (int)(((float)num26 * self.gravDir - 2f) * 20f);
                    if (num31 > 0)
                    {
                        self.Hurt(PlayerDeathReason.ByOther(5), num31, 0);
                        self.immune = false;
                    }
                }
                else if (((self.gravDir == 1f && num26 > num25) || (self.gravDir == -1f && num26 < -num25)) && !self.noFallDmg && self.equippedWings == null)
                {
                    self.immune = false;
                    int num32 = (int)((float)num26 * self.gravDir - (float)num25) * 10;
                    if (self.mount.Active)
                    {
                        num32 = (int)((float)num32 * self.mount.FallDamage);
                    }
                    self.Hurt(PlayerDeathReason.ByOther(0), num32, 0);
                }
                self.fallStart = (int)(self.position.Y / 16f);
            }
            float num47 = (float)Main.maxTilesX / 4200f;
            float num58 = (float)((double)(self.position.Y / 16f - (60f + 10f * num47)) / (Main.worldSurface / 6.0));
            if (self.jump > 0 || self.rocketDelay > 0 || self.wet || self.slowFall || (double)num58 < 0.8 || self.tongued)
            {
                self.fallStart = (int)(self.position.Y / 16f);
            }

            if (Main.SceneMetrics.WaterCandleCount > 0)
            {
                self.AddBuff(86, 2, quiet: false);
            }
            if (Main.SceneMetrics.PeaceCandleCount > 0)
            {
                self.AddBuff(157, 2, quiet: false);
            }
            if (Main.SceneMetrics.ShadowCandleCount > 0)
            {
                self.AddBuff(350, 2, quiet: false);
            }
            if (Main.SceneMetrics.HasCampfire)
            {
                self.AddBuff(87, 2, quiet: false);
            }
            if (Main.SceneMetrics.HasCatBast)
            {
                self.AddBuff(215, 2, quiet: false);
            }
            if (Main.SceneMetrics.HasStarInBottle)
            {
                self.AddBuff(158, 2, quiet: false);
            }
            if (Main.SceneMetrics.HasHeartLantern)
            {
                self.AddBuff(89, 2, quiet: false);
            }
            if (Main.SceneMetrics.HasSunflower)
            {
                self.AddBuff(146, 2, quiet: false);
            }
            if (Main.SceneMetrics.hasBanner)
            {
                self.AddBuff(147, 2, quiet: false);
            }
            if (!self.behindBackWall && self.ZoneSandstorm)
            {
                self.AddBuff(194, 2, quiet: false);
            }

            Player clientPlayerTmp = Main.clientPlayer;

            Main.clientPlayer = self;
            Main.myPlayer = self.whoAmI;

            self.UpdateBuffs(i);

            Main.clientPlayer = clientPlayerTmp;
            Main.myPlayer = Main.clientPlayer.whoAmI;


            if (self.stoned != self.lastStoned)
            {
                int damage = (int)(20.0 * (double)Main.GameModeInfo.EnemyDamageMultiplier);
                self.Hurt(PlayerDeathReason.ByOther(5), damage, 0);
            }
            if (!self.onFire && !self.poisoned)
            {
                self.trapDebuffSource = false;
            }
            self.UpdatePet(i);
            self.UpdatePetLight(i);
            self.isOperatingAnotherEntity = self.ownedProjectileCounts[1020] > 0;

            int maxBuffs = (int)typeof(Player).GetProperty("maxBuffs", BindingFlags.FlattenHierarchy | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            for (int num34 = 0; num34 < maxBuffs; num34++)
            {
                if (self.buffType[num34] > 0 && self.buffTime[num34] <= 0)
                {
                    self.DelBuff(num34);
                }
            }

            bool flag16 = false;
            if (Main.wofNPCIndex >= 0)
            {
                NPC nPC = Main.npc[Main.wofNPCIndex];
                float num83 = nPC.Center.X + (float)(nPC.direction * 200 - self.Center.X);
                float num84 = nPC.Center.Y - self.Center.Y;
                float num85 = (float)Math.Sqrt(num83 * num83 + num84 * num84);
                float num86 = 11f;

                if (Main.expertMode)
                {
                    float value = 22f;
                    float amount = Math.Min(1f, nPC.velocity.Length() / 5f);
                    num86 = MathHelper.Lerp(num86, value, amount);
                }
                if (num85 <= num86)
                    flag16 = true;
            }
            else
                flag16 = true;

            if (flag16)
            {
                for (int num88 = 0; num88 < maxBuffs; num88++)
                {
                    if (self.buffType[num88] == 38)
                    {
                        self.DelBuff(num88);
                    }
                }
            }

            self.WOFTongue();
            if (self.controlHook)
            {
                if (self.releaseHook)
                {
                    self.QuickGrapple();
                }
                self.releaseHook = false;
            }
            else
            {
                self.releaseHook = true;
            }
            if (self.mount.Active && self.mount.Cart && ((Vector2)(self.velocity)).Length() > 4f)
            {
                Rectangle rectangle2 = new Rectangle((int)self.position.X, (int)self.position.Y, self.width, self.height);
                if (self.velocity.X < -1f)
                    rectangle2.X -= 15;

                if (self.velocity.X > 1f)
                    rectangle2.Width += 15;

                if (self.velocity.X < -10f)
                    rectangle2.X -= 10;

                if (self.velocity.X > 10f)
                    rectangle2.Width += 10;

                if (self.velocity.Y < -1f)
                    rectangle2.Y -= 10;

                if (self.velocity.Y > 1f)
                    rectangle2.Height += 10;

                for (int num89 = 0; num89 < 200; num89++)
                {
                    if (Main.npc[num89].active && !Main.npc[num89].dontTakeDamage && !Main.npc[num89].friendly && Main.npc[num89].immune[i] == 0 && self.CanNPCBeHitByPlayerOrPlayerProjectile(Main.npc[num89]) && ((Rectangle)(rectangle2)).Intersects(new Rectangle((int)Main.npc[num89].position.X, (int)Main.npc[num89].position.Y, Main.npc[num89].width, Main.npc[num89].height)))
                    {
                        float num91 = ((List<DamageClass>)typeof(DamageClassLoader).GetField("DamageClasses", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Select((DamageClass t) => self.GetTotalCritChance(t)).Max();

                        bool crit = false;
                        if ((float)Main.rand.Next(1, 101) <= num91)
                        {
                            crit = true;
                        }
                        float currentSpeed = ((Vector2)(self.velocity)).Length() / self.maxRunSpeed;
                        int damage2 = 0;
                        float knockback = 0;
                        ParameterModifier pMod = new ParameterModifier(3);
                        pMod[1] = true;
                        pMod[2] = true;

                        typeof(Player).InvokeMember("ApplyTouchDamage", BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic, null, self, new object[] { currentSpeed, /*out*/
                                                                                                                                                                                                     damage2, /*out*/ knockback });
                        int num92 = 1;
                        if (self.velocity.X < 0f)
                        {
                            num92 = -1;
                        }
                        if (Main.npc[num89].knockBackResist < 1f && Main.npc[num89].knockBackResist > 0f)
                        {
                            knockback /= Main.npc[num89].knockBackResist;
                        }
                        self.ApplyDamageToNPC(Main.npc[num89], damage2, knockback, num92, crit);
                        Main.npc[num89].immune[i] = 30;
                    }
                }
            }
            self.Update_NPCCollision();
            if (!self.shimmering)
            {
                Collision.HurtTile hurtTile = (Collision.HurtTile)typeof(Player).InvokeMember("GetHurtTile", BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic, null, self, null);
                if (hurtTile.type >= 0)
                {
                    typeof(Player).InvokeMember("ApplyTouchDamage", BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic, null, self, new object[] { hurtTile.type, hurtTile.x, hurtTile.y });
                }
            }
            typeof(Player).InvokeMember("TryToShimmerUnstuck", BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic, null, self, null);

            int num93 = self.height;
            if (self.waterWalk)
                num93 -= 6;

            bool flag18 = false;
            if (!self.shimmering)
                flag18 = Collision.LavaCollision(self.position, self.width, num93);

            if (flag18)
            {
                if (!self.lavaImmune && self.hurtCooldowns[4] <= 0)
                {
                    if (self.lavaTime > 0)
                    {
                        self.lavaTime--;
                    }
                    else
                    {
                        int num94 = 80;
                        int num95 = 420;
                        if (Main.remixWorld)
                        {
                            num94 = 200;
                            num95 = 630;
                        }
                        if (!self.ashWoodBonus || !self.lavaRose)
                        {
                            if (self.ashWoodBonus)
                            {
                                if (Main.remixWorld)
                                {
                                    num94 = 145;
                                }
                                num94 /= 2;
                                num95 -= 210;
                            }
                            if (self.lavaRose)
                            {
                                num94 -= 45;
                                num95 -= 210;
                            }
                            if (num94 > 0)
                            {
                                self.Hurt(PlayerDeathReason.ByOther(2), num94, 0, pvp: false, quiet: false, 4); //Crit: false
                            }
                            if (num95 > 0)
                            {
                                self.AddBuff(24, num95);
                            }
                        }
                    }
                }
                self.lavaWet = true;
            }
            if (Collision.shimmer)
            {
                self.shimmerWet = true;
                bool shimmerImmune = (bool)typeof(Player).GetField("shimmerImmune", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self);
                if (!shimmerImmune && !self.shimmerUnstuckHelper.ShouldUnstuck)
                {
                    int num96 = (int)(self.Center.X / 16f);
                    int num97 = (int)((self.position.Y + 1f) / 16f);

                    if (Main.tile[num96, num97] != null && (bool)typeof(Tile).InvokeMember("shimmer", BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic, null, Main.tile[num96, num97], null) && Main.tile[num96, num97].LiquidAmount >= 0 && self.position.Y / 16f < (float)Main.UnderworldLayer)
                        self.AddBuff(353, 60);
                }
            }

        }
    }
    public class FakePlayerItem : ModItem
    {
        public static bool godMode = false;
        static List<Player> fakePlayer = new List<Player>();
        public override void SetDefaults()
        {
            Item.useTime = 1;
            Item.useAnimation = 1;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.noMelee = true;
            Item.autoReuse = true;
        }
        public static Player? Create() //Could throw instead
        {
            int index = Array.FindIndex<Terraria.Player>(Main.player, playerFromArray => playerFromArray.active == false);
            if (index == -1)
                return null;

            Player player = new Player();

            if (UIPlayerPanel.selectedUIPlayerPanel.player != null)
            {
                player.CopyVisuals(UIPlayerPanel.selectedUIPlayerPanel.player);
                player.name = UIPlayerPanel.selectedUIPlayerPanel.player.name;
            }
            else
                player.name = "Error";
            if(godMode)
                player.GetModPlayer<FakePlayer>().isFakePlayerKillable = true;
            player.active = true;
            player.immune = true;
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
                    if (currentFakePlayer.active)
                    {
#if DEBUG
                        Main.NewText("Touching Fake Player");
#endif
                        return currentFakePlayer;

                    }
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
                fakePlayerUISystem.userInterface.SetState(fakePlayerUISystem.userInterface.CurrentState == null ? fakePlayerUISystem.uiPlayerList : null);
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
            Vector2 position;
            if (selectedPlayer == null)
            {
                selectedPlayer = Create();
                if (selectedPlayer == null)
                {
                    Main.NewText("All player slots are maxed out sorry...");
                    return null;
                }
                position = Main.MouseWorld - selectedPlayer.Size / new Vector2(2, 1) + player.velocity;
                selectedPlayer.SpawnX = (int)position.X / 16;
                selectedPlayer.SpawnY = (int)position.Y / 16;
            }
            position = Main.MouseWorld - selectedPlayer.Size / new Vector2(2, 1) + player.velocity;
            selectedPlayer.velocity = new Vector2(0);
            selectedPlayer.position = position;
            selectedPlayer.fallStart = (int)(selectedPlayer.position.Y / 16f);
            selectedPlayer.fallStart2 = (int)(selectedPlayer.position.Y / 16f);
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
        public UIPlayerCustomization uIPlayerCustomization;
        internal GameTime? lastUpdateUIGameTime;
        bool firstTime = true;
        public override void UpdateUI(GameTime gameTime)
        {
            if ((firstTime && !Main.dedServ) || false) //if we are here surely the players are loaded... Right?
            {
                //uIPlayerCustomization = new UIPlayerCustomization();
                //uIPlayerCustomization.Activate();

                uiPlayerList = new UIPlayerList();
                userInterface.SetState(uiPlayerList);
                uiPlayerList.Activate();
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
            uICharacter.OnLeftDoubleClick += DoubleClick;
            Append(uICharacter);

            UIText uIPlayerName = new UIText(playerFileData.Name);
            uIPlayerName.Left.Set(uICharacter.Width.Pixels, 0f);
            uIPlayerName.OnLeftDoubleClick += DoubleClick;
            Append(uIPlayerName);

            Height.Set(uICharacter.Height.Pixels + 25f, 0f);
            Width.Set(0f, 1f);
            base.OnLeftDoubleClick += DoubleClick;
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
                Main.mouseLeft = false;
                Main.mouseLeftRelease = false;
                Main.mouseRight = false;
            Main.mouseRightRelease = false;
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
    /* Plan:
     * Velocity slider's
     * Position changer
     * Team selector's
     * In game player creator
     * Actor and action repeater
     * Mod intergration with other mods
     * Programmable actions in linked blocks?
     */
    /*
     * On death do this:
     * respawn on spawn point
     * don't respawn
     * don't die
     * set spawn location
     */

    public class UIPlayerCustomization : UIState
    {
        public class UISliderBase : UIElement
        {
            internal const int UsageLevel_NotSelected = 0;

            internal const int UsageLevel_SelectedAndLocked = 1;

            internal const int UsageLevel_OtherElementIsLocked = 2;

            internal static UIElement CurrentLockedSlider;

            internal static UIElement CurrentAimedSlider;

            internal int GetUsageLevel()
            {
                int result = 0;
                if (CurrentLockedSlider == this)
                {
                    result = 1;
                }
                else if (CurrentLockedSlider != null)
                {
                    result = 2;
                }

                return result;
            }

            public static void EscapeElements()
            {
                CurrentLockedSlider = null;
                CurrentAimedSlider = null;
            }
        }

        public class UIColoredSlidera : UISliderBase
        {
            private Color _color;

            private LocalizedText _textKey;

            private Func<float> _getStatusTextAct;

            private Action<float> _slideKeyboardAction;

            private Func<float, Color> _blipFunc;

            private Action _slideGamepadAction;

            private const bool BOTHER_WITH_TEXT = false;

            private bool _isReallyMouseOvered;

            private bool _alreadyHovered;

            private bool _soundedUsage;

            public UIColoredSlidera(LocalizedText textKey, Func<float> getStatus, Action<float> setStatusKeyboard, Action setStatusGamepad, Func<float, Color> blipColorFunction, Color color)
            {
                _color = color;
                _textKey = textKey;
                _getStatusTextAct = ((getStatus != null) ? getStatus : ((Func<float>)(() => 0f)));
                _slideKeyboardAction = ((setStatusKeyboard != null) ? setStatusKeyboard : ((Action<float>)delegate
                {
                }));
                _blipFunc = ((blipColorFunction != null) ? blipColorFunction : ((Func<float, Color>)((float s) => Color.Lerp(Color.Black, Color.White, s))));
                _slideGamepadAction = setStatusGamepad;
                _isReallyMouseOvered = false;
            }

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                UISliderBase.CurrentAimedSlider = null;
                if (!Main.mouseLeft)
                {
                    UISliderBase.CurrentLockedSlider = null;
                }

                int usageLevel = GetUsageLevel();

                base.DrawSelf(spriteBatch);
                CalculatedStyle dimensions = GetDimensions();
                Vector2 vector = new Vector2(dimensions.X, dimensions.Y);
                bool flag = false;
                bool flag2 = base.IsMouseHovering;
                if (usageLevel == 2)
                {
                    flag2 = false;
                }

                if (usageLevel == 1)
                {
                    flag2 = true;
                }

                Vector2 drawPosition = vector + new Vector2(0f, 0f);
                Color.Lerp(flag ? Color.Gold : (flag2 ? Color.White : Color.Silver), Color.White, flag2 ? 0.5f : 0f);

                TextureAssets.ColorBar.Frame();
                drawPosition = new Vector2(dimensions.X + dimensions.Width, dimensions.Y);
                bool wasInBar;
                float obj = DrawValueBar(spriteBatch, drawPosition, 1f, _getStatusTextAct(), usageLevel, out wasInBar, _blipFunc);
                if (UISliderBase.CurrentLockedSlider == this || wasInBar)
                {
                    UISliderBase.CurrentAimedSlider = this;
                    if (PlayerInput.Triggers.Current.MouseLeft && !PlayerInput.UsingGamepad && UISliderBase.CurrentLockedSlider == this)
                    {
                        _slideKeyboardAction(obj);
                        if (!_soundedUsage)
                        {
                            SoundEngine.PlaySound(SoundID.MenuTick);
                        }

                        _soundedUsage = true;
                    }
                    else
                    {
                        _soundedUsage = false;
                    }
                }

                if (UISliderBase.CurrentAimedSlider != null && UISliderBase.CurrentLockedSlider == null)
                {
                    UISliderBase.CurrentLockedSlider = UISliderBase.CurrentAimedSlider;
                }

                if (_isReallyMouseOvered)
                {
                    _slideGamepadAction();
                }
            }

            private float DrawValueBar(SpriteBatch sb, Vector2 drawPosition, float drawScale, float sliderPosition, int lockMode, out bool wasInBar, Func<float, Color> blipColorFunc)
            {
                Texture2D value = TextureAssets.ColorBar.Value;
                Vector2 vector = new Vector2(value.Width, value.Height) * drawScale;
                drawPosition.X -= (int)vector.X;
                Rectangle rectangle = new Rectangle((int)drawPosition.X, (int)drawPosition.Y - (int)vector.Y / 2, (int)vector.X, (int)vector.Y);
                Rectangle destinationRectangle = rectangle;
                sb.Draw(value, rectangle, Color.White);
                float num = (float)rectangle.X + 5f * drawScale;
                float num2 = (float)rectangle.Y + 4f * drawScale;
                for (float num3 = 0f; num3 < 167f; num3 += 1f)
                {
                    float arg = num3 / 167f;
                    Color color = blipColorFunc(arg);
                    sb.Draw(TextureAssets.ColorBlip.Value, new Vector2(num + num3 * drawScale, num2), null, color, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);
                }

                rectangle.X = (int)num;
                rectangle.Y = (int)num2;
                bool flag = (_isReallyMouseOvered = rectangle.Contains(new Point(Main.mouseX, Main.mouseY)));
                if (IgnoresMouseInteraction)
                {
                    flag = false;
                }

                if (lockMode == 2)
                {
                    flag = false;
                }

                if (flag || lockMode == 1)
                {
                    sb.Draw(TextureAssets.ColorHighlight.Value, destinationRectangle, Main.OurFavoriteColor);
                    if (!_alreadyHovered)
                    {
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }

                    _alreadyHovered = true;
                }
                else
                {
                    _alreadyHovered = false;
                }

                wasInBar = false;
                if (!IgnoresMouseInteraction)
                {
                    sb.Draw(TextureAssets.ColorSlider.Value, new Vector2(num + 167f * drawScale * sliderPosition, num2 + 4f * drawScale), null, Color.White, 0f, new Vector2(0.5f * (float)TextureAssets.ColorSlider.Value.Width, 0.5f * (float)TextureAssets.ColorSlider.Value.Height), drawScale, SpriteEffects.None, 0f);
                    //sb.Draw(TextureAssets.ColorSlider.Value, new Vector2(num + 167f * drawScale * sliderPosition, num2 + 4f * drawScale), null, Color.White, 0f, new Vector2(0.5f * (float)TextureAssets.ColorSlider.Value.Width, 0.5f * (float)TextureAssets.ColorSlider.Value.Height), drawScale, SpriteEffects.None, 0f);
                    if (Main.mouseX >= rectangle.X && Main.mouseX <= rectangle.X + rectangle.Width)
                    {
                        wasInBar = flag;
                        return (float)(Main.mouseX - rectangle.X) / (float)rectangle.Width;
                    }
                }

                if (rectangle.X >= Main.mouseX)
                {
                    return 0f;
                }

                return 1f;
            }
        }
        public class UIVelocity : UIElement
        {
            public UIText uIName;

            public UIColoredSlidera uISliderX;
            public UIElement uISliderXElement;
            public UIText uISliderXText;
            public float uISliderXStatus = 0.5f;

            public UIColoredSlidera uISliderY;
            public UIElement uISliderYElement;
            public UIText uISliderYText;
            public float uISliderYStatus = 0.5f;
            UIColoredSlidera UISlider(ref float status)
            {
                float status_2 = status;
                UIColoredSlidera uIColoredSlider = new UIColoredSlidera(LocalizedText.Empty, () => status_2, x => status_2 = x, () => { }, null, Color.Transparent);

                status = status_2;

                return uIColoredSlider;
            }

            public UIVelocity()
            {
                UICenter uICenter = new UICenter();

                uIName = new UIText("Velocity");
                Vector2 textSize = (Vector2)typeof(UIText).GetField("_textSize", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(uIName); //Magic to get the uitextbox size

                uIName.Width.Set(textSize.X, uIName.Left.Percent);
                uIName.Height.Set(textSize.Y, uIName.Top.Percent);

                uICenter.Append(uIName);

                uISliderXText = new UIText("X");
                textSize = (Vector2)typeof(UIText).GetField("_textSize", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(uISliderXText); //Magic to get the uitextbox size
                uISliderXText.Width.Set(textSize.X, 0f);
                uISliderXText.Height.Set(textSize.Y, 0f);
                uISliderXText.Top.Pixels = -4f;

                uISliderX = UISlider(ref uISliderXStatus);
                uISliderX.Width.Set(TextureAssets.ColorBar.Value.Width, 0f);
                uISliderX.Height.Set(TextureAssets.ColorBar.Value.Height + 12f, 0f);
                uISliderX.MarginTop = uISliderX.MarginBottom = 2f;
                //uISliderX.Left.Set(-uISliderX.Width.Pixels / 2, 0.5f);
                uISliderXElement = new();

                UILeft panel = new UILeft();

                //panel.Width.Set(uISliderXText.Width.Pixels + uISliderX.Width.Pixels, uISliderXText.Width.Percent + uISliderX.Width.Percent);
                //UIPanel
                panel.Append(uISliderXText);
                panel.Append(uISliderX);
                UIText uITextBox = new UIText("0.1f", 0.75f);
                //textSize = (Vector2)typeof(UIText).GetField("_textSize", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(uITextBox); //Magic to get the uitextbox size
                //uITextBox.Width.Set(textSize.X, 0f);
                //uITextBox.Height.Set(textSize.Y, 0f);

                //UIPanel uIPanel = new UIPanel();

                //uIPanel.Width.Set(uITextBox.Width.Pixels*2, uITextBox.Width.Percent);
                //uIPanel.Height.Set(uITextBox.Height.Pixels*2, uITextBox.Height.Percent);
                //uIPanel.Top.Set(0f, -0.28f);

                //uIPanel.Append(uITextBox);
                panel.Append(uITextBox);

                uICenter.Append(panel);

                panel = new UILeft();

                uISliderY = UISlider(ref uISliderYStatus);
                uISliderY.Width.Set(TextureAssets.ColorBar.Value.Width, 0f);
                uISliderY.Height.Set(TextureAssets.ColorBar.Value.Height + 12f, 0f);
                uISliderY.MarginTop = uISliderY.MarginBottom = 2f;

                uISliderYText = new UIText("Y");
                textSize = (Vector2)typeof(UIText).GetField("_textSize", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(uISliderYText); //Magic to get the uitextbox size
                uISliderYText.Width.Set(textSize.X, 0f);
                uISliderYText.Height.Set(textSize.Y, 0f);
                uISliderYText.Top.Pixels = -4f;

                panel.Append(uISliderYText);
                panel.Append(uISliderY);
                uICenter.Append(panel);

                Append(uICenter);
            }
        };

        public class UILeft : UIElement
        {
            private StyleDimension left;
            public UILeft()
            {
                Width.Precent += 1f;
            }
            public new void Append(UIElement element)
            {
                base.Append(element);

                element.Left.Pixels += left.Pixels;

                left.Pixels += element.Width.Pixels;
                left.Percent += element.Width.Percent;

                this.Width.Pixels += left.Pixels;
                this.Width.Percent += left.Percent;

                foreach (UIElement element1 in Elements)
                {
                    float height = element1.Height.Percent * Main.screenHeight + element1.Height.Pixels;

                    if (height > this.Height.Precent * Main.screenHeight + this.Height.Pixels)
                    {
                        this.Height.Pixels += element1.Height.Pixels;
                        this.Height.Percent += element1.Height.Percent;
                    }
                }
            }

        }
        public class UICenter : UIElement
        {
            private StyleDimension bottom;
            public UICenter()
            {
                Width.Precent += 1f;
                Height.Percent += 1f;
            }
            public new void Append(UIElement element)
            {
                base.Append(element);

                element.Left.Pixels += -(element.Width.Pixels / 2);
                //element.Left.Percent += -(element.Width.Percent / 2);
                element.Left.Percent += 0.5f;

                bottom.Pixels += element.Height.Pixels;
                bottom.Percent += element.Height.Percent;

                element.Top.Set(bottom.Pixels, bottom.Percent);
            }

        }

        public class UIPosition
        {
            public UISliderBase uISliderX;
            public UISliderBase uISliderY;
        };
        public class UITeam
        {
            UIImageButton[] team = new UIImageButton[6];
        };

        public UITeam uITeam;
        public UIScrollbar uIScrollBar;
        public UIPanel uIPanel;
        public class PlayerGodMode : UIElement
        {
            GroupOptionButton<bool> groupOptionButton;
            public PlayerGodMode()
            {
                UIImageFramed uIImageFramed = CreativePowersHelper.GetIconImage(CreativePowersHelper.CreativePowerIconLocations.Godmode);

                uIImageFramed.Left.Set((-uIImageFramed.Width.Pixels + 2) / 2, 0.5f);

                Asset<Texture2D> powerIconAsset = Main.Assets.Request<Texture2D>("Images/UI/Creative/Infinite_Powers");
                uIImageFramed.Width.Pixels = powerIconAsset.Width() / CreativePowersHelper.TextureIconColumns + 2;
                uIImageFramed.Height.Pixels = powerIconAsset.Height() / CreativePowersHelper.TextureIconRows + 2;

                CreativePowerUIElementRequestInfo creativePowerUIElementRequestInfo = new CreativePowerUIElementRequestInfo();
                creativePowerUIElementRequestInfo.PreferredButtonWidth = powerIconAsset.Width() / CreativePowersHelper.TextureIconColumns + 2;
                creativePowerUIElementRequestInfo.PreferredButtonHeight = powerIconAsset.Height() / CreativePowersHelper.TextureIconRows + 2;

                groupOptionButton = CreativePowersHelper.CreateSimpleButton(creativePowerUIElementRequestInfo);
                groupOptionButton.Append(uIImageFramed);
                groupOptionButton.OnLeftClick += OnLeftClick;
                Append(groupOptionButton);
                Width = groupOptionButton.Width;
                Height = groupOptionButton.Height;
            }

            private void OnLeftClick(UIMouseEvent evt, UIElement listeningElement)
            {
                groupOptionButton.SetCurrentOption(!groupOptionButton.IsSelected);
                FakePlayerItem.godMode = !groupOptionButton.IsSelected;
            }
        }

        public UIPlayerCustomization()
        {
            uIPanel = new UIPanel();
            uIPanel.Width.Set(0f, 0.25f);
            uIPanel.Height.Set(0f, 0.5f);
            uIPanel.Left.Set(0f, 0.5f - uIPanel.Width.Precent / 2);
            uIPanel.Top.Set(0f, 0.5f - uIPanel.Height.Precent / 2);
            //uIPanel.Append(new UIText("hello world"));

            UIVelocity uIVelocity = new UIVelocity();
            uIVelocity.Width.Set(0f, 1f);
            uIVelocity.Height.Set(0f, 1f);
            uIVelocity.Left.Set(0f, 0f);
            uIVelocity.Top.Set(0f, 0f);

            uIPanel.Append(uIVelocity);
            UIImageFramed uIImageFramed = CreativePowersHelper.GetIconImage(CreativePowersHelper.CreativePowerIconLocations.Godmode);

            uIImageFramed.Left.Set((-uIImageFramed.Width.Pixels + 2) / 2, 0.5f);

            Asset<Texture2D> powerIconAsset = Main.Assets.Request<Texture2D>("Images/UI/Creative/Infinite_Powers");
            uIImageFramed.Width.Pixels = powerIconAsset.Width() / CreativePowersHelper.TextureIconColumns + 2;
            uIImageFramed.Height.Pixels = powerIconAsset.Height() / CreativePowersHelper.TextureIconRows + 2;

            CreativePowerUIElementRequestInfo creativePowerUIElementRequestInfo = new CreativePowerUIElementRequestInfo();
            creativePowerUIElementRequestInfo.PreferredButtonWidth = powerIconAsset.Width() / CreativePowersHelper.TextureIconColumns + 2;
            creativePowerUIElementRequestInfo.PreferredButtonHeight = powerIconAsset.Height() / CreativePowersHelper.TextureIconRows +2;

            groupOptionButton = CreativePowersHelper.CreateSimpleButton(creativePowerUIElementRequestInfo);

            groupOptionButton.ShowHighlightWhenSelected = true;
            //groupOptionButton.SetCurrentOption(option: false);
            groupOptionButton.SetColorsBasedOnSelectionState(new Color(152, 175, 235), Colors.InventoryDefaultColor, 1f, 0.7f);
            groupOptionButton.Append(uIImageFramed);
            groupOptionButton.OnLeftClick += GroupOptionButton_OnLeftClick;

            uIPanel.Append(groupOptionButton);
            Append(uIPanel);
        }

        GroupOptionButton<bool> groupOptionButton;

        bool godmode_status = false;
        //Shouldn't be like this?
        private void GroupOptionButton_OnLeftClick(UIMouseEvent evt, UIElement listeningElement)
        {
            groupOptionButton.SetCurrentOption(!groupOptionButton.IsSelected);
        }
    }

    public class UIPlayerList : UIState
    {
        public UIPanel uIPlayersTextHolder;
        public UIList uIPlayerList;
        public UIScrollbar uIScrollbar;
        public UIText uIPlayersText;
        public UIPanel uIPlayerListPanel;
        public UIPlayerCustomization.PlayerGodMode playerGodMode;
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

            UIPanel uIPanelOptions = new UIPanel();


            playerGodMode = new UIPlayerCustomization.PlayerGodMode();
            //playerGodMode.Left.Set(uICharacter.Width.Pixels, 0f);
            //Vector2 textSize = (Vector2)typeof(UIText).GetField("_textSize", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(uIPlayerName); //Magic to get the uitextbox size
            //playerGodMode.Top.Set(textSize.Y + 4f, 0f);
            uIPanelOptions.Width.Set(-uIScrollbar.Width.Pixels, 1f);
            uIPanelOptions.Height = playerGodMode.Height;
            uIPanelOptions.Height.Pixels += 24;
            uIPanelOptions.Append(playerGodMode);
            



            uIPlayerList = new UIList();
            uIPlayerList.Height.Set(0f, 1f);
            uIPlayerList.Width.Set(-uIScrollbar.Width.Pixels, 1f);
            uIPlayerList.SetScrollbar(uIScrollbar);

            UIElement dummyUIElement = new UIElement();
            dummyUIElement.Width.Set(-uIScrollbar.Width.Pixels, 1f);
            dummyUIElement.Height = uIPanelOptions.Height;

            uIPlayerList.Add(dummyUIElement);

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
            uIPlayerListPanel.Append(uIPanelOptions);

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