using System;

namespace DXBase.Models
{
    /// <summary>
    /// Unified Object DTO for FastAPI v1.1.0 compatibility
    /// Implements dual-identity pattern: object_guid + unique_key
    /// 
    /// Design:
    /// - object_guid: Extracted GUID from Revit UniqueId (nullable for non-GUID cases)
    /// - unique_key: Deterministic hash for upsert logic (always populated)
    /// - Replaces legacy ObjectRecord.object_id single-identity pattern
    /// 
    /// Related: database/migrations/2025_10_30__unified_objects_identity_fix.sql
    /// </summary>
    public class UnifiedObjectDto
    {
        /// <summary>
        /// GUID extracted from Revit UniqueId (Nullable)
        /// Format: "12345678-1234-5678-9012-123456789abc" (from "GUID-LocalId")
        /// </summary>
        public Guid? ObjectGuid { get; set; }

        /// <summary>
        /// Deterministic unique key for upsert logic (Required)
        /// Generated from: category + family + type + source-specific hash
        /// Example: "walls_basicwall_generic200mm_abc123"
        /// </summary>
        public string UniqueKey { get; set; }

        /// <summary>
        /// BIM object category (e.g., "Walls", "Doors", "Windows")
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Family name (e.g., "Basic Wall", "M_Door-Single")
        /// </summary>
        public string Family { get; set; }

        /// <summary>
        /// Type name (e.g., "Generic - 200mm", "0915 x 2134mm")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// All parameters serialized as JSON
        /// Provides flexibility for source-specific properties
        /// </summary>
        public string Properties { get; set; }

        /// <summary>
        /// 3D bounding box (JSON) - optional
        /// Used for spatial queries and visualization
        /// </summary>
        public string BoundingBox { get; set; }
    }
}
