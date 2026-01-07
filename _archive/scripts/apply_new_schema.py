"""
새 통합 스키마 적용 스크립트
"""
import asyncio
import asyncpg
from pathlib import Path


async def apply_new_schema():
    """새 통합 스키마를 데이터베이스에 적용"""
    conn = await asyncpg.connect('postgresql://postgres:123456@localhost:5432/DX_platform')

    print("=" * 60)
    print("AWP 2025 통합 스키마 적용")
    print("=" * 60)

    try:
        # 1. 스키마 파일 읽기
        schema_file = Path(__file__).parent.parent / 'database' / 'migrations' / '002_integrated_schema.sql'
        bi_views_file = Path(__file__).parent.parent / 'database' / 'migrations' / '003_bi_views.sql'

        print(f"\n📄 스키마 파일 로드:")
        print(f"   - {schema_file}")
        print(f"   - {bi_views_file}")

        # 2. 통합 스키마 적용
        print(f"\n⚙️  통합 스키마 적용 중...")
        with open(schema_file, 'r', encoding='utf-8') as f:
            schema_sql = f.read()

        await conn.execute(schema_sql)
        print("   ✅ 통합 스키마 적용 완료")

        # 3. BI 뷰 생성 (조건부 - 데이터가 있을 때만)
        print(f"\n⚙️  BI 뷰 생성 확인 중...")

        # unified_objects 테이블에 데이터가 있는지 확인
        has_data = await conn.fetchval("SELECT EXISTS(SELECT 1 FROM unified_objects LIMIT 1)")

        if has_data:
            print("   📊 데이터가 있으므로 BI 뷰 생성...")
            with open(bi_views_file, 'r', encoding='utf-8') as f:
                bi_views_sql = f.read()

            await conn.execute(bi_views_sql)
            print("   ✅ BI 뷰 생성 완료")
        else:
            print("   ⚠️  데이터가 없으므로 BI 뷰 생성 스킵")
            print("   💡 데이터 마이그레이션 후 다시 실행하세요")

        # 4. 생성된 테이블 확인
        print(f"\n📊 생성된 테이블 목록:")
        tables = await conn.fetch("""
            SELECT table_name, table_type
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name NOT LIKE '%_backup'
            ORDER BY table_type, table_name
        """)

        for table in tables:
            icon = "📋" if table['table_type'] == 'BASE TABLE' else "👁️"
            print(f"   {icon} {table['table_name']:<40} ({table['table_type']})")

        # 5. 함수 확인
        print(f"\n🔧 생성된 함수:")
        functions = await conn.fetch("""
            SELECT routine_name
            FROM information_schema.routines
            WHERE routine_schema = 'public'
              AND routine_type = 'FUNCTION'
            ORDER BY routine_name
        """)

        for func in functions:
            print(f"   ⚡ {func['routine_name']}")

        print("\n" + "=" * 60)
        print("✅ 스키마 적용 완료!")
        print("=" * 60)

        print("\n📝 다음 단계:")
        print("   1. 기존 데이터 마이그레이션: python scripts/migrate_existing_data.py")
        print("   2. Revit 플러그인 업데이트")
        print("   3. Navisworks 플러그인 업데이트")
        print("   4. FastAPI 엔드포인트 업데이트")

    except Exception as e:
        print(f"\n❌ 오류 발생: {e}")
        raise
    finally:
        await conn.close()


if __name__ == "__main__":
    asyncio.run(apply_new_schema())
