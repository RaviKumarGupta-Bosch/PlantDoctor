import os
import json
import zipfile
import tempfile
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
GEMINI_MODEL = os.getenv("GEMINI_MODEL", "gemini-2.5-flash")  # Stable, proven in asia-northeast1


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


st.set_page_config(
    page_title="PlantDoctor DevPortal", 
    page_icon="🌿", 
    layout="wide",
    initial_sidebar_state="collapsed"
)

# ============================================================================
# SAP BLUE THEME & STYLING
# ============================================================================
st.markdown("""
<style>
    /* SAP Blue Theme */
    :root {
        --sap-blue-primary: #0066CC;
        --sap-blue-dark: #003A7A;
        --sap-blue-light: #E8F1FC;
        --plant-green: #2D5016;
    }
    
    /* Main container */
    .main {
        background: linear-gradient(135deg, #f5f7fa 0%, #E8F1FC 100%);
    }
    
    /* Header styling */
    h1 {
        color: #0066CC;
        border-bottom: 3px solid #0066CC;
        padding-bottom: 10px;
        margin-bottom: 5px;
    }
    
    h2 {
        color: #003A7A;
    }
    
    /* Chat container */
    .chat-container {
        background: white;
        border: 1px solid #0066CC;
        border-radius: 8px;
        padding: 15px;
        margin: 10px 0;
    }
    
    /* Input area */
    .stTextArea textarea {
        border: 2px solid #0066CC !important;
        border-radius: 6px !important;
    }
    
    .stFileUploader {
        border: 2px dashed #0066CC !important;
        border-radius: 6px !important;
        padding: 20px !important;
    }
    
    /* Buttons */
    .stButton > button {
        background-color: #0066CC !important;
        color: white !important;
        border-radius: 6px !important;
        font-weight: 600 !important;
        border: none !important;
    }
    
    .stButton > button:hover {
        background-color: #003A7A !important;
    }
    
    /* Info boxes */
    .stInfo {
        background-color: #E8F1FC !important;
        border-left: 4px solid #0066CC !important;
        border-radius: 6px !important;
    }
    
    .stSuccess {
        background-color: #E8F5E9 !important;
        border-left: 4px solid #4CAF50 !important;
    }
    
    .stWarning {
        background-color: #FFF3E0 !important;
        border-left: 4px solid #FF9800 !important;
    }
    
    .stError {
        background-color: #FFEBEE !important;
        border-left: 4px solid #F44336 !important;
    }
    
    /* Expander */
    .streamlit-expanderHeader {
        background-color: #E8F1FC;
        border-left: 4px solid #0066CC;
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
# HEADER WITH PLANT PICTURE
# ============================================================================
col1, col2 = st.columns([1, 4])

with col1:
    # Simple plant SVG icon
    st.markdown("""
    <svg width="80" height="80" viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
        <!-- Pot -->
        <path d="M 30 50 L 20 80 L 80 80 L 70 50 Z" fill="#D2691E" stroke="#8B4513" stroke-width="2"/>
        <ellipse cx="50" cy="50" rx="20" ry="8" fill="#CD853F" stroke="#8B4513" stroke-width="2"/>
        
        <!-- Soil -->
        <ellipse cx="50" cy="52" rx="18" ry="6" fill="#8B7355"/>
        
        <!-- Main stem -->
        <path d="M 50 52 Q 50 30 45 15" stroke="#2D5016" stroke-width="3" fill="none" stroke-linecap="round"/>
        
        <!-- Left leaf -->
        <path d="M 50 40 Q 35 35 30 25" stroke="#2D5016" stroke-width="2" fill="none" stroke-linecap="round"/>
        <path d="M 50 40 Q 40 28 35 18" stroke="#4CAF50" stroke-width="2" fill="none" stroke-linecap="round"/>
        
        <!-- Right leaf -->
        <path d="M 50 40 Q 65 35 70 25" stroke="#2D5016" stroke-width="2" fill="none" stroke-linecap="round"/>
        <path d="M 50 40 Q 60 28 65 18" stroke="#4CAF50" stroke-width="2" fill="none" stroke-linecap="round"/>
        
        <!-- Top leaf -->
        <path d="M 45 15 L 40 5" stroke="#4CAF50" stroke-width="2" fill="none" stroke-linecap="round"/>
        <path d="M 45 15 L 50 2" stroke="#66BB6A" stroke-width="2" fill="none" stroke-linecap="round"/>
    </svg>
    """, unsafe_allow_html=True)

with col2:
    st.title("🌿 PlantDoctor DevPortal")
    st.caption("**Production Plant Diagnostics for SAP Enterprise**")
    st.caption("🚀 AI-powered issue analysis and SAP BTP integration")

st.markdown("---")

# ============================================================================
# MAIN UNIFIED CHAT INTERFACE
# ============================================================================

# Chat display area
st.subheader("💬 Analysis Chat")
chat_container = st.container()

with chat_container:
    for message in st.session_state.messages:
        with st.chat_message(message["role"]):
            st.markdown(message["content"])

# ============================================================================
# INPUT SECTION
# ============================================================================
st.subheader("🔧 Start Analysis")

# Option selection
input_method = st.radio(
    "How would you like to provide information?",
    ["📝 Text Description", "📁 Upload ZIP File", "❓ Follow-up Question"],
    horizontal=True
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
                        st.markdown("---")
                        structured = structure_analysis_output(analysis)
                        format_structured_analysis(structured)
                        
                        # SAP BTP submission option
                        st.markdown("---")
                        st.subheader("📤 Submit to SAP BTP")
                        
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
                            
                            st.markdown("---")
                            structured = structure_analysis_output(analysis)
                            format_structured_analysis(structured)
                            
                            # SAP BTP submission
                            st.markdown("---")
                            st.subheader("📤 Submit to SAP BTP")
                            
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
# CLEAR CHAT HISTORY
# ============================================================================
st.markdown("---")
if st.button("🔄 Clear Chat History", use_container_width=True):
    st.session_state.messages = []
    st.session_state.zip_contents = None
    st.session_state.current_analysis = None
    st.session_state.current_summary = None
    st.session_state.context_data = {}
    st.rerun()

