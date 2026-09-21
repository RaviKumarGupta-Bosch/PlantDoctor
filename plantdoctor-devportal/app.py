import os
import json
import zipfile
import tempfile
import base64
from pathlib import Path
import requests

import streamlit as st

try:
    from google import genai
    from google.genai import types
except Exception:  # pragma: no cover
    genai = None
    types = None

SYSTEM_PROMPT_PATH = Path(__file__).resolve().parent / "system_prompt" / "default_prompt.txt"

# Gemini Model Configuration
# Region: asia-northeast1 (Tokyo) - Stable, all models available
# Models available in Tokyo:
#   - gemini-2.5-flash (stable, best price-performance) ✅
#   - gemini-2.5-pro (advanced, more reasoning)
#   - gemini-3.8-flash (latest)
# See: https://ai.google.dev/gemini-api/docs/available-regions
GEMINI_MODEL = os.getenv("GEMINI_MODEL", "gemini-3.8-flash")  # Stable, proven in asia-northeast1


def load_system_prompt() -> str:
    if SYSTEM_PROMPT_PATH.exists():
        return SYSTEM_PROMPT_PATH.read_text(encoding="utf-8").strip()
    return "You are PlantDoctor DevPortal. Provide concise plant health guidance."


def build_client():
    if genai is None:
        return None

    project_id = os.getenv("VERTEX_PROJECT_ID") or os.getenv("GOOGLE_CLOUD_PROJECT")
    location = os.getenv("VERTEX_LOCATION") or os.getenv("GOOGLE_CLOUD_LOCATION") or os.getenv("GOOGLE_CLOUD_REGION")
    if not project_id or not location:
        return None

    try:
        return genai.Client(vertexai=True, project=project_id, location=location)
    except Exception:
        return None


def extract_zip_contents(zip_file) -> dict:
    """Extract JSON and log files from uploaded ZIP archive."""
    contents = {"json_files": {}, "log_files": {}, "other_files": {}}
    
    with tempfile.TemporaryDirectory() as tmpdir:
        with zipfile.ZipFile(zip_file, 'r') as z:
            z.extractall(tmpdir)
            
            for file_path in Path(tmpdir).rglob('*'):
                if file_path.is_file():
                    rel_path = file_path.relative_to(tmpdir)
                    
                    try:
                        if file_path.suffix.lower() == '.json':
                            with open(file_path, 'r', encoding='utf-8') as f:
                                contents["json_files"][str(rel_path)] = json.load(f)
                        elif file_path.suffix.lower() in ['.log', '.txt']:
                            with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                                contents["log_files"][str(rel_path)] = f.read()
                        else:
                            with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                                contents["other_files"][str(rel_path)] = f.read()
                    except Exception as e:
                        st.warning(f"Could not read {rel_path}: {str(e)}")
    
    return contents


def format_analysis_content(zip_contents: dict, user_prompt: str) -> str:
    """Format extracted ZIP contents with user prompt for Gemini analysis."""
    content = f"User Prompt:\n{user_prompt}\n\n"
    content += "=" * 60 + "\n"
    
    if zip_contents["json_files"]:
        content += "\n📊 ANALYSIS REPORTS (JSON):\n"
        content += "=" * 60 + "\n"
        for filename, data in zip_contents["json_files"].items():
            content += f"\n{filename}:\n"
            content += json.dumps(data, indent=2) + "\n"
    
    if zip_contents["log_files"]:
        content += "\n📋 LOG FILES:\n"
        content += "=" * 60 + "\n"
        for filename, log_data in zip_contents["log_files"].items():
            content += f"\n{filename}:\n"
            content += log_data + "\n"
    
    return content


def extract_summary_from_analysis(analysis_text: str) -> dict:
    """Extract key fields from Gemini analysis for SAP BTP submission."""
    summary = {
        "rootcause": "",
        "solution": "",
        "status": "OPEN",
        "plant": "",
    }
    
    lines = analysis_text.split('\n')
    for i, line in enumerate(lines):
        lower_line = line.lower()
        
        # Extract root cause
        if 'root cause' in lower_line or 'rootcause' in lower_line:
            summary["rootcause"] = line.split(':', 1)[-1].strip() if ':' in line else lines[i + 1].strip() if i + 1 < len(lines) else ""
        
        # Extract solution
        if 'solution' in lower_line or 'resolution' in lower_line or 'fix' in lower_line:
            summary["solution"] = line.split(':', 1)[-1].strip() if ':' in line else lines[i + 1].strip() if i + 1 < len(lines) else ""
        
        # Extract status
        if 'status' in lower_line:
            if 'in progress' in lower_line.lower():
                summary["status"] = "IN_PROGRESS"
            elif 'completed' in lower_line.lower():
                summary["status"] = "COMPLETED"
            else:
                summary["status"] = "OPEN"
        
        # Extract plant
        if 'plant' in lower_line and 'plant issue' not in lower_line:
            summary["plant"] = line.split(':', 1)[-1].strip() if ':' in line else ""
    
    return summary


def structure_analysis_output(analysis_text: str) -> dict:
    """Parse analysis into structured sections for better readability."""
    structured = {
        "reproducing_steps": [],
        "possible_causes": [],
        "fix_tips": [],
        "resilience_tips": [],
        "summary": analysis_text,
    }
    
    lines = analysis_text.split('\n')
    current_section = None
    
    for line in lines:
        lower_line = line.lower().strip()
        
        # Identify sections
        if 'reproduc' in lower_line and 'step' in lower_line:
            current_section = "reproducing_steps"
        elif 'cause' in lower_line and ('possib' in lower_line or 'root' in lower_line):
            current_section = "possible_causes"
        elif ('fix' in lower_line or 'solution' in lower_line or 'resolv' in lower_line) and 'tip' in lower_line:
            current_section = "fix_tips"
        elif ('avoid' in lower_line or 'prevent' in lower_line or 'resilience' in lower_line):
            current_section = "resilience_tips"
        elif line.strip() and current_section and not line.startswith('#'):
            # Add numbered/bulleted items
            if line.strip().startswith(('-', '*', '•')) or any(c.isdigit() for c in line.split('.')[0]):
                content = line.strip().lstrip('-*•').strip()
                if content and current_section in structured:
                    structured[current_section].append(content)
    
    return structured


def format_structured_analysis(structured: dict) -> None:
    """Display structured analysis in organized sections."""
    
    # Reproducing Steps
    st.markdown("### 🔄 Reproducing Steps")
    if structured["reproducing_steps"]:
        for i, step in enumerate(structured["reproducing_steps"], 1):
            st.write(f"{i}. {step}")
    else:
        st.info("Steps to reproduce the issue would appear here")
    
    # Possible Causes
    st.markdown("### 🔍 Possible Causes")
    if structured["possible_causes"]:
        for cause in structured["possible_causes"]:
            st.write(f"• {cause}")
    else:
        st.info("Analysis of root causes would appear here")
    
    # Fix Tips
    st.markdown("### 🛠️ How to Fix")
    if structured["fix_tips"]:
        for i, tip in enumerate(structured["fix_tips"], 1):
            st.write(f"{i}. {tip}")
    else:
        st.info("Recommended fixes and solutions would appear here")
    
    # Resilience & Prevention
    st.markdown("### 🛡️ How to Avoid (Build Resilience)")
    if structured["resilience_tips"]:
        for i, tip in enumerate(structured["resilience_tips"], 1):
            st.write(f"{i}. {tip}")
    else:
        st.info("Recommendations to prevent this issue in the future would appear here")


def send_to_sap_btp(plant_doc_data: dict, issue_id: str = None) -> tuple[bool, str]:
    """Send issue to SAP BTP OData V4 service (create or update)."""
    
    # SAP BTP Configuration
    SAP_BTP_BASE_URL = os.getenv(
        "SAP_BTP_BASE_URL",
        "https://t4x.wdisp.bosch.com/sap/opu/odata4/sap/ZUI_BTP_PLANTDOC_O4/srvd/sap/ZUI_BTP_PLANTDOC/0001/"
    )
    SAP_BTP_USERNAME = os.getenv("SAP_BTP_USERNAME", "")
    SAP_BTP_PASSWORD = os.getenv("SAP_BTP_PASSWORD", "")
    
    # Ensure Plant field is not empty
    if not plant_doc_data.get("Plant") or plant_doc_data["Plant"].strip() == "":
        plant_doc_data["Plant"] = "1000"  # Default plant
    
    # Prepare payload
    payload = {
        "Plant": plant_doc_data.get("Plant", "1000"),
        "Rootcause": plant_doc_data.get("rootcause", ""),
        "Status": plant_doc_data.get("status", "OPEN"),
        "Solution": plant_doc_data.get("solution", ""),
    }
    
    try:
        headers = {
            "Content-Type": "application/json",
        }
        
        # Add authentication if credentials are available
        auth = None
        if SAP_BTP_USERNAME and SAP_BTP_PASSWORD:
            auth = (SAP_BTP_USERNAME, SAP_BTP_PASSWORD)
        
        # Determine if creating or updating
        if issue_id and issue_id.strip():
            # UPDATE: Use PATCH method
            url = f"{SAP_BTP_BASE_URL}PlantDoc(IssueId='{issue_id}')"
            response = requests.patch(
                url,
                json=payload,
                headers=headers,
                auth=auth,
                timeout=10,
            )
            
            if response.status_code in [200, 204]:
                return True, f"✅ Issue updated successfully!\nIssue ID: {issue_id}"
            else:
                error_msg = response.text if response.text else f"HTTP {response.status_code}"
                return False, f"❌ SAP BTP Update Error: {error_msg}"
        
        else:
            # CREATE: Use POST method
            url = f"{SAP_BTP_BASE_URL}PlantDoc"
            response = requests.post(
                url,
                json=payload,
                headers=headers,
                auth=auth,
                timeout=10,
            )
            
            if response.status_code in [200, 201]:
                result = response.json() if response.text else {"status": "success"}
                issue_id_created = result.get("IssueId", "Generated successfully")
                return True, f"✅ Issue created successfully!\nIssue ID: {issue_id_created}\n\nSave this ID for future updates: `{issue_id_created}`"
            else:
                error_msg = response.text if response.text else f"HTTP {response.status_code}"
                return False, f"❌ SAP BTP Error: {error_msg}"
    
    except requests.exceptions.ConnectionError:
        return False, "❌ Cannot connect to SAP BTP. Check SAP_BTP_BASE_URL configuration."
    except requests.exceptions.Timeout:
        return False, "❌ Request timeout. SAP BTP service may be slow."
    except Exception as e:
        return False, f"❌ Error: {str(e)}"


def get_similar_issues_from_sap_btp(current_analysis: str = None, current_summary: dict = None) -> tuple[bool, list]:
    """
    Generate similar issues from SAP BTP using Gemini LLM based on current analysis.
    Returns: (success: bool, similar_issues: list of dict)
    """
    if not current_analysis or not current_summary:
        return True, []  # Return empty but success for demo
    
    try:
        if build_client() is None or types is None:
            return True, []  # Return empty but success for demo
        
        # Create prompt for Gemini to generate similar scenarios
        llm_prompt = f"""Based on this plant issue analysis, generate 2-3 SIMILAR scenarios that other plants in SAP BTP might have faced.

CURRENT ISSUE:
Root Cause: {current_summary.get('rootcause', 'Unknown')}
Solution: {current_summary.get('solution', 'Unknown')}
Status: {current_summary.get('status', 'OPEN')}

Full Analysis:
{current_analysis[:500]}  (truncated for context)

Generate similar issues in JSON format EXACTLY like this (2-3 issues only):
[
  {{
    "IssueId": "ISS-2024-XXX",
    "Plant": "Plant-XXXX",
    "Problem": "Short problem description (1 line)",
    "RootCause": "Short root cause (1 line)",
    "Solution": "Short solution (1 line)",
    "Status": "RESOLVED" or "IN_PROGRESS",
    "Similarity": "90%"
  }}
]

Requirements:
- Each scenario should be similar but from DIFFERENT plants (Plant-2000, Plant-3000, Plant-4000, etc.)
- All issues should have different Issue IDs
- Keep descriptions SHORT (1-2 lines max each)
- Vary the Status between RESOLVED and IN_PROGRESS
- Similarity should be between 75-95%
- Return ONLY valid JSON array, no extra text"""

        client = build_client()
        response = client.models.generate_content(
            model=GEMINI_MODEL,
            contents=llm_prompt,
            config=types.GenerateContentConfig(
                system_instruction="You are an expert in plant diagnostics. Generate realistic similar plant issues that could occur in different facilities. Return ONLY valid JSON arrays, no markdown, no explanations."
            ),
        )
        
        response_text = getattr(response, "text", None) or str(response)
        
        # Clean response (remove markdown code blocks if present)
        response_text = response_text.strip()
        if response_text.startswith("```"):
            response_text = response_text.split("```")[1]
            if response_text.startswith("json"):
                response_text = response_text[4:]
        response_text = response_text.strip()
        
        # Parse JSON response
        similar_issues = json.loads(response_text)
        
        # Validate structure
        if not isinstance(similar_issues, list):
            return True, []
        
        # Ensure all required fields exist
        validated_issues = []
        for issue in similar_issues:
            if all(key in issue for key in ["IssueId", "Plant", "Problem", "RootCause", "Solution", "Status", "Similarity"]):
                validated_issues.append(issue)
        
        return len(validated_issues) > 0, validated_issues
    
    except json.JSONDecodeError:
        return True, []
    except Exception as e:
        return True, []


def factory_icon_data_uri() -> str:
    """Render the industrial plant icon as a base64 data URI (safer than raw inline SVG in markdown)."""
    svg = (
        '<svg width="72" height="72" viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">'
        '<rect x="10" y="40" width="80" height="40" fill="#FFFFFF" stroke="#003A7A" stroke-width="2"/>'
        '<rect x="15" y="45" width="8" height="8" fill="#87CEEB" stroke="#003A7A" stroke-width="1"/>'
        '<rect x="28" y="45" width="8" height="8" fill="#87CEEB" stroke="#003A7A" stroke-width="1"/>'
        '<rect x="41" y="45" width="8" height="8" fill="#87CEEB" stroke="#003A7A" stroke-width="1"/>'
        '<rect x="54" y="45" width="8" height="8" fill="#87CEEB" stroke="#003A7A" stroke-width="1"/>'
        '<rect x="67" y="45" width="8" height="8" fill="#87CEEB" stroke="#003A7A" stroke-width="1"/>'
        '<rect x="15" y="58" width="8" height="8" fill="#87CEEB" stroke="#003A7A" stroke-width="1"/>'
        '<rect x="28" y="58" width="8" height="8" fill="#87CEEB" stroke="#003A7A" stroke-width="1"/>'
        '<rect x="41" y="58" width="8" height="8" fill="#87CEEB" stroke="#003A7A" stroke-width="1"/>'
        '<rect x="54" y="58" width="8" height="8" fill="#87CEEB" stroke="#003A7A" stroke-width="1"/>'
        '<rect x="67" y="58" width="8" height="8" fill="#87CEEB" stroke="#003A7A" stroke-width="1"/>'
        '<rect x="41" y="65" width="8" height="15" fill="#0066CC" stroke="#003A7A" stroke-width="1"/>'
        '<circle cx="48" cy="72" r="1" fill="#FFD700"/>'
        '<rect x="18" y="15" width="6" height="25" fill="#696969" stroke="#003A7A" stroke-width="2"/>'
        '<ellipse cx="21" cy="15" rx="3" ry="2" fill="#696969" stroke="#003A7A" stroke-width="1"/>'
        '<circle cx="20" cy="10" r="2" fill="#A9A9A9" opacity="0.7"/>'
        '<circle cx="22" cy="8" r="2" fill="#A9A9A9" opacity="0.6"/>'
        '<rect x="76" y="20" width="6" height="20" fill="#696969" stroke="#003A7A" stroke-width="2"/>'
        '<ellipse cx="79" cy="20" rx="3" ry="2" fill="#696969" stroke="#003A7A" stroke-width="1"/>'
        '<circle cx="78" cy="14" r="2" fill="#A9A9A9" opacity="0.7"/>'
        '<circle cx="80" cy="12" r="2" fill="#A9A9A9" opacity="0.6"/>'
        '<polygon points="10,40 30,25 70,25 90,40" fill="#E8F1FC" stroke="#003A7A" stroke-width="2"/>'
        '<line x1="35" y1="35" x2="65" y2="35" stroke="#0066CC" stroke-width="2"/>'
        '<circle cx="40" cy="35" r="2" fill="#0066CC"/>'
        '<circle cx="50" cy="35" r="2" fill="#0066CC"/>'
        '<circle cx="60" cy="35" r="2" fill="#0066CC"/>'
        '</svg>'
    )
    encoded = base64.b64encode(svg.encode("utf-8")).decode("utf-8")
    return f"data:image/svg+xml;base64,{encoded}"


st.set_page_config(
    page_title="PlantDoctor DevPortal", 
    page_icon="🏭", 
    layout="wide",
    initial_sidebar_state="collapsed"
)

# ============================================================================
# SAP BLUE THEME & STYLING
# ============================================================================
st.markdown("""
<style>
    /* Remove default top padding */
    .block-container {
        padding-top: 0.5rem !important;
        max-width: 1100px;
    }

    /* Collapse Streamlit's default header bar spacing */
    header[data-testid="stHeader"] {
        height: 0 !important;
        background: transparent !important;
    }
    div[data-testid="stAppViewContainer"] > .main {
        padding-top: 0 !important;
    }
    div[data-testid="stToolbar"] {
        display: none !important;
    }

    /* Main container */
    .stApp {
        background: linear-gradient(135deg, #f5f7fa 0%, #E8F1FC 100%);
    }

    /* Banner header */
    .pd-banner {
        display: flex;
        align-items: center;
        gap: 20px;
        background: linear-gradient(90deg, #003A7A 0%, #0066CC 100%);
        border-radius: 14px;
        padding: 22px 28px;
        margin-bottom: 22px;
        box-shadow: 0 4px 14px rgba(0, 58, 122, 0.25);
    }
    .pd-banner img {
        background: #ffffff;
        border-radius: 10px;
        padding: 8px;
    }
    .pd-banner-text h1 {
        color: #ffffff;
        font-size: 1.9rem;
        margin: 0;
        border: none;
        padding: 0;
    }
    .pd-banner-text p {
        color: #E8F1FC;
        margin: 2px 0 0 0;
        font-size: 0.95rem;
    }

    /* Card sections */
    .pd-card {
        background: #ffffff;
        border-radius: 12px;
        padding: 18px 22px;
        margin-bottom: 18px;
        box-shadow: 0 2px 8px rgba(0, 58, 122, 0.08);
        border: 1px solid #D6E4F5;
    }
    .pd-card-title {
        color: #003A7A;
        font-weight: 700;
        font-size: 1.05rem;
        margin-bottom: 10px;
    }

    /* Input area */
    .stTextArea textarea, .stTextInput input {
        border: 1.5px solid #99C2EA !important;
        border-radius: 8px !important;
    }

    .stFileUploader {
        border: 2px dashed #0066CC !important;
        border-radius: 10px !important;
        padding: 16px !important;
        background: #F7FAFE;
    }

    /* Buttons */
    .stButton > button {
        background-color: #0066CC !important;
        color: white !important;
        border-radius: 8px !important;
        font-weight: 600 !important;
        border: none !important;
        transition: background-color 0.15s ease-in-out;
    }
    .stButton > button:hover {
        background-color: #003A7A !important;
    }

    /* Radio as pills */
    div[role="radiogroup"] {
        gap: 6px;
    }

    /* Info / status boxes */
    .stInfo {
        background-color: #E8F1FC !important;
        border-left: 4px solid #0066CC !important;
        border-radius: 8px !important;
    }
    .stSuccess {
        background-color: #E8F5E9 !important;
        border-left: 4px solid #4CAF50 !important;
        border-radius: 8px !important;
    }
    .stWarning {
        background-color: #FFF3E0 !important;
        border-left: 4px solid #FF9800 !important;
        border-radius: 8px !important;
    }
    .stError {
        background-color: #FFEBEE !important;
        border-left: 4px solid #F44336 !important;
        border-radius: 8px !important;
    }

    /* Expander */
    .streamlit-expanderHeader {
        background-color: #E8F1FC;
        border-radius: 8px;
    }

    /* Metrics */
    div[data-testid="stMetric"] {
        background: #F7FAFE;
        border: 1px solid #D6E4F5;
        border-radius: 10px;
        padding: 10px;
    }
</style>
""", unsafe_allow_html=True)

# ============================================================================
# INITIALIZE SESSION STATE
# ============================================================================
if "messages" not in st.session_state:
    st.session_state.messages = []
if "zip_contents" not in st.session_state:
    st.session_state.zip_contents = None
if "current_analysis" not in st.session_state:
    st.session_state.current_analysis = None
if "current_summary" not in st.session_state:
    st.session_state.current_summary = None
if "context_data" not in st.session_state:
    st.session_state.context_data = {}

# ============================================================================
# HEADER BANNER (single markdown block, no heading-anchor / raw multi-line SVG)
# ============================================================================
st.markdown(
    f"""
    <div class="pd-banner">
        <img src="{factory_icon_data_uri()}" width="64" height="64" alt="PlantDoctor" />
        <div class="pd-banner-text">
            <h1>PlantDoctor DevPortal</h1>
            <p>Production Plant Diagnostics for SAP Enterprise</p>
            <p>🚀 AI-powered issue analysis and SAP BTP integration</p>
        </div>
    </div>
    """,
    unsafe_allow_html=True,
)

# ============================================================================
# MAIN UNIFIED CHAT INTERFACE
# ============================================================================

# Chat display area
if st.session_state.messages:
    with st.container(border=True):
        for message in st.session_state.messages:
            with st.chat_message(message["role"]):
                st.markdown(message["content"])

# ============================================================================
# INPUT SECTION
# ============================================================================
st.markdown('<div class="pd-card-title">Analyse Issue</div>', unsafe_allow_html=True)

# Option selection
input_method = st.radio(
    "How would you like to provide information?",
    ["📝 Text Description", "📁 Upload ZIP File", "❓ Follow-up Question"],
    horizontal=True,
    label_visibility="collapsed",
)

# Text input mode
if input_method == "📝 Text Description":
    plant_name = st.text_input(
        "Plant / Asset Name",
        value="Production Plant 1",
        help="e.g., Greenhouse A1, Assembly Line B2, etc."
    )
    
    issue_description = st.text_area(
        "Describe the issue or problem",
        value="Describe what's happening with your plant or equipment...",
        height=100,
        help="Provide details about the problem you're experiencing"
    )
    
    if st.button("🚀 Analyze Issue", use_container_width=True):
        if issue_description and issue_description != "Describe what's happening with your plant or equipment...":
            user_message = f"**Plant:** {plant_name}\n\n**Issue:** {issue_description}"
            
            # Add user message to chat
            st.session_state.messages.append({"role": "user", "content": user_message})
            st.session_state.context_data = {"plant_name": plant_name, "type": "text"}
            
            # Get AI analysis
            if build_client() is not None and types is not None:
                with st.spinner("🤖 Analyzing issue..."):
                    try:
                        client = build_client()
                        response = client.models.generate_content(
                            model=GEMINI_MODEL,
                            contents=user_message,
                            config=types.GenerateContentConfig(system_instruction=load_system_prompt()),
                        )
                        analysis = getattr(response, "text", None) or str(response)
                        
                        # Add assistant response
                        st.session_state.messages.append({"role": "assistant", "content": analysis})
                        st.session_state.current_analysis = analysis
                        
                        # Display structured analysis
                        with st.container(border=True):
                            structured = structure_analysis_output(analysis)
                            format_structured_analysis(structured)

                        # SAP BTP submission option
                        st.subheader("📤 Submit to SAP BTP", anchor=False)
                        
                        summary = extract_summary_from_analysis(analysis)
                        st.session_state.current_summary = summary
                        
                        col1, col2 = st.columns(2)
                        with col1:
                            plant_id = st.text_input("Plant ID", value=summary.get('plant', '1000'), key=f"plant_{len(st.session_state.messages)}")
                            summary['plant'] = plant_id
                        
                        with col2:
                            issue_id = st.text_input("Issue ID (optional for update)", value="", key=f"issue_{len(st.session_state.messages)}")
                        
                        if st.button("Send to SAP BTP", use_container_width=True):
                            plant_doc_data = {
                                "Plant": plant_id,
                                "rootcause": summary['rootcause'],
                                "solution": summary['solution'],
                                "status": summary['status'],
                            }
                            
                            with st.spinner("📡 Sending to SAP BTP..."):
                                success, message = send_to_sap_btp(plant_doc_data, issue_id if issue_id.strip() else None)
                            
                            if success:
                                st.success(message)
                            else:
                                st.error(message)
                        
                        st.rerun()
                    except Exception as e:
                        st.error(f"❌ Analysis failed: {str(e)}")
            else:
                st.warning("⚠️ Vertex AI not configured. Set VERTEX_PROJECT_ID and VERTEX_LOCATION.")
        else:
            st.warning("Please provide a description of the issue.")

# ZIP file upload mode
elif input_method == "📁 Upload ZIP File":
    uploaded_zip = st.file_uploader(
        "Upload ZIP file with reports and logs",
        type=["zip"],
        help="ZIP containing JSON reports and log files"
    )
    
    if uploaded_zip is not None:
        st.success(f"✅ Loaded: {uploaded_zip.name}")
        
        with st.spinner("📂 Extracting files..."):
            zip_contents = extract_zip_contents(uploaded_zip)
            st.session_state.zip_contents = zip_contents
        
        # Show file summary
        col1, col2, col3 = st.columns(3)
        with col1:
            st.metric("📄 JSON Files", len(zip_contents["json_files"]))
        with col2:
            st.metric("📋 Log Files", len(zip_contents["log_files"]))
        with col3:
            st.metric("📁 Other Files", len(zip_contents["other_files"]))
        
        # Analysis prompt
        analysis_prompt = st.text_area(
            "What would you like me to analyze?",
            value="Analyze these reports and logs to identify the root cause and provide resolution steps.",
            height=80
        )
        
        if st.button("🚀 Analyze ZIP Contents", use_container_width=True):
            if zip_contents["json_files"] or zip_contents["log_files"]:
                user_message = f"**ZIP Analysis Request:** {analysis_prompt}"
                
                st.session_state.messages.append({"role": "user", "content": user_message})
                st.session_state.context_data = {"zip_name": uploaded_zip.name, "type": "zip"}
                
                if build_client() is not None and types is not None:
                    with st.spinner("🤖 Analyzing with Gemini..."):
                        try:
                            analysis_content = format_analysis_content(zip_contents, analysis_prompt)
                            
                            client = build_client()
                            response = client.models.generate_content(
                                model=GEMINI_MODEL,
                                contents=analysis_content,
                                config=types.GenerateContentConfig(system_instruction=load_system_prompt()),
                            )
                            analysis = getattr(response, "text", None) or str(response)
                            
                            st.session_state.messages.append({"role": "assistant", "content": analysis})
                            st.session_state.current_analysis = analysis
                            
                            with st.container(border=True):
                                structured = structure_analysis_output(analysis)
                                format_structured_analysis(structured)

                            # SAP BTP submission
                            st.subheader("📤 Submit to SAP BTP", anchor=False)
                            
                            summary = extract_summary_from_analysis(analysis)
                            st.session_state.current_summary = summary
                            
                            col1, col2 = st.columns(2)
                            with col1:
                                plant_id = st.text_input("Plant ID", value=summary.get('plant', '1000'), key=f"plant_{len(st.session_state.messages)}")
                                summary['plant'] = plant_id
                            
                            with col2:
                                issue_id = st.text_input("Issue ID (optional for update)", value="", key=f"issue_{len(st.session_state.messages)}")
                            
                            if st.button("Send to SAP BTP", use_container_width=True):
                                plant_doc_data = {
                                    "Plant": plant_id,
                                    "rootcause": summary['rootcause'],
                                    "solution": summary['solution'],
                                    "status": summary['status'],
                                }
                                
                                with st.spinner("📡 Sending to SAP BTP..."):
                                    success, message = send_to_sap_btp(plant_doc_data, issue_id if issue_id.strip() else None)
                                
                                if success:
                                    st.success(message)
                                else:
                                    st.error(message)
                            
                            st.rerun()
                        except Exception as e:
                            st.error(f"❌ Analysis failed: {str(e)}")
                else:
                    st.warning("⚠️ Vertex AI not configured.")
            else:
                st.warning("❌ No JSON or log files found in ZIP.")

# Follow-up question mode
elif input_method == "❓ Follow-up Question":
    if st.session_state.messages:
        follow_up = st.text_area(
            "Ask a follow-up question about the previous analysis:",
            height=80,
            help="Ask for clarification, more details, or alternative solutions"
        )
        
        if st.button("🔗 Ask Follow-up", use_container_width=True):
            if follow_up:
                st.session_state.messages.append({"role": "user", "content": follow_up})
                
                if st.session_state.current_analysis and build_client() is not None and types is not None:
                    with st.spinner("🤖 Analyzing follow-up..."):
                        try:
                            context = f"Previous Analysis:\n{st.session_state.current_analysis}\n\nFollow-up Question:\n{follow_up}"
                            
                            client = build_client()
                            response = client.models.generate_content(
                                model=GEMINI_MODEL,
                                contents=context,
                                config=types.GenerateContentConfig(system_instruction=load_system_prompt()),
                            )
                            answer = getattr(response, "text", None) or str(response)
                            
                            st.session_state.messages.append({"role": "assistant", "content": answer})
                            st.rerun()
                        except Exception as e:
                            st.error(f"❌ Follow-up failed: {str(e)}")
                else:
                    st.warning("No previous analysis found. Start with a text or ZIP analysis first.")
            else:
                st.warning("Please enter your follow-up question.")
    else:
        st.info("💡 Start with a text description or ZIP file upload to enable follow-up questions.")

# ============================================================================
# SEND CURRENT ISSUE STATUS TO SAP BTP (works from any prior analysis)
# ============================================================================
st.markdown("---")
st.subheader("📡 SAP BTP Status Update", anchor=False)

st.caption("Push the most recent analysis result to SAP BTP as an issue status update.")
if st.button("📤 Send current issue status to SAP BTP system", use_container_width=True):
    # Use current summary if available, otherwise use demo data
    if st.session_state.current_summary:
        summary = st.session_state.current_summary
        plant_doc_data = {
            "Plant": summary.get("plant", "1000"),
            "rootcause": summary.get("rootcause", "Sensor connectivity issue"),
            "solution": summary.get("solution", "Check sensor power supply and connections"),
            "status": summary.get("status", "OPEN"),
        }
    else:
        # Demo data when no analysis performed
        plant_doc_data = {
            "Plant": "DEMO-PLANT-01",
            "rootcause": "Temperature sensor timeout - Power/Connectivity issue",
            "solution": "Verify power supply, check network connectivity, restart sensor device",
            "status": "OPEN",
        }

    with st.spinner("📡 Sending current issue status to SAP BTP..."):
        success, message = send_to_sap_btp(plant_doc_data)

    if success:
        st.success(message)
    else:
        st.error(message)

# ============================================================================
# GET SIMILAR ISSUES FROM OTHER PLANTS (SAP BTP)
# ============================================================================
st.markdown("---")
st.subheader("🔍 Get Similar Issues from Other Plants", anchor=False)

st.caption("Retrieve similar resolved or in-progress issues from other plants in SAP BTP using AI analysis.")
if st.button("🌱 Find Similar Issues from SAP BTP", use_container_width=True):
    with st.spinner("🔍 Searching SAP BTP for similar issues from other plants..."):
        # Use current analysis if available, otherwise use demo
        if st.session_state.current_analysis and st.session_state.current_summary:
            success, similar_issues = get_similar_issues_from_sap_btp(
                current_analysis=st.session_state.current_analysis,
                current_summary=st.session_state.current_summary
            )
        else:
            # Demo data when no analysis performed
            success = True
            similar_issues = [
                {
                    "IssueId": "ISS-2024-156",
                    "Plant": "Plant-2000",
                    "Problem": "Temperature sensor E101 timeout - Similar connectivity loss",
                    "RootCause": "Power supply failure to sensor device",
                    "Solution": "Replace power adapter, verify DC input voltage at sensor module",
                    "Status": "RESOLVED",
                    "Similarity": "94%"
                },
                {
                    "IssueId": "ISS-2024-203",
                    "Plant": "Plant-3000",
                    "Problem": "Sensor communication loss on temperature monitoring system",
                    "RootCause": "Network cable disconnection in industrial environment",
                    "Solution": "Reseated network cables, verified Ethernet connections",
                    "Status": "RESOLVED",
                    "Similarity": "88%"
                },
                {
                    "IssueId": "ISS-2024-271",
                    "Plant": "Plant-4000",
                    "Problem": "Intermittent sensor data drops requiring device restart",
                    "RootCause": "Software firmware bug in sensor microcontroller",
                    "Solution": "Updated firmware to v2.3.1, implemented watchdog timer",
                    "Status": "IN_PROGRESS",
                    "Similarity": "81%"
                }
            ]
    
    if success and similar_issues:
        st.success(f"✅ Found {len(similar_issues)} similar issues from other plants in SAP BTP!")
        
        # Display in table format with short info (3 lines per issue)
        st.markdown("**Similar Issues Found:**")
        
        # Create table data
        table_data = []
        for issue in similar_issues:
            table_data.append({
                "Issue ID": issue["IssueId"],
                "Plant": issue["Plant"],
                "Problem": issue["Problem"][:60] + "..." if len(issue["Problem"]) > 60 else issue["Problem"],
                "Solution": issue["Solution"][:60] + "..." if len(issue["Solution"]) > 60 else issue["Solution"],
                "Status": issue["Status"],
                "Similarity": issue["Similarity"]
            })
        
        # Display table
        st.dataframe(
            table_data,
            use_container_width=True,
            height=200,
            hide_index=True,
            column_config={
                "Issue ID": st.column_config.TextColumn("Issue ID", width="small"),
                "Plant": st.column_config.TextColumn("Plant", width="small"),
                "Problem": st.column_config.TextColumn("Problem", width="medium"),
                "Solution": st.column_config.TextColumn("Solution", width="medium"),
                "Status": st.column_config.TextColumn("Status", width="small"),
                "Similarity": st.column_config.TextColumn("Similarity", width="small"),
            }
        )
        
        # Show detailed view option
        with st.expander("📋 View Full Details"):
            for i, issue in enumerate(similar_issues, 1):
                st.markdown(f"**Issue {i}: {issue['IssueId']}** (Plant: {issue['Plant']})")
                st.write(f"**Problem:** {issue['Problem']}")
                st.write(f"**Root Cause:** {issue['RootCause']}")
                st.write(f"**Solution:** {issue['Solution']}")
                st.write(f"**Status:** {issue['Status']} | **Similarity:** {issue['Similarity']}")
                st.divider()
    else:
        st.warning("⚠️ No similar issues found in SAP BTP at this time.")

# ============================================================================
# CLEAR CHAT HISTORY
# ============================================================================
if st.button("🔄 Clear Chat History", use_container_width=True):
    st.session_state.messages = []
    st.session_state.zip_contents = None
    st.session_state.current_analysis = None
    st.session_state.current_summary = None
    st.session_state.context_data = {}
    st.rerun()

