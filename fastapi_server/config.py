from pydantic_settings import BaseSettings
from typing import List


class Settings(BaseSettings):
    DATABASE_URL: str
    HOST: str = "0.0.0.0"
    PORT: int = 5000
    DEBUG: bool = False
    LOG_LEVEL: str = "INFO"
    ALLOWED_ORIGINS: List[str] = ["*"]
    DB_POOL_MIN: int = 1
    DB_POOL_MAX: int = 10
    ALLOWED_HOSTS: List[str] = ["*"]

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


settings = Settings()
