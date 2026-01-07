"""
현재 데이터베이스 스키마 확인 스크립트
"""
import asyncio
import asyncpg


async def check_current_schema():
    """현재 데이터베이스의 테이블 목록 및 레코드 수 확인"""
    conn = await asyncpg.connect('postgresql://postgres:123456@localhost:5432/DX_platform')

    print("=" * 60)
    print("현재 데이터베이스 스키마")
    print("=" * 60)

    # 테이블 목록 조회
    tables = await conn.fetch("""
        SELECT table_name
        FROM information_schema.tables
        WHERE table_schema = 'public'
        ORDER BY table_name
    """)

    print(f"\n📊 총 {len(tables)}개의 테이블:")
    for table in tables:
        table_name = table['table_name']

        # 각 테이블의 레코드 수 조회
        count = await conn.fetchval(f"SELECT COUNT(*) FROM {table_name}")

        print(f"  ✓ {table_name:<30} ({count:>6} rows)")

    print("\n" + "=" * 60)

    await conn.close()


if __name__ == "__main__":
    asyncio.run(check_current_schema())
