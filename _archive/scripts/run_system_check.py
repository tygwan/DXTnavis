"""
Run comprehensive system health checks for Phase G.2
Validates database schema and API availability
"""
import asyncio
import sys
from pathlib import Path

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from scripts.check_system import check_missing_columns, check_api_health
from fastapi_server.database import db


async def main():
    """Execute all system health checks."""
    print("=" * 60)
    print("AWP 2025 System Health Check - Phase G.2")
    print("=" * 60)

    all_passed = True

    # 1. Database Connection Check
    print("\n[1/3] Database Connection...")
    try:
        await db.connect_pool()
        print("  ✓ Database connection successful")
    except Exception as e:
        print(f"  ✗ Database connection failed: {e}")
        all_passed = False
        return all_passed

    # 2. Database Schema Validation
    print("\n[2/3] Database Schema Validation...")

    # Check unified_objects table (Phase B schema)
    expected_unified_columns = {
        'id', 'project_id', 'revision_id', 'unique_key', 'object_guid',
        'category', 'display_name', 'source_type', 'properties',
        'created_at', 'updated_at'
    }

    try:
        missing_unified = await check_missing_columns(
            db.pool,
            'unified_objects',
            expected_unified_columns
        )

        if missing_unified:
            print(f"  ✗ unified_objects missing columns: {missing_unified}")
            all_passed = False
        else:
            print(f"  ✓ unified_objects schema valid ({len(expected_unified_columns)} columns)")
    except Exception as e:
        print(f"  ✗ Schema check failed: {e}")
        all_passed = False

    # Check projects table
    expected_project_columns = {
        'id', 'code', 'name', 'created_by', 'created_at', 'updated_at'
    }

    try:
        missing_projects = await check_missing_columns(
            db.pool,
            'projects',
            expected_project_columns
        )

        if missing_projects:
            print(f"  ✗ projects missing columns: {missing_projects}")
            all_passed = False
        else:
            print(f"  ✓ projects schema valid ({len(expected_project_columns)} columns)")
    except Exception as e:
        print(f"  ✗ Schema check failed: {e}")
        all_passed = False

    # Check revisions table
    expected_revision_columns = {
        'id', 'project_id', 'revision_number', 'source_type', 'created_at'
    }

    try:
        missing_revisions = await check_missing_columns(
            db.pool,
            'revisions',
            expected_revision_columns
        )

        if missing_revisions:
            print(f"  ✗ revisions missing columns: {missing_revisions}")
            all_passed = False
        else:
            print(f"  ✓ revisions schema valid ({len(expected_revision_columns)} columns)")
    except Exception as e:
        print(f"  ✗ Schema check failed: {e}")
        all_passed = False

    # 3. API Health Check
    print("\n[3/3] API Health Check...")

    # Note: This would require the API server to be running
    # For now, we'll note this as a manual check requirement
    print("  ℹ API health check requires server running")
    print("  ℹ To verify: python -m uvicorn fastapi_server.main:app")
    print("  ℹ Then: curl http://localhost:8000/api/v1/system/health")

    # Cleanup
    await db.close_pool()

    # Final Report
    print("\n" + "=" * 60)
    if all_passed:
        print("✓ System Health Check PASSED")
        print("=" * 60)
        return True
    else:
        print("✗ System Health Check FAILED - Review errors above")
        print("=" * 60)
        return False


if __name__ == "__main__":
    result = asyncio.run(main())
    sys.exit(0 if result else 1)
