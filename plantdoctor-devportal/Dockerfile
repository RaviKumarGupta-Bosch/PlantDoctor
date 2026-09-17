FROM python:3.11-slim

WORKDIR /app

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PORT=8080

COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py ./
COPY src/ ./src/
COPY system_prompt/ ./system_prompt/

# Streamlit behind Cloud Run's proxy often requires disabling XSRF protection.
# Revisit this if the app needs a production-grade CSRF posture or if
# authenticated browser sessions are required across the Cloud Run edge.
CMD streamlit run app.py --server.port=$PORT --server.address=0.0.0.0 \
    --server.headless=true --server.enableCORS=false \
    --server.enableXsrfProtection=false

EXPOSE 8080
