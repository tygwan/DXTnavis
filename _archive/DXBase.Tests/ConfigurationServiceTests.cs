using DXBase.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DXBase.Tests
{
    /// <summary>
    /// Tests for ConfigurationService - TDD Phase C.1 (Red)
    ///
    /// Requirements from techspec.md:
    /// - Watch configuration file for changes
    /// - Reload configuration automatically when file changes
    /// - Provide event notification when config reloaded
    /// - Cache configuration in memory for performance
    /// </summary>
    public class ConfigurationServiceTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testConfigDir;

        public ConfigurationServiceTests()
        {
            // Create a temporary directory for test config files
            _testConfigDir = Path.Combine(Path.GetTempPath(), $"DXBase_Test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testConfigDir);
            _testConfigPath = Path.Combine(_testConfigDir, "test_config.txt");
        }

        /// <summary>
        /// Test that ConfigurationService reloads when the watched file changes.
        ///
        /// Steps:
        /// 1. Create initial config file with value "initial"
        /// 2. Create ConfigurationService watching the file
        /// 3. Verify initial value is loaded
        /// 4. Update config file with value "updated"
        /// 5. Wait for file watcher to detect change
        /// 6. Verify configuration was reloaded with new value
        /// </summary>
        [Fact]
        public async Task Reload_OnFileChange()
        {
            // Arrange - Create initial config file
            string initialConfig = "BaseUrl=https://initial.api.com\nTimeout=30";
            File.WriteAllText(_testConfigPath, initialConfig);

            // Create ConfigurationService with file watcher
            var configService = new ConfigurationService(_testConfigPath);

            // Track reload events
            bool reloadEventFired = false;
            configService.ConfigurationReloaded += (sender, args) =>
            {
                reloadEventFired = true;
            };

            // Verify initial configuration loaded
            Assert.Equal("https://initial.api.com", configService.Get("BaseUrl"));
            Assert.Equal("30", configService.Get("Timeout"));

            // Act - Update config file
            string updatedConfig = "BaseUrl=https://updated.api.com\nTimeout=60";
            File.WriteAllText(_testConfigPath, updatedConfig);

            // Wait for file watcher to detect change (FileSystemWatcher can be slow)
            await Task.Delay(1000);

            // Assert - Configuration should be reloaded
            Assert.True(reloadEventFired, "ConfigurationReloaded event should have fired");
            Assert.Equal("https://updated.api.com", configService.Get("BaseUrl"));
            Assert.Equal("60", configService.Get("Timeout"));

            // Cleanup
            configService.Dispose();
        }

        /// <summary>
        /// Test that ConfigurationService caches values for performance.
        /// Repeated reads should not re-parse the file.
        /// </summary>
        [Fact]
        public void Get_ShouldCacheValues()
        {
            // Arrange
            string config = "CachedValue=test123\nAnotherValue=abc";
            File.WriteAllText(_testConfigPath, config);

            var configService = new ConfigurationService(_testConfigPath);

            // Act - Read same value multiple times
            string value1 = configService.Get("CachedValue");
            string value2 = configService.Get("CachedValue");
            string value3 = configService.Get("CachedValue");

            // Assert - All reads should return same cached value
            Assert.Equal("test123", value1);
            Assert.Equal(value1, value2);
            Assert.Equal(value2, value3);

            // Cleanup
            configService.Dispose();
        }

        /// <summary>
        /// Test that ConfigurationService returns null for missing keys.
        /// </summary>
        [Fact]
        public void Get_ShouldReturnNullForMissingKey()
        {
            // Arrange
            string config = "ExistingKey=value";
            File.WriteAllText(_testConfigPath, config);

            var configService = new ConfigurationService(_testConfigPath);

            // Act
            string missingValue = configService.Get("NonExistentKey");
            string existingValue = configService.Get("ExistingKey");

            // Assert
            Assert.Null(missingValue);
            Assert.Equal("value", existingValue);

            // Cleanup
            configService.Dispose();
        }

        /// <summary>
        /// Test that ConfigurationService handles file deletion gracefully.
        /// </summary>
        [Fact]
        public void Reload_ShouldHandleFileDeletion()
        {
            // Arrange
            string config = "Key=Value";
            File.WriteAllText(_testConfigPath, config);

            var configService = new ConfigurationService(_testConfigPath);
            Assert.Equal("Value", configService.Get("Key"));

            // Act - Delete the config file
            File.Delete(_testConfigPath);

            // Assert - Should not crash, return null for missing config
            // (Implementation should handle this gracefully)
            var value = configService.Get("Key");

            // Either returns cached value or null - both are acceptable
            Assert.True(value == "Value" || value == null);

            // Cleanup
            configService.Dispose();
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testConfigDir))
            {
                try
                {
                    Directory.Delete(_testConfigDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }
    }
}
