from celery import Celery
import os
from .app.main import embed_document, download_from_minio

BROKER = os.getenv("REDIS_URL", "redis://redis:6379/0")
cel = Celery("pyproc", broker=BROKER, backend=BROKER)

@cel.task
def extract_task(document_id, file_url):
    # This runs in a worker process (ensure PYTHONPATH includes app)
    from app.main import extract
    # Here you could call extract logic synchronously; for simplicity we call embed_document after download
    # Implement robust retry/visibility timeouts in production
    return True

@cel.task
def embed_task(document_id):
    from app.main import embed_document
    return embed_document(document_id)
