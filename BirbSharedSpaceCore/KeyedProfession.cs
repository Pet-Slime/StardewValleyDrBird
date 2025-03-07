using Microsoft.Xna.Framework.Graphics;
using SpaceCore;
using SpaceCore.Interface;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BirbShared
{
    internal class KeyedProfession : Skills.Skill.Profession
    {
        readonly object Tokens;
        readonly ITranslationHelper I18n;

        readonly bool PrestigeEnabled = false;
        readonly Texture2D PrestigeIcon;
        readonly Texture2D NormalIcon;
        private bool IsPrestiged = false;
        readonly IModHelper ModHelper;

        public KeyedProfession(Skills.Skill skill, string id, Texture2D icon, ITranslationHelper i18n, object tokens = null) : base(skill, id)
        {
            this.Icon = icon;
            this.I18n = i18n;
            this.Tokens = tokens;
        }

        public KeyedProfession(Skills.Skill skill, string id, Texture2D icon, Texture2D prestigeIcon, IModHelper modHelper, object tokens = null) : base(skill, id)
        {
            this.Icon = icon;
            this.I18n = modHelper.Translation;
            this.Tokens = tokens;

            this.PrestigeEnabled = true;
            this.PrestigeIcon = prestigeIcon;
            this.NormalIcon = icon;
            this.ModHelper = modHelper;

            modHelper.Events.Display.MenuChanged += this.DisplayEvents_MenuChanged_MARGO;
            modHelper.Events.GameLoop.SaveLoaded += this.GameLoop_SaveLoaded_MARGO;
        }

        private void GameLoop_SaveLoaded_MARGO(object sender, SaveLoadedEventArgs e)
        {
            if (Game1.player.HasCustomPrestigeProfession(this))
            {
                this.Icon = this.PrestigeIcon;
                this.IsPrestiged = true;
            }
        }

        private void DisplayEvents_MenuChanged_MARGO(object sender, MenuChangedEventArgs e)
        {
            // After the upgrade selection menu, unset the prestige description and icon of the profession that wasn't chosen.
            if (e.OldMenu is SkillLevelUpMenu oldMenu && oldMenu.isProfessionChooser)
            {
                if (Game1.player.HasCustomPrestigeProfession(this))
                {
                    return;
                }
                this.Icon = this.NormalIcon;
                this.IsPrestiged = false;
            }
        }

        public override string GetDescription()
        {
            if (CheckPrestigeMenu())
            {
                return this.I18n.Get($"{this.Id}.prestige.desc", this.Tokens);
            }
            else
            {
                return this.I18n.Get($"{this.Id}.desc", this.Tokens);
            }
        }

        private bool CheckPrestigeMenu()
        {
            if (!this.PrestigeEnabled)
            {
                return false;
            }
            if (this.IsPrestiged)
            {
                return true;
            }
            if (Game1.activeClickableMenu is not SkillLevelUpMenu currMenu)
            {
                return false;
            }
            if (!currMenu.isProfessionChooser)
            {
                return false;
            }
            string currSkill = ModHelper.Reflection.GetField<string>(currMenu, "currentSkill").GetValue();
            if (currSkill != this.Skill.Id)
            {
                return false;
            }
            int currentLevel = ModHelper.Reflection.GetField<int>(currMenu, "currentLevel").GetValue();
            if (currentLevel <= 10)
            {
                return false;
            }

            // All checks pass, we are in or after the prestiged skill select menu.
            // Set our description and icon to prestiged variants.
            // It's a bit weird to do this in GetDescription, but there's no earlier place.
            this.Icon = this.PrestigeIcon;
            this.IsPrestiged = true;

            return true;
        }

        public override string GetName()
        {
            return this.I18n.Get($"{this.Id}.name", this.Tokens);
        }
    }
}
