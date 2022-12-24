#nullable enable
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FakePlayer
{
    public class FakePlayerItem : ModItem
    {
        static Player? player;
        public override void SetDefaults()
        {
            Item.useTime = 0;
            Item.useAnimation = 0;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.noMelee = true;
            Item.autoReuse = true;
        }
        Player Create()
        {
            int index = Array.FindIndex<Terraria.Player>(Main.player, playerFromArray => playerFromArray.active == false);
            if (index == -1)
                throw new Exception("Couldn't find empty player slot");

            Player player = new Player();
            player.active = true;
            player.name = "Player Dummy";
            player.whoAmI = index;
            Main.player[index] = player;
            return player;
        }
        //Theres gotta be better than this
        void Destroy(ref Player? player)
        {
            if (player == null)
                return;

            player.active = false;
            player = null;
            return;
        }
        public override bool? UseItem(Player player)
        {
            if (FakePlayerItem.player == null || !FakePlayerItem.player.active)
                FakePlayerItem.player = Create();

            FakePlayerItem.player.velocity = new Vector2(0);
            FakePlayerItem.player.position = Main.MouseWorld - FakePlayerItem.player.Size / new Vector2(2, 1) + player.velocity;

            return null;
        }
        public override bool AltFunctionUse(Player player)
        {
            Destroy(ref FakePlayerItem.player);
            return false;
        }
    }
}