"""
Navisworks Hierarchy CSV Import Script
======================================

Navisworks 계층구조 CSV 파일을 PostgreSQL 데이터베이스로 안전하게 Import하는 스크립트

Features:
- UTF-8 BOM 자동 처리
- 배치 처리로 메모리 효율적
- 진행률 표시
- 오류 복구
- 데이터 검증

Usage:
    python import_hierarchy_csv.py

Requirements:
    pip install asyncpg
"""

import asyncio
import csv
import logging
from pathlib import Path
from typing import List, Tuple
import sys

# 로깅 설정
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


class HierarchyImporter:
    """Navisworks Hierarchy CSV Importer"""

    def __init__(
        self,
        csv_path: str,
        db_url: str = 'postgresql://postgresql:1234@localhost:5432/dx_platform',
        batch_size: int = 1000,
        model_version: str = None
    ):
        self.csv_path = Path(csv_path)
        self.db_url = db_url
        self.batch_size = batch_size
        self.model_version = model_version or self.csv_path.stem  # 파일명을 버전으로 사용
        self.conn = None
        self.total_rows = 0
        self.inserted_rows = 0
        self.error_rows = 0

    async def connect(self):
        """데이터베이스 연결"""
        try:
            import asyncpg
            self.conn = await asyncpg.connect(self.db_url)
            logger.info(f"✅ Database connected: {self.db_url}")
        except ImportError:
            logger.error("❌ asyncpg not installed. Run: pip install asyncpg")
            sys.exit(1)
        except Exception as e:
            logger.error(f"❌ Database connection failed: {e}")
            sys.exit(1)

    async def close(self):
        """데이터베이스 연결 종료"""
        if self.conn:
            await self.conn.close()
            logger.info("🔌 Database connection closed")

    async def create_table(self):
        """테이블 생성 (없으면)"""
        logger.info("📋 Checking/Creating table...")

        create_table_sql = """
        CREATE TABLE IF NOT EXISTS navisworks_hierarchy (
            id BIGSERIAL PRIMARY KEY,
            object_id UUID NOT NULL,
            parent_id UUID NOT NULL,
            level INTEGER NOT NULL,
            display_name VARCHAR(500),
            category VARCHAR(255),
            property_name VARCHAR(255),
            property_value TEXT,
            model_version VARCHAR(255),
            created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            CONSTRAINT uq_hierarchy_object_property
                UNIQUE (object_id, property_name, model_version)
        );
        """

        try:
            await self.conn.execute(create_table_sql)
            logger.info("✅ Table ready: navisworks_hierarchy")

            # 인덱스 생성
            await self._create_indexes()

        except Exception as e:
            logger.error(f"❌ Table creation failed: {e}")
            raise

    async def _create_indexes(self):
        """인덱스 생성"""
        indexes = [
            "CREATE INDEX IF NOT EXISTS idx_hierarchy_object_id ON navisworks_hierarchy(object_id);",
            "CREATE INDEX IF NOT EXISTS idx_hierarchy_parent_id ON navisworks_hierarchy(parent_id);",
            "CREATE INDEX IF NOT EXISTS idx_hierarchy_level ON navisworks_hierarchy(level);",
            "CREATE INDEX IF NOT EXISTS idx_hierarchy_category ON navisworks_hierarchy(category);",
            "CREATE INDEX IF NOT EXISTS idx_hierarchy_property_name ON navisworks_hierarchy(property_name);",
        ]

        for idx_sql in indexes:
            try:
                await self.conn.execute(idx_sql)
            except Exception as e:
                logger.warning(f"⚠️  Index creation warning: {e}")

    async def import_csv(self):
        """CSV 파일 Import"""
        if not self.csv_path.exists():
            logger.error(f"❌ CSV file not found: {self.csv_path}")
            sys.exit(1)

        logger.info(f"📂 Reading CSV: {self.csv_path.name}")
        logger.info(f"📦 Batch size: {self.batch_size}")
        logger.info(f"🏷️  Model version: {self.model_version}")

        batch: List[Tuple] = []
        error_count = 0

        try:
            with open(self.csv_path, 'r', encoding='utf-8-sig') as f:
                reader = csv.DictReader(f)

                for row_num, row in enumerate(reader, start=2):  # start=2 (헤더 다음부터)
                    try:
                        # 데이터 변환
                        record = (
                            row['ObjectId'].strip(),
                            row['ParentId'].strip(),
                            int(row['Level']),
                            row['DisplayName'].strip() or None,
                            row['Category'].strip() or None,
                            row['PropertyName'].strip() or None,
                            row['PropertyValue'].strip() or None,
                            self.model_version
                        )

                        batch.append(record)

                        # 배치 단위로 Insert
                        if len(batch) >= self.batch_size:
                            await self._insert_batch(batch)
                            self.inserted_rows += len(batch)
                            batch.clear()

                            # 진행률 표시
                            if self.inserted_rows % (self.batch_size * 10) == 0:
                                logger.info(f"📊 Progress: {self.inserted_rows:,} rows inserted...")

                    except (ValueError, KeyError) as e:
                        error_count += 1
                        logger.warning(f"⚠️  Row {row_num} skipped: {e}")

                        if error_count > 100:
                            logger.error("❌ Too many errors (>100). Aborting.")
                            raise

                # 남은 데이터 Insert
                if batch:
                    await self._insert_batch(batch)
                    self.inserted_rows += len(batch)

                self.total_rows = reader.line_num - 1  # 헤더 제외
                self.error_rows = error_count

        except Exception as e:
            logger.error(f"❌ Import failed: {e}")
            raise

    async def _insert_batch(self, batch: List[Tuple]):
        """배치 Insert"""
        insert_sql = """
        INSERT INTO navisworks_hierarchy
        (object_id, parent_id, level, display_name, category, property_name, property_value, model_version)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
        ON CONFLICT (object_id, property_name, model_version) DO NOTHING
        """

        try:
            await self.conn.executemany(insert_sql, batch)
        except Exception as e:
            logger.error(f"❌ Batch insert failed: {e}")
            raise

    async def validate(self):
        """Import 결과 검증"""
        logger.info("\n🔍 Validating import results...")

        # 총 행 수
        count = await self.conn.fetchval(
            "SELECT COUNT(*) FROM navisworks_hierarchy WHERE model_version = $1",
            self.model_version
        )

        # 고유 객체 수
        unique_objects = await self.conn.fetchval(
            "SELECT COUNT(DISTINCT object_id) FROM navisworks_hierarchy WHERE model_version = $1",
            self.model_version
        )

        # 카테고리별 통계
        categories = await self.conn.fetch(
            """
            SELECT category, COUNT(*) as cnt
            FROM navisworks_hierarchy
            WHERE model_version = $1
            GROUP BY category
            ORDER BY cnt DESC
            LIMIT 5
            """,
            self.model_version
        )

        logger.info(f"\n{'='*60}")
        logger.info(f"📊 Import Summary")
        logger.info(f"{'='*60}")
        logger.info(f"Total CSV rows:        {self.total_rows:,}")
        logger.info(f"Successfully inserted: {count:,}")
        logger.info(f"Error rows:            {self.error_rows:,}")
        logger.info(f"Unique objects:        {unique_objects:,}")
        logger.info(f"\n🏷️  Top 5 Categories:")
        for cat in categories:
            logger.info(f"  - {cat['category']:<20} {cat['cnt']:>10,} rows")
        logger.info(f"{'='*60}\n")

        if count == 0:
            logger.warning("⚠️  No data was imported!")
            return False

        return True


async def main():
    """메인 실행 함수"""
    # 설정
    CSV_PATH = r"c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\Hierarchy_20251012_170425.csv"
    DB_URL = "postgresql://postgresql:1234@localhost:5432/dx_platform"
    BATCH_SIZE = 1000

    logger.info("\n" + "="*60)
    logger.info("🚀 Navisworks Hierarchy CSV Import Tool")
    logger.info("="*60 + "\n")

    importer = HierarchyImporter(
        csv_path=CSV_PATH,
        db_url=DB_URL,
        batch_size=BATCH_SIZE
    )

    try:
        # 1. 데이터베이스 연결
        await importer.connect()

        # 2. 테이블 생성
        await importer.create_table()

        # 3. CSV Import
        await importer.import_csv()

        # 4. 검증
        success = await importer.validate()

        if success:
            logger.info("✅ Import completed successfully!")
        else:
            logger.error("❌ Import completed with errors")

    except KeyboardInterrupt:
        logger.warning("\n⚠️  Import interrupted by user")
    except Exception as e:
        logger.error(f"\n❌ Import failed: {e}")
        sys.exit(1)
    finally:
        await importer.close()


if __name__ == "__main__":
    asyncio.run(main())
