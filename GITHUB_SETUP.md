# GitHub Actions Setup for PlantDoctor Deployment

## Quick Start: Add Repository Variables

Follow these steps to configure your GitHub repository for automated Cloud Run deployments.

### Step 1: Navigate to Repository Settings

1. Go to: https://github.com/RaviKumarGupta-Bosch/PlantDoctor
2. Click **Settings** tab
3. Select **Secrets and variables** → **Variables** (left sidebar)

### Step 2: Create Repository Variables

Add the following variables. Click "New repository variable" for each:

#### Required Variables (Core Configuration)

| Variable Name | Value | Description |
|---------------|-------|-------------|
| `GCP_WORKLOAD_IDENTITY_PROVIDER` | `projects/655336664371/locations/global/workloadIdentityPools/github-pool/providers/github-provider` | WIF Provider for GitHub OIDC authentication |
| `GCP_SERVICE_ACCOUNT` | `plantdoctor-deployer@plantdoctor-codingaura.iam.gserviceaccount.com` | Service account for Cloud Run deployments |
| `PROJECT_ID` | `plantdoctor-codingaura` | GCP Project ID |
| `REGION` | `asia-south1` | GCP Region (India) |

#### Optional Variables (Recommended)

| Variable Name | Value | Description |
|---------------|-------|-------------|
| `SAP_BTP_MOCK_MODE` | `true` | Enable mock mode for testing without SAP BTP credentials |
| `DEVPORTAL_ALLOWED_INVOKERS` | *(leave empty or add service accounts)* | Service accounts allowed to invoke the Cloud Run service |

### Step 3: How to Add Each Variable

**Example: Adding GCP_WORKLOAD_IDENTITY_PROVIDER**

```
1. Click "New repository variable"
2. Name: GCP_WORKLOAD_IDENTITY_PROVIDER
3. Value: projects/655336664371/locations/global/workloadIdentityPools/github-pool/providers/github-provider
4. Click "Add variable"
```

Repeat for all other variables.

### Step 4: Verify Setup

✅ All variables should now be visible in the Variables section (you should see 4-6 entries)

### Step 5: Test Deployment (Optional)

To test the pipeline:

```bash
# Option A: Push code to main branch
git push origin main

# Option B: Trigger manually
# 1. Go to your repo → Actions tab
# 2. Select "Deploy plantdoctor-devportal to Cloud Run"
# 3. Click "Run workflow" → "Run workflow"
```

---

## Troubleshooting

### ❌ "Billing account not found"
- This is a known issue with GCP API propagation
- Solution: Manually enable APIs in Cloud Console:
  - Go to: https://console.cloud.google.com/apis/library
  - Search for and enable:
    - Cloud Run API
    - Artifact Registry API
    - Secret Manager API

### ❌ "Workload Identity authentication failed"
- Verify `GCP_WORKLOAD_IDENTITY_PROVIDER` is copied exactly (no spaces/typos)
- Verify `GCP_SERVICE_ACCOUNT` is correct email format

### ❌ "Permission denied" errors
- Check that all IAM roles are assigned to the service account
- In Cloud Console: IAM → Service Accounts → `plantdoctor-deployer` → Roles tab
- Should have: `roles/run.admin`, `roles/aiplatform.user`, `roles/secretmanager.admin`, `roles/artifactregistry.writer`

---

## GCP Infrastructure Status

### ✅ Already Configured

- Workload Identity Pool: `github-pool`
- Workload Identity Provider: `github-provider`
- Service Account: `plantdoctor-deployer@plantdoctor-codingaura.iam.gserviceaccount.com`
- IAM Roles: All required roles assigned
- GitHub ↔ GCP Trust: Configured via principal set

### ⚡ Manual Actions (if needed)

If you encounter billing errors, enable APIs manually:

```bash
gcloud services enable \
  iam.googleapis.com \
  cloudresourcemanager.googleapis.com \
  sts.googleapis.com \
  serviceusage.googleapis.com \
  run.googleapis.com \
  artifactregistry.googleapis.com \
  aiplatform.googleapis.com \
  secretmanager.googleapis.com \
  --project=plantdoctor-codingaura
```

---

## Next Steps

1. ✅ Add all repository variables (see Step 2 above)
2. Push your code to the `main` branch
3. GitHub Actions will automatically:
   - Build the Docker image
   - Push to Artifact Registry (`asia-south1-docker.pkg.dev/plantdoctor-codingaura/adk-agents/plantdoctor-devportal:SHA`)
   - Deploy to Cloud Run
   - Output the service URL

3. Once deployed, you can access the service at the URL provided in the workflow output

---

## Architecture Summary

```
GitHub Actions (CI/CD)
    ↓
    ├─ Authenticate via Workload Identity Federation (WIF)
    ├─ Build Docker image
    ├─ Push to Artifact Registry
    ├─ Deploy to Cloud Run (asia-south1)
    └─ Output service URL

GCP Resources:
    ├─ Cloud Run Service: plantdoctor-devportal
    ├─ Artifact Registry: adk-agents (asia-south1)
    ├─ Secret Manager: SAP_BTP_CLIENT_SECRET
    ├─ Vertex AI: Claude API integration
    └─ Service Account: plantdoctor-deployer
```

---

**Questions?** Check the [original deploy-devportal.yml](../.github/workflows/deploy-devportal.yml) for detailed comments.
