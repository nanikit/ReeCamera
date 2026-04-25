using UnityEngine;
using Xunit;

namespace ReeCamera.Tests {
    public class CameraDragMathTests {
        [Theory]
        [InlineData(0f, 0f)]
        [InlineData(90f, 90f)]
        [InlineData(180f, 180f)]
        [InlineData(-180f, -180f)]
        [InlineData(270f, -90f)]
        [InlineData(-270f, 90f)]
        [InlineData(360f, 0f)]
        [InlineData(-360f, 0f)]
        [InlineData(540f, 180f)]
        [InlineData(-540f, -180f)]
        [InlineData(720f, 0f)]
        [InlineData(45.5f, 45.5f)]
        [InlineData(359.99f, -0.01f)]
        public void NormalizeAngle_WrapsToPlusMinus180(float input, float expected) {
            var actual = CameraDragMath.NormalizeAngle(input);
            Assert.Equal(expected, actual, precision: 4);
        }

        [Fact]
        public void NormalizeEuler_NormalizesEachAxisIndependently() {
            var input = new Vector3(270f, -270f, 540f);

            var actual = CameraDragMath.NormalizeEuler(input);

            Assert.Equal(-90f, actual.x, precision: 4);
            Assert.Equal(90f, actual.y, precision: 4);
            Assert.Equal(180f, actual.z, precision: 4);
        }

        [Fact]
        public void NormalizeEuler_LeavesValuesInRangeUntouched() {
            var input = new Vector3(15f, -45f, 179f);

            var actual = CameraDragMath.NormalizeEuler(input);

            Assert.Equal(15f, actual.x, precision: 4);
            Assert.Equal(-45f, actual.y, precision: 4);
            Assert.Equal(179f, actual.z, precision: 4);
        }
    }
}
