﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbfSharpTests
{
    class AbfSweepValues
    {
        // values obtained from Python and pyABF (script in dev folder)
        [TestCase("16921011-vc-memtest-tags.abf", new double[] { -7.202148, -6.7138667, -5.737304, -4.15039, -3.4179685 })]
        [TestCase("17n16012-vc-steps.abf", new double[] { -120.23925, -119.26269, -118.164055, -119.01855, -120.11718 })]
        [TestCase("17n16016-ic-ramp.abf", new double[] { -61.431885, -61.553955, -61.401367, -61.584473, -61.431885 })]
        [TestCase("17n16016-ic-steps.abf", new double[] { -62.469482, -62.316895, -62.438965, -62.438965, -62.316895 })]
        [TestCase("18808025-memtest.abf", new double[] { -14.770507, -15.502929, -16.23535, -16.35742, -15.86914 })]
        public void Test_FirstValues(string filename, double[] expectedFirstValues)
        {
            string abfPath = SampleData.GetAbfPath(filename);
            var abf = new AbfSharp.ABF(abfPath);
            var firstSweep = abf.GetSweep(0);

            for (int i = 0; i < expectedFirstValues.Length; i++)
                Assert.AreEqual(expectedFirstValues[i], firstSweep.Values[i], 1e-5);
        }
    }
}
