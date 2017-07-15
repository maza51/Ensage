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

    using SharpDX;

    using AbilityId = Ensage.AbilityId;
    using UnitExtensions = Ensage.SDK.Extensions.UnitExtensions;
    using EntityExtensions = Ensage.SDK.Extensions.EntityExtensions;
    using Vector3Extensions = Ensage.SDK.Extensions.Vector3Extensions;

    public class SuperAxe : KeyPressOrbwalkingModeAsync
    {

        public Ability R { get; }

        public Ability W { get; }

        public Ability Q { get; }

        private readonly Config config;

        private readonly Lazy<ITargetSelectorManager> targetSelector;

        private readonly string[] cuntCullModifiers =
        {
            "modifier_obsidian_destroyer_astral_imprisonment_prison",
            "modifier_puck_phase_shift",
            "modifier_shadow_demon_disruption",
            "modifier_tusk_snowball_movement",
            "modifier_riki_tricks_of_the_trade_phase"
        };

        public SuperAxe(Key key, Config config, Lazy<IOrbwalkerManager> orbwalker, Lazy<IInputManager> input, Lazy<ITargetSelectorManager> targetSelector)
            : base(orbwalker.Value, input.Value, key)
        {
            this.config = config;
            this.targetSelector = targetSelector;

            R = Owner.Spellbook.SpellR;
            W = Owner.Spellbook.SpellW;
            Q = Owner.Spellbook.SpellQ;
        }

        protected override void OnActivate()
        {
            UpdateManager.BeginInvoke(Loop);

            base.OnActivate();
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!config.Enabled)
            {
                return;
            }

            var target = targetSelector.Value.Active.GetTargets().FirstOrDefault();

            if (target == null)
            {
                Orbwalker.OrbwalkTo(null);
                return;
            }

            if (!Owner.CanCast())
            {
                return;
            }

            var posForHitChance = target.BasePredict(450 + Game.Ping);
            var distanceToHitChance = EntityExtensions.Distance2D(Owner, posForHitChance);
            var blink = Owner.GetItemById(AbilityId.item_blink);
            var force = Owner.GetItemById(AbilityId.item_force_staff);
            var blinkReady = blink != null && blink.CanBeCasted() && config.UseItemsInit.Value.IsEnabled(blink.Name);
            var forceReady = force != null && force.CanBeCasted() && config.UseItemsInit.Value.IsEnabled(force.Name);

            if (config.EnabledForcePlusBlink && forceReady && blinkReady)
            {
                if (distanceToHitChance < 1900 && distanceToHitChance > 1200 && Q.CanBeCasted())
                {
                    await TurnTo(posForHitChance, token);

                    if (Vector3Extensions.Distance(UnitExtensions.InFront(Owner, 600), posForHitChance) < 1200)
                    {
                        force.UseAbility(Owner);
                        await Task.Delay(10, token);
                    }
                }
            }

            if (blinkReady)
            {
                if (distanceToHitChance < 1200 && !Q.CanHit(target) && Q.CanBeCasted())
                {
                    blink.UseAbility(posForHitChance);
                    await Task.Delay(10, token);
                }
            }

            if (forceReady)
            {
                if (distanceToHitChance < 750 && !Q.CanHit(target) && Q.CanBeCasted())
                {
                    await TurnTo(posForHitChance, token);

                    if (Vector3Extensions.Distance(UnitExtensions.InFront(Owner, 600), posForHitChance) < 260)
                    {
                        force.UseAbility(Owner);
                        await Task.Delay(10, token);
                    }
                }
            }

            await Kill(token);
                
            if (Q.CanBeCasted())
            {
                if (Q.CanHit(target) && !target.HasModifiers(cuntCullModifiers, false))
                {
                    Q.UseAbility();
                    await Task.Delay((int)(Q.FindCastPoint() * 1000 + Game.Ping), token);
                }

                Orbwalker.Move(posForHitChance);
            }
            else
            {
                Orbwalker.OrbwalkTo(target);
            }

            await UseItems(target, token);
        }

        private async void Loop()
        {
            while (IsActive)
            {
                if (!config.Enabled)
                {
                    return;
                }

                if (config.EnabledKillSteal && (!CanExecute || !Q.CanBeCasted()))
                {
                    await Kill();
                }

                if (CanExecute)
                {
                    await AntiFail();
                }

                await Task.Delay(100);
            }
        }

        private async Task Kill(CancellationToken token = new CancellationToken())
        {
            var enemies = EntityManager<Hero>.Entities
                .Where(x => Owner.Team != x.Team && x.IsValid && !x.IsIllusion && x.IsAlive && x.Distance2D(Owner) < 400)
                .OrderBy(e => e.Distance2D(Owner))
                .ToList();

            if (!enemies.Any())
            {
                return;
            }

            if (!Owner.CanCast())
            {
                return;
            }

            var threshold = R.GetAbilityData("kill_threshold");

            foreach (var enemy in enemies)
            {
                if (enemy.Modifiers.Any(m => m.Name == "modifier_skeleton_king_reincarnation_scepter_active"))
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

                if (!R.CanBeCasted(enemy))
                {
                    continue;
                }

                R.UseAbility(enemy);
                await Task.Delay((int)(R.FindCastPoint() * 1000 + Game.Ping), token);
            }
        }

        private async Task AntiFail(CancellationToken token = new CancellationToken())
        {
            if (Q != null && Q.IsInAbilityPhase)
            {
                var enemies = EntityManager<Hero>.Entities
                    .Count(x => Owner.Team != x.Team && x.IsValid && !x.IsIllusion && x.IsAlive && Owner.Distance2D(x) < 260 && !x.HasModifiers(cuntCullModifiers, false));

                if (enemies == 0)
                {
                    Owner.Stop();
                    await Task.Delay(10, token);
                }
            }
        }

        private async Task UseItems(Unit target, CancellationToken token)
        {
            var called = EntityManager<Hero>.Entities
                .Where(x => Owner.Team != x.Team && x.IsValid && !x.IsIllusion && x.IsAlive && x.Modifiers.Any(m => m.Name == "modifier_axe_berserkers_call"))
                .ToList();

            if (called.Any())
            {
                var bkb = Owner.GetItemById(AbilityId.item_black_king_bar);
                if (bkb != null && bkb.CanBeCasted() && config.UseItems.Value.IsEnabled(bkb.Name))
                {
                    bkb.UseAbility();
                    await Task.Delay(10, token);
                }

                var bladeMail = Owner.GetItemById(AbilityId.item_blade_mail);
                if (bladeMail != null && bladeMail.CanBeCasted() && config.UseItems.Value.IsEnabled(bladeMail.Name))
                {
                    bladeMail.UseAbility();
                    await Task.Delay(10, token);
                }

                var lotus = Owner.GetItemById(AbilityId.item_lotus_orb);
                if (lotus != null && lotus.CanBeCasted() && config.UseItems.Value.IsEnabled(lotus.Name))
                {
                    lotus.UseAbility(Owner);
                    await Task.Delay(10, token);
                }

                var mjollnir = Owner.GetItemById(AbilityId.item_mjollnir);
                if (mjollnir != null && mjollnir.CanBeCasted() && config.UseItems.Value.IsEnabled(mjollnir.Name))
                {
                    mjollnir.UseAbility(Owner);
                    await Task.Delay(10, token);
                }

                var shiva = Owner.GetItemById(AbilityId.item_shivas_guard);
                if (shiva != null && shiva.CanBeCasted() && config.UseItems.Value.IsEnabled(shiva.Name))
                {
                    shiva.UseAbility();
                    await Task.Delay(10, token);
                }

                var crimson = Owner.GetItemById(AbilityId.item_crimson_guard);
                if (crimson != null && crimson.CanBeCasted() && config.UseItems.Value.IsEnabled(crimson.Name))
                {
                    crimson.UseAbility();
                    await Task.Delay(10, token);
                }

                var pipe = Owner.GetItemById(AbilityId.item_pipe);
                if (pipe != null && pipe.CanBeCasted() && config.UseItems.Value.IsEnabled(pipe.Name))
                {
                    pipe.UseAbility();
                    await Task.Delay(10, token);
                }

                var dagon = Owner.GetDagon();
                if (dagon != null && dagon.CanBeCasted() && config.UseItems.Value.IsEnabled("item_dagon") && !UnitExtensions.IsMagicImmune(target) && !UnitExtensions.IsLinkensProtected(target))
                {
                    dagon.UseAbility(target);
                    await Task.Delay(10, token);
                }

                if (W != null && W.CanBeCasted() && Owner.CanCast() && !UnitExtensions.IsMagicImmune(target))
                {
                    if (W.ManaCost + R.ManaCost < Owner.Mana)
                    {
                        W.UseAbility(target);
                        await Task.Delay((int)(W.FindCastPoint() * 1000 + Game.Ping), token);
                    }
                }
            }
        }

        private async Task TurnTo(Vector3 posForHitChance, CancellationToken token = new CancellationToken())
        {
            var posForTurn = Owner.Position.Extend(posForHitChance, 65);

            Orbwalker.Move(posForTurn);

            await Task.Delay((int)(Owner.GetTurnTime(posForTurn) * 1000 + Game.Ping + 200), token);
        }
    }
}