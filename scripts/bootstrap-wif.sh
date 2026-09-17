#!/bin/bash

# ============================================================================
# WIF Bootstrap Script for PlantDoctor GCP Project
# ============================================================================
# This script sets up Workload Identity Federation (WIF) for GitHub Actions
# to deploy to Cloud Run without using long-lived secrets.
#
# Project Details:
#   - Project ID: plantdoctor-codingaura
#   - Project Number: 655336664371
#   - GitHub Repo: https://github.com/RaviKumarGupta-Bosch/PlantDoctor
#   - Region: asia-south1 (India)
# ============================================================================

set -e

# Configuration
PROJECT_ID="plantdoctor-codingaura"
PROJECT_NUMBER="655336664371"
REGION="asia-south1"
GITHUB_ORG="RaviKumarGupta-Bosch"
GITHUB_REPO="PlantDoctor"
WORKLOAD_IDENTITY_POOL_ID="github-pool"
WORKLOAD_IDENTITY_PROVIDER_ID="github-provider"
SERVICE_ACCOUNT_ID="plantdoctor-deployer"
SERVICE_ACCOUNT_EMAIL="${SERVICE_ACCOUNT_ID}@${PROJECT_ID}.iam.gserviceaccount.com"

echo "🚀 Starting Workload Identity Federation Setup"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Project ID: ${PROJECT_ID}"
echo "Project Number: ${PROJECT_NUMBER}"
echo "Region: ${REGION}"
echo "GitHub: ${GITHUB_ORG}/${GITHUB_REPO}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Step 1: Set the project
echo ""
echo "📋 Step 1: Setting GCP Project..."
gcloud config set project "${PROJECT_ID}"
echo "✅ Project set to ${PROJECT_ID}"

# Step 2: Enable required APIs
echo ""
echo "🔌 Step 2: Enabling required APIs..."
gcloud services enable \
  iam.googleapis.com \
  cloudresourcemanager.googleapis.com \
  sts.googleapis.com \
  serviceusage.googleapis.com \
  run.googleapis.com \
  artifactregistry.googleapis.com \
  aiplatform.googleapis.com \
  secretmanager.googleapis.com
echo "✅ APIs enabled"

# Step 3: Create Workload Identity Pool
echo ""
echo "🏊 Step 3: Creating Workload Identity Pool..."
if ! gcloud iam workload-identity-pools describe "${WORKLOAD_IDENTITY_POOL_ID}" \
  --location=global \
  --project="${PROJECT_ID}" >/dev/null 2>&1; then
  gcloud iam workload-identity-pools create "${WORKLOAD_IDENTITY_POOL_ID}" \
    --project="${PROJECT_ID}" \
    --location=global \
    --display-name="GitHub Pool for ${GITHUB_ORG}/${GITHUB_REPO}"
  echo "✅ Workload Identity Pool created: ${WORKLOAD_IDENTITY_POOL_ID}"
else
  echo "⏭️  Workload Identity Pool already exists: ${WORKLOAD_IDENTITY_POOL_ID}"
fi

# Step 4: Create Workload Identity Provider (GitHub)
echo ""
echo "🔐 Step 4: Creating Workload Identity Provider for GitHub..."
if ! gcloud iam workload-identity-providers describe "${WORKLOAD_IDENTITY_PROVIDER_ID}" \
  --location=global \
  --workload-identity-pool="${WORKLOAD_IDENTITY_POOL_ID}" \
  --project="${PROJECT_ID}" >/dev/null 2>&1; then
  gcloud iam workload-identity-providers create-oidc "${WORKLOAD_IDENTITY_PROVIDER_ID}" \
    --project="${PROJECT_ID}" \
    --location=global \
    --workload-identity-pool="${WORKLOAD_IDENTITY_POOL_ID}" \
    --display-name="GitHub Provider" \
    --attribute-mapping="google.subject=assertion.sub,attribute.aud=assertion.aud,attribute.repository=assertion.repository,attribute.repository_owner=assertion.repository_owner" \
    --issuer-uri="https://token.actions.githubusercontent.com"
  echo "✅ Workload Identity Provider created: ${WORKLOAD_IDENTITY_PROVIDER_ID}"
else
  echo "⏭️  Workload Identity Provider already exists: ${WORKLOAD_IDENTITY_PROVIDER_ID}"
fi

# Step 5: Create Service Account
echo ""
echo "👤 Step 5: Creating Service Account..."
if ! gcloud iam service-accounts describe "${SERVICE_ACCOUNT_EMAIL}" \
  --project="${PROJECT_ID}" >/dev/null 2>&1; then
  gcloud iam service-accounts create "${SERVICE_ACCOUNT_ID}" \
    --project="${PROJECT_ID}" \
    --display-name="PlantDoctor CI/CD Deployer" \
    --description="Service account for GitHub Actions to deploy to Cloud Run"
  echo "✅ Service Account created: ${SERVICE_ACCOUNT_EMAIL}"
else
  echo "⏭️  Service Account already exists: ${SERVICE_ACCOUNT_EMAIL}"
fi

# Step 6: Grant IAM roles to Service Account
echo ""
echo "🔑 Step 6: Granting IAM roles to Service Account..."

ROLES=(
  "roles/run.admin"                    # Deploy to Cloud Run
  "roles/aiplatform.user"              # Use Vertex AI (for Claude API)
  "roles/secretmanager.admin"          # Create/update secrets
  "roles/artifactregistry.writer"      # Push to Artifact Registry
  "roles/serviceusage.serviceUsageConsumer"  # Use APIs
)

for ROLE in "${ROLES[@]}"; do
  echo "  Granting ${ROLE}..."
  gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
    --member="serviceAccount:${SERVICE_ACCOUNT_EMAIL}" \
    --role="${ROLE}" \
    --condition=None 2>/dev/null || echo "    ℹ️  Role may already be assigned"
done
echo "✅ IAM roles granted"

# Step 7: Set up Workload Identity Pool Provider for GitHub
echo ""
echo "🔗 Step 7: Configuring GitHub ↔ GCP Trust..."

# Get the Workload Identity Pool resource name
WIP_NAME="projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/${WORKLOAD_IDENTITY_POOL_ID}"
WIP_PROVIDER_NAME="${WIP_NAME}/providers/${WORKLOAD_IDENTITY_PROVIDER_ID}"

# Grant the service account permission to use the Workload Identity Provider
gcloud iam service-accounts add-iam-policy-binding "${SERVICE_ACCOUNT_EMAIL}" \
  --project="${PROJECT_ID}" \
  --role="roles/iam.workloadIdentityUser" \
  --member="principalSet://iam.googleapis.com/projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/${WORKLOAD_IDENTITY_POOL_ID}/attribute.repository/${GITHUB_ORG}/${GITHUB_REPO}" \
  2>/dev/null || echo "✅ Trust already configured"

echo "✅ GitHub ↔ GCP Trust configured"

# Step 8: Create Artifact Registry Repository (if needed)
echo ""
echo "📦 Step 8: Setting up Artifact Registry..."
if ! gcloud artifacts repositories describe "adk-agents" \
  --location="${REGION}" \
  --project="${PROJECT_ID}" >/dev/null 2>&1; then
  gcloud artifacts repositories create "adk-agents" \
    --repository-format=docker \
    --location="${REGION}" \
    --project="${PROJECT_ID}" \
    --description="PlantDoctor ADK Agents Docker Repository"
  echo "✅ Artifact Registry repository created: adk-agents"
else
  echo "⏭️  Artifact Registry repository already exists: adk-agents"
fi

# Step 9: Display Configuration Summary
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "✅ WIF Setup Complete!"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "📝 Next Steps: Configure GitHub Repository Variables"
echo ""
echo "Add these as GitHub repository variables (Settings > Secrets and variables > Variables):"
echo ""
echo "  1. GCP_WORKLOAD_IDENTITY_PROVIDER:"
echo "     ${WIP_PROVIDER_NAME}"
echo ""
echo "  2. GCP_SERVICE_ACCOUNT:"
echo "     ${SERVICE_ACCOUNT_EMAIL}"
echo ""
echo "  3. PROJECT_ID:"
echo "     ${PROJECT_ID}"
echo ""
echo "  4. REGION:"
echo "     ${REGION}"
echo ""
echo "Optional GitHub Variables:"
echo ""
echo "  5. SAP_BTP_MOCK_MODE:"
echo "     true (for testing without SAP BTP credentials)"
echo ""
echo "  6. DEVPORTAL_ALLOWED_INVOKERS:"
echo "     serviceAccount:YOUR_SERVICE_ACCOUNT@PROJECT_ID.iam.gserviceaccount.com (comma-separated)"
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "🔗 Workload Identity Provider Resource Name:"
echo "   ${WIP_PROVIDER_NAME}"
echo ""
echo "👤 Service Account:"
echo "   ${SERVICE_ACCOUNT_EMAIL}"
echo ""
echo "📍 Region: ${REGION}"
echo "🌍 Artifact Registry: ${REGION}-docker.pkg.dev/${PROJECT_ID}/adk-agents"
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
