from pydantic import BaseModel, Field
from typing import List, Optional, Dict, Any
from datetime import datetime


# Match DXrevit PascalCase payload

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

