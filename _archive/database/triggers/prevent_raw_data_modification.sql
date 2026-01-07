-- ========================================
-- 불변성 보장 트리거: UPDATE/DELETE 방지
-- ========================================

-- UPDATE 방지 트리거 함수
CREATE OR REPLACE FUNCTION prevent_raw_data_update()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'UPDATE operation is not allowed on raw data tables. All changes must be recorded as new versions.';
END;
$$ LANGUAGE plpgsql;

-- DELETE 방지 트리거 함수
CREATE OR REPLACE FUNCTION prevent_raw_data_delete()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'DELETE operation is not allowed on raw data tables. Use logical deletion or versioning instead.';
END;
$$ LANGUAGE plpgsql;

-- metadata 테이블에 트리거 적용
DROP TRIGGER IF EXISTS trg_prevent_metadata_update ON metadata;
CREATE TRIGGER trg_prevent_metadata_update
BEFORE UPDATE ON metadata
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_update();

DROP TRIGGER IF EXISTS trg_prevent_metadata_delete ON metadata;
CREATE TRIGGER trg_prevent_metadata_delete
BEFORE DELETE ON metadata
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_delete();

-- objects 테이블에 트리거 적용
DROP TRIGGER IF EXISTS trg_prevent_objects_update ON objects;
CREATE TRIGGER trg_prevent_objects_update
BEFORE UPDATE ON objects
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_update();

DROP TRIGGER IF EXISTS trg_prevent_objects_delete ON objects;
CREATE TRIGGER trg_prevent_objects_delete
BEFORE DELETE ON objects
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_delete();

-- relationships 테이블에 트리거 적용
DROP TRIGGER IF EXISTS trg_prevent_relationships_update ON relationships;
CREATE TRIGGER trg_prevent_relationships_update
BEFORE UPDATE ON relationships
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_update();

DROP TRIGGER IF EXISTS trg_prevent_relationships_delete ON relationships;
CREATE TRIGGER trg_prevent_relationships_delete
BEFORE DELETE ON relationships
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_delete();
