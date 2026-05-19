#!/usr/bin/env bash
# bootstrap.sh — Testurio one-time infrastructure bootstrap
#
# Usage:
#   ./bootstrap.sh --env dev|prod --subscription <subscription-id> \
#                  --app-registration-id <object-id> \
#                  --publisher-email <email>
#
# What this script does:
#   1. Validates az CLI (2.50+) and jq are installed.
#   2. Accepts --env dev|prod.
#   3. Creates or verifies the resource group.
#   4. Creates/updates an OIDC Federated Credential on the App Registration
#      for the correct branch (develop → dev, main → prod).
#   5. Deploys main.bicep with the matching .bicepparam file (what-if + deploy).
#   6. Seeds Key Vault secret placeholders with __REPLACE__ sentinel values.
#   7. Outputs the GitHub Actions secret names and values to configure.

set -euo pipefail

# ─── Defaults ────────────────────────────────────────────────────────────────

ENV=""
SUBSCRIPTION_ID=""
APP_REG_OBJECT_ID=""
PUBLISHER_EMAIL=""
PREFIX="testurio"
LOCATION="eastus"

# ─── Argument parsing ─────────────────────────────────────────────────────────

usage() {
  echo "Usage: $0 --env dev|prod --subscription <id> --app-registration-id <object-id> --publisher-email <email>"
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env)                  ENV="$2";                shift 2 ;;
    --subscription)         SUBSCRIPTION_ID="$2";    shift 2 ;;
    --app-registration-id)  APP_REG_OBJECT_ID="$2";  shift 2 ;;
    --publisher-email)      PUBLISHER_EMAIL="$2";    shift 2 ;;
    *) echo "Unknown argument: $1"; usage ;;
  esac
done

[[ -z "$ENV" || -z "$SUBSCRIPTION_ID" || -z "$APP_REG_OBJECT_ID" || -z "$PUBLISHER_EMAIL" ]] && usage
[[ "$ENV" != "dev" && "$ENV" != "prod" ]] && { echo "ERROR: --env must be 'dev' or 'prod'"; exit 1; }

RESOURCE_GROUP="${PREFIX}-rg-${ENV}"
BRANCH="$( [[ "$ENV" == "prod" ]] && echo "refs/heads/main" || echo "refs/heads/develop" )"
BICEP_PARAM_FILE="$( cd "$(dirname "$0")" && pwd )/${ENV}.bicepparam"

# ─── Step 1: Validate prerequisites ──────────────────────────────────────────

echo "==> Checking prerequisites..."

if ! command -v az &>/dev/null; then
  echo "ERROR: Azure CLI (az) is not installed. Install from https://aka.ms/installazurecli"
  exit 1
fi

AZ_VERSION=$(az version --query '"azure-cli"' -o tsv 2>/dev/null || echo "0.0.0")
REQUIRED_VERSION="2.50.0"
if ! printf '%s\n' "$REQUIRED_VERSION" "$AZ_VERSION" | sort -V -C; then
  echo "ERROR: az CLI version $AZ_VERSION is below the required $REQUIRED_VERSION. Please upgrade."
  exit 1
fi

if ! command -v jq &>/dev/null; then
  echo "ERROR: jq is not installed. Install from https://stedolan.github.io/jq/"
  exit 1
fi

echo "    az $AZ_VERSION and jq are present."

# ─── Step 2: Set subscription ─────────────────────────────────────────────────

echo "==> Setting subscription to $SUBSCRIPTION_ID..."
az account set --subscription "$SUBSCRIPTION_ID"

# ─── Step 3: Create / verify resource group ───────────────────────────────────

echo "==> Ensuring resource group '$RESOURCE_GROUP' exists in '$LOCATION'..."
if az group show --name "$RESOURCE_GROUP" &>/dev/null; then
  echo "    Resource group already exists."
else
  az group create --name "$RESOURCE_GROUP" --location "$LOCATION"
  echo "    Resource group created."
fi

# ─── Step 4: OIDC Federated Credential ────────────────────────────────────────

CREDENTIAL_NAME="github-actions-${ENV}"
GITHUB_ORG="${GITHUB_ORG:-__REPLACE_GITHUB_ORG__}"
GITHUB_REPO="${GITHUB_REPO:-testur.io}"

echo "==> Creating/updating OIDC federated credential '$CREDENTIAL_NAME'..."
CREDENTIAL_SUBJECT="repo:${GITHUB_ORG}/${GITHUB_REPO}:ref:${BRANCH}"

# Check if credential already exists
EXISTING=$(az ad app federated-credential list --id "$APP_REG_OBJECT_ID" \
  --query "[?name=='${CREDENTIAL_NAME}'].id" -o tsv 2>/dev/null || echo "")

CREDENTIAL_JSON=$(jq -n \
  --arg name "$CREDENTIAL_NAME" \
  --arg issuer "https://token.actions.githubusercontent.com" \
  --arg subject "$CREDENTIAL_SUBJECT" \
  --arg description "GitHub Actions OIDC for ${ENV} environment" \
  '{name: $name, issuer: $issuer, subject: $subject, description: $description, audiences: ["api://AzureADTokenExchange"]}')

if [[ -n "$EXISTING" ]]; then
  az ad app federated-credential update --id "$APP_REG_OBJECT_ID" \
    --federated-credential-id "$EXISTING" \
    --parameters "$CREDENTIAL_JSON"
  echo "    Federated credential updated."
else
  az ad app federated-credential create --id "$APP_REG_OBJECT_ID" \
    --parameters "$CREDENTIAL_JSON"
  echo "    Federated credential created."
fi

# ─── Step 5: Deploy Bicep ─────────────────────────────────────────────────────

echo "==> Running Bicep what-if for ${ENV}..."
az deployment group what-if \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$(dirname "$0")/main.bicep" \
  --parameters "$BICEP_PARAM_FILE" \
  --parameters apimPublisherEmail="$PUBLISHER_EMAIL" \
  --no-pretty-print || true

read -r -p "Proceed with deployment? [y/N] " CONFIRM
if [[ "${CONFIRM:-N}" != "y" && "${CONFIRM:-N}" != "Y" ]]; then
  echo "Deployment cancelled."
  exit 0
fi

echo "==> Deploying infrastructure for ${ENV}..."
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$(dirname "$0")/main.bicep" \
  --parameters "$BICEP_PARAM_FILE" \
  --parameters apimPublisherEmail="$PUBLISHER_EMAIL" \
  --name "testurio-${ENV}-$(date +%Y%m%d%H%M%S)"

# ─── Step 6: Retrieve outputs and seed Key Vault secrets ─────────────────────

echo "==> Retrieving deployment outputs..."
OUTPUTS=$(az deployment group show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$(az deployment group list --resource-group "$RESOURCE_GROUP" --query '[0].name' -o tsv)" \
  --query properties.outputs -o json)

KV_NAME=$(echo "$OUTPUTS" | jq -r '.keyVaultName.value')
echo "    Key Vault: $KV_NAME"

echo "==> Seeding Key Vault secret placeholders..."
SECRETS=(
  "stripe-secret-key"
  "stripe-webhook-secret"
  "anthropic-api-key"
  "cosmos-connection-string"
  "servicebus-connection-string"
  "adb2c-client-secret"
  "swa-deployment-token"
)

for SECRET in "${SECRETS[@]}"; do
  EXISTING_SECRET=$(az keyvault secret show --vault-name "$KV_NAME" --name "$SECRET" \
    --query value -o tsv 2>/dev/null || echo "")
  if [[ -z "$EXISTING_SECRET" ]]; then
    az keyvault secret set --vault-name "$KV_NAME" --name "$SECRET" --value "__REPLACE__" > /dev/null
    echo "    Seeded placeholder for '$SECRET'."
  else
    echo "    Secret '$SECRET' already has a value — skipping."
  fi
done

# ─── Step 7: Output GitHub Actions secrets ────────────────────────────────────

APP_REG_CLIENT_ID=$(az ad app show --id "$APP_REG_OBJECT_ID" --query appId -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
ACR_LOGIN_SERVER=$(echo "$OUTPUTS" | jq -r '.acrLoginServer.value')
APP_SERVICE_NAME=$(echo "$OUTPUTS" | jq -r '.appServiceName.value')
CONTAINER_APP_NAME=$(echo "$OUTPUTS" | jq -r '.workerContainerAppName.value')

ENV_UPPER=$(echo "$ENV" | tr '[:lower:]' '[:upper:]')

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo " GitHub Actions secrets to configure for the ${ENV_UPPER} environment"
echo "═══════════════════════════════════════════════════════════════"
echo ""
echo " Secret name                         Value"
echo " ─────────────────────────────────── ────────────────────────────────────"
printf " %-36s %s\n" "AZURE_CLIENT_ID_${ENV_UPPER}"     "$APP_REG_CLIENT_ID"
printf " %-36s %s\n" "AZURE_TENANT_ID"                   "$TENANT_ID"
printf " %-36s %s\n" "AZURE_SUBSCRIPTION_ID"             "$SUBSCRIPTION_ID"
printf " %-36s %s\n" "AZURE_RESOURCE_GROUP_${ENV_UPPER}" "$RESOURCE_GROUP"
printf " %-36s %s\n" "ACR_LOGIN_SERVER_${ENV_UPPER}"     "$ACR_LOGIN_SERVER"
printf " %-36s %s\n" "APP_SERVICE_NAME_${ENV_UPPER}"     "$APP_SERVICE_NAME"
printf " %-36s %s\n" "CONTAINER_APP_NAME_${ENV_UPPER}"   "$CONTAINER_APP_NAME"
printf " %-36s %s\n" "KEY_VAULT_NAME_${ENV_UPPER}"       "$KV_NAME"
echo ""
echo " After setting secrets, replace all Key Vault __REPLACE__ placeholders"
echo " with real values using:"
echo "   az keyvault secret set --vault-name $KV_NAME --name <name> --value <value>"
echo ""
echo "==> Bootstrap complete."
