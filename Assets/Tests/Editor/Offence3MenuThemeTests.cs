using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Vigilante8.Gameplay;

namespace Vigilante8.EditorTests
{
    public sealed class Offence3MenuThemeTests
    {
        [Test]
        public void DistinctThirdGameIdentityIsGeneratedAtRuntime()
        {
            var surface = new VisualElement();
            Offence3MenuTheme.AddIdentity(surface);
            Assert.That(surface.Query<Label>().ToList().Count, Is.EqualTo(3));
            Assert.That(surface.Query<Label>().ToList()[1].text, Is.EqualTo("OFFENCE 3"));
            Assert.That(surface.Query<Label>().ToList()[2].text, Does.Contain("WINDOWS"));
        }

        [Test]
        public void IndustrialSafetyStripeAndPanelHaveExplicitGeometry()
        {
            var surface = new VisualElement();
            Offence3MenuTheme.AddHazardStripe(surface);
            Assert.That(surface.childCount, Is.EqualTo(1));
            Assert.That(surface[0].childCount, Is.EqualTo(22));
            var card = Offence3MenuTheme.ContentPanel();
            Assert.That(card.style.borderTopWidth.value, Is.EqualTo(2f));
            Assert.That(card.style.borderLeftWidth.value, Is.EqualTo(1f));
        }

        [Test]
        public void ActionAndSelectorHaveVisibleStyling()
        {
            var button = new Button();
            Offence3MenuTheme.Action(button, true);
            Assert.That(button.style.minHeight.value.value, Is.GreaterThan(40f));
            Assert.That(button.style.borderLeftWidth.value, Is.EqualTo(4f));

            var dropdown = new DropdownField();
            Offence3MenuTheme.Selector(dropdown);
            Assert.That(dropdown.style.borderBottomWidth.value, Is.EqualTo(2f));
        }
    }
}
