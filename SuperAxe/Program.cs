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
    using Ensage.SDK.Input;
    using Ensage.SDK.Orbwalker;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;
    using Ensage.SDK.TargetSelector;
    using Ensage.SDK.Helpers;

    using SharpDX;

    [ExportPlugin("SuperAxe!", HeroId.npc_dota_hero_axe)]
    public class Program : Plugin
    {
        private Unit MyHero;

        private readonly Lazy<IInputManager> Input;

        private readonly Lazy<IOrbwalkerManager> OrbwalkerManager;

        private readonly Lazy<ITargetSelectorManager> TargetSelector;

        public SuperAxe OrbwalkerMode { get; private set; }

        public Config Config { get; private set; }

        [ImportingConstructor]
        public Program(
            [Import] Lazy<IServiceContext> context,
            [Import] Lazy<IInputManager> input,
            [Import] Lazy<IOrbwalkerManager> orbwalkerManager,
            [Import] Lazy<ITargetSelectorManager> targetSelector)
        {
            this.MyHero = context.Value.Owner as Hero;
            this.Input = input;
            this.OrbwalkerManager = orbwalkerManager;
            this.TargetSelector = targetSelector;
        }

        protected override void OnActivate()
        {
            this.Config = new Config();
            this.Config.Key.Item.ValueChanged += HotkeyChanged;

            this.OrbwalkerMode = new SuperAxe(
                KeyInterop.KeyFromVirtualKey((int)this.Config.Key.Value.Key),
                this.Config,
                this.OrbwalkerManager,
                this.Input,
                this.TargetSelector);

            this.OrbwalkerManager.Value.RegisterMode(this.OrbwalkerMode);

            Drawing.OnDraw += this.Drawing_OnDraw;
        }

        protected override void OnDeactivate()
        {
            this.OrbwalkerManager.Value.UnregisterMode(this.OrbwalkerMode);

            this.Config.Key.Item.ValueChanged -= HotkeyChanged;
            this.Config.Dispose();

            Drawing.OnDraw -= this.Drawing_OnDraw;
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (!this.Config.Enabled)
                return;

            var enemies = EntityManager<Hero>.Entities
                .Where(x => this.MyHero.Team != x.Team && x.IsValid && !x.IsIllusion && x.IsAlive && x.IsVisible)
                .ToList();

            if (enemies == null)
                return;

            var threshold = this.MyHero.Spellbook.SpellR.GetAbilityData("kill_threshold");

            if (threshold > 0)
            {
                foreach (var enemy in enemies)
                {
                    var tmp = enemy.Health < threshold ? enemy.Health : threshold;
                    var perc = (float)tmp / (float)enemy.MaximumHealth;
                    var pos = HUDInfo.GetHPbarPosition(enemy) + 2;
                    var size = new Vector2(HUDInfo.GetHPBarSizeX(enemy) - 6, HUDInfo.GetHpBarSizeY(enemy) - 2);

                    Drawing.DrawRect(pos, new Vector2(size.X * perc, size.Y), Color.Chocolate);
                }
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
            this.OrbwalkerMode.Key = key;
        }
    }
}