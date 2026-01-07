"""
PostgreSQL 데이터베이스 상세 분석 스크립트
"""
import asyncio
import asyncpg
import json
from typing import Any, List, Optional


# 데이터베이스 연결 정보
DATABASE_URL = "postgresql://postgres:123456@localhost:5432/DX_platform"


async def get_connection():
    """데이터베이스 연결 생성"""
    return await asyncpg.connect(DATABASE_URL)


async def analyze_objects_by_category():
    """카테고리별 객체 수 분석"""
    print("\n=== 카테고리별 객체 수 분석 ===")
    query = """
        SELECT
            category,
            COUNT(*) as object_count,
            COUNT(DISTINCT family) as family_count,
            COUNT(DISTINCT type) as type_count,
            COUNT(activity_id) as with_activity_count
        FROM objects
        GROUP BY category
        ORDER BY object_count DESC
        LIMIT 20;
    """

    conn = await get_connection()
    try:
        results = await conn.fetch(query)

        print(f"\n{'카테고리':<30} {'객체수':<10} {'패밀리수':<10} {'타입수':<10} {'활동ID 할당':<15}")
        print("-" * 85)
        for row in results:
            print(f"{row['category']:<30} {row['object_count']:<10} {row['family_count']:<10} {row['type_count']:<10} {row['with_activity_count']:<15}")

        print(f"\n총 {len(results)}개 카테고리")

    finally:
        await conn.close()


async def analyze_model_versions():
    """모델 버전별 분석"""
    print("\n=== 모델 버전별 분석 ===")
    query = """
        SELECT
            o.model_version,
            COUNT(*) as total_objects,
            COUNT(DISTINCT o.category) as category_count,
            COUNT(o.activity_id) as objects_with_activity,
            m.timestamp,
            m.project_name
        FROM objects o
        LEFT JOIN metadata m ON o.model_version = m.model_version
        GROUP BY o.model_version, m.timestamp, m.project_name
        ORDER BY m.timestamp DESC;
    """

    conn = await get_connection()
    try:
        results = await conn.fetch(query)

        print(f"\n{'프로젝트명':<20} {'모델 버전':<25} {'총 객체수':<12} {'카테고리수':<12} {'활동ID 할당':<15} {'타임스탬프':<25}")
        print("-" * 130)
        for row in results:
            version = row['model_version'][:22] + "..." if len(row['model_version']) > 25 else row['model_version']
            project = row['project_name'][:17] + "..." if row['project_name'] and len(row['project_name']) > 20 else (row['project_name'] or 'N/A')
            print(f"{project:<20} {version:<25} {row['total_objects']:<12} {row['category_count']:<12} {row['objects_with_activity']:<15} {str(row['timestamp']):<25}")

        print(f"\n총 {len(results)}개 모델 버전")

    finally:
        await conn.close()


async def analyze_relationships():
    """관계 유형별 분석"""
    print("\n=== 관계 유형별 분석 ===")
    query = """
        SELECT
            relation_type,
            COUNT(*) as count,
            COUNT(DISTINCT source_object_id) as unique_sources,
            COUNT(DISTINCT target_object_id) as unique_targets
        FROM relationships
        GROUP BY relation_type
        ORDER BY count DESC;
    """

    conn = await get_connection()
    try:
        results = await conn.fetch(query)

        print(f"\n{'관계 유형':<20} {'관계 수':<12} {'소스 객체수':<15} {'타겟 객체수':<15}")
        print("-" * 65)
        for row in results:
            print(f"{row['relation_type']:<20} {row['count']:<12} {row['unique_sources']:<15} {row['unique_targets']:<15}")

        print(f"\n총 {len(results)}개 관계 유형")

    finally:
        await conn.close()


async def sample_properties():
    """객체 속성 샘플 조회"""
    print("\n=== 객체 속성 샘플 (Properties JSONB) ===")
    query = """
        SELECT
            object_id,
            category,
            family,
            type,
            properties
        FROM objects
        WHERE properties IS NOT NULL
        LIMIT 3;
    """

    conn = await get_connection()
    try:
        results = await conn.fetch(query)

        for i, row in enumerate(results, 1):
            print(f"\n--- 샘플 {i} ---")
            print(f"Object ID: {row['object_id']}")
            print(f"Category: {row['category']}")
            print(f"Family: {row['family']}")
            print(f"Type: {row['type']}")
            print(f"\nProperties (처음 5개):")

            props = row['properties']
            if isinstance(props, str):
                props = json.loads(props)

            for j, (key, value) in enumerate(props.items()):
                if j >= 5:
                    print(f"  ... (총 {len(props)}개 속성)")
                    break
                value_str = str(value)[:50] + "..." if len(str(value)) > 50 else str(value)
                print(f"  {key}: {value_str}")

    finally:
        await conn.close()


async def find_objects_with_activity():
    """활동 ID가 할당된 객체 조회"""
    print("\n=== 활동 ID가 할당된 객체 샘플 ===")
    query = """
        SELECT
            object_id,
            element_id,
            category,
            family,
            type,
            activity_id
        FROM objects
        WHERE activity_id IS NOT NULL
        LIMIT 10;
    """

    conn = await get_connection()
    try:
        results = await conn.fetch(query)

        if not results:
            print("활동 ID가 할당된 객체가 없습니다.")
            return

        print(f"\n{'Object ID':<40} {'Element ID':<12} {'Category':<20} {'Family':<20} {'Activity ID':<15}")
        print("-" * 110)
        for row in results:
            obj_id = row['object_id'][:37] + "..." if len(row['object_id']) > 40 else row['object_id']
            family = (row['family'][:17] + "...") if row['family'] and len(row['family']) > 20 else (row['family'] or 'N/A')
            print(f"{obj_id:<40} {row['element_id']:<12} {row['category']:<20} {family:<20} {row['activity_id'] or 'N/A':<15}")

        print(f"\n총 활동 ID 할당: {len(results)}개 표시")

    finally:
        await conn.close()


async def analyze_bounding_boxes():
    """Bounding Box 데이터 분석"""
    print("\n=== Bounding Box 데이터 분석 ===")
    query = """
        SELECT
            COUNT(*) as total,
            COUNT(bounding_box) as with_bbox,
            COUNT(*) - COUNT(bounding_box) as without_bbox
        FROM objects;
    """

    conn = await get_connection()
    try:
        result = await conn.fetchrow(query)

        print(f"\n총 객체 수: {result['total']:,}개")
        print(f"Bounding Box 있음: {result['with_bbox']:,}개 ({result['with_bbox']/result['total']*100:.1f}%)")
        print(f"Bounding Box 없음: {result['without_bbox']:,}개 ({result['without_bbox']/result['total']*100:.1f}%)")

        # Bounding Box 샘플 조회
        if result['with_bbox'] > 0:
            query_sample = """
                SELECT object_id, category, bounding_box
                FROM objects
                WHERE bounding_box IS NOT NULL
                LIMIT 2;
            """
            samples = await conn.fetch(query_sample)

            print("\n=== Bounding Box 샘플 ===")
            for i, row in enumerate(samples, 1):
                print(f"\n샘플 {i}:")
                print(f"Object ID: {row['object_id']}")
                print(f"Category: {row['category']}")
                bbox = row['bounding_box']
                if isinstance(bbox, str):
                    bbox = json.loads(bbox)
                print(f"Bounding Box: {json.dumps(bbox, indent=2, ensure_ascii=False)}")

    finally:
        await conn.close()


async def check_data_quality():
    """데이터 품질 검사"""
    print("\n=== 데이터 품질 검사 ===")

    conn = await get_connection()
    try:
        # NULL 값 검사
        null_check_query = """
            SELECT
                COUNT(*) FILTER (WHERE family IS NULL) as null_family,
                COUNT(*) FILTER (WHERE type IS NULL) as null_type,
                COUNT(*) FILTER (WHERE activity_id IS NULL) as null_activity,
                COUNT(*) FILTER (WHERE properties IS NULL) as null_properties,
                COUNT(*) as total
            FROM objects;
        """
        null_result = await conn.fetchrow(null_check_query)

        print("\n=== NULL 값 통계 ===")
        print(f"총 객체 수: {null_result['total']:,}개")
        print(f"Family NULL: {null_result['null_family']:,}개 ({null_result['null_family']/null_result['total']*100:.1f}%)")
        print(f"Type NULL: {null_result['null_type']:,}개 ({null_result['null_type']/null_result['total']*100:.1f}%)")
        print(f"Activity ID NULL: {null_result['null_activity']:,}개 ({null_result['null_activity']/null_result['total']*100:.1f}%)")
        print(f"Properties NULL: {null_result['null_properties']:,}개 ({null_result['null_properties']/null_result['total']*100:.1f}%)")

        # 중복 검사
        duplicate_check_query = """
            SELECT
                object_id,
                COUNT(*) as count
            FROM objects
            GROUP BY object_id
            HAVING COUNT(*) > 1
            LIMIT 5;
        """
        duplicates = await conn.fetch(duplicate_check_query)

        print("\n=== 중복 Object ID 검사 ===")
        if duplicates:
            print(f"중복된 Object ID 발견: {len(duplicates)}개")
            for row in duplicates:
                print(f"  {row['object_id']}: {row['count']}번 중복")
        else:
            print("✅ 중복된 Object ID 없음")

    finally:
        await conn.close()


async def main():
    """메인 함수"""
    print("=" * 100)
    print("PostgreSQL 데이터베이스 상세 분석")
    print("=" * 100)

    try:
        await analyze_model_versions()
        print("\n" + "=" * 100)

        await analyze_objects_by_category()
        print("\n" + "=" * 100)

        await analyze_relationships()
        print("\n" + "=" * 100)

        await sample_properties()
        print("\n" + "=" * 100)

        await find_objects_with_activity()
        print("\n" + "=" * 100)

        await analyze_bounding_boxes()
        print("\n" + "=" * 100)

        await check_data_quality()
        print("\n" + "=" * 100)

        print("\n분석 완료!")

    except Exception as e:
        print(f"\n오류 발생: {e}")
        raise


if __name__ == "__main__":
    asyncio.run(main())
