using System;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace DXrevit.Tests
{
    public class DataExtractorTests
    {
        [Fact]
        public void ExtractObject_ShouldPopulateUniqueKeyAndGuid()
        {
            string revitUniqueId = "12345678-1234-5678-9012-123456789abc-00012345";
            string expectedGuid = "12345678-1234-5678-9012-123456789abc";
            
            string category = "Walls";
            string family = "Basic Wall";
            string type = "Generic - 200mm";
            
            var result = DataExtractorHelper.TryExtractGuid(revitUniqueId, out Guid objectGuid);
            var uniqueKey = DataExtractorHelper.GenerateUniqueKey(category, family, type);
            
            Assert.True(result, "Should successfully extract GUID from valid UniqueId");
            Assert.Equal(expectedGuid, objectGuid.ToString(), ignoreCase: true);
            Assert.NotNull(uniqueKey);
            Assert.NotEmpty(uniqueKey);
            
            var uniqueKey2 = DataExtractorHelper.GenerateUniqueKey(category, family, type);
            Assert.Equal(uniqueKey, uniqueKey2);
        }

        [Fact]
        public void ExtractObject_ShouldHandleNonGuidUniqueId()
        {
            string invalidUniqueId1 = "not-a-guid-at-all";
            string invalidUniqueId2 = "12345-67890";
            string invalidUniqueId3 = "";
            
            string category = "Doors";
            string family = "M_Door-Single";
            string type = "0915 x 2134mm";
            
            var result1 = DataExtractorHelper.TryExtractGuid(invalidUniqueId1, out Guid guid1);
            Assert.False(result1, "Should return false for invalid GUID format");
            Assert.Equal(Guid.Empty, guid1);
            
            var result2 = DataExtractorHelper.TryExtractGuid(invalidUniqueId2, out Guid guid2);
            Assert.False(result2, "Should return false for partial GUID format");
            Assert.Equal(Guid.Empty, guid2);
            
            var result3 = DataExtractorHelper.TryExtractGuid(invalidUniqueId3, out Guid guid3);
            Assert.False(result3, "Should return false for empty string");
            Assert.Equal(Guid.Empty, guid3);
            
            var uniqueKey = DataExtractorHelper.GenerateUniqueKey(category, family, type);
            Assert.NotNull(uniqueKey);
            Assert.NotEmpty(uniqueKey);
            
            var uniqueKey2 = DataExtractorHelper.GenerateUniqueKey("Windows", "M_Window", "1200 x 1500mm");
            Assert.NotEqual(uniqueKey, uniqueKey2);
        }

        [Fact]
        public void UnifiedObjectDto_ShouldMapAllRequiredFields()
        {
            var objectGuid = Guid.NewGuid();
            var uniqueKey = "walls_basicwall_generic200mm_abc123";
            var category = "Walls";
            var family = "Basic Wall";
            var type = "Generic - 200mm";
            var properties = "{\"Height\": 3000, \"Width\": 200}";
            
            var dto = new UnifiedObjectDto
            {
                ObjectGuid = objectGuid,
                UniqueKey = uniqueKey,
                Category = category,
                Family = family,
                Type = type,
                Properties = properties
            };
            
            Assert.Equal(objectGuid, dto.ObjectGuid);
            Assert.Equal(uniqueKey, dto.UniqueKey);
            Assert.Equal(category, dto.Category);
            Assert.Equal(family, dto.Family);
            Assert.Equal(type, dto.Type);
            Assert.Equal(properties, dto.Properties);
        }
    }
    
    public static class DataExtractorHelper
    {
        public static bool TryExtractGuid(string revitUniqueId, out Guid objectGuid)
        {
            objectGuid = Guid.Empty;
            
            if (string.IsNullOrEmpty(revitUniqueId))
                return false;
            
            if (revitUniqueId.Length >= 36)
            {
                string guidPortion = revitUniqueId.Substring(0, 36);
                if (Guid.TryParse(guidPortion, out objectGuid))
                {
                    return true;
                }
            }
            
            if (Guid.TryParse(revitUniqueId, out objectGuid))
            {
                return true;
            }
            
            return false;
        }
        
        public static string GenerateUniqueKey(string category, string family, string type)
        {
            string normalizedCategory = NormalizeForKey(category ?? "Unknown");
            string normalizedFamily = NormalizeForKey(family ?? "Unknown");
            string normalizedType = NormalizeForKey(type ?? "Unknown");
            
            string combined = $"{normalizedCategory}_{normalizedFamily}_{normalizedType}";
            
            string hash = GetShortHash(combined);
            
            return $"{combined}_{hash}";
        }
        
        private static string NormalizeForKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "unknown";
            
            string normalized = input.ToLowerInvariant();
            
            StringBuilder sb = new StringBuilder();
            foreach (char c in normalized)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else if (char.IsWhiteSpace(c) || c == '-' || c == '_')
                    sb.Append('_');
            }
            
            string result = sb.ToString();
            while (result.Contains("__"))
                result = result.Replace("__", "_");
            
            return result.Trim('_');
        }
        
        private static string GetShortHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha256.ComputeHash(bytes);
                
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < Math.Min(4, hash.Length); i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                
                return sb.ToString();
            }
        }
    }
    
    public class UnifiedObjectDto
    {
        public Guid? ObjectGuid { get; set; }
        public string UniqueKey { get; set; }
        public string Category { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string Properties { get; set; }
    }
}
