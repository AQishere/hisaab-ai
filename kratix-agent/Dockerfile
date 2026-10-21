# The assistant for a Hugging Face Space (scripts/deploy-space.sh). Build context: this folder.
FROM python:3.13-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY app ./app
# Spaces run the container as a non-root user, so chat history and reports go to /tmp; they are emptied on every
# restart, which is fine for the demo.
ENV TZ=Asia/Karachi \
    PYTHONUNBUFFERED=1 \
    AGENT_DATA_DIR=/tmp/kratix-agent
EXPOSE 10000
CMD ["python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "10000"]
