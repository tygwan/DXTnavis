"""
데이터베이스 테이블 초기화 스크립트
"""
import asyncio
import asyncpg
from pathlib import Path

async def init_database():
    """데이터베이스 테이블 생성"""

    conn_str = "postgresql://postgres:123456@localhost:5432/DX_platform"
    sql_file = Path(r"c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\temp_init.sql")

    print("=" * 60)
    print("데이터베이스 테이블 초기화")
    print("=" * 60)

    try:
        # 연결
        print(f"\n[1/3] 데이터베이스 연결 중...")
        conn = await asyncpg.connect(conn_str)
        print(f"✅ 연결 성공: DX_platform")

        # SQL 파일 읽기
        print(f"\n[2/3] SQL 파일 읽는 중...")
        sql_content = sql_file.read_text(encoding='utf-8')
        print(f"✅ 파일 읽기 성공: {len(sql_content)} bytes")

        # SQL 실행
        print(f"\n[3/3] 테이블 생성 중...")
        await conn.execute(sql_content)
        print(f"✅ 테이블 생성 완료!")

        # 생성된 테이블 확인
        print(f"\n📋 생성된 테이블 목록:")
        tables = await conn.fetch("""
            SELECT tablename
            FROM pg_tables
            WHERE schemaname = 'public'
            ORDER BY tablename
        """)

        for table in tables:
            print(f"  - {table['tablename']}")

        await conn.close()

        print(f"\n{'=' * 60}")
        print(f"✅ 데이터베이스 초기화 완료!")
        print(f"{'=' * 60}\n")

        return True

    except Exception as e:
        print(f"\n❌ 오류 발생: {e}")
        return False

if __name__ == "__main__":
    success = asyncio.run(init_database())
    if not success:
        exit(1)
