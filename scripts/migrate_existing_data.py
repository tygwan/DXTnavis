"""
기존 데이터를 새 통합 스키마로 마이그레이션

마이그레이션 절차:
1. metadata → projects 테이블
2. metadata → revisions 테이블
3. objects → unified_objects 테이블
"""
import asyncio
import asyncpg
from datetime import datetime
import hashlib
import re
import json


def generate_project_code(project_name: str) -> str:
    """
    프로젝트 이름에서 프로젝트 코드 생성
    예: "프로젝트 이름" → "프로젝트_이름"
        "Snowdon Towers" → "SNOWDON_TOWERS"
    """
    # 공백, 하이픈을 언더스코어로
    code = project_name.replace(" ", "_").replace("-", "_").upper()

    # 특수문자 제거 (한글, 영문, 숫자, 언더스코어만 허용)
    code = re.sub(r'[^A-Z0-9_가-힣]', '', code)

    # 길이 제한
    if len(code) > 50:
        code = code[:50]

    return code if code else "UNKNOWN_PROJECT"


async def migrate_existing_data():
    """기존 데이터 마이그레이션 실행"""
    conn = await asyncpg.connect('postgresql://postgres:123456@localhost:5432/DX_platform')

    print("=" * 60)
    print("기존 데이터 마이그레이션")
    print("=" * 60)

    try:
        # 0. 기존 데이터 확인
        print("\n📊 기존 데이터 확인:")
        metadata_count = await conn.fetchval("SELECT COUNT(*) FROM metadata")
        objects_count = await conn.fetchval("SELECT COUNT(*) FROM objects")

        print(f"   - metadata: {metadata_count} rows")
        print(f"   - objects: {objects_count} rows")

        if metadata_count == 0 and objects_count == 0:
            print("\n⚠️  마이그레이션할 데이터가 없습니다.")
            return

        # 1. metadata → projects 마이그레이션
        print(f"\n⚙️  Step 1: metadata → projects")

        metadata_rows = await conn.fetch("SELECT * FROM metadata")

        for meta in metadata_rows:
            project_name = meta['project_name']
            project_code = generate_project_code(project_name)

            # 프로젝트 존재 확인
            existing = await conn.fetchval(
                "SELECT id FROM projects WHERE code = $1",
                project_code
            )

            if existing:
                print(f"   ⏭️  프로젝트 이미 존재: {project_code}")
                project_id = existing
            else:
                # 프로젝트 생성
                project_id = await conn.fetchval("""
                    INSERT INTO projects (
                        code, name, project_number, client_name, address,
                        created_by, metadata
                    ) VALUES ($1, $2, $3, $4, $5, $6, $7::jsonb)
                    RETURNING id
                """,
                    project_code,
                    project_name,
                    meta.get('project_number'),
                    meta.get('client_name'),
                    meta.get('address'),
                    meta.get('author', 'migration_script'),
                    json.dumps({
                        'model_version': meta.get('model_version'),
                        'snapshot_time': str(meta.get('snapshot_time')) if meta.get('snapshot_time') else None
                    })
                )
                print(f"   ✅ 프로젝트 생성: {project_code} (ID: {project_id})")

            # 2. metadata → revisions 마이그레이션
            print(f"\n⚙️  Step 2: metadata → revisions")

            # 리비전 번호 결정
            model_version = meta.get('model_version', '')
            revision_number = await conn.fetchval(
                "SELECT get_next_revision_number($1, $2)",
                project_id, 'revit'
            )

            # 리비전 생성
            revision_id = await conn.fetchval("""
                INSERT INTO revisions (
                    project_id, revision_number, version_tag, description,
                    source_type, total_objects, created_by, metadata
                ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8::jsonb)
                RETURNING id
            """,
                project_id,
                revision_number,
                model_version,
                f'Migrated from old schema (model_version: {model_version})',
                'revit',
                meta.get('total_object_count', 0),
                meta.get('author', 'migration_script'),
                json.dumps({
                    'original_model_version': model_version,
                    'migrated_at': datetime.now().isoformat()
                })
            )
            print(f"   ✅ 리비전 생성: Revision #{revision_number} (ID: {revision_id})")

            # 3. objects → unified_objects 마이그레이션
            print(f"\n⚙️  Step 3: objects → unified_objects")

            # 해당 model_version의 객체 조회
            objects = await conn.fetch("""
                SELECT * FROM objects
                WHERE model_version = $1
            """, model_version)

            print(f"   📦 마이그레이션할 객체: {len(objects)}개")

            # 배치 삽입
            migrated_count = 0
            for obj in objects:
                try:
                    # object_id 생성 (Revit UniqueId 사용 또는 GUID 생성)
                    object_id = obj.get('revit_unique_id') or obj.get('guid')

                    if not object_id:
                        # GUID 생성 (Element ID 기반)
                        object_id_str = f"{model_version}_{obj['element_id']}"
                        object_id = hashlib.md5(object_id_str.encode()).hexdigest()
                        # UUID 형식으로 변환
                        object_id = f"{object_id[:8]}-{object_id[8:12]}-{object_id[12:16]}-{object_id[16:20]}-{object_id[20:]}"

                    # unified_objects 삽입
                    await conn.execute("""
                        INSERT INTO unified_objects (
                            project_id, revision_id, object_id, element_id,
                            source_type, level, display_name, category,
                            family, type, activity_id, properties, bounding_box
                        ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12::jsonb, $13::jsonb)
                        ON CONFLICT (revision_id, object_id, source_type) DO NOTHING
                    """,
                        project_id,
                        revision_id,
                        object_id,
                        obj.get('element_id'),
                        'revit',
                        0,  # 기존 Revit 데이터는 계층 정보 없음
                        obj.get('name', obj.get('family', 'Unknown')),
                        obj.get('category'),
                        obj.get('family'),
                        obj.get('type'),
                        obj.get('activity_id'),
                        json.dumps({
                            'original_id': str(obj.get('id')),
                            **{k: v for k, v in obj.items()
                               if k not in ['id', 'element_id', 'category', 'family', 'type',
                                          'activity_id', 'model_version', 'created_at',
                                          'bounding_box_min_x', 'bounding_box_min_y', 'bounding_box_min_z',
                                          'bounding_box_max_x', 'bounding_box_max_y', 'bounding_box_max_z']
                               and v is not None}
                        }),
                        json.dumps({
                            'MinX': obj.get('bounding_box_min_x'),
                            'MinY': obj.get('bounding_box_min_y'),
                            'MinZ': obj.get('bounding_box_min_z'),
                            'MaxX': obj.get('bounding_box_max_x'),
                            'MaxY': obj.get('bounding_box_max_y'),
                            'MaxZ': obj.get('bounding_box_max_z')
                        }) if obj.get('bounding_box_min_x') else None
                    )
                    migrated_count += 1

                    if migrated_count % 100 == 0:
                        print(f"   📦 진행 중... {migrated_count}/{len(objects)}")

                except Exception as e:
                    print(f"   ⚠️  객체 마이그레이션 실패 (Element ID: {obj.get('element_id')}): {e}")

            print(f"   ✅ 객체 마이그레이션 완료: {migrated_count}/{len(objects)}")

        # 4. 최종 통계
        print("\n" + "=" * 60)
        print("📊 마이그레이션 결과:")
        print("=" * 60)

        projects_count = await conn.fetchval("SELECT COUNT(*) FROM projects")
        revisions_count = await conn.fetchval("SELECT COUNT(*) FROM revisions")
        unified_objects_count = await conn.fetchval("SELECT COUNT(*) FROM unified_objects")

        print(f"   ✅ Projects: {projects_count}")
        print(f"   ✅ Revisions: {revisions_count}")
        print(f"   ✅ Unified Objects: {unified_objects_count}")

        # 5. BI 뷰 생성 (이제 데이터가 있으므로)
        print("\n⚙️  BI 뷰 생성 중...")
        from pathlib import Path

        bi_views_file = Path(__file__).parent.parent / 'database' / 'migrations' / '003_bi_views.sql'
        with open(bi_views_file, 'r', encoding='utf-8') as f:
            bi_views_sql = f.read()

        await conn.execute(bi_views_sql)
        print("   ✅ BI 뷰 생성 완료")

        print("\n" + "=" * 60)
        print("✅ 마이그레이션 완료!")
        print("=" * 60)

        print("\n📝 다음 단계:")
        print("   1. Power BI/Tableau에서 새 뷰 연결 테스트")
        print("   2. Revit 플러그인 업데이트")
        print("   3. Navisworks 플러그인 업데이트")
        print("   4. FastAPI 엔드포인트 업데이트")

    except Exception as e:
        print(f"\n❌ 오류 발생: {e}")
        import traceback
        traceback.print_exc()
        raise
    finally:
        await conn.close()


if __name__ == "__main__":
    asyncio.run(migrate_existing_data())
