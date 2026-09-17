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
GEMINI_MODEL = os.getenv("GEMINI_MODEL", "gemini-2.0-pro")  # Advanced model by default


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


st.set_page_config(page_title="PlantDoctor DevPortal", page_icon="🌿", layout="wide")
st.title("🌿 PlantDoctor DevPortal")
st.caption("Diagnose plant issues with AI analysis and route actions to SAP BTP workflows.")

# Display active Gemini model
col1, col2, col3 = st.columns([3, 1, 1])
with col3:
    st.caption(f"🤖 Model: `{GEMINI_MODEL}`")

prompt = load_system_prompt()

# Create tabs for different input methods
tab1, tab2 = st.tabs(["📝 Manual Analysis", "📁 Upload Report (ZIP)"])

# ============================================================================
# TAB 1: Manual Input Mode
# ============================================================================
with tab1:
    st.subheader("Manual Plant Diagnostics")
    
    with st.form("diagnostic_form"):
        plant_name = st.text_input("Plant / asset name", value="Greenhouse A1")
        issue_summary = st.text_area(
            "Issue summary",
            value="Leaves are yellowing and the soil is dry despite recent watering.",
            height=180,
        )
        submitted = st.form_submit_button("🔍 Analyze", use_container_width=True)

    if submitted:
        user_message = f"Plant: {plant_name}\nIssue summary: {issue_summary}"

        if build_client() is not None and types is not None:
            with st.spinner("Analyzing plant health..."):
                client = build_client()
                response = client.models.generate_content(
                    model=GEMINI_MODEL,
                    contents=user_message,
                    config=types.GenerateContentConfig(system_instruction=prompt),
                )
                answer = getattr(response, "text", None) or str(response)
        else:
            answer = (
                "⚠️ Vertex AI is not configured for this local run. "
                "Set VERTEX_PROJECT_ID and VERTEX_LOCATION to enable live analysis. "
                "For Cloud Run, the deployment workflow injects those values automatically."
            )

        st.markdown("---")
        st.markdown("## 🔍 Analysis Results")
        
        # Structure the analysis for better readability
        structured = structure_analysis_output(answer)
        format_structured_analysis(structured)
        
        # Show full analysis in expander
        with st.expander("📖 Full Analysis Report"):
            st.markdown(answer)

    else:
        st.info(
            "💡 Enter a plant issue and click Analyze to get a diagnostic recommendation. "
            "This service is intended to run behind Cloud Run with Google Vertex AI enabled."
        )

# ============================================================================
# TAB 2: File Upload Mode
# ============================================================================
with tab2:
    st.subheader("Analyze Offline AI Reports & Logs")
    
    col1, col2 = st.columns([2, 1])
    
    with col1:
        st.write(
            "📦 **Upload a ZIP file** containing:\n"
            "- JSON analysis reports (from Ollama, TensorFlow, or other offline AI)\n"
            "- Log files (.log, .txt)\n\n"
            "The system will analyze the reports and logs to diagnose issues and provide resolution steps."
        )
    
    uploaded_zip = st.file_uploader(
        "Choose a ZIP file",
        type=["zip"],
        help="Upload a ZIP file containing analysis reports (JSON) and log files",
    )
    
    if uploaded_zip is not None:
        st.success(f"✅ ZIP file loaded: {uploaded_zip.name}")
        
        # Extract contents
        with st.spinner("📂 Extracting files from ZIP..."):
            zip_contents = extract_zip_contents(uploaded_zip)
        
        # Display extracted files summary
        st.subheader("📊 Extracted Contents")
        col1, col2, col3 = st.columns(3)
        with col1:
            st.metric("📄 JSON Files", len(zip_contents["json_files"]))
        with col2:
            st.metric("📋 Log Files", len(zip_contents["log_files"]))
        with col3:
            st.metric("📁 Other Files", len(zip_contents["other_files"]))
        
        # Show file preview
        with st.expander("📋 Preview Extracted Files"):
            if zip_contents["json_files"]:
                st.write("**JSON Files:**")
                for filename in zip_contents["json_files"].keys():
                    st.code(filename, language="text")
            
            if zip_contents["log_files"]:
                st.write("**Log Files:**")
                for filename in zip_contents["log_files"].keys():
                    st.code(filename, language="text")
        
        # User prompt for analysis
        st.subheader("🎯 Analysis Request")
        user_prompt = st.text_area(
            "Describe what issue you're investigating:",
            value="Analyze the provided reports and logs to identify the root cause and provide reproduction steps.",
            height=120,
            help="Provide context about what you're investigating or what errors you're seeing",
        )
        
        if st.button("🚀 Send to Gemini for Analysis", use_container_width=True):
            if build_client() is not None and types is not None:
                with st.spinner("🤖 Analyzing with Gemini..."):
                    # Format content for Gemini
                    analysis_content = format_analysis_content(zip_contents, user_prompt)
                    
                    client = build_client()
                    response = client.models.generate_content(
                        model=GEMINI_MODEL,
                        contents=analysis_content,
                        config=types.GenerateContentConfig(system_instruction=prompt),
                    )
                    analysis_result = getattr(response, "text", None) or str(response)
                
                # Display results
                st.markdown("---")
                st.markdown("## 🔍 Analysis Results")
                
                # Structure the analysis for better readability
                structured = structure_analysis_output(analysis_result)
                format_structured_analysis(structured)
                
                # Show full analysis in expander
                with st.expander("📖 Full Analysis Report"):
                    st.markdown(analysis_result)
                
                # Extract and display summary
                st.markdown("---")
                st.markdown("### 📋 Analysis Summary for SAP BTP")
                
                summary = extract_summary_from_analysis(analysis_result)
                
                col1, col2 = st.columns(2)
                with col1:
                    st.info(f"**Root Cause:**\n{summary['rootcause'] or 'Not extracted'}")
                with col2:
                    st.success(f"**Status:**\n{summary['status']}")
                
                col1, col2 = st.columns(2)
                with col1:
                    st.warning(f"**Solution:**\n{summary['solution'] or 'Not extracted'}")
                with col2:
                    plant_input = st.text_input("Plant ID", value=summary.get('plant', '1000'), key="plant_input")
                    summary['plant'] = plant_input
                
                # SAP BTP Integration
                st.markdown("---")
                st.markdown("### 🚀 Submit to SAP BTP")
                
                # Issue ID input for updates
                col1, col2 = st.columns([2, 1])
                with col1:
                    issue_id_input = st.text_input(
                        "Issue ID (optional - leave empty to create new, or paste existing ID to update)",
                        value="",
                        placeholder="e.g., 550e8400-e29b-41d4-a716-446655440000",
                        help="If you have an existing Issue ID from a previous submission, paste it here to update that issue instead of creating a new one."
                    )
                with col2:
                    if issue_id_input.strip():
                        st.info("🔄 **Update Mode**\nWill update existing issue")
                    else:
                        st.info("✨ **Create Mode**\nWill create new issue")
                
                # Send to SAP BTP Button
                col1, col2, col3 = st.columns([2, 1, 1])
                
                with col1:
                    st.info("Send this analysis to SAP BTP for workflow processing and tracking.")
                
                with col2:
                    if st.button("📤 Send to SAP BTP", use_container_width=True):
                        # Prepare data for SAP BTP
                        plant_doc_data = {
                            "Plant": summary.get('plant', '1000'),
                            "rootcause": summary['rootcause'],
                            "solution": summary['solution'],
                            "status": summary['status'],
                        }
                        
                        with st.spinner("📡 Sending to SAP BTP..."):
                            success, message = send_to_sap_btp(plant_doc_data, issue_id_input.strip() if issue_id_input else None)
                        
                        if success:
                            st.success(message)
                            # Copy issue ID to clipboard info
                            if not issue_id_input.strip():
                                st.info("💡 **Tip:** Copy the Issue ID shown above and paste it here next time to update this issue.")
                        else:
                            st.error(message)
                
                with col3:
                    st.button("⚙️ Settings", key="settings_btn", help="Configure SAP BTP connection")
                
                # Option to copy results
                st.download_button(
                    label="📥 Download Analysis as Text",
                    data=analysis_result,
                    file_name="plantdoctor_analysis.txt",
                    mime="text/plain",
                )
            else:
                st.error(
                    "⚠️ Vertex AI is not configured. "
                    "Set VERTEX_PROJECT_ID and VERTEX_LOCATION environment variables."
                )
