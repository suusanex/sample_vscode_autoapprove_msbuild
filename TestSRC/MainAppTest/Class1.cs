using MainApp;
using NUnit.Framework;

namespace MainAppTest
{
    [TestFixture]
    public class Class1
    {
        private MainAppClass1? _subject;

        [SetUp]
        public void SetUp()
        {
            _subject = new MainAppClass1();
        }

        [Test]
        public void Add_ReturnsCorrectSum()
        {
            Assert.That(_subject!.Add(2, 3), Is.EqualTo(5));
        }

        [Test]
        public void Add_HandlesNegativeNumbers()
        {
            Assert.That(_subject!.Add(-4, -1), Is.EqualTo(-5));
        }
    }
}
