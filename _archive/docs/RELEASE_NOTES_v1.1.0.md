# Release Notes - AWP 2025 v1.1.0

**Release Date**: 2025-11-01
**Version**: 1.1.0
**Codename**: Dual-Identity Integration

---

## 🎯 Overview

This release introduces the **Dual-Identity Pattern** for BIM data integration, enabling seamless interoperability between Revit and Navisworks through a unified backend schema. All components have been developed using strict **Test-Driven Development (TDD)** methodology, ensuring robust and reliable functionality.

---

## ✨ Key Features

### 1. Dual-Identity Schema (Phase A & B)

The unified backend now supports **two identifier types** for maximum flexibility:

- **`unique_key`**: Semantic identifier for human-readable tracking
- **`object_guid`**: UUID-based identifier for system-wide uniqueness

**Benefits**:
- Seamless matching across BIM tools (Revit, Navisworks, IFC)
- Backward compatible with legacy `unique_id` field
- Flexible project detection with OR-based matching

**API Endpoints**:
- `POST /api/v1/ingest` - Unified data ingestion with automatic format detection
- `POST /api/v1/projects/detect-by-objects` - Smart project detection with caching

### 2. DXrevit Plugin V2 (Phase C & D)

Enhanced Revit plugin with dual-identity support:

- **DataExtractorV2**: Extracts both `unique_key` and `object_guid` from Revit elements
- **SnapshotViewModelV2**: Updated UI for Phase B payload format
- **Project & Revision Managers**: Centralized metadata management
- **TDD Coverage**: 6 passing tests validating dual-identity extraction

### 3. DXnavis Plugin (Phase E)

New Navisworks integration capabilities:

- **CSV Hierarchy Uploader**: Parse and process Navisworks hierarchy exports
- **Object Sampling**: Extract up to 100 object IDs for project detection
- **Property Normalization**: Handle Navisworks-specific property formats
- **TDD Coverage**: 4 passing tests for hierarchy upload workflow

### 4. Performance Excellence (Phase G)

Exceptional performance validated through comprehensive testing:

| Metric | Threshold | Actual | Performance |
|--------|-----------|--------|-------------|
| **Ingest Throughput** | 100 obj / 5s | 100 obj / 0.022s | **227x faster** |
| **Detection Latency (p95)** | < 200ms | 3.28ms | **61x faster** |
| **E2E Workflow** | Manual | Automated | **100% validated** |

### 5. System Reliability (Phase F)

Production-ready monitoring and validation:

- **Automated Health Checks**: Database schema and API validation
- **Deployment Utilities**: Backup generation and system verification
- **Schema Validation**: Comprehensive column and constraint checking

---

## 🔧 Technical Improvements

### Database
- **New Constraint**: `(revision_id, source_type, unique_key)` for dual-identity uniqueness
- **View Optimization**: `v_unified_objects_latest` for fast latest-revision queries
- **Connection Pooling**: asyncpg with retry mechanism and exponential backoff
- **Performance**: Batch upsert operations with ON CONFLICT

### API
- **Auto-Detection**: Payload format automatically detected (legacy vs. Phase B)
- **Response Caching**: 5-minute TTL for detection endpoint
- **Standardized Schemas**: IngestResponseV2 and DetectResponse
- **Error Handling**: Comprehensive with structured error codes

### Testing
- **13 Total Tests**: 10 unit + 2 performance + 1 integration
- **TDD Methodology**: Strict Red → Green → Refactor cycle
- **Performance Validation**: Throughput and latency thresholds verified
- **E2E Validation**: Complete Revit → API → DB → Navisworks workflow

---

## 📊 Performance Metrics

### Ingest Performance
- **Throughput**: 4,605 objects/second
- **Processing Time**: 22ms for 100 objects
- **Batch Size**: Tested up to 100 objects
- **Database**: PostgreSQL with asyncpg connection pooling

### Detection Performance
- **p50 Latency**: 2.25ms
- **p95 Latency**: 3.28ms
- **p99 Latency**: 4.61ms
- **Query Sizes**: 5, 10, 20 objects tested
- **Caching**: Response caching enabled (TTL=300s)

### End-to-End Workflow
- **Ingestion**: ✓ 20 objects stored successfully
- **Detection**: ✓ 10/10 query objects matched (100% coverage)
- **Dual-Identity**: ✓ Both identifier types verified
- **Confidence**: 50% (10/20 objects matched)

---

## 🔄 Migration Guide

### For Existing Users

**Automatic Migration**:
- Legacy `unique_id` fields automatically promoted to `unique_key`
- No action required for existing integrations
- Both old and new APIs supported

**Recommended Updates**:
1. Update DXrevit plugin to V2 for dual-identity extraction
2. Update Navisworks integration to use new detection API
3. Test dual-identity matching with sample data

### Database Schema Updates

If upgrading from v0.1.0, run migration scripts:

```bash
# Apply Phase B schema migration
psql -U your_user -d your_db -f database/migrations/2025_10_30__add_dual_identity.sql

# Verify schema with health check
python scripts/run_system_check.py
```

---

## 📦 What's Included

### Backend (fastapi_server/)
- Dual-identity ingest API (IngestRequestV2)
- Project detection API with caching
- Unified objects schema with latest-revision view
- Health check endpoint
- Backward compatibility layer

### DXrevit Plugin
- DataExtractorV2 (dual-identity extraction)
- SnapshotViewModelV2 (Phase B UI)
- ProjectManager service
- RevisionManager service

### DXnavis Plugin
- HierarchyUploader service
- CSV parsing utilities
- Property normalization
- TestableViewModel pattern

### Scripts & Utilities
- `scripts/check_system.py` - Health validation
- `scripts/deploy_all.py` - Deployment automation
- `scripts/run_system_check.py` - System verification

### Tests
- `tests/perf/test_ingest_throughput.py`
- `tests/perf/test_detection_latency.py`
- `tests/integration/test_end_to_end.py`
- DXrevit.Tests (6 tests for dual-identity)
- DXnavis.Tests (4 tests for hierarchy upload)

---

## 🐛 Bug Fixes

- Fixed asyncpg pool reference in health check scripts (`_pool` → `pool`)
- Corrected API response field names (`objects_processed` → `object_count`)
- Resolved pytest module import issues with pythonpath configuration

---

## 📝 Documentation

### New Documents
- **PERFORMANCE_METRICS_G.md**: Comprehensive performance analysis
- **RELEASE_NOTES_v1.1.0.md**: This document
- **CHANGELOG.md**: Updated with Phase A-G changes

### Updated Documents
- **TODO.md**: Phase A-G completion status
- **docs/techspec.md**: Dual-identity schema specification
- **docs/plan.md**: Implementation roadmap

---

## ✅ Testing & Validation

All tests passing:
```
tests/perf/test_detection_latency.py::test_detection_p95_under_threshold PASSED
tests/perf/test_ingest_throughput.py::test_ingest_batch_processing_time PASSED
tests/integration/test_end_to_end.py::test_revit_to_navisworks_roundtrip PASSED

3 passed in 1.45s
```

System health check:
```
[1/3] Database Connection... ✓
[2/3] Database Schema Validation... ✓
[3/3] API Health Check... ℹ (Manual verification)

System Health Check PASSED
```

---

## 🚀 Getting Started

### Quick Start

1. **Update Backend**:
   ```bash
   cd fastapi_server
   pip install -r requirements.txt
   python -m uvicorn main:app --reload
   ```

2. **Verify System**:
   ```bash
   python scripts/run_system_check.py
   ```

3. **Run Performance Tests**:
   ```bash
   pytest tests/perf/ tests/integration/ -v
   ```

### API Usage Example

**Ingest Revit Data** (Phase B format):
```python
import httpx

async with httpx.AsyncClient() as client:
    response = await client.post("http://localhost:8000/api/v1/ingest", json={
        "project_code": "MY_PROJECT",
        "revision_number": 1,
        "source_type": "revit",
        "objects": [
            {
                "unique_key": "wall-exterior-001",
                "object_guid": "12345678-1234-1234-1234-123456789012",
                "category": "Walls",
                "name": "Basic Wall",
                "properties": {"Height": "3000", "Width": "200"},
                "source_type": "revit"
            }
        ]
    })
    print(response.json())
```

**Detect Project** (Navisworks workflow):
```python
response = await client.post("http://localhost:8000/api/v1/projects/detect-by-objects", json={
    "object_ids": ["wall-exterior-001", "12345678-1234-1234-1234-123456789012"],
    "min_confidence": 0.5,
    "max_candidates": 10
})
print(response.json())
```

---

## 🔮 What's Next

### Planned for v1.2.0
- Real-time synchronization between Revit and Navisworks
- Advanced analytics and reporting dashboard
- Multi-user collaboration features
- Webhook notifications for data changes

### Known Limitations
- API health check requires manual server verification
- Detection cache is in-memory (not persistent across restarts)
- Maximum 1000 object IDs per detection query

---

## 🙏 Acknowledgments

This release was developed following Kent Beck's **Test-Driven Development** and **Tidy First** principles, ensuring high code quality and maintainability throughout all phases.

**Development Methodology**: TDD Red → Green → Refactor cycle strictly followed across all 7 phases (A-G).

---

## 📞 Support

For issues, questions, or feedback:
- **Issue Tracker**: [GitHub Issues](https://github.com/your-org/dx-platform/issues)
- **Documentation**: See `docs/` folder for technical specifications
- **Performance Metrics**: `docs/PERFORMANCE_METRICS_G.md`

---

**Thank you for using AWP 2025 BIM Data Integration System!**
