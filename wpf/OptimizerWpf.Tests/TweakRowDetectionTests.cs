using System.Collections.Generic;
using System.Threading.Tasks;
using OptimizerWpf.Services;
using OptimizerWpf.Views;
using Xunit;

namespace OptimizerWpf.Tests
{
    public class TweakRowDetectionTests
    {
        private static SimpleTweak T(string id, bool? state) =>
            new(id, "d", () => { }, () => { }, () => state, id);

        [Fact]
        public async Task DeferredRowsGetRealStateAfterDetection()
        {
            var rows = new List<TweakRowVm> { new(T("A", true), "X", detectNow: false), new(T("B", false), "X", detectNow: false) };
            Assert.False(rows[0].IsOn); // πριν την ανίχνευση: προσωρινά κλειστό, χωρίς να μπλοκάρει ο constructor
            await TweakRowVm.DetectAllAsync(rows);
            Assert.True(rows[0].IsOn);
            Assert.False(rows[1].IsOn);
        }

        [Fact]
        public async Task UserChoiceMadeBeforeDetectionIsNotOverwritten()
        {
            var row = new TweakRowVm(T("A", false), "X", detectNow: false);
            row.IsOn = true; // ο χρήστης πάτησε τον διακόπτη πριν τελειώσει η ανίχνευση
            await TweakRowVm.DetectAllAsync(new[] { row });
            Assert.True(row.IsOn);
        }

        [Fact]
        public async Task ThrowingOrMissingDetectorsLeaveRowOff()
        {
            var throwing = new SimpleTweak("T", "d", () => { }, () => { }, () => throw new System.InvalidOperationException(), "T");
            var none = new SimpleTweak("N", "d", () => { }, () => { }, null, "N");
            var rows = new[] { new TweakRowVm(throwing, "X", detectNow: false), new TweakRowVm(none, "X", detectNow: false) };
            await TweakRowVm.DetectAllAsync(rows);
            Assert.False(rows[0].IsOn);
            Assert.False(rows[1].IsOn);
        }

        [Fact]
        public void SynchronousModeStillDetectsImmediately() =>
            Assert.True(new TweakRowVm(T("A", true), "X").IsOn);
    }
}
