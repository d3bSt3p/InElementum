using System.Collections.Generic;
using InElementum.Data.Characters;
using InElementum.Encounters;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Menus
{
    /// <summary>
    /// One tab of the <see cref="GameMenu"/>. Builds its elements once under <see cref="Root"/>, then redraws from the
    /// party in <see cref="Refresh"/>. The menu forwards input while the screen has the cursor.
    /// </summary>
    public abstract class MenuScreen
    {
        protected GameMenu Menu { get; private set; }
        protected PartyService Party => Menu.Party;
        protected PartyMemberData Member => Menu.Member;

        public VisualElement Root { get; private set; }

        /// <summary>The name in the menu's category list.</summary>
        public abstract string Title { get; }
        /// <summary>Whether the screen takes the cursor when chosen. False for a screen that only shows information.</summary>
        public virtual bool Interactive => true;
        /// <summary>Whether the screen shows one member, so the party strip and member switching apply.</summary>
        public virtual bool PerMember => true;

        public void Attach(GameMenu menu, VisualElement container)
        {
            Menu = menu;
            Root = new VisualElement();
            Root.AddToClassList("screen");
            Root.AddToClassList("screen--" + Title.ToLowerInvariant());
            container.Add(Root);
            Build();
        }

        protected abstract void Build();

        /// <summary>Redraws from the party and the current member.</summary>
        public abstract void Refresh();

        /// <summary>The screen takes the cursor.</summary>
        public virtual void Enter() { }
        /// <summary>The cursor goes back to the category list.</summary>
        public virtual void Exit() { }

        public virtual void Move(Vector2Int direction) { }
        public virtual void Confirm() { }
        /// <summary>Backs out one step inside the screen. False hands the cursor back to the category list.</summary>
        public virtual bool Cancel() => false;
        public virtual void Secondary() { }
        public virtual void Switch() { }
        /// <summary>Handles Previous/Next itself. False lets the menu switch member.</summary>
        public virtual bool Cycle(int step) => false;

        /// <summary>Button prompts while the screen has the cursor, after Confirm and Cancel's defaults.</summary>
        public virtual IEnumerable<(MenuButton button, string label)> Prompts()
        {
            yield return (MenuButton.Confirm, "Select");
            yield return (MenuButton.Cancel, "Return");
        }
    }
}
