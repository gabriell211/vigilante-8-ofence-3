using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Original third-generation arcade presentation: industrial 70s palette,
    /// electric-blue highlights, sharp gold edge work, accessible focus states.
    /// Inspired by the menu structure of the classic games, not copied assets.
    /// </summary>
    public static class Offence3MenuTheme
    {
        public static readonly Color Background = new Color(0.038f, 0.055f, 0.065f, 1f);
        public static readonly Color Surface = new Color(0.082f, 0.102f, 0.114f, 0.98f);
        public static readonly Color Raised = new Color(0.14f, 0.18f, 0.195f, 1f);
        public static readonly Color Bronze = new Color(1f, 0.55f, 0.16f, 1f);
        public static readonly Color Gold = new Color(1f, 0.79f, 0.27f, 1f);
        public static readonly Color ElectricBlue = new Color(0.25f, 0.75f, 0.96f, 1f);
        public static readonly Color White = new Color(0.98f, 0.985f, 0.96f, 1f);
        public static readonly Color Subtle = new Color(0.69f, 0.74f, 0.72f, 1f);
        public static readonly Color Danger = new Color(0.98f, 0.41f, 0.31f, 1f);
        public static readonly Color Ready = new Color(0.47f, 0.91f, 0.67f, 1f);

        public static void Card(VisualElement panel)
        {
            panel.style.backgroundColor = Surface;
            panel.style.borderTopWidth = 2;
            panel.style.borderBottomWidth = 2;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopColor = Gold;
            panel.style.borderBottomColor = Bronze;
            panel.style.borderLeftColor = new Color(0.34f, 0.37f, 0.34f);
            panel.style.borderRightColor = new Color(0.34f, 0.37f, 0.34f);
            panel.style.borderTopLeftRadius = 12;
            panel.style.borderTopRightRadius = 12;
            panel.style.borderBottomLeftRadius = 12;
            panel.style.borderBottomRightRadius = 12;
        }

        public static void Action(Button button, bool primary)
        {
            Color normal = primary ? Bronze : Raised;
            Color highlighted = primary ? Gold : new Color(0.18f, 0.34f, 0.40f);
            button.style.backgroundColor = normal;
            button.style.color = primary ? Background : White;
            button.style.fontSize = 15;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.minHeight = 54;
            button.style.marginTop = 6;
            button.style.marginBottom = 6;
            button.style.paddingLeft = 19;
            button.style.paddingRight = 19;
            button.style.borderTopLeftRadius = 7;
            button.style.borderTopRightRadius = 7;
            button.style.borderBottomLeftRadius = 7;
            button.style.borderBottomRightRadius = 7;
            button.style.borderLeftWidth = 4;
            button.style.borderLeftColor = primary ? Gold : ElectricBlue;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopColor = new Color(0.60f, 0.66f, 0.62f);
            button.style.borderRightColor = new Color(0.22f, 0.30f, 0.33f);
            button.style.borderBottomColor = new Color(0.22f, 0.30f, 0.33f);

            // Works with mouse and keyboard/gamepad navigation focus.
            button.RegisterCallback<PointerEnterEvent>(_ => Highlight(button, highlighted));
            button.RegisterCallback<PointerLeaveEvent>(_ => Highlight(button, normal));
            button.RegisterCallback<FocusInEvent>(_ => Highlight(button, highlighted));
            button.RegisterCallback<FocusOutEvent>(_ => Highlight(button, normal));
        }

        private static void Highlight(Button button, Color target)
        {
            if (button == null || !button.enabledInHierarchy) return;
            button.style.backgroundColor = target;
            button.style.borderLeftColor = Gold;
        }

        public static void Selector(DropdownField input)
        {
            input.style.color = White;
            input.style.fontSize = 16;
            input.style.marginBottom = 16;
            input.style.minHeight = 46;
            input.style.borderBottomWidth = 2;
            input.style.borderBottomColor = ElectricBlue;
        }

        public static void AddHazardStripe(VisualElement parent)
        {
            var stripe = new VisualElement();
            stripe.name = "offence3-hazard-stripe";
            stripe.style.height = 8;
            stripe.style.flexDirection = FlexDirection.Row;
            stripe.style.marginBottom = 20;
            stripe.style.overflow = Overflow.Hidden;
            for (int i = 0; i < 22; i++)
            {
                var segment = new VisualElement();
                segment.style.flexGrow = 1;
                segment.style.backgroundColor = (i % 2 == 0) ? Gold : Background;
                stripe.Add(segment);
            }
            parent.Add(stripe);
        }

        public static void AddIdentity(VisualElement parent)
        {
            var eyebrow = new Label("VIGILANTE 8  /  NOVA GERAÇÃO");
            eyebrow.style.color = Gold;
            eyebrow.style.fontSize = 12;
            eyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            eyebrow.style.letterSpacing = 2;
            eyebrow.style.marginBottom = 1;
            parent.Add(eyebrow);

            var title = new Label("OFFENCE 3");
            title.style.color = White;
            title.style.fontSize = 53;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 1;
            parent.Add(title);

            var edition = new Label("COMBATE VEICULAR   /   WINDOWS EDITION");
            edition.style.color = ElectricBlue;
            edition.style.fontSize = 12;
            edition.style.unityFontStyleAndWeight = FontStyle.Bold;
            edition.style.marginBottom = 24;
            parent.Add(edition);
        }

        public static Label Chip(string value, Color accent)
        {
            var label = new Label(value);
            label.style.color = accent;
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.paddingTop = 8;
            label.style.paddingBottom = 8;
            label.style.paddingLeft = 10;
            label.style.paddingRight = 10;
            label.style.marginRight = 9;
            label.style.marginBottom = 9;
            label.style.backgroundColor = Raised;
            label.style.borderBottomWidth = 2;
            label.style.borderBottomColor = accent;
            return label;
        }

        public static VisualElement ContentPanel()
        {
            var panel = new VisualElement();
            Card(panel);
            panel.style.paddingTop = 18;
            panel.style.paddingBottom = 18;
            panel.style.paddingLeft = 18;
            panel.style.paddingRight = 18;
            panel.style.marginTop = 10;
            panel.style.marginBottom = 14;
            return panel;
        }

        public static void Emphasize(Label label, bool available)
        {
            if (label == null) return;
            label.style.color = available ? Ready : Gold;
            label.style.backgroundColor = Raised;
            label.style.paddingTop = 12;
            label.style.paddingBottom = 12;
            label.style.paddingLeft = 14;
            label.style.paddingRight = 14;
            label.style.borderLeftWidth = 4;
            label.style.borderLeftColor = available ? Ready : Bronze;
        }
    }
}
