using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InElementum.Core.UI
{
    /// <summary>
    /// Finds the Kenney input prompt icons for a key or button name as the input code prints it: "E", "Esc", "LMB",
    /// "↑↓", "A" or "RT" on an Xbox pad, "✕" on a PlayStation pad, or "LMB / E" for two inputs. Controller names
    /// are read in the style of the pad plugged in. Anything without an icon gets null, so the caller can fall back to
    /// drawing the text.
    /// </summary>
    public static class InputPrompts
    {
        public enum PadStyle
        {
            Xbox,
            PlayStation,
            Nintendo
        }

        private const string Separator = " / ";

        private static readonly Dictionary<(string glyph, bool gamepad, PadStyle pad), Texture2D[]> Cache =
            new Dictionary<(string, bool, PadStyle), Texture2D[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        /// <summary>The style of the current pad; Xbox when there is none or it is not recognised.</summary>
        public static PadStyle CurrentPad()
        {
            var layout = Gamepad.current?.layout;
            if (string.IsNullOrEmpty(layout)) return PadStyle.Xbox;
            if (IsLayout(layout, "DualShockGamepad")) return PadStyle.PlayStation;
            if (IsLayout(layout, "SwitchProControllerHID")) return PadStyle.Nintendo;
            return PadStyle.Xbox;
        }

        /// <summary>
        /// One icon per input in <paramref name="glyph"/>, in order, or null unless every one has an icon. A
        /// controller name is tried before a keyboard one when <paramref name="gamepad"/>, since a controller scheme
        /// still shows keyboard names for actions with no pad binding.
        /// </summary>
        public static Texture2D[] Icons(string glyph, bool gamepad)
        {
            var art = UiArt.Instance;
            if (art == null || string.IsNullOrWhiteSpace(glyph)) return null;

            var pad = gamepad ? CurrentPad() : PadStyle.Xbox;
            var key = (glyph, gamepad, pad);
            if (Cache.TryGetValue(key, out var icons)) return icons;

            var parts = glyph.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries);
            icons = new Texture2D[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                var icon = gamepad ? art.Prompt(PadIconName(part, pad)) : null;
                icon ??= art.Prompt(KeyIconName(part));
                if (icon == null)
                {
                    icons = null;
                    break;
                }

                icons[i] = icon;
            }

            if (icons != null && icons.Length == 0) icons = null;
            Cache[key] = icons;
            return icons;
        }

        /// <summary>
        /// How much of a full key's width <paramref name="icon"/>'s art fills, 0 to 1. Kenney's keys and pad buttons
        /// span the whole 48-pixel visible area, but the mice are narrower, so a prompt sized by the full width
        /// would leave a gap beside them.
        /// </summary>
        public static float VisibleWidth(Texture2D icon)
        {
            var name = icon != null ? icon.name : null;
            if (string.IsNullOrEmpty(name)) return 1f;
            switch (name)
            {
                case "mouse_small":
                case "mouse_vertical":
                    return 22f / 48f;
                case "keyboard_numpad_plus":
                case "mouse":
                case "mouse_left":
                case "mouse_right":
                case "mouse_scroll":
                case "mouse_scroll_up":
                case "mouse_scroll_down":
                case "mouse_scroll_vertical":
                    return 34f / 48f;
            }

            return name.StartsWith("mouse_", StringComparison.Ordinal) && name.EndsWith("_outline", StringComparison.Ordinal) &&
                   !name.StartsWith("mouse_side", StringComparison.Ordinal)
                ? 36f / 48f
                : 1f;
        }

        /// <summary>The keyboard or mouse icon's file name for <paramref name="text"/>, or null.</summary>
        public static string KeyIconName(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (text.Length == 1 && char.IsLetterOrDigit(text[0]) && text[0] < 128)
                return "keyboard_" + char.ToLowerInvariant(text[0]);

            switch (text.ToLowerInvariant())
            {
                case "lmb": case "left button": return "mouse_left";
                case "rmb": case "right button": return "mouse_right";
                case "mmb": case "middle button": return "mouse_scroll";
                case "esc": case "escape": return "keyboard_escape";
                case "enter": case "return": return "keyboard_enter";
                case "space": return "keyboard_space";
                case "tab": return "keyboard_tab";
                case "bksp": case "backspace": return "keyboard_backspace";
                case "shift": case "left shift": case "right shift": return "keyboard_shift";
                case "ctrl": case "control": case "left ctrl": case "right ctrl": case "left control": case "right control": return "keyboard_ctrl";
                case "alt": case "left alt": case "right alt": return "keyboard_alt";
                case "↑": case "up": case "up arrow": return "keyboard_arrow_up";
                case "↓": case "down": case "down arrow": return "keyboard_arrow_down";
                case "←": case "left": case "left arrow": return "keyboard_arrow_left";
                case "→": case "right": case "right arrow": return "keyboard_arrow_right";
                case "↑↓": return "keyboard_arrows_vertical";
                case "←→": return "keyboard_arrows_horizontal";
                case "delete": case "del": return "keyboard_delete";
                case "insert": return "keyboard_insert";
                case "home": return "keyboard_home";
                case "end": return "keyboard_end";
                case "page up": return "keyboard_page_up";
                case "page down": return "keyboard_page_down";
                case "caps lock": return "keyboard_capslock";
                case "-": return "keyboard_minus";
                case "=": return "keyboard_equals";
                case "[": return "keyboard_bracket_open";
                case "]": return "keyboard_bracket_close";
                case ";": return "keyboard_semicolon";
                case "'": return "keyboard_apostrophe";
                case ",": return "keyboard_comma";
                case ".": return "keyboard_period";
                case "/": return "keyboard_slash_forward";
                case "\\": return "keyboard_slash_back";
                case "`": return "keyboard_tilde";
            }

            var lower = text.ToLowerInvariant();
            if (lower.Length >= 2 && lower.Length <= 3 && lower[0] == 'f' && int.TryParse(lower.Substring(1), out var function) && function >= 1 && function <= 12)
                return "keyboard_f" + function;
            return null;
        }

        /// <summary>The controller icon's file name for <paramref name="text"/> on a <paramref name="pad"/>, or null.</summary>
        public static string PadIconName(string text, PadStyle pad)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var prefix = pad == PadStyle.PlayStation ? "playstation" : pad == PadStyle.Nintendo ? "switch" : "xbox";
            var lower = text.ToLowerInvariant();

            // Directions and sticks share a naming scheme across the pads.
            switch (lower)
            {
                case "d-pad": case "dpad": return prefix + "_dpad";
                case "d-pad ↑": case "d-pad up": return prefix + "_dpad_up";
                case "d-pad ↓": case "d-pad down": return prefix + "_dpad_down";
                case "d-pad ←": case "d-pad left": return prefix + "_dpad_left";
                case "d-pad →": case "d-pad right": return prefix + "_dpad_right";
                case "↑↓": return prefix + "_dpad_vertical";
                case "←→": return prefix + "_dpad_horizontal";
                case "l-stick": case "left stick": return prefix + "_stick_l";
                case "r-stick": case "right stick": return prefix + "_stick_r";
            }

            // Face buttons by position, for names that give the position rather than the label.
            switch (lower)
            {
                case "button south": case "south": lower = pad == PadStyle.PlayStation ? "cross" : pad == PadStyle.Nintendo ? "b" : "a"; break;
                case "button east": case "east": lower = pad == PadStyle.PlayStation ? "circle" : pad == PadStyle.Nintendo ? "a" : "b"; break;
                case "button west": case "west": lower = pad == PadStyle.PlayStation ? "square" : pad == PadStyle.Nintendo ? "y" : "x"; break;
                case "button north": case "north": lower = pad == PadStyle.PlayStation ? "triangle" : pad == PadStyle.Nintendo ? "x" : "y"; break;
            }

            switch (pad)
            {
                case PadStyle.PlayStation:
                    switch (lower)
                    {
                        case "✕": case "cross": return "playstation_button_color_cross";
                        case "○": case "circle": return "playstation_button_color_circle";
                        case "□": case "square": return "playstation_button_color_square";
                        case "△": case "triangle": return "playstation_button_color_triangle";
                        case "l1": case "left shoulder": return "playstation_trigger_l1";
                        case "r1": case "right shoulder": return "playstation_trigger_r1";
                        case "l2": case "left trigger": return "playstation_trigger_l2";
                        case "r2": case "right trigger": return "playstation_trigger_r2";
                        case "l3": case "left stick press": return "playstation_button_l3";
                        case "r3": case "right stick press": return "playstation_button_r3";
                        case "options": case "start": return "playstation5_button_options";
                        case "share": case "create": case "select": return "playstation5_button_create";
                    }
                    return null;

                case PadStyle.Nintendo:
                    switch (lower)
                    {
                        case "a": case "b": case "x": case "y": return "switch_button_" + lower;
                        case "l": case "left shoulder": return "switch_button_l";
                        case "r": case "right shoulder": return "switch_button_r";
                        case "zl": case "left trigger": return "switch_button_zl";
                        case "zr": case "right trigger": return "switch_button_zr";
                        case "ls": case "left stick press": return "switch_stick_l_press";
                        case "rs": case "right stick press": return "switch_stick_r_press";
                        case "+": case "start": return "switch_button_plus";
                        case "−": case "-": case "select": return "switch_button_minus";
                    }
                    return null;

                default:
                    switch (lower)
                    {
                        case "a": case "b": case "x": case "y": return "xbox_button_color_" + lower;
                        case "lb": case "left shoulder": return "xbox_lb";
                        case "rb": case "right shoulder": return "xbox_rb";
                        case "lt": case "left trigger": return "xbox_lt";
                        case "rt": case "right trigger": return "xbox_rt";
                        case "ls": case "left stick press": return "xbox_ls";
                        case "rs": case "right stick press": return "xbox_rs";
                        case "menu": case "start": return "xbox_button_menu";
                        case "view": case "back": case "select": return "xbox_button_view";
                    }
                    return null;
            }
        }

        private static bool IsLayout(string layout, string baseLayout) =>
            string.Equals(layout, baseLayout, StringComparison.OrdinalIgnoreCase) ||
            InputSystem.IsFirstLayoutBasedOnSecond(layout, baseLayout);
    }
}
