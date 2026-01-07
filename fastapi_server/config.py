import json
from pathlib import Path
from typing import Any, Callable, Dict, List

from pydantic import field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


BASE_DIR = Path(__file__).resolve().parent
ENV_PATH = BASE_DIR / ".env"


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=ENV_PATH,
        env_file_encoding="utf-8",
        extra="ignore",
    )

    DATABASE_URL: str
    HOST: str = "0.0.0.0"
    PORT: int = 8000
    DEBUG: bool = True
    LOG_LEVEL: str = "INFO"
    ALLOWED_ORIGINS: List[str] | str = ["*"]
    DB_POOL_MIN: int = 1
    DB_POOL_MAX: int = 10
    ALLOWED_HOSTS: List[str] | str = ["*"]

    @field_validator("ALLOWED_ORIGINS", "ALLOWED_HOSTS", mode="before")
    @classmethod
    def split_comma_separated(cls, value):  # convert comma separated strings to list entries
        if isinstance(value, str):
            items = [item.strip() for item in value.split(",") if item.strip()]
            return items
        return value

    @staticmethod
    def _load_json_or_return(value: Any) -> Any:
        if isinstance(value, str):
            try:
                return json.loads(value)
            except json.JSONDecodeError:
                return value
        return value

    @classmethod
    def _with_json_fallback(cls, source: Callable[..., Dict[str, Any]]) -> Callable[..., Dict[str, Any]]:
        def inner(*args, **kwargs) -> Dict[str, Any]:
            data = source(*args, **kwargs)
            return {key: cls._load_json_or_return(val) for key, val in data.items()}

        return inner

    @classmethod
    def settings_customise_sources(
        cls,
        settings_cls,
        init_settings,
        env_settings,
        dotenv_settings,
        file_secret_settings,
    ):
        return (
            init_settings,
            cls._with_json_fallback(env_settings),
            cls._with_json_fallback(dotenv_settings),
            file_secret_settings,
        )


settings = Settings()
