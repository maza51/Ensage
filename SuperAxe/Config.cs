namespace SuperAxe
{
    using System.Collections.Generic;
    
    using Ensage.Common.Menu;
    using Ensage.SDK.Menu;

    public class Config
    {
        public MenuFactory Menu { get; }

        public MenuItem<bool> Enabled { get; }
        public MenuItem<KeyBind> Key { get; }
        public MenuItem<AbilityToggler> UseItemsInit { get; }
        public MenuItem<bool> EnabledForcePlusBlink { get; }
        public MenuItem<AbilityToggler> UseItems { get; }
        public MenuItem<bool> EnabledKillSteal { get; }

        public Dictionary<string, bool> Items = new Dictionary<string, bool>
        {
            { "item_pipe", true },
            { "item_crimson_guard", true },
            { "item_blade_mail", true },
            { "item_lotus_orb", true },
            { "item_mjollnir", true },
            { "item_black_king_bar", false },
            { "item_shivas_guard", true },
            { "item_dagon", true }
        };

        public Dictionary<string, bool> ItemsInitiation = new Dictionary<string, bool>
        {
            { "item_blink", true },
            { "item_force_staff", true }
        };

        public Config()
        {
            Menu = MenuFactory.Create("SuperAxe!");
            Enabled = Menu.Item("Enabled", true);
            Key = Menu.Item("Combo Key", new KeyBind(32));
            UseItemsInit = Menu.Item("Items For Initiation", new AbilityToggler(ItemsInitiation));
            EnabledForcePlusBlink = Menu.Item("Enable Force + Blink Together", true);
            UseItems = Menu.Item("Use Items In Call", new AbilityToggler(Items));
            EnabledKillSteal = Menu.Item("Enable KillSteal", true);

            Menu.Target.TextureName = "npc_dota_hero_axe";
            Menu.Target.ShowTextWithTexture = true;
        }

        public void Dispose()
        {
            Menu?.Dispose();
        }
    }
}