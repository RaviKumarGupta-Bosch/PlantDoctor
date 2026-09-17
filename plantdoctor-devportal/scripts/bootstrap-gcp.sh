#!/usr/bin/env bash
# One-time bootstrap for a NEW GCP project that will run plantdoctor-devportal
# via .github/workflows/deploy-devportal.yml using Workload Identity Federation
# (WIF) — no service account keys, no GitHub secrets required for GCP auth.
#
# Run this ONCE per new project, from a machine authenticated as a user/owner
# with permission to enable APIs, create service accounts, and manage IAM on
# the target project (e.g. via `gcloud auth login` with Owner/Editor + IAM
# Admin, or Cloud Shell in the target project).
#
# Safe to re-run: every step checks for an existing resource before creating
# it, matching the same idempotent style used in deploy-devportal.yml.
set -euo pipefail

# ---------------------------------------------------------------------------
# Configuration — edit these before running, or export as env vars.
# ---------------------------------------------------------------------------
PROJECT_ID="${PROJECT_ID:-plantdoctor-teamcodingaura}"
PROJECT_NUMBER="${PROJECT_NUMBER:-305630320430}"
REGION="${REGION:-us-central1}"
AR_REPO="${AR_REPO:-adk-agents}"

# GitHub host and "org/repo" slug that is allowed to
# impersonate the deployment service account. Restricting the WIF provider's
# attribute-condition to this exact repo prevents any other repo on GitHub
# from assuming this identity.
GH_HOST="${GH_HOST:-github.com}"
GH_REPO="${GH_REPO:-RaviKumarGupta-Bosch/PlantDoctor}"

POOL_ID="${POOL_ID:-github-actions-pool}"
PROVIDER_ID="${PROVIDER_ID:-github-actions-provider}"
SA_NAME="${SA_NAME:-plantdoctor-devportal-deployer}"
SA_EMAIL="${SA_NAME}@${PROJECT_ID}.iam.gserviceaccount.com"

echo "🔧 Using project: ${PROJECT_ID} (number: ${PROJECT_NUMBER}), region: ${REGION}"
gcloud config set project "${PROJECT_ID}" >/dev/null

# ---------------------------------------------------------------------------
# 1. Enable required APIs
# ---------------------------------------------------------------------------
echo "🔧 Enabling required APIs..."
gcloud services enable \
  run.googleapis.com \
  artifactregistry.googleapis.com \
  iamcredentials.googleapis.com \
  iam.googleapis.com \
  secretmanager.googleapis.com \
  aiplatform.googleapis.com \
  sts.googleapis.com \
  cloudresourcemanager.googleapis.com \
  --project="${PROJECT_ID}"

# ---------------------------------------------------------------------------
# 2. Create the deployment / runtime service account
# ---------------------------------------------------------------------------
echo "🔐 Ensuring service account ${SA_EMAIL} exists..."
gcloud iam service-accounts describe "${SA_EMAIL}" --project="${PROJECT_ID}" >/dev/null 2>&1 || \
  gcloud iam service-accounts create "${SA_NAME}" \
    --project="${PROJECT_ID}" \
    --display-name="PlantDoctor DevPortal CI/CD + Cloud Run runtime"

# ---------------------------------------------------------------------------
# 3. Grant the service account the roles it needs
#    (this one identity is used both by CI to deploy and by Cloud Run at
#    runtime, since deploy-devportal.yml passes --service-account="${SA_EMAIL}")
# ---------------------------------------------------------------------------
echo "🔐 Granting IAM roles to ${SA_EMAIL}..."
for ROLE in \
  "roles/run.admin" \
  "roles/artifactregistry.writer" \
  "roles/iam.serviceAccountUser" \
  "roles/aiplatform.user" \
  "roles/secretmanager.admin"; do
  echo "  - ${ROLE}"
  gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
    --member="serviceAccount:${SA_EMAIL}" \
    --role="${ROLE}" \
    --condition=None 2>/dev/null || echo "    already granted"
done
# Note: roles/secretmanager.admin (not just secretAccessor) is required
# because this same SA both creates/updates SAP_BTP_CLIENT_SECRET during CI
# and reads it at Cloud Run runtime. secretAccessor alone cannot create
# secrets, which would silently break the idempotent "create if missing"
# step in deploy-devportal.yml.

# ---------------------------------------------------------------------------
# 4. Create the shared Artifact Registry repo
# ---------------------------------------------------------------------------
echo "📦 Ensuring Artifact Registry repo '${AR_REPO}' exists in ${REGION}..."
gcloud artifacts repositories describe "${AR_REPO}" \
  --project="${PROJECT_ID}" --location="${REGION}" >/dev/null 2>&1 || \
  gcloud artifacts repositories create "${AR_REPO}" \
    --project="${PROJECT_ID}" \
    --location="${REGION}" \
    --repository-format=docker \
    --description="ADK agent container images"

# ---------------------------------------------------------------------------
# 5. Create the Workload Identity Pool
# ---------------------------------------------------------------------------
echo "🪪 Ensuring Workload Identity Pool '${POOL_ID}' exists..."
gcloud iam workload-identity-pools describe "${POOL_ID}" \
  --project="${PROJECT_ID}" --location="global" >/dev/null 2>&1 || \
  gcloud iam workload-identity-pools create "${POOL_ID}" \
    --project="${PROJECT_ID}" \
    --location="global" \
    --display-name="GitHub Actions (GHES) Pool"

# ---------------------------------------------------------------------------
# 6. Create the OIDC provider trusting GitHub's token issuer,
#    restricted to this exact repository.
# ---------------------------------------------------------------------------
echo "🪪 Ensuring OIDC provider '${PROVIDER_ID}' exists..."
ISSUER_URI="https://token.actions.githubusercontent.com"
if [ "${GH_HOST}" != "github.com" ]; then
  ISSUER_URI="https://${GH_HOST}/_services/token"
fi

gcloud iam workload-identity-pools providers describe "${PROVIDER_ID}" \
  --project="${PROJECT_ID}" --location="global" --workload-identity-pool="${POOL_ID}" >/dev/null 2>&1 || \
  gcloud iam workload-identity-pools providers create-oidc "${PROVIDER_ID}" \
    --project="${PROJECT_ID}" \
    --location="global" \
    --workload-identity-pool="${POOL_ID}" \
    --issuer-uri="${ISSUER_URI}" \
    --attribute-mapping="google.subject=assertion.sub,attribute.repository=assertion.repository,attribute.ref=assertion.ref" \
    --attribute-condition="assertion.repository=='${GH_REPO}'"

# ---------------------------------------------------------------------------
# 7. Allow only this specific repo to impersonate the service account
# ---------------------------------------------------------------------------
echo "🔗 Binding ${GH_REPO} to impersonate ${SA_EMAIL}..."
gcloud iam service-accounts add-iam-policy-binding "${SA_EMAIL}" \
  --project="${PROJECT_ID}" \
  --role="roles/iam.workloadIdentityUser" \
  --member="principalSet://iam.googleapis.com/projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/${POOL_ID}/attribute.repository/${GH_REPO}" \
  --condition=None 2>/dev/null || echo "already bound"

WIF_PROVIDER="projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/${POOL_ID}/providers/${PROVIDER_ID}"

echo ""
echo "🎉 Bootstrap complete. Set these as GitHub repository variables"
echo "   (Settings → Secrets and variables → Actions → Variables) — no GitHub"
echo "   secrets are needed for GCP auth:"
echo ""
echo "   PROJECT_ID=${PROJECT_ID}"
echo "   REGION=${REGION}"
echo "   GCP_WORKLOAD_IDENTITY_PROVIDER=${WIF_PROVIDER}"
echo "   GCP_SERVICE_ACCOUNT=${SA_EMAIL}"
echo ""
echo "   Optional: DEVPORTAL_ALLOWED_INVOKERS, SAP_BTP_MOCK_MODE"
echo ""
echo "   SAP_BTP_CLIENT_SECRET stays in Secret Manager (never a GitHub secret)."
echo "   deploy-devportal.yml will create it with a placeholder on first run —"
echo "   update it afterwards with:"
echo "     gcloud secrets versions add SAP_BTP_CLIENT_SECRET --project=${PROJECT_ID} --data-file=-"
