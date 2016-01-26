using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Meyn.TestLink;

namespace nunitTests
{
    [TestFixture]
    [TestLinkFixture(
    ConfigFile = "tlinkconfig.xml")]
    public class Class1Tests
    {
        [Test]
        public void FailThis()
        {
            Assert.Fail("Failed because it had to");
        }
        [Test]
        public void Succeed()
        {
        }
    }
}
