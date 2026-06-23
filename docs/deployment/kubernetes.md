# Kubernetes Deployment (Minikube)

## Prerequisites

```bash
minikube start --driver=docker --memory=6144 --cpus=4
```

Install tools if missing:
```bash
# kubectl
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
chmod +x kubectl && mv kubectl ~/.local/bin/

# minikube
curl -LO https://storage.googleapis.com/minikube/releases/latest/minikube-linux-amd64
chmod +x minikube-linux-amd64 && mv minikube-linux-amd64 ~/.local/bin/minikube

# Linkerd
curl -sL https://run.linkerd.io/install | sh
export PATH=$PATH:$HOME/.linkerd2/bin
```

## Install Linkerd mTLS

```bash
kubectl apply --server-side -f https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.2.1/standard-install.yaml
linkerd install --crds | kubectl apply -f -
linkerd install --set proxyInit.runAsRoot=true | kubectl apply -f -
linkerd check
```

## Build and Load Images

```bash
cd scada-sensor-platform
docker compose build

# Tag for K8s
for svc in auth-service api-gateway ingestion-service consensus-service \
           sensormgmt-service notification-service analytics-service; do
  docker tag scada-sensor-platform-${svc}:latest scada/${svc}:latest
  minikube image load scada/${svc}:latest
done

# Build and load frontend
cd src/Frontend/scada-ui
docker build -t scada/frontend:latest .
minikube image load scada/frontend:latest
```

## Deploy

```bash
kubectl apply -k k8s/overlays/minikube
kubectl get pods -n scada --watch
```

Expected steady state: all pods `2/2 Running` (app container + Linkerd proxy sidecar).

## Manifest Structure

```
k8s/
├── base/
│   ├── namespace.yaml          (scada namespace, linkerd.io/inject: enabled)
│   ├── secrets.yaml            (JWT key, postgres password, InfluxDB token, ECDSA keys)
│   ├── kafka/                  (Deployment + Service + PVC)
│   ├── auth-service/           (auth-service × 2 + postgres-auth + redis-auth)
│   ├── api-gateway/            (api-gateway × 2 + redis-gateway + LoadBalancer service)
│   ├── ingestion-service/      (ingestion-service × 2 + influxdb-ingestion)
│   ├── consensus-service/      (consensus-service + postgres-consensus)
│   ├── sensormgmt-service/     (sensormgmt-service × 2 + postgres-sensormgmt + redis-sensormgmt)
│   ├── notification-service/   (notification-service)
│   ├── analytics-service/      (analytics-service + postgres-analytics)
│   ├── frontend/               (frontend × 2 replicas + LoadBalancer)
│   └── kustomization.yaml
└── overlays/minikube/
    ├── kustomization.yaml
    └── patches/
        ├── gateway-nodeport.yaml    (NodePort 30080 for api-gateway)
        ├── frontend-nodeport.yaml   (NodePort 30081 for frontend)
        ├── resources-small.yaml     (reduced CPU/memory for minikube)
        ├── image-pull-never.yaml    (imagePullPolicy: Never for local images)
        ├── db-connections.yaml      (hardcoded connection strings — K8s env var expansion limitation)
        ├── kafka-no-linkerd.yaml    (Kafka excluded from Linkerd — Kafka protocol incompatibility)
        └── gateway-probe.yaml       (TCP readiness probe — gateway has no anonymous /health)
```

## Smoke Test

```bash
# Get NodePort URL
GW_URL=$(minikube service api-gateway-external -n scada --url)

# Login
curl -s -X POST "$GW_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq .
# Expect: {accessToken: "...", refreshToken: "..."}
```

## Seed Admin User

On first deploy, the postgres-auth DB is empty. Seed the admin user:

```bash
HASH=$(docker exec postgres-auth psql -U postgres -d auth -t -c \
  "SELECT password_hash FROM users WHERE username='admin';")
PGPOD=$(kubectl get pods -n scada -l app=postgres-auth -o jsonpath='{.items[0].metadata.name}')
kubectl exec -n scada $PGPOD -c postgres -- psql -U postgres -d auth -c \
  "INSERT INTO users (username, password_hash, role) VALUES ('admin', '$HASH', 'ADMIN') ON CONFLICT DO NOTHING;"
```

## Verify Linkerd mTLS

```bash
# Install viz extension
linkerd viz install | kubectl apply -f -
linkerd viz check

# Check all deployments are meshed
linkerd -n scada viz stat deployment
# Expect: MESHED=1/1 for all deployments, SUCCESS=100%

# Check proxy health
linkerd -n scada check --proxy
```

## Known Limitations (minikube)

| Issue | Cause | Workaround |
|-------|-------|-----------|
| Kafka `CrashLoopBackOff` | Linkerd iptables rules intercept Kafka's internal controller port | `kafka-no-linkerd.yaml` patch disables injection; Kafka pod runs without mTLS sidecar |
| No anonymous `/health` on gateway | `JwtCacheMiddleware` blocks all requests without a Bearer token | `gateway-probe.yaml` uses TCP socket probe |
| `$(POSTGRES_PASSWORD)` not expanded | K8s env var substitution only works if referencing var is declared before | `db-connections.yaml` hardcodes connection strings |
| Stateful services restart clean | PVCs on fresh cluster have no data | Re-run seed script after initial deploy |

## Linkerd mTLS Note

All service pods in the `scada` namespace have the Linkerd sidecar injected (via namespace annotation `linkerd.io/inject: enabled`). Inter-service HTTP calls go through the sidecar and are encrypted with mTLS automatically. Kafka is excluded because Kafka's internal controller uses localhost connections that conflict with Linkerd's iptables intercept rules.
