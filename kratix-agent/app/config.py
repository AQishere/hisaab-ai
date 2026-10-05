from pathlib import Path

from pydantic_settings import BaseSettings, SettingsConfigDict

AGENT_DIR = Path(__file__).resolve().parent.parent


class Settings(BaseSettings):
    """Read from environment variables prefixed AGENT_ or from kratix-agent/.env."""

    model_config = SettingsConfigDict(env_file=AGENT_DIR / ".env", env_prefix="AGENT_", extra="ignore")

    erp_api_url: str = "http://127.0.0.1:5069/api"
    # 127.0.0.1, not localhost: on Windows "localhost" tries IPv6 first and Ollama listens on IPv4 only,
    # which added about 2 s to every model call.
    ollama_url: str = "http://127.0.0.1:11434"
    ollama_model: str = "qwen2.5:7b"
    company_name: str = "Crescent Flour Mills"
    cors_origins: list[str] = ["http://localhost:5173", "http://localhost:3000"]

    # The ERP's Jwt:Key, Jwt:Issuer and Jwt:Audience. The agent verifies tokens itself; the ERP verifies them again.
    jwt_key: str = ""
    jwt_issuer: str = "Kratix.ERP"
    jwt_audience: str = "kratix-ui"

    data_dir: Path = AGENT_DIR / "data"

    # Each round lets the model call tools once more before it must answer.
    max_tool_rounds: int = 4
    context_tokens: int = 8192
    # The model writes about 75 tokens/s on the laptop GPU, so answer length is response time. 200 tokens is about 2.7 s.
    max_answer_tokens: int = 200
    # 0: the same question gets the same answer, so a fix can be tested and a demo is predictable. At 0.1 answers
    # varied between runs (2026-10-05).
    temperature: float = 0.0
    # How long Ollama keeps the model loaded after a request ("-1" keeps it forever on a dedicated server).
    # Ollama's default of 5 minutes made the first question after a pause take 26 s.
    keep_alive: str = "30m"
    # Questions answered by the model at the same time; match OLLAMA_NUM_PARALLEL (1 on 8 GB VRAM).
    llm_parallel: int = 1
    # Questions waiting for or using the model before new ones get "busy".
    llm_queue_cap: int = 10
    ollama_timeout_seconds: float = 180.0
    erp_timeout_seconds: float = 30.0
    request_timeout_seconds: float = 90.0

    # Earlier messages quoted to the model for follow-up questions; each one adds prompt to read.
    history_messages: int = 6
    questions_per_minute: int = 20
    # ERP responses are reused for this long per user and endpoint; answers themselves are never cached.
    erp_cache_seconds: float = 30.0

    report_retention_days: int = 7
    reports_per_hour: int = 10
    log_retention_days: int = 180
    # Conversations a user deleted are kept this long (hidden from them), then removed.
    deleted_session_days: int = 30
    backups_kept: int = 14
    # Cleanup and backup run at startup and then every this many hours.
    housekeeping_hours: float = 24.0


settings = Settings()
