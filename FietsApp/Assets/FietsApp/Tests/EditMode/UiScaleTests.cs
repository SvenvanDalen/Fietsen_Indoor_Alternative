using FietsApp.Mobile;
using NUnit.Framework;

namespace FietsApp.Tests
{
    /// <summary>De interface groeit op kleine schermen (story 14.2), maar blijft altijd in beeld.</summary>
    public class UiScaleTests
    {
        [Test]
        public void Monitor_StaysAsDesigned()
        {
            // 24"-monitor, 1080 px hoog bij 92 dpi: 11,7 inch hoog, groter dan het doel van 4,5 inch.
            Assert.That(UiScale.Desired(1080, 92f), Is.EqualTo(1f));
        }

        [Test]
        public void Phone_GrowsToTheTargetHeight()
        {
            // Telefoon liggend, 1080 px bij 400 dpi: 2,7 inch hoog, dus 4,5 / 2,7 = 1,667 keer zo groot.
            Assert.That(UiScale.Desired(1080, 400f), Is.EqualTo(1.6667f).Within(0.001f));
        }

        [Test]
        public void SmallTablet_GrowsALittle()
        {
            // 1200 px bij 320 dpi: 3,75 inch, dus 4,5 / 3,75 = 1,2.
            Assert.That(UiScale.Desired(1200, 320f), Is.EqualTo(1.2f).Within(0.001f));
        }

        [Test]
        public void LargeTablet_StaysAsDesigned()
        {
            // 1600 px bij 280 dpi: 5,7 inch.
            Assert.That(UiScale.Desired(1600, 280f), Is.EqualTo(1f));
        }

        [Test]
        public void TinyScreen_IsCapped()
        {
            Assert.That(UiScale.Desired(720, 400f), Is.EqualTo(UiScale.MaxScale));
        }

        [Test]
        public void UnknownDpi_StaysAsDesigned()
        {
            Assert.That(UiScale.Desired(1080, 0f), Is.EqualTo(1f));
        }

        [Test]
        public void Fit_LimitsToWhatFitsOnScreen()
        {
            // Startscherm 1240×760 op een canvas van 2147×966 met 16 marge: hoogte beperkt, (966 - 32) / 760 = 1,2289.
            Assert.That(UiScale.Fit(2.4f, 1240f, 760f, 2147f, 966f), Is.EqualTo(1.2289f).Within(0.001f));
        }

        [Test]
        public void Fit_KeepsDesiredWhenThereIsRoom()
        {
            Assert.That(UiScale.Fit(1.5f, 640f, 230f, 2147f, 966f), Is.EqualTo(1.5f));
        }

        [Test]
        public void Fit_ShrinksAPanelThatIsTooBig()
        {
            // Wereldkaart 1380×860 op 1200×675: (675 - 32) / 860 = 0,7477.
            Assert.That(UiScale.Fit(1f, 1380f, 860f, 1200f, 675f), Is.EqualTo(0.7477f).Within(0.001f));
        }

        [Test]
        public void Available_FromAnEdge()
        {
            // Ritmenu: bovenkant 190 onder de rand van een canvas van 966 hoog, 16 marge onderaan: 760.
            Assert.That(UiScale.Available(966f, 1f, -190f), Is.EqualTo(760f));
            // Paneel linksonder op 24 van de rand, canvas 2147 breed: 2107.
            Assert.That(UiScale.Available(2147f, 0f, 24f), Is.EqualTo(2107f));
        }

        [Test]
        public void Available_InTheMiddle()
        {
            Assert.That(UiScale.Available(966f, 0.5f, 0f), Is.EqualTo(934f));
            Assert.That(UiScale.Available(966f, 0.5f, 50f), Is.EqualTo(834f));
        }
    }
}
