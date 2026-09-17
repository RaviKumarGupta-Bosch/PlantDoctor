# ============================================================================
# WIF Bootstrap Script for PlantDoctor GCP Project (PowerShell)
# ============================================================================
# This script sets up Workload Identity Federation (WIF) for GitHub Actions
# to deploy to Cloud Run without using long-lived secrets.
#
# Run as: .\bootstrap-wif.ps1
# ============================================================================

$ErrorActionPreference = "Stop"

# Configuration
$PROJECT_ID = "plantdoctor-codingaura"
$PROJECT_NUMBER = "655336664371"
$REGION = "asia-south1"
$GITHUB_ORG = "RaviKumarGupta-Bosch"
$GITHUB_REPO = "PlantDoctor"
$WORKLOAD_IDENTITY_POOL_ID = "github-pool"
$WORKLOAD_IDENTITY_PROVIDER_ID = "github-provider"
$SERVICE_ACCOUNT_ID = "plantdoctor-deployer"
$SERVICE_ACCOUNT_EMAIL = "${SERVICE_ACCOUNT_ID}@${PROJECT_ID}.iam.gserviceaccount.com"

Write-Host "🚀 Starting Workload Identity Federation Setup" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "Project ID: $PROJECT_ID"
Write-Host "Project Number: $PROJECT_NUMBER"
Write-Host "Region: $REGION"
Write-Host "GitHub: $GITHUB_ORG/$GITHUB_REPO"
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

# Step 1: Set the project
Write-Host ""
Write-Host "📋 Step 1: Setting GCP Project..." -ForegroundColor Yellow
gcloud config set project $PROJECT_ID
Write-Host "✅ Project set to $PROJECT_ID" -ForegroundColor Green

# Step 2: Enable required APIs
Write-Host ""
Write-Host "🔌 Step 2: Enabling required APIs..." -ForegroundColor Yellow
gcloud services enable `
  iam.googleapis.com `
  cloudresourcemanager.googleapis.com `
  sts.googleapis.com `
  serviceusage.googleapis.com `
  run.googleapis.com `
  artifactregistry.googleapis.com `
  aiplatform.googleapis.com `
  secretmanager.googleapis.com
Write-Host "✅ APIs enabled" -ForegroundColor Green

# Step 3: Create Workload Identity Pool
Write-Host ""
Write-Host "🏊 Step 3: Creating Workload Identity Pool..." -ForegroundColor Yellow
try {
    gcloud iam workload-identity-pools describe $WORKLOAD_IDENTITY_POOL_ID `
      --location=global `
      --project=$PROJECT_ID 2>&1 | Out-Null
    Write-Host "⏭️  Workload Identity Pool already exists: $WORKLOAD_IDENTITY_POOL_ID" -ForegroundColor Cyan
}
catch {
    gcloud iam workload-identity-pools create $WORKLOAD_IDENTITY_POOL_ID `
      --project=$PROJECT_ID `
      --location=global `
      --display-name="GitHub Pool for $GITHUB_ORG/$GITHUB_REPO"
    Write-Host "✅ Workload Identity Pool created: $WORKLOAD_IDENTITY_POOL_ID" -ForegroundColor Green
}

# Step 4: Create Workload Identity Provider (GitHub)
Write-Host ""
Write-Host "🔐 Step 4: Creating Workload Identity Provider for GitHub..." -ForegroundColor Yellow
try {
    gcloud iam workload-identity-providers describe $WORKLOAD_IDENTITY_PROVIDER_ID `
      --location=global `
      --workload-identity-pool=$WORKLOAD_IDENTITY_POOL_ID `
      --project=$PROJECT_ID 2>&1 | Out-Null
    Write-Host "⏭️  Workload Identity Provider already exists: $WORKLOAD_IDENTITY_PROVIDER_ID" -ForegroundColor Cyan
}
catch {
    gcloud iam workload-identity-providers create-oidc $WORKLOAD_IDENTITY_PROVIDER_ID `
      --project=$PROJECT_ID `
      --location=global `
      --workload-identity-pool=$WORKLOAD_IDENTITY_POOL_ID `
      --display-name="GitHub Provider" `
      --attribute-mapping="google.subject=assertion.sub,attribute.aud=assertion.aud,attribute.repository=assertion.repository,attribute.repository_owner=assertion.repository_owner" `
      --issuer-uri="https://token.actions.githubusercontent.com"
    Write-Host "✅ Workload Identity Provider created: $WORKLOAD_IDENTITY_PROVIDER_ID" -ForegroundColor Green
}

# Step 5: Create Service Account
Write-Host ""
Write-Host "👤 Step 5: Creating Service Account..." -ForegroundColor Yellow
try {
    gcloud iam service-accounts describe $SERVICE_ACCOUNT_EMAIL `
      --project=$PROJECT_ID 2>&1 | Out-Null
    Write-Host "⏭️  Service Account already exists: $SERVICE_ACCOUNT_EMAIL" -ForegroundColor Cyan
}
catch {
    gcloud iam service-accounts create $SERVICE_ACCOUNT_ID `
      --project=$PROJECT_ID `
      --display-name="PlantDoctor CI/CD Deployer" `
      --description="Service account for GitHub Actions to deploy to Cloud Run"
    Write-Host "✅ Service Account created: $SERVICE_ACCOUNT_EMAIL" -ForegroundColor Green
}

# Step 6: Grant IAM roles to Service Account
Write-Host ""
Write-Host "🔑 Step 6: Granting IAM roles to Service Account..." -ForegroundColor Yellow

$roles = @(
    "roles/run.admin",
    "roles/aiplatform.user",
    "roles/secretmanager.admin",
    "roles/artifactregistry.writer",
    "roles/serviceusage.serviceUsageConsumer"
)

foreach ($role in $roles) {
    Write-Host "  Granting $role..." -ForegroundColor Gray
    gcloud projects add-iam-policy-binding $PROJECT_ID `
      --member="serviceAccount:$SERVICE_ACCOUNT_EMAIL" `
      --role="$role" `
      --condition=None 2>&1 | Out-Null
}
Write-Host "✅ IAM roles granted" -ForegroundColor Green

# Step 7: Set up Workload Identity Pool Provider for GitHub
Write-Host ""
Write-Host "🔗 Step 7: Configuring GitHub ↔ GCP Trust..." -ForegroundColor Yellow

$WIP_NAME = "projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/$WORKLOAD_IDENTITY_POOL_ID"
$WIP_PROVIDER_NAME = "$WIP_NAME/providers/$WORKLOAD_IDENTITY_PROVIDER_ID"

gcloud iam service-accounts add-iam-policy-binding $SERVICE_ACCOUNT_EMAIL `
  --project=$PROJECT_ID `
  --role="roles/iam.workloadIdentityUser" `
  --member="principalSet://iam.googleapis.com/projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/$WORKLOAD_IDENTITY_POOL_ID/attribute.repository/$GITHUB_ORG/$GITHUB_REPO" `
  2>&1 | Out-Null

Write-Host "✅ GitHub ↔ GCP Trust configured" -ForegroundColor Green

# Step 8: Create Artifact Registry Repository (if needed)
Write-Host ""
Write-Host "📦 Step 8: Setting up Artifact Registry..." -ForegroundColor Yellow
try {
    gcloud artifacts repositories describe "adk-agents" `
      --location=$REGION `
      --project=$PROJECT_ID 2>&1 | Out-Null
    Write-Host "⏭️  Artifact Registry repository already exists: adk-agents" -ForegroundColor Cyan
}
catch {
    gcloud artifacts repositories create "adk-agents" `
      --repository-format=docker `
      --location=$REGION `
      --project=$PROJECT_ID `
      --description="PlantDoctor ADK Agents Docker Repository"
    Write-Host "✅ Artifact Registry repository created: adk-agents" -ForegroundColor Green
}

# Step 9: Display Configuration Summary
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
Write-Host "✅ WIF Setup Complete!" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
Write-Host ""
Write-Host "📝 Next Steps: Configure GitHub Repository Variables" -ForegroundColor Yellow
Write-Host ""
Write-Host "Add these as GitHub repository variables (Settings > Secrets and variables > Variables):" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. GCP_WORKLOAD_IDENTITY_PROVIDER:" -ForegroundColor White
Write-Host "   $WIP_PROVIDER_NAME" -ForegroundColor Magenta
Write-Host ""
Write-Host "2. GCP_SERVICE_ACCOUNT:" -ForegroundColor White
Write-Host "   $SERVICE_ACCOUNT_EMAIL" -ForegroundColor Magenta
Write-Host ""
Write-Host "3. PROJECT_ID:" -ForegroundColor White
Write-Host "   $PROJECT_ID" -ForegroundColor Magenta
Write-Host ""
Write-Host "4. REGION:" -ForegroundColor White
Write-Host "   $REGION" -ForegroundColor Magenta
Write-Host ""
Write-Host "Optional GitHub Variables:" -ForegroundColor Cyan
Write-Host ""
Write-Host "5. SAP_BTP_MOCK_MODE:" -ForegroundColor White
Write-Host "   true (for testing without SAP BTP credentials)" -ForegroundColor Magenta
Write-Host ""
Write-Host "6. DEVPORTAL_ALLOWED_INVOKERS:" -ForegroundColor White
Write-Host "   serviceAccount:YOUR_SERVICE_ACCOUNT@PROJECT_ID.iam.gserviceaccount.com (comma-separated)" -ForegroundColor Magenta
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "🔗 Workload Identity Provider Resource Name:" -ForegroundColor Cyan
Write-Host "   $WIP_PROVIDER_NAME" -ForegroundColor Magenta
Write-Host ""
Write-Host "👤 Service Account:" -ForegroundColor Cyan
Write-Host "   $SERVICE_ACCOUNT_EMAIL" -ForegroundColor Magenta
Write-Host ""
Write-Host "📍 Region: $REGION" -ForegroundColor Cyan
Write-Host "🌍 Artifact Registry: $REGION-docker.pkg.dev/$PROJECT_ID/adk-agents" -ForegroundColor Cyan
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
