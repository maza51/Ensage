namespace SuperAxe
{
    using System;
    using System.ComponentModel.Composition;
    using System.Windows.Input;

    using Ensage;
    using Ensage.Common.Menu;
    using Ensage.SDK.Input;
    using Ensage.SDK.Orbwalker;
    using Ensage.SDK.Service;
    using Ensage.SDK.Service.Metadata;
    using Ensage.SDK.TargetSelector;

    [ExportPlugin("SuperAxe!", HeroId.npc_dota_hero_axe)]
    public class Program : Plugin
    {
        private readonly Lazy<IInputManager> Input;

        private readonly Lazy<IOrbwalkerManager> OrbwalkerManager;

        private readonly Lazy<ITargetSelectorManager> TargetSelector;

        public SuperAxe OrbwalkerMode { get; private set; }

        public Config Config { get; private set; }

        [ImportingConstructor]
        public Program(
            [Import] Lazy<IInputManager> input,
            [Import] Lazy<IOrbwalkerManager> orbwalkerManager,
            [Import] Lazy<ITargetSelectorManager> targetSelector)
        {
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
        }

        protected override void OnDeactivate()
        {
            this.OrbwalkerManager.Value.UnregisterMode(this.OrbwalkerMode);

            this.Config.Key.Item.ValueChanged -= HotkeyChanged;
            this.Config.Dispose();
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