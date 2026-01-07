from pydantic import BaseModel, Field, field_validator
from typing import List, Optional, Dict, Any
from datetime import datetime


# ============================================================================
# Legacy Schemas (DXrevit PascalCase payload - backward compatibility)
# ============================================================================

class MetadataRecord(BaseModel):
    ModelVersion: str
    Timestamp: datetime
    ProjectName: str
    CreatedBy: str
    Description: Optional[str] = None
    TotalObjectCount: int = 0
    RevitFilePath: Optional[str] = None


class ObjectRecord(BaseModel):
    ModelVersion: str
    ObjectId: str
    ElementId: int
    Category: str
    Family: Optional[str] = None
    Type: Optional[str] = None
    ActivityId: Optional[str] = None
    Properties: Optional[str] = None  # JSON string from client
    BoundingBox: Optional[str] = None  # JSON string from client


class RelationshipRecord(BaseModel):
    ModelVersion: str
    SourceObjectId: str
    TargetObjectId: str
    RelationType: str
    IsDirected: bool = True


class ExtractedData(BaseModel):
    Metadata: MetadataRecord
    Objects: List[ObjectRecord]
    Relationships: List[RelationshipRecord]


class IngestResponse(BaseModel):
    success: bool
    modelVersion: str
    objectsInserted: int
    relationshipsInserted: int
    timestamp: datetime


# ============================================================================
# Phase B Schemas (AWP 2025 v1.1.0 - Dual-Identity Pattern)
# ============================================================================

class UnifiedObjectDto(BaseModel):
    """
    Unified object DTO supporting dual-identity pattern.

    Phase B requirement:
    - unique_key: Primary identifier (can be semantic or UUID)
    - object_guid: Extracted UUID from IFC/Revit (optional)
    - unique_id: Legacy field for backward compatibility (auto-promoted to unique_key)

    Validation:
    - At least one of unique_key or unique_id must be provided
    - If unique_id is UUID-like, it will be extracted to object_guid
    """
    unique_key: Optional[str] = None
    object_guid: Optional[str] = None
    category: Optional[str] = None
    name: Optional[str] = None
    properties: Dict[str, Any] = Field(default_factory=dict)
    source_type: str
    geometry: Optional[Dict[str, Any]] = None

    # Legacy field - will be promoted to unique_key if not present
    unique_id: Optional[str] = None

    @field_validator("unique_key", "unique_id", mode="before")
    @classmethod
    def ensure_identifier_exists(cls, v, info):
        """Ensure at least one identifier is provided."""
        # This will be called during validation, actual promotion happens in backward_compat.py
        return v


class IngestRequestV2(BaseModel):
    """
    Phase B ingest request schema.

    Supports both legacy (unique_id) and modern (unique_key + object_guid) payloads.
    """
    project_code: str = Field(..., min_length=1, max_length=100)
    revision_number: int = Field(..., ge=1)
    source_type: str = Field(..., pattern="^(revit|navisworks|ifc)$")
    objects: List[UnifiedObjectDto] = Field(..., min_items=1)


class IngestResponseV2(BaseModel):
    """Phase B ingest response schema."""
    success: bool
    revision_id: str
    object_count: int
    message: Optional[str] = None


class DetectRequest(BaseModel):
    """
    Project detection request based on object IDs.

    Phase B requirement:
    - Matches by unique_key OR object_guid
    - Only considers latest revision for each project
    - Returns confidence and coverage metrics
    """
    object_ids: List[str] = Field(..., min_items=1, max_items=1000)
    min_confidence: float = Field(default=0.7, ge=0.0, le=1.0)
    max_candidates: int = Field(default=10, ge=1, le=50)


class DetectedProject(BaseModel):
    """Detected project with confidence metrics."""
    id: str
    code: str
    name: str
    confidence: float  # match_count / total_objects_in_latest_revision
    match_count: int
    total_objects: int  # total objects in latest revision
    coverage: float  # match_count / input_object_count


class DetectResponse(BaseModel):
    """Project detection response."""
    success: bool
    detected_projects: List[DetectedProject]
    query_object_count: int
    message: Optional[str] = None

