namespace SuperAxe
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Extensions;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Input;
    using Ensage.SDK.Orbwalker;
    using Ensage.SDK.Orbwalker.Modes;
    using Ensage.SDK.TargetSelector;

    using SharpDX;

    using AbilityId = Ensage.AbilityId;
    using UnitExtensions = Ensage.SDK.Extensions.UnitExtensions;
    using EntityExtensions = Ensage.SDK.Extensions.EntityExtensions;
    using Vector3Extensions = Ensage.SDK.Extensions.Vector3Extensions;

    public class SuperAxe : KeyPressOrbwalkingModeAsync
    {
        private Unit MyHero;
        private Unit Target;

        private Ability UltAbility { get; set; }
        private Ability BattleAbility { get; set; }
        private Ability CallAbility { get; set; }

        public Config Config { get; }

        private Lazy<ITargetSelectorManager> TargetSelector { get; }

        public SuperAxe(Key key, Config config, Lazy<IOrbwalkerManager> orbwalker, Lazy<IInputManager> input, Lazy<ITargetSelectorManager> targetSelector)
            : base(orbwalker.Value, input.Value, key)
        {
            this.Config = config;
            this.TargetSelector = targetSelector;
        }

        protected override void OnActivate()
        {
            this.MyHero = Owner as Hero;
            this.UltAbility = this.MyHero.Spellbook.SpellR;
            this.BattleAbility = this.MyHero.Spellbook.SpellW;
            this.CallAbility = this.MyHero.Spellbook.SpellQ;

            UpdateManager.BeginInvoke(this.Loop);

            Drawing.OnDraw += this.Drawing_OnDraw;

            base.OnActivate();
        }

        protected override void OnDeactivate()
        {
            Drawing.OnDraw -= this.Drawing_OnDraw;

            base.OnDeactivate();
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (!this.Config.Enabled)
                return;

            var enemies = ObjectManager.GetEntities<Hero>()
                .Where(x => x.IsVisible && x.IsAlive && !x.IsIllusion && x.Team != this.MyHero.Team)
                .ToList();

            if (enemies == null)
                return;

            var threshold = this.UltAbility.GetAbilityData("kill_threshold");

            foreach (var enemy in enemies)
            {
                var tmp = enemy.Health < threshold ? enemy.Health : threshold;
                var perc = (float)tmp / (float)enemy.MaximumHealth;
                var pos = HUDInfo.GetHPbarPosition(enemy) + 2;
                var size = new Vector2(HUDInfo.GetHPBarSizeX(enemy) - 6, HUDInfo.GetHpBarSizeY(enemy) - 2);

                Drawing.DrawRect(pos, new Vector2(size.X * perc, size.Y), Color.Chocolate);
            }
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!this.Config.Enabled)
                return;

            var target = this.TargetSelector.Value.Active.GetTargets().FirstOrDefault();

            if (this.CallAbility.CanBeCasted() && !UnitExtensions.IsSilenced(this.MyHero) && this.MyHero.IsAlive && target != null)
            {
                var blink = this.MyHero.GetItemById(AbilityId.item_blink);
                var forece = this.MyHero.GetItemById(AbilityId.item_force_staff);
                var rangeCallAbility = 300 + target.HullRadius;
                var delayCallAbility = this.CallAbility.FindCastPoint() * 1000 + Game.Ping;
                var posForHitChance = UnitExtensions.InFront(target, (target.IsMoving ? (target.MovementSpeed / 2) : 0));
                var distanceToHitChance = EntityExtensions.Distance2D(this.MyHero, posForHitChance);

                // Prediction? no, have not heard..
                if (distanceToHitChance < rangeCallAbility)
                {
                    this.CallAbility.UseAbility();
                    await Task.Delay((int)delayCallAbility, token);
                }
                else if (distanceToHitChance < 1200 && blink != null && blink.CanBeCasted() && this.Config.UseItemsInit.Value.IsEnabled(blink.Name))
                {
                    blink.UseAbility(posForHitChance);
                    await Task.Delay(10, token);
                }
                // 800?
                else if (distanceToHitChance < 800 && forece != null && forece.CanBeCasted() && this.Config.UseItemsInit.Value.IsEnabled(forece.Name))
                {
                    if (Vector3Extensions.Distance(UnitExtensions.InFront(this.MyHero, 800), posForHitChance) < rangeCallAbility)
                    {
                        forece.UseAbility(this.MyHero);
                        await Task.Delay(10, token);
                    }
                    else
                    {
                        var posForTurn = this.MyHero.Position.Extend(posForHitChance, 70);

                        this.Orbwalker.Move(posForTurn);

                        await Task.Delay((int)(MyHero.GetTurnTime(posForHitChance) * 1000.0 + Game.Ping), token);
                    }
                }
                else
                {
                    this.Orbwalker.Move(posForHitChance);
                }
            }
            else
            {
                this.Orbwalker.OrbwalkTo(target);
            }

            await Kill(token);

            await UseItems(target, token);

            await Task.Delay(50, token);
        }

        private async void Loop()
        {
            while (this.IsActive)
            {
                if (!this.CanExecute && this.Config.EnabledKillingWithoutCombos && this.Config.Enabled)
                {
                    await Kill();
                }

                await Task.Delay(100);
            }
        }

        public async Task Kill(CancellationToken token = new CancellationToken())
        {
            var enemies = EntityManager<Hero>.Entities
                .Where(x => this.MyHero.Team != x.Team && x.IsValid && !x.IsIllusion && x.IsAlive)
                .OrderBy(e => e.Distance2D(MyHero))
                .ToList();

            if (enemies == null)
                return;

            var threshold = this.UltAbility.GetAbilityData("kill_threshold");

            foreach (var enemy in enemies)
            {
                if (enemy.Health + (enemy.HealthRegeneration / 2) <= threshold)
                {
                    if (!UnitExtensions.IsSilenced(this.MyHero) && this.UltAbility.CanBeCasted(enemy) && this.UltAbility.CanHit(enemy))
                    {
                        this.UltAbility.UseAbility(enemy);

                        // Can be made shorter than FindCastPoint
                        await Task.Delay(50, token);
                    }
                }
            }
        }

        public async Task UseItems(Unit target, CancellationToken token)
        {
            var called = EntityManager<Hero>.Entities
                .Where(x => this.MyHero.Team != x.Team && x.IsValid && !x.IsIllusion && x.IsAlive && x.Modifiers.Where(m => m.Name == "modifier_axe_berserkers_call").Any())
                .ToList();

            if (called.Any())
            {
                var bkb = this.MyHero.GetItemById(AbilityId.item_black_king_bar);
                if (bkb != null && bkb.CanBeCasted() && this.Config.UseItems.Value.IsEnabled(bkb.Name))
                {
                    bkb.UseAbility();
                    await Task.Delay(10, token);
                }

                var bladeMail = this.MyHero.GetItemById(AbilityId.item_blade_mail);
                if (bladeMail != null && bladeMail.CanBeCasted() && this.Config.UseItems.Value.IsEnabled(bladeMail.Name))
                {
                    bladeMail.UseAbility();
                    await Task.Delay(10, token);
                }

                var lotus = this.MyHero.GetItemById(AbilityId.item_lotus_orb);
                if (lotus != null && lotus.CanBeCasted() && this.Config.UseItems.Value.IsEnabled(lotus.Name))
                {
                    lotus.UseAbility(this.MyHero);
                    await Task.Delay(10, token);
                }

                var mjollnir = this.MyHero.GetItemById(AbilityId.item_mjollnir);
                if (mjollnir != null && mjollnir.CanBeCasted() && this.Config.UseItems.Value.IsEnabled(mjollnir.Name))
                {
                    mjollnir.UseAbility(this.MyHero);
                    await Task.Delay(10, token);
                }

                var shiva = this.MyHero.GetItemById(AbilityId.item_shivas_guard);
                if (shiva != null && shiva.CanBeCasted() && this.Config.UseItems.Value.IsEnabled(shiva.Name))
                {
                    shiva.UseAbility();
                    await Task.Delay(10, token);
                }

                var dagon = MyHero.GetDagon();
                if (dagon != null && dagon.CanBeCasted() && !UnitExtensions.IsMagicImmune(target) && this.Config.UseItems.Value.IsEnabled("item_dagon"))
                {
                    dagon.UseAbility(target);
                    await Task.Delay(10, token);
                }
            }
        }
    }
}