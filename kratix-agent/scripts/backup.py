"""Back up the agent's SQLite file now, alongside the ERP backups. Safe while the agent is running.

Usage (from kratix-agent/):
    .venv/Scripts/python scripts/backup.py                 # into data/backups/, keeping the newest 14
    .venv/Scripts/python scripts/backup.py D:/Backups      # into another folder (nothing is removed there)

The agent also backs up and cleans up by itself at startup and every 24 hours.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from app.admin import Housekeeping  # noqa: E402
from app.config import settings  # noqa: E402
from app.store import Store, now  # noqa: E402


def main() -> None:
    store = Store(settings.data_dir / "agent.db")
    try:
        if len(sys.argv) > 1:
            target = Path(sys.argv[1]) / f"agent-{now()[:19].replace(':', '').replace('-', '')}.db"
            store.backup(target)
        else:
            target = Housekeeping(store, settings.data_dir, log_days=settings.log_retention_days,
                                  deleted_session_days=settings.deleted_session_days,
                                  backups_kept=settings.backups_kept).backup()
    finally:
        store.close()
    print(f"Backup written to {target}")


if __name__ == "__main__":
    main()
