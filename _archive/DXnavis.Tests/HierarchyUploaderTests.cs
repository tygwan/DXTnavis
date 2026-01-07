using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using DXnavis.Services;

namespace DXnavis.Tests
{
    public class HierarchyUploaderTests
    {
        [Fact]
        public void SampleObjectIdsFromCsv_ShouldLimitTo100()
        {
            // Arrange: Create a test CSV with 150 rows
            var testCsvPath = Path.GetTempFileName();
            var lines = new List<string> { "DisplayString,ID" }; // Header

            for (int i = 1; i <= 150; i++)
            {
                lines.Add($"Object_{i},ID_{i}");
            }

            File.WriteAllLines(testCsvPath, lines);

            try
            {
                // Act: Sample object IDs with max limit
                var uploader = new HierarchyUploader();
                var result = uploader.SampleObjectIdsFromCsv(testCsvPath, maxSamples: 100);

                // Assert: Should return exactly 100 samples
                Assert.NotNull(result);
                Assert.Equal(100, result.Count);
                Assert.All(result, id => Assert.False(string.IsNullOrWhiteSpace(id)));
            }
            finally
            {
                // Cleanup
                if (File.Exists(testCsvPath))
                    File.Delete(testCsvPath);
            }
        }

        [Fact]
        public void StripPrefixes_ShouldRemoveDisplayString()
        {
            // Arrange
            var uploader = new HierarchyUploader();
            var testCases = new Dictionary<string, string>
            {
                { "DisplayString:Wall 001", "Wall 001" },
                { "NamedConstant:Type A", "Type A" },
                { "Boolean:True", "True" },
                { "Double:3.14", "3.14" },
                { "Integer:42", "42" },
                { "NoPrefix", "NoPrefix" },
                { "  DisplayString:  Spaced  ", "Spaced" }
            };

            // Act & Assert
            foreach (var testCase in testCases)
            {
                var result = uploader.StripPrefixes(testCase.Key);
                Assert.Equal(testCase.Value, result);
            }
        }

        [Fact]
        public void DetectProject_ShouldCallApiWithConfidenceThreshold()
        {
            // Arrange: Create mock HTTP client service
            var mockHttpClient = new MockHttpClientService();
            var uploader = new HierarchyUploader(mockHttpClient);

            var objectIds = new List<string> { "ID_1", "ID_2", "ID_3" };

            // Act
            var result = uploader.DetectProject(objectIds, minConfidence: 0.7);

            // Assert: Verify API was called with correct parameters
            Assert.NotNull(result);
            Assert.NotNull(mockHttpClient.LastRequest);
            Assert.Equal(objectIds, mockHttpClient.LastRequest.ObjectIds);
            Assert.Equal(0.7, mockHttpClient.LastRequest.MinConfidence);
            Assert.Equal(3, mockHttpClient.LastRequest.MaxCandidates);
        }
    }

    // Mock classes for testing
    public class MockHttpClientService
    {
        public DetectRequest LastRequest { get; private set; }

        public DetectResponse PostAsync(string endpoint, DetectRequest request)
        {
            LastRequest = request;
            return new DetectResponse
            {
                Candidates = new List<ProjectCandidate>
                {
                    new ProjectCandidate { ProjectId = "test-project", Confidence = 0.85, ProjectName = "Test Project" }
                }
            };
        }
    }

    public class DetectRequest
    {
        public List<string> ObjectIds { get; set; }
        public double MinConfidence { get; set; }
        public int MaxCandidates { get; set; }
    }

    public class DetectResponse
    {
        public List<ProjectCandidate> Candidates { get; set; }
    }

    public class ProjectCandidate
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public double Confidence { get; set; }
    }
}
