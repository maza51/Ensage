namespace SuperAxe
{
    using System;
    using System.Linq;
    using System.ComponentModel.Composition;
    using System.Windows.Input;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Extensions;
    using Ensage.Common.Menu;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;
    using Ensage.SDK.Helpers;

    using SharpDX;

    [ExportPlugin("SuperAxe!", HeroId.npc_dota_hero_axe)]
    public class Program : Plugin
    {
        private readonly Unit myHero;

        public SuperAxe OrbwalkerMode { get; private set; }

        public Config Config { get; private set; }

        public IServiceContext Context { get; private set; }

        [ImportingConstructor]
        public Program(
            [Import] IServiceContext context)
        {
            myHero = context.Owner as Hero;
            Context = context;
        }

        protected override void OnActivate()
        {
            Config = new Config();
            Config.Key.Item.ValueChanged += HotkeyChanged;

            OrbwalkerMode = new SuperAxe(
                KeyInterop.KeyFromVirtualKey((int)Config.Key.Value.Key),
                Config,
                Context);

            Context.Orbwalker.RegisterMode(OrbwalkerMode);

            Drawing.OnDraw += Drawing_OnDraw;
        }

        protected override void OnDeactivate()
        {
            Context.Orbwalker.UnregisterMode(OrbwalkerMode);

            Config.Key.Item.ValueChanged -= HotkeyChanged;
            Config.Dispose();

            Drawing.OnDraw -= Drawing_OnDraw;
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (!Config.Enabled)
            {
                return;
            }

            var enemies = EntityManager<Hero>.Entities
                .Where(x => myHero.Team != x.Team && x.IsValid && !x.IsIllusion && x.IsAlive && x.IsVisible)
                .ToList();

            if (!enemies.Any())
            {
                return;
            }

            var threshold = OrbwalkerMode.R.GetAbilityData("kill_threshold");

            if (threshold <= 0)
            {
                return;
            }

            foreach (var enemy in enemies)
            {
                var tmp = enemy.Health < threshold ? enemy.Health : threshold;
                var perc = tmp / enemy.MaximumHealth;
                var pos = HUDInfo.GetHPbarPosition(enemy) + 2;
                var size = new Vector2(HUDInfo.GetHPBarSizeX(enemy) - 6, HUDInfo.GetHpBarSizeY(enemy) - 2);

                Drawing.DrawRect(pos, new Vector2(size.X * perc, size.Y), Color.Chocolate);
            }
        }

        private void HotkeyChanged(object sender, OnValueChangeEventArgs e)
        {
            var keyCode = e.GetNewValue<KeyBind>().Key;

            if (keyCode == e.GetOldValue<KeyBind>().Key)
            {
                return;
            }

            var key = KeyInterop.KeyFromVirtualKey((int)keyCode);
            OrbwalkerMode.Key = key;
        }
    }
}