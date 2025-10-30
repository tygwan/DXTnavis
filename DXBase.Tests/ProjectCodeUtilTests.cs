using DXBase.Models;
using Xunit;

namespace DXBase.Tests
{
    /// <summary>
    /// Tests for ProjectCodeUtil - TDD Phase C.1 (Red)
    ///
    /// Requirements from techspec.md:
    /// - Generate normalized project codes from Korean and English names
    /// - Fallback to default when name is empty
    /// - Remove special characters and spaces
    /// - Convert to uppercase
    /// </summary>
    public class ProjectCodeUtilTests
    {
        [Fact]
        public void Generate_ShouldNormalizeKoreanAndEnglishNames()
        {
            // Arrange - Test various project name formats
            var testCases = new[]
            {
                // English name with spaces
                ("Snowdon Towers", "SNOWDON_TOWERS"),

                // Mixed alphanumeric
                ("DX Platform 2025", "DX_PLATFORM_2025"),

                // With special characters
                ("Project-ABC (Phase 1)", "PROJECT_ABC_PHASE_1"),

                // Lowercase to uppercase
                ("test project", "TEST_PROJECT"),

                // Korean romanization (phonetic)
                ("서울", "SEOUL"),  // Phonetic: seo-ul

                // Mixed Korean/English - Korean gets romanized
                ("ABC프로젝트", "ABCPEUROJEKTEU")
            };

            foreach (var (input, expected) in testCases)
            {
                // Act
                string result = ProjectCodeUtil.Generate(input);

                // Assert
                Assert.Equal(expected, result);
            }
        }

        [Fact]
        public void Generate_ShouldFallbackWhenEmpty()
        {
            // Arrange - Empty or null inputs
            var testCases = new[]
            {
                (null, "PROJECT_UNKNOWN"),
                ("", "PROJECT_UNKNOWN"),
                ("   ", "PROJECT_UNKNOWN")  // Whitespace only
            };

            foreach (var (input, expected) in testCases)
            {
                // Act
                string result = ProjectCodeUtil.Generate(input);

                // Assert
                Assert.Equal(expected, result);
            }
        }

        [Fact]
        public void Generate_ShouldHandleConsecutiveSpaces()
        {
            // Arrange
            string input = "Project    With     Many      Spaces";
            string expected = "PROJECT_WITH_MANY_SPACES";

            // Act
            string result = ProjectCodeUtil.Generate(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Generate_ShouldTrimWhitespace()
        {
            // Arrange
            string input = "  Leading and Trailing Spaces  ";
            string expected = "LEADING_AND_TRAILING_SPACES";

            // Act
            string result = ProjectCodeUtil.Generate(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
