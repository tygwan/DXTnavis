"""
제약 조건 제거 스크립트
"""
import asyncio
import asyncpg

async def fix_constraint():
    """model_version 제약 조건 제거"""

    conn_str = "postgresql://postgres:123456@localhost:5432/DX_platform"

    print("=" * 60)
    print("model_version 제약 조건 제거")
    print("=" * 60)

    try:
        # 연결
        print(f"\n[1/2] 데이터베이스 연결 중...")
        conn = await asyncpg.connect(conn_str)
        print(f"✅ 연결 성공: DX_platform")

        # 제약 조건 제거
        print(f"\n[2/2] 제약 조건 제거 중...")
        await conn.execute("""
            ALTER TABLE metadata
            DROP CONSTRAINT IF EXISTS chk_model_version_format;
        """)
        print(f"✅ 제약 조건 제거 완료!")

        await conn.close()

        print(f"\n{'=' * 60}")
        print(f"✅ 수정 완료! 이제 한글 model_version이 허용됩니다.")
        print(f"{'=' * 60}\n")

        return True

    except Exception as e:
        print(f"\n❌ 오류 발생: {e}")
        return False

if __name__ == "__main__":
    success = asyncio.run(fix_constraint())
    if not success:
        exit(1)
