import re
import uuid
from typing import Optional


IFC_GUID_KEY_CANDIDATES = (
    "IfcGUID",
    "IFC_GUID",
    "IFC Guid",
    "IFC GUID",
    "Ifc Guid",
)


def to_deterministic_uuid(value: str) -> uuid.UUID:
    """
    Return a stable UUID for the provided identifier string.

    Navisworks object identifiers are already UUID compliant, so they are returned
    as-is. Revit UniqueIds contain hyphenated suffixes and are not valid UUIDs, so
    we map them to a UUIDv5 using the URL namespace.
    """
    normalized = (value or "").strip()
    if not normalized:
        raise ValueError("Identifier value is empty.")

    try:
        return uuid.UUID(normalized)
    except ValueError:
        return uuid.uuid5(uuid.NAMESPACE_URL, normalized)


def extract_ifc_guid(properties: dict) -> Optional[str]:
    """
    Retrieve the IFC GUID from the provided properties dictionary.
    Keys coming from different exporters vary in casing/spacing, so we scan a set
    of known candidates. Returns None if nothing is found or the value is empty.
    """
    if not isinstance(properties, dict):
        return None

    for candidate in IFC_GUID_KEY_CANDIDATES:
        if candidate in properties and properties[candidate]:
            return str(properties[candidate]).strip()

    return None


def sanitize_display_name(*candidates: Optional[str]) -> Optional[str]:
    """
    Choose the first non-empty string, collapse whitespace, and trim.
    """
    for candidate in candidates:
        if candidate:
            collapsed = re.sub(r"\s+", " ", str(candidate)).strip()
            if collapsed:
                return collapsed
    return None


def generate_project_code(project_name: str) -> str:
    """
    Mirror the project code strategy used across the API so ingest can create
    projects without importing the router module (avoids circular imports).
    """
    code = (project_name or "").replace(" ", "_").replace("-", "_").upper()
    code = re.sub(r"[^A-Z0-9_가-힣]", "", code)
    if not code:
        return "UNKNOWN_PROJECT"
    return code[:50]
