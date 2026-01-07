from fastapi import APIRouter
from typing import List, Dict, Any
import logging

from ..database import db


router = APIRouter(prefix="/api/v1", tags=["timeliner"])
logger = logging.getLogger(__name__)


@router.get("/timeliner/{version}/mapping")
async def get_timeliner_mapping(version: str) -> List[Dict[str, Any]]:
    rows = await db.fetch(
        "SELECT * FROM analytics_4d_link_data WHERE model_version = $1 ORDER BY activity_id",
        (version,),
    )
    return [dict(r) for r in rows]
