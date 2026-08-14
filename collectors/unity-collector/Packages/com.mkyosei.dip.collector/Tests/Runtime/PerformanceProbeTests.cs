using System.Collections.Generic;
using NUnit.Framework;
using Dip.UnityCollector.Runtime;

namespace Dip.UnityCollector.Runtime.Tests
{
    public sealed class PerformanceProbeTests
    {
        [Test]
        public void Percentile_InterpolatesSortedSamples()
        {
            var values = new List<double> { 10, 20, 30, 40, 50 };
            Assert.That(PerformanceProbe.Percentile(values, 0.50), Is.EqualTo(30).Within(0.001));
            Assert.That(PerformanceProbe.Percentile(values, 0.95), Is.EqualTo(48).Within(0.001));
        }

        [Test]
        public void Percentile_EmptyInputReturnsZero()
        {
            Assert.That(PerformanceProbe.Percentile(new List<double>(), 0.95), Is.Zero);
        }
    }
}
