"""
Snowdon Towers 데이터 안전 삭제 스크립트
Python을 통한 대화형 삭제 프로세스
"""
import asyncio
import asyncpg
from datetime import datetime


# 데이터베이스 연결 정보
DATABASE_URL = "postgresql://postgres:123456@localhost:5432/DX_platform"


async def get_connection():
    """데이터베이스 연결"""
    return await asyncpg.connect(DATABASE_URL)


async def confirm_deletion():
    """사용자 확인"""
    print("\n⚠️  경고: Snowdon Towers 프로젝트 데이터를 삭제합니다.")
    print("이 작업은 되돌릴 수 없습니다!")
    print("\n삭제하기 전에 백업을 수행하는 것을 강력히 권장합니다.")

    response = input("\n계속하시겠습니까? (yes/no): ").strip().lower()
    return response == 'yes'


async def create_backup():
    """백업 파일 생성 안내"""
    print("\n" + "=" * 60)
    print("백업 권장")
    print("=" * 60)
    print("\n터미널에서 다음 명령어를 실행하여 백업하세요:")
    print("\npg_dump -h localhost -U postgres -d DX_platform \\")
    print(f"  > backup_before_delete_{datetime.now().strftime('%Y%m%d_%H%M%S')}.sql")

    response = input("\n백업을 완료하셨습니까? (yes/no): ").strip().lower()
    return response == 'yes'


async def preview_deletion():
    """삭제될 데이터 미리보기"""
    print("\n" + "=" * 60)
    print("삭제 대상 데이터 확인")
    print("=" * 60)

    conn = await get_connection()
    try:
        # 1. metadata 확인
        metadata_query = """
            SELECT
                project_name,
                model_version,
                total_object_count,
                created_at
            FROM metadata
            WHERE project_name = 'Snowdon Towers';
        """
        metadata_records = await conn.fetch(metadata_query)

        print("\n📋 Metadata 레코드:")
        for record in metadata_records:
            print(f"  프로젝트: {record['project_name']}")
            print(f"  버전: {record['model_version']}")
            print(f"  객체 수: {record['total_object_count']:,}개")
            print(f"  생성일: {record['created_at']}")

        # 2. 삭제 통계
        stats_query = """
            SELECT
                'metadata' AS table_name,
                COUNT(*) AS count
            FROM metadata
            WHERE project_name = 'Snowdon Towers'

            UNION ALL

            SELECT
                'objects' AS table_name,
                COUNT(*) AS count
            FROM objects
            WHERE model_version LIKE 'Snowdon Towers%'

            UNION ALL

            SELECT
                'relationships' AS table_name,
                COUNT(*) AS count
            FROM relationships
            WHERE model_version LIKE 'Snowdon Towers%';
        """
        stats = await conn.fetch(stats_query)

        print("\n📊 삭제될 데이터 통계:")
        total = 0
        for stat in stats:
            count = stat['count']
            total += count
            print(f"  {stat['table_name']:<20} {count:>10,}개")

        print(f"  {'─' * 20} {'-' * 10}")
        print(f"  {'총계':<20} {total:>10,}개")

        return total > 0

    finally:
        await conn.close()


async def delete_snowdon_towers():
    """Snowdon Towers 데이터 삭제 (트랜잭션)"""
    print("\n" + "=" * 60)
    print("데이터 삭제 진행 중...")
    print("=" * 60)

    conn = await get_connection()

    try:
        # 트랜잭션 시작
        async with conn.transaction():
            print("\n⏳ 트랜잭션 시작...")

            # 1. relationships 삭제
            print("  1/3: relationships 삭제 중...")
            delete_relationships = """
                DELETE FROM relationships
                WHERE model_version LIKE 'Snowdon Towers%';
            """
            result = await conn.execute(delete_relationships)
            rel_count = int(result.split()[-1])
            print(f"      ✅ {rel_count}개 관계 삭제 완료")

            # 2. objects 삭제
            print("  2/3: objects 삭제 중...")
            delete_objects = """
                DELETE FROM objects
                WHERE model_version LIKE 'Snowdon Towers%';
            """
            result = await conn.execute(delete_objects)
            obj_count = int(result.split()[-1])
            print(f"      ✅ {obj_count:,}개 객체 삭제 완료")

            # 3. metadata 삭제
            print("  3/3: metadata 삭제 중...")
            delete_metadata = """
                DELETE FROM metadata
                WHERE project_name = 'Snowdon Towers';
            """
            result = await conn.execute(delete_metadata)
            meta_count = int(result.split()[-1])
            print(f"      ✅ {meta_count}개 메타데이터 삭제 완료")

            print("\n✅ 트랜잭션 커밋 완료")

            return {
                'relationships': rel_count,
                'objects': obj_count,
                'metadata': meta_count
            }

    except Exception as e:
        print(f"\n❌ 오류 발생: {e}")
        print("⏪ 트랜잭션 자동 롤백됨 (데이터 복구)")
        raise
    finally:
        await conn.close()


async def verify_deletion():
    """삭제 결과 확인"""
    print("\n" + "=" * 60)
    print("삭제 결과 검증")
    print("=" * 60)

    conn = await get_connection()
    try:
        # 1. Snowdon Towers 데이터 남아있는지 확인
        check_query = """
            SELECT
                (SELECT COUNT(*) FROM metadata WHERE project_name = 'Snowdon Towers') AS metadata_count,
                (SELECT COUNT(*) FROM objects WHERE model_version LIKE 'Snowdon Towers%') AS objects_count,
                (SELECT COUNT(*) FROM relationships WHERE model_version LIKE 'Snowdon Towers%') AS relationships_count;
        """
        result = await conn.fetchrow(check_query)

        print("\n🔍 Snowdon Towers 잔여 데이터 확인:")
        print(f"  Metadata: {result['metadata_count']}개")
        print(f"  Objects: {result['objects_count']}개")
        print(f"  Relationships: {result['relationships_count']}개")

        if result['metadata_count'] == 0 and result['objects_count'] == 0 and result['relationships_count'] == 0:
            print("\n✅ 모든 Snowdon Towers 데이터가 성공적으로 삭제되었습니다!")
        else:
            print("\n⚠️  일부 데이터가 남아있습니다. 수동 확인이 필요합니다.")

        # 2. 전체 데이터 통계
        total_query = """
            SELECT
                (SELECT COUNT(*) FROM metadata) AS metadata_total,
                (SELECT COUNT(*) FROM objects) AS objects_total,
                (SELECT COUNT(*) FROM relationships) AS relationships_total;
        """
        totals = await conn.fetchrow(total_query)

        print("\n📊 전체 데이터베이스 통계:")
        print(f"  Metadata: {totals['metadata_total']}개")
        print(f"  Objects: {totals['objects_total']:,}개")
        print(f"  Relationships: {totals['relationships_total']}개")

        # 3. 남아있는 프로젝트 확인
        remaining_query = """
            SELECT
                project_name,
                model_version,
                total_object_count,
                created_at
            FROM metadata
            ORDER BY created_at DESC;
        """
        remaining = await conn.fetch(remaining_query)

        if remaining:
            print("\n📁 남아있는 프로젝트:")
            for project in remaining:
                print(f"  - {project['project_name']} ({project['total_object_count']:,}개 객체)")
        else:
            print("\n📁 남아있는 프로젝트가 없습니다.")

    finally:
        await conn.close()


async def optimize_database():
    """데이터베이스 최적화"""
    print("\n" + "=" * 60)
    print("데이터베이스 최적화 (선택사항)")
    print("=" * 60)

    response = input("\nVACUUM 및 ANALYZE를 실행하시겠습니까? (yes/no): ").strip().lower()

    if response != 'yes':
        print("최적화를 건너뜁니다.")
        return

    conn = await get_connection()
    try:
        print("\n⏳ VACUUM FULL 실행 중... (시간이 걸릴 수 있습니다)")

        # VACUUM은 트랜잭션 외부에서 실행해야 함
        await conn.execute("VACUUM FULL objects;")
        print("  ✅ objects 테이블 VACUUM 완료")

        await conn.execute("VACUUM FULL relationships;")
        print("  ✅ relationships 테이블 VACUUM 완료")

        await conn.execute("VACUUM FULL metadata;")
        print("  ✅ metadata 테이블 VACUUM 완료")

        print("\n⏳ ANALYZE 실행 중...")
        await conn.execute("ANALYZE objects;")
        await conn.execute("ANALYZE relationships;")
        await conn.execute("ANALYZE metadata;")
        print("  ✅ 통계 정보 업데이트 완료")

        print("\n✅ 데이터베이스 최적화 완료!")

    finally:
        await conn.close()


async def main():
    """메인 함수"""
    print("=" * 60)
    print("Snowdon Towers 데이터 삭제 도구")
    print("=" * 60)

    try:
        # 1. 사용자 확인
        if not await confirm_deletion():
            print("\n❌ 삭제가 취소되었습니다.")
            return

        # 2. 백업 확인
        if not await create_backup():
            print("\n⚠️  백업 없이 계속하는 것은 권장하지 않습니다.")
            response = input("그래도 계속하시겠습니까? (yes/no): ").strip().lower()
            if response != 'yes':
                print("\n❌ 삭제가 취소되었습니다.")
                return

        # 3. 삭제 대상 미리보기
        has_data = await preview_deletion()
        if not has_data:
            print("\n❌ 삭제할 Snowdon Towers 데이터가 없습니다.")
            return

        # 4. 최종 확인
        print("\n" + "⚠️ " * 20)
        response = input("\n정말로 삭제하시겠습니까? (DELETE 입력): ").strip()
        if response != 'DELETE':
            print("\n❌ 삭제가 취소되었습니다.")
            return

        # 5. 삭제 실행
        deleted_counts = await delete_snowdon_towers()

        print("\n📊 삭제 완료 통계:")
        print(f"  - Relationships: {deleted_counts['relationships']}개")
        print(f"  - Objects: {deleted_counts['objects']:,}개")
        print(f"  - Metadata: {deleted_counts['metadata']}개")

        # 6. 결과 검증
        await verify_deletion()

        # 7. 최적화
        await optimize_database()

        print("\n" + "=" * 60)
        print("✅ 모든 작업이 완료되었습니다!")
        print("=" * 60)

    except Exception as e:
        print(f"\n❌ 예상치 못한 오류: {e}")
        print("데이터베이스 상태를 확인하세요.")


if __name__ == "__main__":
    asyncio.run(main())
