from celery import Celery
import os

redis_url = os.getenv("REDIS_URL", "redis://localhost:6379/0")
cel = Celery("pyproc", broker=redis_url, backend=redis_url)

@cel.task
def extract_task(document_id, file_url):
    print("Running extract task", document_id, file_url)
    # implement extraction pipeline here
    return True
