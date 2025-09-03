import logging
import os
from .config import LOG_DIR


def setup_logging() -> None:
    os.makedirs(LOG_DIR, exist_ok=True)
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
        handlers=[
            logging.FileHandler(os.path.join(LOG_DIR, "app.log"), encoding="utf-8"),
            logging.StreamHandler(),
        ],
    )

