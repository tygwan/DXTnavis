"""
최신 Revit 리비전 상세 확인
"""
import asyncpg
import asyncio


async def check_latest_revision():
    """최신 리비전 상세 확인"""

    conn = await asyncpg.connect(
        host='localhost',
        port=5432,
        user='postgres',
        password='123456',
        database='DX_platform'
    )

    try:
        print("\n" + "=" * 80)
        print("🔍 최신 Revit 리비전 상세 확인")
        print("=" * 80 + "\n")

        # 실제 존재하는 모델 버전 사용
        model_version = "프로젝트 이름_20251021_142105"

        # ========================================
        # 1) 모델 버전 정보
        # ========================================
        print(f"1️⃣  모델 버전: {model_version}")
        print("-" * 80)

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
            print(f"   ⚠️  모델 버전을 찾을 수 없습니다.")
            return

        print()

        # ========================================
        # 2) 리비전 기본 정보
        # ========================================
        print("2️⃣  리비전 기본 정보")
        print("-" * 80)

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
            print(f"   Revision ID: {revision_info['id']}")
            print(f"   프로젝트 ID: {revision_info['project_id']}")
            print(f"   프로젝트 코드: {revision_info['project_code']}")
            print(f"   프로젝트 이름: {revision_info['project_name']}")
            print(f"   리비전 번호: {revision_info['revision_number']}")
            print(f"   소스 타입: {revision_info['source_type']}")
            print(f"   생성일: {revision_info['created_at']}")
            if revision_info['metadata']:
                print(f"   메타데이터: {revision_info['metadata']}")

        print()

        # ========================================
        # 3) 통합 객체 통계
        # ========================================
        print("3️⃣  통합 객체 통계")
        print("-" * 80)

        object_count = await conn.fetchval("""
            SELECT COUNT(*)
            FROM unified_objects
            WHERE revision_id = $1
            AND source_type = 'revit'
        """, revision_id)

        print(f"   📦 총 Revit 객체 수: {object_count:,}")

        if object_count > 0:
            print(f"   ✅ 객체가 정상적으로 적재되었습니다!")
        else:
            print(f"   ⚠️  객체가 적재되지 않았습니다.")

        print()

        # ========================================
        # 4) 카테고리별 분포
        # ========================================
        print("4️⃣  카테고리별 객체 분포")
        print("-" * 80)

        categories = await conn.fetch("""
            SELECT category, COUNT(*) AS count
            FROM unified_objects
            WHERE revision_id = $1
            AND source_type = 'revit'
            GROUP BY category
            ORDER BY count DESC
        """, revision_id)

        if categories:
            print(f"   총 {len(categories)}개 카테고리\n")
            for i, cat in enumerate(categories, 1):
                cat_name = cat['category'] if cat['category'] else '(미분류)'
                percentage = (cat['count'] / object_count * 100) if object_count > 0 else 0
                print(f"   {i:2d}. {cat_name:35s} : {cat['count']:5,}개 ({percentage:5.1f}%)")
        else:
            print("   ⚠️  카테고리 정보가 없습니다.")

        print()

        # ========================================
        # 5) 샘플 객체 확인
        # ========================================
        print("5️⃣  샘플 객체 (처음 5개)")
        print("-" * 80)

        samples = await conn.fetch("""
            SELECT id, unique_id, category, name, properties
            FROM unified_objects
            WHERE revision_id = $1
            AND source_type = 'revit'
            ORDER BY category, name
            LIMIT 5
        """, revision_id)

        if samples:
            for i, obj in enumerate(samples, 1):
                print(f"\n   {i}. {obj['category']} - {obj['name']}")
                print(f"      ID: {obj['id']}")
                print(f"      UniqueId: {obj['unique_id']}")
                if obj['properties']:
                    # 주요 속성 몇 개만 표시
                    import json
                    props = obj['properties'] if isinstance(obj['properties'], dict) else json.loads(obj['properties'])
                    key_props = list(props.keys())[:3]
                    for key in key_props:
                        print(f"      {key}: {props[key]}")
        else:
            print("   ⚠️  샘플 객체가 없습니다.")

        print()

    finally:
        await conn.close()


async def main():
    await check_latest_revision()
    print("=" * 80)
    print("✅ 확인 완료")
    print("=" * 80 + "\n")


if __name__ == "__main__":
    asyncio.run(main())
