using FietsApp.World;
using NUnit.Framework;

namespace FietsApp.Tests
{
    public class ClimbCategoryTests
    {
        // Score = meters × gemiddeld procent; grenzen 8000 / 16000 / 32000 / 64000 / 80000.
        [TestCase(1000f, 5f, ClimbCategory.None)]   // 5000
        [TestCase(2000f, 4f, ClimbCategory.Cat4)]   // 8000, precies de grens
        [TestCase(4000f, 5f, ClimbCategory.Cat3)]   // 20000
        [TestCase(5000f, 7f, ClimbCategory.Cat2)]   // 35000
        [TestCase(10000f, 7f, ClimbCategory.Cat1)]  // 70000
        [TestCase(13800f, 8.1f, ClimbCategory.HC)]  // 111780, Alpe d'Huez
        public void FromLengthTimesGrade(float length, float grade, ClimbCategory expected)
        {
            Assert.That(ClimbCategories.Of(length, grade), Is.EqualTo(expected));
        }

        [Test]
        public void LongFalseFlat_IsNoClimb()
        {
            // 10 km aan 2,5% scoort 25000, maar onder 3% gemiddeld telt het niet als klim.
            Assert.That(ClimbCategories.Of(10000f, 2.5f), Is.EqualTo(ClimbCategory.None));
        }

        [Test]
        public void Labels()
        {
            Assert.That(ClimbCategories.Label(ClimbCategory.HC), Is.EqualTo("HC"));
            Assert.That(ClimbCategories.Label(ClimbCategory.Cat2), Is.EqualTo("cat. 2"));
            Assert.That(ClimbCategories.Label(ClimbCategory.None), Is.Empty);
        }
    }
}
