"""
PostgreSQL 데이터베이스 조회 스크립트
"""
import asyncio
import asyncpg
from typing import Any, List, Optional


# 데이터베이스 연결 정보
DATABASE_URL = "postgresql://postgres:123456@localhost:5432/DX_platform"


async def get_connection():
    """데이터베이스 연결 생성"""
    return await asyncpg.connect(DATABASE_URL)


async def show_tables():
    """모든 테이블 목록 조회"""
    print("\n=== 데이터베이스 테이블 목록 ===")
    query = """
        SELECT
            schemaname,
            tablename,
            pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
        FROM pg_tables
        WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
        ORDER BY schemaname, tablename;
    """

    conn = await get_connection()
    try:
        results = await conn.fetch(query)

        if not results:
            print("테이블이 없습니다.")
            return

        print(f"\n{'스키마':<15} {'테이블명':<30} {'크기':<10}")
        print("-" * 60)
        for row in results:
            print(f"{row['schemaname']:<15} {row['tablename']:<30} {row['size']:<10}")

        print(f"\n총 {len(results)}개의 테이블")

    except Exception as e:
        print(f"오류 발생: {e}")
        raise
    finally:
        await conn.close()


async def describe_table(table_name: str):
    """특정 테이블의 구조 조회"""
    print(f"\n=== {table_name} 테이블 구조 ===")
    query = """
        SELECT
            column_name,
            data_type,
            character_maximum_length,
            is_nullable,
            column_default
        FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = $1
        ORDER BY ordinal_position;
    """

    conn = await get_connection()
    try:
        results = await conn.fetch(query, table_name)

        if not results:
            print(f"테이블 '{table_name}'을 찾을 수 없습니다.")
            return

        print(f"\n{'컬럼명':<30} {'데이터 타입':<20} {'길이':<10} {'NULL 허용':<10} {'기본값':<20}")
        print("-" * 100)
        for row in results:
            length = row['character_maximum_length'] or '-'
            default = row['column_default'] or '-'
            print(f"{row['column_name']:<30} {row['data_type']:<20} {str(length):<10} {row['is_nullable']:<10} {default:<20}")

        print(f"\n총 {len(results)}개의 컬럼")

    except Exception as e:
        print(f"오류 발생: {e}")
        raise
    finally:
        await conn.close()


async def query_data(table_name: str, limit: int = 10):
    """테이블 데이터 조회"""
    print(f"\n=== {table_name} 데이터 (최대 {limit}개) ===")

    conn = await get_connection()
    try:
        # 먼저 총 레코드 수 확인
        count_query = f"SELECT COUNT(*) FROM {table_name};"
        total_count = await conn.fetchval(count_query)
        print(f"총 레코드 수: {total_count:,}개\n")

        # 데이터 조회
        query = f"SELECT * FROM {table_name} LIMIT $1;"
        results = await conn.fetch(query, limit)

        if not results:
            print("데이터가 없습니다.")
            return

        # 컬럼명 출력
        columns = list(results[0].keys())
        print(" | ".join(f"{col:<20}" for col in columns))
        print("-" * (22 * len(columns)))

        # 데이터 출력
        for row in results:
            values = []
            for col in columns:
                val = row[col]
                if val is None:
                    val_str = "NULL"
                elif isinstance(val, str) and len(val) > 20:
                    val_str = val[:17] + "..."
                else:
                    val_str = str(val)
                values.append(f"{val_str:<20}")
            print(" | ".join(values))

        print(f"\n표시된 레코드: {len(results)}개 / 전체: {total_count:,}개")

    except Exception as e:
        print(f"오류 발생: {e}")
        raise
    finally:
        await conn.close()


async def run_custom_query(query: str):
    """커스텀 SQL 쿼리 실행"""
    print(f"\n=== 커스텀 쿼리 실행 ===")
    print(f"Query: {query}\n")

    conn = await get_connection()
    try:
        results = await conn.fetch(query)

        if not results:
            print("결과가 없습니다.")
            return

        # 컬럼명 출력
        columns = list(results[0].keys())
        print(" | ".join(f"{col:<20}" for col in columns))
        print("-" * (22 * len(columns)))

        # 데이터 출력
        for row in results:
            values = []
            for col in columns:
                val = row[col]
                if val is None:
                    val_str = "NULL"
                elif isinstance(val, str) and len(val) > 20:
                    val_str = val[:17] + "..."
                else:
                    val_str = str(val)
                values.append(f"{val_str:<20}")
            print(" | ".join(values))

        print(f"\n총 {len(results)}개의 결과")

    except Exception as e:
        print(f"오류 발생: {e}")
        raise
    finally:
        await conn.close()


async def main():
    """메인 함수"""
    print("=" * 80)
    print("PostgreSQL 데이터베이스 조회 도구")
    print("=" * 80)

    # 1. 모든 테이블 목록 조회
    await show_tables()

    # 2. metadata 테이블 상세 정보
    print("\n" + "=" * 80)
    await describe_table('metadata')
    print("\n" + "=" * 80)
    await query_data('metadata', limit=10)

    # 3. objects 테이블 상세 정보
    print("\n" + "=" * 80)
    await describe_table('objects')
    print("\n" + "=" * 80)
    await query_data('objects', limit=5)

    # 4. relationships 테이블 상세 정보
    print("\n" + "=" * 80)
    await describe_table('relationships')
    print("\n" + "=" * 80)
    await query_data('relationships', limit=5)

    print("\n" + "=" * 80)
    print("조회 완료!")


if __name__ == "__main__":
    asyncio.run(main())
