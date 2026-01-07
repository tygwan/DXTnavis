"""
PostgreSQL 연결 테스트 스크립트
"""
import asyncio
import asyncpg
import sys

async def test_connection():
    """데이터베이스 연결 테스트"""

    # 여러 연결 문자열 시도
    connection_strings = [
        "postgresql://postgres:123456@localhost:5432/DX_platform",
        "postgresql://postgres:123456@localhost:5432/dx_platform",
        "postgresql://postgres:123456@127.0.0.1:5432/DX_platform",
        "postgresql://postgres:123456@127.0.0.1:5432/dx_platform",
    ]

    print("=" * 60)
    print("PostgreSQL 연결 테스트")
    print("=" * 60)

    for i, conn_str in enumerate(connection_strings, 1):
        print(f"\n[테스트 {i}] {conn_str}")
        try:
            conn = await asyncpg.connect(conn_str)
            version = await conn.fetchval('SELECT version()')
            db_name = await conn.fetchval('SELECT current_database()')
            user_name = await conn.fetchval('SELECT current_user')

            print(f"✅ 연결 성공!")
            print(f"   Database: {db_name}")
            print(f"   User: {user_name}")
            print(f"   Version: {version[:60]}...")

            await conn.close()

            print(f"\n{'=' * 60}")
            print(f"✅ 성공! 이 연결 문자열을 사용하세요:")
            print(f"DATABASE_URL={conn_str}")
            print(f"{'=' * 60}")
            return True

        except asyncpg.InvalidPasswordError:
            print(f"❌ 비밀번호 오류")
        except asyncpg.InvalidCatalogNameError:
            print(f"❌ 데이터베이스 'dx_platform'이 존재하지 않음")
        except Exception as e:
            print(f"❌ 연결 실패: {type(e).__name__}: {e}")

    print(f"\n{'=' * 60}")
    print("❌ 모든 연결 시도 실패")
    print("\n다음을 확인하세요:")
    print("1. PostgreSQL 서비스가 실행 중인지 확인")
    print("2. 데이터베이스 'dx_platform'이 생성되었는지 확인")
    print("3. 사용자 비밀번호가 올바른지 확인")
    print("=" * 60)
    return False

if __name__ == "__main__":
    success = asyncio.run(test_connection())
    sys.exit(0 if success else 1)
