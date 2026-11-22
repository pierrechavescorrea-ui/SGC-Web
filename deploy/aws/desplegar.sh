#!/usr/bin/env bash
# Construye la imagen, la sube a ECR, registra la definición de tarea y fuerza el despliegue en ECS Fargate.
# Guía completa: docs/aws-despliegue.md. Requiere AWS CLI v2, Docker y jq, con credenciales de quien despliega.
# Uso: CUENTA=123456789012 REGION=sa-east-1 CLUSTER=sigweb SERVICIO=sigweb ./deploy/aws/desplegar.sh
set -euo pipefail

: "${CUENTA:?Falta CUENTA (id de la cuenta AWS)}"
: "${REGION:?Falta REGION (p. ej. sa-east-1)}"
CLUSTER="${CLUSTER:-sigweb}"
SERVICIO="${SERVICIO:-sigweb}"
REPO="${REPO:-sigweb}"
RAIZ="$(cd "$(dirname "$0")/../.." && pwd)"
ETIQUETA="$(git -C "$RAIZ" rev-parse --short HEAD)"
REGISTRO="$CUENTA.dkr.ecr.$REGION.amazonaws.com"

# Validar la definición antes de construir o modificar recursos en AWS.
TAREA="${TAREA:-$RAIZ/deploy/aws/ecs-task-definition.json}"
DEF="$(sed -e "s/<CUENTA>/$CUENTA/g" -e "s/<REGION>/$REGION/g" -e "s/<ETIQUETA>/$ETIQUETA/g" "$TAREA")"
if grep -Eq '<[A-Z_]+>' <<< "$DEF"; then
  echo "Hay marcadores pendientes en $TAREA." >&2; exit 1
fi
DEF="$(jq -e --arg image "$REGISTRO/$REPO:$ETIQUETA" '
  if ([.containerDefinitions[] | select(.name == "sigweb")] | length) == 1
  then (.containerDefinitions[] | select(.name == "sigweb").image) = $image
  else error("La tarea debe definir un contenedor llamado sigweb") end' <<< "$DEF")"

echo "1/5 Imagen $REPO:$ETIQUETA"
docker build --pull --platform linux/amd64 -t "$REPO:$ETIQUETA" "$RAIZ"

echo "2/5 ECR"
aws ecr describe-repositories --region "$REGION" --repository-names "$REPO" >/dev/null 2>&1 \
  || aws ecr create-repository --region "$REGION" --repository-name "$REPO" --image-scanning-configuration scanOnPush=true >/dev/null
aws ecr get-login-password --region "$REGION" | docker login --username AWS --password-stdin "$REGISTRO"
docker tag "$REPO:$ETIQUETA" "$REGISTRO/$REPO:$ETIQUETA"
docker push "$REGISTRO/$REPO:$ETIQUETA"

echo "3/5 Copia (snapshot) de RDS antes de migrar"
if [[ -n "${RDS_INSTANCIA:-}" ]]; then
  SNAPSHOT="sigweb-$ETIQUETA-$(date +%Y%m%d%H%M%S)"
  aws rds create-db-snapshot --region "$REGION" --db-instance-identifier "$RDS_INSTANCIA" \
    --db-snapshot-identifier "$SNAPSHOT" >/dev/null
  aws rds wait db-snapshot-available --region "$REGION" --db-snapshot-identifier "$SNAPSHOT"
else
  echo "   RDS_INSTANCIA no definida: haga el snapshot a mano antes de seguir."
fi

echo "4/5 Definición de tarea"
ARN="$(aws ecs register-task-definition --region "$REGION" --cli-input-json "$DEF" | jq -er '.taskDefinition.taskDefinitionArn')"
echo "   $ARN"

echo "5/5 Servicio $CLUSTER/$SERVICIO"
aws ecs update-service --region "$REGION" --cluster "$CLUSTER" --service "$SERVICIO" --task-definition "$ARN" --force-new-deployment >/dev/null
aws ecs wait services-stable --region "$REGION" --cluster "$CLUSTER" --services "$SERVICIO"
ACTUAL="$(aws ecs describe-services --region "$REGION" --cluster "$CLUSTER" --services "$SERVICIO" \
  --query 'services[0].deployments[?status==`PRIMARY`].taskDefinition | [0]' --output text)"
if [[ "$ACTUAL" != "$ARN" ]]; then
  echo "El servicio se estabilizó con $ACTUAL en lugar de $ARN." >&2; exit 1
fi
echo "Listo: $REGISTRO/$REPO:$ETIQUETA en servicio."
