using DXBase.Services;
using DXBase.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DXBase.Tests
{
    /// <summary>
    /// Tests for HttpClientService - TDD Phase C.1 (Red)
    ///
    /// Requirements from techspec.md:
    /// - Implement resilient HTTP client with retry policy
    /// - Use Polly for transient fault handling
    /// - Support generic request/response DTOs
    /// - Log retry attempts and failures
    /// </summary>
    public class HttpClientServiceTests
    {
        /// <summary>
        /// Test that PostAsync retries on transient failures (5xx errors, timeouts)
        /// and succeeds on the final attempt.
        ///
        /// Transient errors to handle:
        /// - HTTP 500 Internal Server Error
        /// - HTTP 503 Service Unavailable
        /// - HTTP 429 Too Many Requests
        /// - Network timeouts
        /// - Connection failures
        /// </summary>
        [Fact]
        public async Task PostAsync_ShouldRetryOnFailure()
        {
            // Arrange - Create a mock HTTP handler that fails twice then succeeds
            int attemptCount = 0;
            var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
            {
                attemptCount++;

                // Fail on first two attempts with 503 Service Unavailable
                if (attemptCount < 3)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    {
                        Content = new StringContent("{\"Error\":\"Service temporarily unavailable\"}")
                    });
                }

                // Succeed on third attempt (PascalCase JSON to match C# DTOs)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"Success\":true,\"Data\":\"processed\"}")
                });
            });

            var httpClient = new HttpClient(mockHandler)
            {
                BaseAddress = new Uri("https://api.test.com")
            };

            // Use constructor that accepts HttpClient for testing
            var httpClientService = new HttpClientService(httpClient);

            var requestDto = new TestRequestDto { Data = "test-data" };

            // Act - Call PostAsync which should retry and eventually succeed
            var response = await httpClientService.PostAsync<TestRequestDto, TestResponseDto>(
                "/endpoint",
                requestDto
            );

            // Assert
            Assert.Equal(3, attemptCount); // Should have tried 3 times
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            Assert.Equal("processed", response.Data.Data);
        }

        /// <summary>
        /// Test that PostAsync gives up after maximum retry attempts
        /// and returns failure response.
        /// </summary>
        [Fact]
        public async Task PostAsync_ShouldFailAfterMaxRetries()
        {
            // Arrange - Create a mock handler that always fails
            int attemptCount = 0;
            var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
            {
                attemptCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{\"Error\":\"Service down\"}")
                });
            });

            var httpClient = new HttpClient(mockHandler)
            {
                BaseAddress = new Uri("https://api.test.com")
            };

            var httpClientService = new HttpClientService(httpClient);
            var requestDto = new TestRequestDto { Data = "test-data" };

            // Act - Should eventually return failure after max retries
            var response = await httpClientService.PostAsync<TestRequestDto, TestResponseDto>(
                "/endpoint",
                requestDto
            );

            // Assert - Should have retried multiple times (3-5 attempts typical)
            Assert.True(attemptCount >= 3, $"Expected at least 3 retry attempts, got {attemptCount}");
            Assert.NotNull(response);
            Assert.False(response.Success);
            Assert.NotNull(response.ErrorMessage);
        }

        /// <summary>
        /// Test that PostAsync does NOT retry on client errors (4xx)
        /// as these are not transient failures.
        /// </summary>
        [Fact]
        public async Task PostAsync_ShouldNotRetryOn4xxErrors()
        {
            // Arrange - Create a mock handler that returns 400 Bad Request
            int attemptCount = 0;
            var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
            {
                attemptCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"Error\":\"Invalid request\"}")
                });
            });

            var httpClient = new HttpClient(mockHandler)
            {
                BaseAddress = new Uri("https://api.test.com")
            };

            var httpClientService = new HttpClientService(httpClient);
            var requestDto = new TestRequestDto { Data = "invalid-data" };

            // Act - Should return failure immediately without retrying
            var response = await httpClientService.PostAsync<TestRequestDto, TestResponseDto>(
                "/endpoint",
                requestDto
            );

            // Assert - Should only try once, no retries for 4xx errors
            Assert.Equal(1, attemptCount);
            Assert.NotNull(response);
            Assert.False(response.Success);
        }
    }

    // Test DTOs
    public class TestRequestDto
    {
        public string? Data { get; set; }
    }

    public class TestResponseDto
    {
        public bool Success { get; set; }
        public string? Data { get; set; }
    }

    // Mock HTTP message handler for testing
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
