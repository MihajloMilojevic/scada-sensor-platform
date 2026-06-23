#!/usr/bin/env bash
# =============================================================================
# deploy-minikube.sh — SCADA Sensor Platform (Minikube)
#
# Pokretanje:
#   chmod +x deploy-minikube.sh
#   ./deploy-minikube.sh           # pun deploy (build + load + apply)
#   ./deploy-minikube.sh --skip-build   # samo apply (ako su slike već učitane)
#   ./deploy-minikube.sh --only-images  # samo build + load, bez apply
#   ./deploy-minikube.sh --teardown     # briši sve
#   ./deploy-minikube.sh --smoke-test   # samo smoke test
# =============================================================================

set -euo pipefail

# ──────────────────────────────────────────────────────────────────────────────
# Boje i utility funkcije
# ──────────────────────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BLUE='\033[0;34m'; CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'

info()    { echo -e "${CYAN}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[OK]${NC}    $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
error()   { echo -e "${RED}[ERROR]${NC} $*" >&2; exit 1; }
step()    { echo -e "\n${BOLD}${BLUE}▶ $*${NC}"; }

# ──────────────────────────────────────────────────────────────────────────────
# Argumenti
# ──────────────────────────────────────────────────────────────────────────────
SKIP_BUILD=false
ONLY_IMAGES=false
TEARDOWN=false
SMOKE_TEST_ONLY=false

for arg in "$@"; do
  case $arg in
    --skip-build)    SKIP_BUILD=true ;;
    --only-images)   ONLY_IMAGES=true ;;
    --teardown)      TEARDOWN=true ;;
    --smoke-test)    SMOKE_TEST_ONLY=true ;;
    *) warn "Nepoznat argument: $arg" ;;
  esac
done

# ──────────────────────────────────────────────────────────────────────────────
# Konstante
# ──────────────────────────────────────────────────────────────────────────────
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OVERLAY="k8s/overlays/minikube"
NAMESPACE="scada"

# docker-compose image prefix (scada-sensor-platform-<svc>:latest)
COMPOSE_PREFIX="scada-sensor-platform"

# Servisi koji se grade i učitavaju u minikube
SERVICES=(
  "auth-service"
  "api-gateway"
  "ingestion-service"
  "consensus-service"
  "sensormgmt-service"
  "notification-service"
  "analytics-service"
)

FRONTEND_IMAGE="scada/frontend:latest"
FRONTEND_DIR="src/Frontend/scada-ui"

# ──────────────────────────────────────────────────────────────────────────────
# Provjera preduslova
# ──────────────────────────────────────────────────────────────────────────────
check_prerequisites() {
  step "Provjera preduslova"
  local missing=()

  for cmd in docker kubectl minikube; do
    if command -v "$cmd" &>/dev/null; then
      success "$cmd pronađen: $(command -v $cmd)"
    else
      missing+=("$cmd")
    fi
  done

  if [[ ${#missing[@]} -gt 0 ]]; then
    error "Nedostaju alati: ${missing[*]}"
  fi

  # Provjeri da li je minikube pokrenut
  if ! minikube status --format='{{.Host}}' 2>/dev/null | grep -q "Running"; then
    warn "Minikube nije pokrenut. Pokretanje..."
    minikube start --driver=docker --memory=6144 --cpus=4
    success "Minikube pokrenut"
  else
    success "Minikube je već pokrenut"
  fi
}

# ──────────────────────────────────────────────────────────────────────────────
# Build Docker slika
# ──────────────────────────────────────────────────────────────────────────────
build_images() {
  step "Build Docker slika (docker compose build)"
  cd "$REPO_ROOT"
  docker compose build --parallel
  success "docker compose build završen"
}

# ──────────────────────────────────────────────────────────────────────────────
# Tag i učitavanje slika u Minikube
# ──────────────────────────────────────────────────────────────────────────────
load_images() {
  step "Tagovanje i učitavanje slika u Minikube"
  cd "$REPO_ROOT"

  for svc in "${SERVICES[@]}"; do
    local src_image="${COMPOSE_PREFIX}-${svc}:latest"
    local dst_image="scada/${svc}:latest"

    info "Tag: $src_image → $dst_image"
    docker tag "$src_image" "$dst_image"

    info "Load: $dst_image → minikube"
    minikube image load "$dst_image"
    success "$dst_image učitan"
  done

  # Frontend
  step "Build i učitavanje Frontend slike"
  docker build -t "$FRONTEND_IMAGE" "$FRONTEND_DIR"
  minikube image load "$FRONTEND_IMAGE"
  success "Frontend slika učitana"
}

# ──────────────────────────────────────────────────────────────────────────────
# Linkerd instalacija
# ──────────────────────────────────────────────────────────────────────────────
install_linkerd() {
  step "Provjera Linkerd instalacije"

  if ! command -v linkerd &>/dev/null; then
    warn "linkerd CLI nije pronađen. Instalacija..."
    curl -sL https://run.linkerd.io/install | sh
    export PATH=$PATH:$HOME/.linkerd2/bin
    echo 'export PATH=$PATH:$HOME/.linkerd2/bin' >> ~/.bashrc
    success "linkerd CLI instaliran"
  else
    success "linkerd CLI pronađen"
  fi

  # Provjeri da li je Linkerd već instaliran u clusteru
  if kubectl get namespace linkerd &>/dev/null 2>&1; then
    success "Linkerd namespace već postoji — preskačem instalaciju"
    return
  fi

  info "Instalacija Gateway API CRD-ova..."
  kubectl apply --server-side -f \
    https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.2.1/standard-install.yaml

  info "Instalacija Linkerd CRD-ova..."
  linkerd install --crds | kubectl apply -f -

  info "Instalacija Linkerd control plane..."
  linkerd install --set proxyInit.runAsRoot=true | kubectl apply -f -

  info "Čekanje da Linkerd bude spreman..."
  linkerd check
  success "Linkerd instaliran i spreman"
}

# ──────────────────────────────────────────────────────────────────────────────
# Deploy K8s manifesta
# ──────────────────────────────────────────────────────────────────────────────
deploy() {
  step "Primjena K8s konfiguracija (kustomize overlay: minikube)"
  cd "$REPO_ROOT"
  kubectl apply -k "$OVERLAY"
  success "Manifesti primijenjeni"

  step "Čekanje da podovi budu Ready"
  info "Ovo može potrajati 2-4 minute (inicijalno povlačenje postgres/redis slika)..."

  # Čekaj max 5 minuta
  local timeout=300
  local start
  start=$(date +%s)

  while true; do
    local now
    now=$(date +%s)
    local elapsed=$(( now - start ))

    if [[ $elapsed -ge $timeout ]]; then
      warn "Timeout — neki podovi možda nisu Ready. Provjeri: kubectl get pods -n $NAMESPACE"
      break
    fi

    local not_ready
    not_ready=$(kubectl get pods -n "$NAMESPACE" --no-headers 2>/dev/null \
      | grep -v "Running\|Completed" | grep -v "^$" | wc -l)

    if [[ "$not_ready" -eq 0 ]]; then
      success "Svi podovi su Running"
      break
    fi

    echo -ne "\r  ${YELLOW}Čekanje... (${elapsed}s / ${timeout}s) — $not_ready podova nije Ready${NC}   "
    sleep 5
  done

  echo ""
  kubectl get pods -n "$NAMESPACE"
}

# ──────────────────────────────────────────────────────────────────────────────
# Seed admin korisnika
# ──────────────────────────────────────────────────────────────────────────────
seed_admin() {
  step "Seed admin korisnika u postgres-auth"

  local pg_pod
  pg_pod=$(kubectl get pods -n "$NAMESPACE" -l app=postgres-auth \
    -o jsonpath='{.items[0].metadata.name}' 2>/dev/null || true)

  if [[ -z "$pg_pod" ]]; then
    warn "postgres-auth pod nije pronađen — preskačem seed"
    return
  fi

  info "Koristi pod: $pg_pod"

  # Provjeri da li admin već postoji
  local exists
  exists=$(kubectl exec -n "$NAMESPACE" "$pg_pod" -c postgres -- \
    psql -U postgres -d auth -tAc \
    "SELECT COUNT(*) FROM users WHERE username='admin';" 2>/dev/null || echo "0")
  exists=$(echo "$exists" | tr -d '[:space:]')

  if [[ "$exists" == "1" ]]; then
    success "Admin korisnik već postoji — preskačem"
    return
  fi

  # bcrypt hash za 'admin123' (cost 11, unaprijed izračunat)
  local ADMIN_HASH='$2a$11$K7RmHFb4.YuR59GiuVCjwuRR9s/0WmSr3eWPqLuGFdKP/6z2KwJdK'

  kubectl exec -n "$NAMESPACE" "$pg_pod" -c postgres -- \
    psql -U postgres -d auth -c \
    "INSERT INTO users (username, password_hash, role)
     VALUES ('admin', '${ADMIN_HASH}', 'ADMIN')
     ON CONFLICT (username) DO NOTHING;" 2>/dev/null && \
    success "Admin korisnik kreiran (admin / admin123)" || \
    warn "Seed neuspješan — tabela možda još nije inicijalizovana. Pokreni ručno poslije."
}

# ──────────────────────────────────────────────────────────────────────────────
# Smoke test
# ──────────────────────────────────────────────────────────────────────────────
smoke_test() {
  step "Smoke test"

  local gw_url
  gw_url=$(minikube service api-gateway-external -n "$NAMESPACE" --url 2>/dev/null | head -1)

  if [[ -z "$gw_url" ]]; then
    warn "Ne mogu dobiti URL gateway-a. Provjeri: minikube service api-gateway-external -n $NAMESPACE --url"
    return
  fi

  info "Gateway URL: $gw_url"

  # Login test
  info "POST $gw_url/api/auth/login ..."
  local resp
  resp=$(curl -s -o /dev/null -w "%{http_code}" \
    -X POST "$gw_url/api/auth/login" \
    -H "Content-Type: application/json" \
    -d '{"username":"admin","password":"admin123"}' \
    --max-time 10 || echo "000")

  if [[ "$resp" == "200" ]]; then
    success "Login endpoint vratio 200 OK"
  elif [[ "$resp" == "401" ]]; then
    warn "Login vratio 401 — admin seed možda nije završen ili je pogrešan hash"
  else
    warn "Login vratio HTTP $resp"
  fi

  echo ""
  echo -e "${BOLD}URLs:${NC}"
  echo -e "  API Gateway:  ${GREEN}$gw_url${NC}"
  echo -e "  Frontend:     ${GREEN}$(minikube service frontend-external -n $NAMESPACE --url 2>/dev/null | head -1 || echo 'N/A')${NC}"
  echo ""
  echo -e "${BOLD}Korisni kubectl komande:${NC}"
  echo -e "  kubectl get pods -n $NAMESPACE"
  echo -e "  kubectl logs -n $NAMESPACE -l app=api-gateway -f"
  echo -e "  kubectl logs -n $NAMESPACE -l app=auth-service -f"
}

# ──────────────────────────────────────────────────────────────────────────────
# Teardown
# ──────────────────────────────────────────────────────────────────────────────
teardown() {
  step "Brisanje svih resursa (namespace: $NAMESPACE)"
  warn "Ovo briše sve podove, baze i PVC-ove u namespace-u $NAMESPACE!"
  read -r -p "  Nastavi? (y/N): " confirm
  [[ "$confirm" =~ ^[Yy]$ ]] || { info "Otkazano."; exit 0; }

  kubectl delete namespace "$NAMESPACE" --ignore-not-found
  success "Namespace $NAMESPACE obrisan"
}

# ──────────────────────────────────────────────────────────────────────────────
# Main
# ──────────────────────────────────────────────────────────────────────────────
main() {
  echo -e "\n${BOLD}${BLUE}╔══════════════════════════════════════════════╗${NC}"
  echo -e "${BOLD}${BLUE}║  SCADA Sensor Platform — Minikube Deploy     ║${NC}"
  echo -e "${BOLD}${BLUE}╚══════════════════════════════════════════════╝${NC}\n"

  if $TEARDOWN; then
    teardown
    exit 0
  fi

  if $SMOKE_TEST_ONLY; then
    smoke_test
    exit 0
  fi

  check_prerequisites

  if ! $SKIP_BUILD; then
    build_images
    load_images
  else
    info "Preskačem build (--skip-build)"
  fi

  if $ONLY_IMAGES; then
    info "Slike učitane. Izlazak (--only-images)."
    exit 0
  fi

  install_linkerd
  deploy
  seed_admin
  smoke_test

  echo -e "\n${GREEN}${BOLD}✓ Deploy završen!${NC}\n"
}

main "$@"
