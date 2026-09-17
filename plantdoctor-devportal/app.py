import os
from pathlib import Path

import streamlit as st

try:
    from google import genai
    from google.genai import types
except Exception:  # pragma: no cover
    genai = None
    types = None

SYSTEM_PROMPT_PATH = Path(__file__).resolve().parent / "system_prompt" / "default_prompt.txt"


def load_system_prompt() -> str:
    if SYSTEM_PROMPT_PATH.exists():
        return SYSTEM_PROMPT_PATH.read_text(encoding="utf-8").strip()
    return "You are PlantDoctor DevPortal. Provide concise plant health guidance."


def build_client():
    if genai is None:
        return None

    project_id = os.getenv("VERTEX_PROJECT_ID")
    location = os.getenv("VERTEX_LOCATION")
    if not project_id or not location:
        return None

    try:
        return genai.Client(vertexai=True, project=project_id, location=location)
    except Exception:
        return None


st.set_page_config(page_title="PlantDoctor DevPortal", page_icon="🌿", layout="wide")
st.title("PlantDoctor DevPortal")
st.caption("Diagnose plant issues and route actions to the SAP BTP workflow.")

prompt = load_system_prompt()

with st.form("diagnostic_form"):
    plant_name = st.text_input("Plant / asset name", value="Greenhouse A1")
    issue_summary = st.text_area(
        "Issue summary",
        value="Leaves are yellowing and the soil is dry despite recent watering.",
        height=180,
    )
    submitted = st.form_submit_button("Analyze")

if submitted:
    user_message = f"Plant: {plant_name}\nIssue summary: {issue_summary}"

    if build_client() is not None and types is not None:
        client = build_client()
        response = client.models.generate_content(
            model="gemini-2.0-flash",
            contents=user_message,
            config=types.GenerateContentConfig(system_instruction=prompt),
        )
        answer = getattr(response, "text", None) or str(response)
    else:
        answer = (
            "Vertex AI is not configured for this local run. "
            "Set VERTEX_PROJECT_ID and VERTEX_LOCATION to enable live analysis. "
            "For Cloud Run, the deployment workflow injects those values automatically."
        )

    st.markdown("### Recommendation")
    st.write(answer)

else:
    st.info(
        "Enter a plant issue and click Analyze to get a diagnostic recommendation. "
        "This service is intended to run behind Cloud Run with Google Vertex AI enabled."
    )
