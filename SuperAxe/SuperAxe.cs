namespace SuperAxe
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Input;
    using Ensage.SDK.Orbwalker;
    using Ensage.SDK.Orbwalker.Modes;
    using Ensage.SDK.TargetSelector;

    using AbilityId = Ensage.AbilityId;
    using UnitExtensions = Ensage.SDK.Extensions.UnitExtensions;
    using EntityExtensions = Ensage.SDK.Extensions.EntityExtensions;
    using Vector3Extensions = Ensage.SDK.Extensions.Vector3Extensions;

    public class SuperAxe : KeyPressOrbwalkingModeAsync
    {
        private Unit MyHero;

        private Ability UltAbility { get; set; }
        private Ability BattleAbility { get; set; }
        private Ability CallAbility { get; set; }

        public Config Config { get; }

        private Lazy<ITargetSelectorManager> TargetSelector { get; }

        private static readonly string[] CuntCullModifiers =
        {
            "modifier_obsidian_destroyer_astral_imprisonment_prison",
            "modifier_puck_phase_shift",
            "modifier_shadow_demon_disruption",
            "modifier_tusk_snowball_movement"
        };

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

            base.OnActivate();
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!this.Config.Enabled)
                return;

            var target = this.TargetSelector.Value.Active.GetTargets().FirstOrDefault();

            if (target == null)
            {
                this.Orbwalker.OrbwalkTo(target);
                return;
            }

            if (this.CallAbility.CanBeCasted() && this.MyHero.CanCast())
            {
                var posForHitChance = target.BasePredict(450 + Game.Ping);
                var distanceToHitChance = EntityExtensions.Distance2D(this.MyHero, posForHitChance);
                var blink = this.MyHero.GetItemById(AbilityId.item_blink);
                var force = this.MyHero.GetItemById(AbilityId.item_force_staff);

                if (blink != null && blink.CanBeCasted() && this.Config.UseItemsInit.Value.IsEnabled(blink.Name))
                {
                    if (distanceToHitChance < 1200 && !this.CallAbility.CanHit(target))
                    {
                        blink.UseAbility(posForHitChance);
                        await Task.Delay(10, token);
                    }
                }

                if (force != null && force.CanBeCasted() && this.Config.UseItemsInit.Value.IsEnabled(force.Name))
                {
                    if (distanceToHitChance < 750 && !this.CallAbility.CanHit(target))
                    {
                        var posForTurn = this.MyHero.Position.Extend(posForHitChance, 70);

                        this.Orbwalker.Move(posForTurn);

                        await Task.Delay((int)(this.MyHero.GetTurnTime(posForTurn) * 1000 + Game.Ping + 200), token);

                        if (Vector3Extensions.Distance(UnitExtensions.InFront(this.MyHero, 600), posForHitChance) < 260)
                        {
                            force.UseAbility(this.MyHero);
                            await Task.Delay(10, token);
                        }
                    }
                }

                await Kill();

                if (this.CallAbility.CanHit(target) && !target.HasModifiers(CuntCullModifiers, false))
                {
                    this.CallAbility.UseAbility();
                    await Task.Delay((int)(this.CallAbility.FindCastPoint() * 1000 + Game.Ping), token);
                }

                this.Orbwalker.Move(posForHitChance);
            }
            else
            {
                this.Orbwalker.OrbwalkTo(target);
            }

            await UseItems(target, token);
        }

        private async void Loop()
        {
            while (this.IsActive)
            {
                if (!this.Config.Enabled)
                {
                    return;
                }

                if (this.Config.EnabledKillSteal && (!this.CanExecute || !this.CallAbility.CanBeCasted()))
                {
                    await Kill();
                }

                if (this.CanExecute)
                {
                    await AntiFail();
                }

                await Task.Delay(100);
            }
        }

        public async Task Kill(CancellationToken token = new CancellationToken())
        {
            var enemies = EntityManager<Hero>.Entities
                .Where(x => this.MyHero.Team != x.Team && x.IsValid && !x.IsIllusion && x.IsAlive && x.Distance2D(this.MyHero) < 400)
                .OrderBy(e => e.Distance2D(this.MyHero))
                .ToList();

            if (enemies == null)
                return;

            var threshold = this.UltAbility.GetAbilityData("kill_threshold");

            foreach (var enemy in enemies)
            {
                if (enemy.Modifiers.Where(m => m.Name == "modifier_skeleton_king_reincarnation_scepter_active").Any())
                {
                    continue;
                }

                if (enemy.Health + (enemy.HealthRegeneration / 2) >= threshold)
                {
                    continue;
                }

                if (UnitExtensions.IsLinkensProtected(enemy))
                {
                    continue;
                }

                if (this.UltAbility.CanBeCasted(enemy) && this.MyHero.CanCast())
                {
                    this.UltAbility.UseAbility(enemy);

                    await Task.Delay((int)(this.UltAbility.FindCastPoint() * 1000 + Game.Ping), token);
                }
            }
        }

        public async Task AntiFail(CancellationToken token = new CancellationToken())
        {
            if (this.CallAbility != null && this.CallAbility.IsInAbilityPhase)
            {
                var enemies = EntityManager<Hero>.Entities
                    .Where(x => this.MyHero.Team != x.Team && x.IsValid && !x.IsIllusion && x.IsAlive && this.MyHero.Distance2D(x) < 260 && !x.HasModifiers(CuntCullModifiers, false))
                    .Count();

                if (enemies == 0)
                {
                    this.MyHero.Stop();
                    await Task.Delay(10, token);
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

                var crimson = this.MyHero.GetItemById(AbilityId.item_crimson_guard);
                if (crimson != null && crimson.CanBeCasted() && this.Config.UseItems.Value.IsEnabled(crimson.Name))
                {
                    crimson.UseAbility();
                    await Task.Delay(10, token);
                }

                var pipe = this.MyHero.GetItemById(AbilityId.item_pipe);
                if (pipe != null && pipe.CanBeCasted() && this.Config.UseItems.Value.IsEnabled(pipe.Name))
                {
                    pipe.UseAbility();
                    await Task.Delay(10, token);
                }

                var dagon = MyHero.GetDagon();
                if (dagon != null && dagon.CanBeCasted() && this.Config.UseItems.Value.IsEnabled("item_dagon") && !UnitExtensions.IsMagicImmune(target) && !UnitExtensions.IsLinkensProtected(target))
                {
                    dagon.UseAbility(target);
                    await Task.Delay(10, token);
                }

                if (this.BattleAbility != null && this.BattleAbility.CanBeCasted() && this.MyHero.CanCast() && !UnitExtensions.IsMagicImmune(target))
                {
                    if (this.BattleAbility.ManaCost + this.UltAbility.ManaCost < this.MyHero.Mana)
                    {
                        this.BattleAbility.UseAbility(target);
                        await Task.Delay((int)(this.BattleAbility.FindCastPoint() * 1000 + Game.Ping), token);
                    }
                }
            }
        }
    }
}