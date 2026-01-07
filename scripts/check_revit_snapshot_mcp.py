"""
Revit 스냅샷 연결 확인 스크립트 (MCP 대체용)
- revision_versions 테이블 확인
- 리비전 기본 정보 확인
- 통합 객체 적재 확인
"""
import asyncpg
import asyncio


async def check_revit_snapshot():
    """Revit 스냅샷 데이터 검증"""

    conn = await asyncpg.connect(
        host='localhost',
        port=5432,
        user='postgres',
        password='123456',
        database='DX_platform'
    )

    try:
        print("\n" + "=" * 80)
        print("🔍 Revit 스냅샷 연결 확인")
        print("=" * 80 + "\n")

        # ========================================
        # 1) revision_versions 테이블 존재 확인
        # ========================================
        print("1️⃣  revision_versions 테이블 확인")
        print("-" * 80)

        table_exists = await conn.fetchval("""
            SELECT EXISTS (
                SELECT FROM information_schema.tables
                WHERE table_schema = 'public'
                AND table_name = 'revision_versions'
            )
        """)

        if table_exists:
            print("   ✅ revision_versions 테이블 존재")

            # 모든 모델 버전 조회
            all_versions = await conn.fetch("""
                SELECT model_version, source_type, extracted_at
                FROM revision_versions
                ORDER BY extracted_at DESC
                LIMIT 10
            """)

            if all_versions:
                print(f"\n   📋 최근 10개 모델 버전:")
                for i, ver in enumerate(all_versions, 1):
                    print(f"      {i}. {ver['model_version']}")
                    print(f"         타입: {ver['source_type']}, 추출: {ver['extracted_at']}")
            else:
                print("   ⚠️  모델 버전이 없습니다.")
        else:
            print("   ⚠️  revision_versions 테이블이 존재하지 않습니다.")
            print("      → 이 테이블은 선택적 기능일 수 있습니다.")
            table_exists = False

        print()

        # ========================================
        # 2) 특정 모델 버전 확인
        # ========================================
        model_version = "프로젝트 이름_20251020_175529"
        print(f"2️⃣  모델 버전 '{model_version}' 확인")
        print("-" * 80)

        if table_exists:
            version_info = await conn.fetchrow("""
                SELECT model_version, revision_id, source_type,
                       source_file_path, extracted_at
                FROM revision_versions
                WHERE model_version = $1
            """, model_version)

            if version_info:
                print(f"   ✅ 모델 버전 발견!")
                print(f"      버전: {version_info['model_version']}")
                print(f"      Revision ID: {version_info['revision_id']}")
                print(f"      소스 타입: {version_info['source_type']}")
                print(f"      파일 경로: {version_info['source_file_path']}")
                print(f"      추출 시각: {version_info['extracted_at']}")
                revision_id = version_info['revision_id']
            else:
                print(f"   ⚠️  모델 버전 '{model_version}'을 찾을 수 없습니다.")
                print("\n   💡 사용 가능한 모델 버전:")
                available = await conn.fetch("""
                    SELECT model_version
                    FROM revision_versions
                    WHERE source_type = 'revit'
                    ORDER BY extracted_at DESC
                    LIMIT 5
                """)
                for ver in available:
                    print(f"      - {ver['model_version']}")
                revision_id = None
        else:
            # revision_versions 테이블이 없으면 최신 리비전 사용
            print("   ℹ️  revision_versions 테이블 없음, 최신 Revit 리비전 사용")
            latest = await conn.fetchrow("""
                SELECT id, revision_number, created_at
                FROM revisions
                WHERE source_type = 'revit'
                ORDER BY created_at DESC
                LIMIT 1
            """)
            if latest:
                revision_id = latest['id']
                print(f"   ✅ 최신 Revit 리비전 ID: {revision_id}")
                print(f"      리비전 번호: {latest['revision_number']}")
                print(f"      생성일: {latest['created_at']}")
            else:
                print("   ⚠️  Revit 리비전이 없습니다.")
                revision_id = None

        print()

        # ========================================
        # 3) 리비전 기본 정보 확인
        # ========================================
        print("3️⃣  리비전 기본 정보 확인")
        print("-" * 80)

        if revision_id:
            revision_info = await conn.fetchrow("""
                SELECT
                    r.id,
                    r.project_id,
                    p.code AS project_code,
                    p.name AS project_name,
                    r.revision_number,
                    r.source_type,
                    r.created_at,
                    r.metadata
                FROM revisions r
                JOIN projects p ON r.project_id = p.id
                WHERE r.id = $1
            """, revision_id)

            if revision_info:
                print(f"   ✅ 리비전 정보:")
                print(f"      Revision ID: {revision_info['id']}")
                print(f"      프로젝트 ID: {revision_info['project_id']}")
                print(f"      프로젝트 코드: {revision_info['project_code']}")
                print(f"      프로젝트 이름: {revision_info['project_name']}")
                print(f"      리비전 번호: {revision_info['revision_number']}")
                print(f"      소스 타입: {revision_info['source_type']}")
                print(f"      생성일: {revision_info['created_at']}")
                if revision_info['metadata']:
                    print(f"      메타데이터: {revision_info['metadata']}")

                # total_objects, revit_objects, navisworks_objects 컬럼이 있는지 확인
                columns = await conn.fetch("""
                    SELECT column_name
                    FROM information_schema.columns
                    WHERE table_name = 'revisions'
                    AND column_name IN ('total_objects', 'revit_objects', 'navisworks_objects')
                """)

                if columns:
                    print("\n   📊 객체 통계:")
                    stats = await conn.fetchrow("""
                        SELECT total_objects, revit_objects, navisworks_objects
                        FROM revisions
                        WHERE id = $1
                    """, revision_id)
                    if stats:
                        print(f"      총 객체 수: {stats.get('total_objects', 'N/A')}")
                        print(f"      Revit 객체 수: {stats.get('revit_objects', 'N/A')}")
                        print(f"      Navisworks 객체 수: {stats.get('navisworks_objects', 'N/A')}")
            else:
                print(f"   ⚠️  리비전 정보를 찾을 수 없습니다.")
        else:
            print("   ⚠️  확인할 리비전이 없습니다.")

        print()

        # ========================================
        # 4) 통합 객체 적재 확인
        # ========================================
        print("4️⃣  통합 객체 적재 확인")
        print("-" * 80)

        if revision_id:
            object_count = await conn.fetchval("""
                SELECT COUNT(*) AS unified_objects_revit
                FROM unified_objects
                WHERE revision_id = $1
                AND source_type = 'revit'
            """, revision_id)

            print(f"   📦 Revision {revision_id}:")
            print(f"      Revit 통합 객체 수: {object_count:,}")

            if object_count > 0:
                print(f"      ✅ 객체가 정상적으로 적재되었습니다!")

                # 카테고리별 분포
                print("\n   📊 카테고리별 분포 (상위 10개):")
                categories = await conn.fetch("""
                    SELECT category, COUNT(*) AS count
                    FROM unified_objects
                    WHERE revision_id = $1
                    AND source_type = 'revit'
                    GROUP BY category
                    ORDER BY count DESC
                    LIMIT 10
                """, revision_id)

                for i, cat in enumerate(categories, 1):
                    cat_name = cat['category'] if cat['category'] else '(미분류)'
                    print(f"      {i:2d}. {cat_name:30s} : {cat['count']:,}개")
            else:
                print(f"      ⚠️  객체가 적재되지 않았습니다.")
        else:
            print("   ⚠️  확인할 리비전이 없습니다.")

        print()

        # ========================================
        # 5) 전체 요약
        # ========================================
        print("=" * 80)
        print("📊 전체 요약")
        print("=" * 80)

        summary = await conn.fetchrow("""
            SELECT
                (SELECT COUNT(*) FROM projects) AS total_projects,
                (SELECT COUNT(*) FROM revisions WHERE source_type = 'revit') AS total_revit_revisions,
                (SELECT COUNT(*) FROM unified_objects uo
                 JOIN revisions r ON uo.revision_id = r.id
                 WHERE r.source_type = 'revit') AS total_revit_objects
        """)

        print(f"   총 프로젝트 수: {summary['total_projects']}")
        print(f"   총 Revit 리비전 수: {summary['total_revit_revisions']}")
        print(f"   총 Revit 객체 수: {summary['total_revit_objects']:,}")
        print()

        if summary['total_projects'] == 0:
            print("   ⚠️  프로젝트가 없습니다. Revit에서 프로젝트를 먼저 생성하세요.")
        elif summary['total_revit_revisions'] == 0:
            print("   ⚠️  Revit 리비전이 없습니다. Revit에서 데이터를 업로드하세요.")
        elif summary['total_revit_objects'] == 0:
            print("   ⚠️  Revit 객체가 없습니다. 데이터 업로드를 확인하세요.")
        else:
            print("   ✅ Revit 데이터가 정상적으로 적재되어 있습니다!")

        print()

    finally:
        await conn.close()


async def main():
    await check_revit_snapshot()
    print("=" * 80)
    print("✅ 확인 완료")
    print("=" * 80 + "\n")


if __name__ == "__main__":
    asyncio.run(main())
