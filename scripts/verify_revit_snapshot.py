"""
Revit 스냅샷 연결 확인 스크립트
- 프로젝트 코드 확인
- Revit 모델 버전 매핑 확인
- 리비전 기본 정보 확인
- 통합 객체 적재 확인
"""
import asyncpg
import asyncio
from datetime import datetime


async def verify_revit_snapshot():
    """Revit 스냅샷 데이터 검증"""

    # 데이터베이스 연결
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
        # 1) 프로젝트 코드 확인
        # ========================================
        print("1️⃣  프로젝트 코드 확인")
        print("-" * 80)

        projects = await conn.fetch("""
            SELECT code, name, created_at
            FROM projects
            WHERE name LIKE '%배관테스트%'
        """)

        if projects:
            for proj in projects:
                print(f"   ✅ 프로젝트 발견:")
                print(f"      코드: {proj['code']}")
                print(f"      이름: {proj['name']}")
                print(f"      생성일: {proj['created_at']}")
        else:
            print("   ⚠️  '배관테스트' 관련 프로젝트를 찾을 수 없습니다!")
            print("      → Revit에서 프로젝트를 먼저 생성하세요.")

        # 모든 프로젝트 목록 표시
        print("\n   📋 전체 프로젝트 목록:")
        all_projects = await conn.fetch("""
            SELECT code, name, created_at
            FROM projects
            ORDER BY created_at DESC
        """)
        for i, proj in enumerate(all_projects, 1):
            print(f"      {i}. {proj['code']} - {proj['name']} ({proj['created_at']})")

        print()

        # ========================================
        # 2) Revit 모델 버전 매핑 확인
        # ========================================
        print("2️⃣  Revit 모델 버전 매핑 확인")
        print("-" * 80)

        # revision_versions 테이블이 있는지 확인
        table_exists = await conn.fetchval("""
            SELECT EXISTS (
                SELECT FROM information_schema.tables
                WHERE table_schema = 'public'
                AND table_name = 'revision_versions'
            )
        """)

        if not table_exists:
            print("   ⚠️  'revision_versions' 테이블이 존재하지 않습니다.")
            print("      → 이 테이블은 선택적 기능일 수 있습니다.")
            print()

            # 대신 revisions 테이블 직접 확인
            print("   📊 revisions 테이블 직접 확인:")
            revisions = await conn.fetch("""
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
                WHERE r.source_type = 'revit'
                ORDER BY r.created_at DESC
                LIMIT 5
            """)

            if revisions:
                for rev in revisions:
                    print(f"\n      Revision ID: {rev['id']}")
                    print(f"      프로젝트: {rev['project_code']} - {rev['project_name']}")
                    print(f"      리비전 번호: {rev['revision_number']}")
                    print(f"      소스 타입: {rev['source_type']}")
                    print(f"      생성일: {rev['created_at']}")
                    if rev['metadata']:
                        print(f"      메타데이터: {rev['metadata']}")
            else:
                print("      ⚠️  Revit 리비전이 없습니다.")
        else:
            # 특정 모델 버전 확인
            model_version = "프로젝트 이름_20251021_142105"
            print(f"   🔍 모델 버전 검색: {model_version}")

            version_info = await conn.fetch("""
                SELECT model_version, revision_id, source_type,
                       source_file_path, extracted_at
                FROM revision_versions
                WHERE model_version = $1
            """, model_version)

            if version_info:
                for info in version_info:
                    print(f"      ✅ 모델 버전 발견:")
                    print(f"         버전: {info['model_version']}")
                    print(f"         Revision ID: {info['revision_id']}")
                    print(f"         소스 타입: {info['source_type']}")
                    print(f"         파일 경로: {info['source_file_path']}")
                    print(f"         추출 시각: {info['extracted_at']}")
            else:
                print(f"      ⚠️  모델 버전 '{model_version}'을 찾을 수 없습니다.")

            # 모든 모델 버전 목록
            print("\n   📋 전체 모델 버전 목록:")
            all_versions = await conn.fetch("""
                SELECT model_version, source_type, extracted_at
                FROM revision_versions
                ORDER BY extracted_at DESC
                LIMIT 10
            """)

            if all_versions:
                for i, ver in enumerate(all_versions, 1):
                    print(f"      {i}. {ver['model_version']} ({ver['source_type']}) - {ver['extracted_at']}")
            else:
                print("      ⚠️  모델 버전이 없습니다.")

        print()

        # ========================================
        # 3) 리비전 기본 정보 확인
        # ========================================
        print("3️⃣  리비전 기본 정보 확인")
        print("-" * 80)

        if table_exists:
            revision_info = await conn.fetchrow("""
                SELECT
                    r.id,
                    r.project_id,
                    p.code AS project_code,
                    p.name AS project_name,
                    r.revision_number,
                    r.source_type,
                    r.total_objects,
                    r.revit_objects,
                    r.navisworks_objects,
                    r.created_at
                FROM revisions r
                JOIN projects p ON r.project_id = p.id
                WHERE r.id = (
                    SELECT revision_id
                    FROM revision_versions
                    WHERE model_version = $1
                )
            """, model_version)

            if revision_info:
                print(f"   ✅ 리비전 정보:")
                print(f"      Revision ID: {revision_info['id']}")
                print(f"      프로젝트 ID: {revision_info['project_id']}")
                print(f"      프로젝트: {revision_info['project_code']} - {revision_info['project_name']}")
                print(f"      리비전 번호: {revision_info['revision_number']}")
                print(f"      소스 타입: {revision_info['source_type']}")
                print(f"      총 객체 수: {revision_info['total_objects']}")
                print(f"      Revit 객체 수: {revision_info['revit_objects']}")
                print(f"      Navisworks 객체 수: {revision_info['navisworks_objects']}")
                print(f"      생성일: {revision_info['created_at']}")
            else:
                print(f"   ⚠️  해당 모델 버전의 리비전을 찾을 수 없습니다.")
        else:
            # 최신 Revit 리비전 정보 표시
            print("   📊 최신 Revit 리비전 정보:")
            latest_revision = await conn.fetchrow("""
                SELECT
                    r.id,
                    r.project_id,
                    p.code AS project_code,
                    p.name AS project_name,
                    r.revision_number,
                    r.source_type,
                    r.created_at,
                    (SELECT COUNT(*) FROM unified_objects uo
                     WHERE uo.revision_id = r.id) AS object_count
                FROM revisions r
                JOIN projects p ON r.project_id = p.id
                WHERE r.source_type = 'revit'
                ORDER BY r.created_at DESC
                LIMIT 1
            """)

            if latest_revision:
                print(f"      Revision ID: {latest_revision['id']}")
                print(f"      프로젝트: {latest_revision['project_code']} - {latest_revision['project_name']}")
                print(f"      리비전 번호: {latest_revision['revision_number']}")
                print(f"      소스 타입: {latest_revision['source_type']}")
                print(f"      객체 수: {latest_revision['object_count']}")
                print(f"      생성일: {latest_revision['created_at']}")
            else:
                print("      ⚠️  Revit 리비전이 없습니다.")

        print()

        # ========================================
        # 4) 통합 객체 적재 확인
        # ========================================
        print("4️⃣  통합 객체 적재 확인")
        print("-" * 80)

        if table_exists:
            # 특정 모델 버전의 객체 수 확인
            object_count = await conn.fetchval("""
                SELECT COUNT(*) AS unified_objects_revit
                FROM unified_objects
                WHERE revision_id = (
                    SELECT revision_id
                    FROM revision_versions
                    WHERE model_version = $1
                )
                AND source_type = 'revit'
            """, model_version)

            print(f"   📦 모델 버전 '{model_version}':")
            print(f"      Revit 통합 객체 수: {object_count if object_count else 0}")

            if object_count and object_count > 0:
                print(f"      ✅ 객체가 정상적으로 적재되었습니다!")
            else:
                print(f"      ⚠️  객체가 적재되지 않았습니다.")
        else:
            # 전체 통합 객체 통계
            print("   📊 전체 통합 객체 통계:")

            stats = await conn.fetch("""
                SELECT
                    r.source_type,
                    p.code AS project_code,
                    COUNT(*) AS object_count,
                    MAX(r.created_at) AS latest_update
                FROM unified_objects uo
                JOIN revisions r ON uo.revision_id = r.id
                JOIN projects p ON r.project_id = p.id
                GROUP BY r.source_type, p.code
                ORDER BY latest_update DESC
            """)

            if stats:
                for stat in stats:
                    print(f"\n      프로젝트: {stat['project_code']}")
                    print(f"      소스: {stat['source_type']}")
                    print(f"      객체 수: {stat['object_count']:,}")
                    print(f"      최종 업데이트: {stat['latest_update']}")
            else:
                print("      ⚠️  통합 객체가 없습니다.")

        print()

        # ========================================
        # 5) 추가: 카테고리별 객체 분포
        # ========================================
        print("5️⃣  추가: Revit 객체 카테고리별 분포")
        print("-" * 80)

        category_stats = await conn.fetch("""
            SELECT
                uo.category,
                COUNT(*) AS count
            FROM unified_objects uo
            JOIN revisions r ON uo.revision_id = r.id
            WHERE r.source_type = 'revit'
            GROUP BY uo.category
            ORDER BY count DESC
            LIMIT 10
        """)

        if category_stats:
            print("   📊 상위 10개 카테고리:")
            for i, cat in enumerate(category_stats, 1):
                category_name = cat['category'] if cat['category'] else '(미분류)'
                print(f"      {i:2d}. {category_name:30s} : {cat['count']:,}개")
        else:
            print("   ⚠️  카테고리 통계가 없습니다.")

        print()

        # ========================================
        # 요약
        # ========================================
        print("=" * 80)
        print("📊 검증 요약")
        print("=" * 80)

        # 전체 통계
        total_projects = await conn.fetchval("SELECT COUNT(*) FROM projects")
        total_revisions = await conn.fetchval("SELECT COUNT(*) FROM revisions WHERE source_type = 'revit'")
        total_objects = await conn.fetchval("""
            SELECT COUNT(*)
            FROM unified_objects uo
            JOIN revisions r ON uo.revision_id = r.id
            WHERE r.source_type = 'revit'
        """)

        print(f"   총 프로젝트 수: {total_projects}")
        print(f"   총 Revit 리비전 수: {total_revisions}")
        print(f"   총 Revit 객체 수: {total_objects:,}")
        print()

        # 진단
        if total_projects == 0:
            print("   ⚠️  프로젝트가 없습니다. Revit에서 프로젝트를 생성하세요.")
        elif total_revisions == 0:
            print("   ⚠️  Revit 리비전이 없습니다. Revit에서 데이터를 업로드하세요.")
        elif total_objects == 0:
            print("   ⚠️  Revit 객체가 없습니다. 데이터 업로드를 확인하세요.")
        else:
            print("   ✅ Revit 데이터가 정상적으로 적재되어 있습니다!")

        print()

    finally:
        await conn.close()


async def main():
    """메인 함수"""
    await verify_revit_snapshot()
    print("=" * 80)
    print("✅ 검증 완료")
    print("=" * 80 + "\n")


if __name__ == "__main__":
    asyncio.run(main())
