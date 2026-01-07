using DXBase.Models;
using DXBase.Services;
using DXrevit.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DXrevit.Tests
{
    public class ApiDataWriterTests
    {
        [Fact]
        public async Task Upload_ShouldFallbackToSecondaryEndpoint()
        {
            int primaryCallCount = 0;
            int secondaryCallCount = 0;
            
            var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
            {
                string requestUri = request.RequestUri.ToString();

                // Check more specific path first to avoid overlap
                if (requestUri.Contains("/api/v1/ingest-backup"))
                {
                    secondaryCallCount++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"success\":true,\"message\":\"Data received\"}")
                    });
                }

                if (requestUri.Contains("/api/v1/ingest"))
                {
                    primaryCallCount++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    {
                        Content = new StringContent("{\"error\":\"Primary server unavailable\"}")
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });
            
            var httpClient = new HttpClient(mockHandler)
            {
                BaseAddress = new Uri("https://api.test.com")
            };
            
            var apiDataWriter = new ApiDataWriter(httpClient, 
                primaryEndpoint: "/api/v1/ingest",
                secondaryEndpoint: "/api/v1/ingest-backup");
            
            var testData = new ExtractedData
            {
                Metadata = new MetadataRecord { ModelVersion = "v1.0", ProjectName = "Test" },
                Objects = new System.Collections.Generic.List<ObjectRecord>(),
                Relationships = new System.Collections.Generic.List<RelationshipRecord>()
            };
            
            bool result = await apiDataWriter.SendDataAsync(testData);
            
            Assert.True(result, "SendDataAsync should return true when secondary endpoint succeeds");
            Assert.Equal(1, primaryCallCount);
            Assert.Equal(1, secondaryCallCount);
        }

        [Fact]
        public async Task Upload_ShouldFailWhenBothEndpointsUnavailable()
        {
            int totalCallCount = 0;
            
            var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
            {
                totalCallCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{\"error\":\"All servers unavailable\"}")
                });
            });
            
            var httpClient = new HttpClient(mockHandler)
            {
                BaseAddress = new Uri("https://api.test.com")
            };
            
            var apiDataWriter = new ApiDataWriter(httpClient,
                primaryEndpoint: "/api/v1/ingest",
                secondaryEndpoint: "/api/v1/ingest-backup");
            
            var testData = new ExtractedData
            {
                Metadata = new MetadataRecord { ModelVersion = "v1.0", ProjectName = "Test" },
                Objects = new System.Collections.Generic.List<ObjectRecord>(),
                Relationships = new System.Collections.Generic.List<RelationshipRecord>()
            };
            
            bool result = await apiDataWriter.SendDataAsync(testData);
            
            Assert.False(result, "SendDataAsync should return false when all endpoints fail");
            Assert.True(totalCallCount >= 2, "Both endpoints should have been attempted");
        }

        [Fact]
        public async Task Upload_ShouldUseOnlyPrimaryWhenAvailable()
        {
            int primaryCallCount = 0;
            int secondaryCallCount = 0;
            
            var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
            {
                string requestUri = request.RequestUri.ToString();

                // Check more specific path first to avoid overlap
                if (requestUri.Contains("/api/v1/ingest-backup"))
                {
                    secondaryCallCount++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"success\":true}")
                    });
                }

                if (requestUri.Contains("/api/v1/ingest"))
                {
                    primaryCallCount++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"success\":true}")
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });
            
            var httpClient = new HttpClient(mockHandler)
            {
                BaseAddress = new Uri("https://api.test.com")
            };
            
            var apiDataWriter = new ApiDataWriter(httpClient,
                primaryEndpoint: "/api/v1/ingest",
                secondaryEndpoint: "/api/v1/ingest-backup");
            
            var testData = new ExtractedData
            {
                Metadata = new MetadataRecord { ModelVersion = "v1.0", ProjectName = "Test" },
                Objects = new System.Collections.Generic.List<ObjectRecord>(),
                Relationships = new System.Collections.Generic.List<RelationshipRecord>()
            };
            
            bool result = await apiDataWriter.SendDataAsync(testData);
            
            Assert.True(result, "SendDataAsync should succeed");
            Assert.Equal(1, primaryCallCount);
            Assert.Equal(0, secondaryCallCount);
        }
    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _sendAsync(request, cancellationToken);
        }
    }
}
