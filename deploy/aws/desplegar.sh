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

echo "1/5 Imagen $REPO:$ETIQUETA"
docker build --pull -t "$REPO:$ETIQUETA" "$RAIZ"

echo "2/5 ECR"
aws ecr describe-repositories --region "$REGION" --repository-names "$REPO" >/dev/null 2>&1 \
  || aws ecr create-repository --region "$REGION" --repository-name "$REPO" --image-scanning-configuration scanOnPush=true >/dev/null
aws ecr get-login-password --region "$REGION" | docker login --username AWS --password-stdin "$REGISTRO"
docker tag "$REPO:$ETIQUETA" "$REGISTRO/$REPO:$ETIQUETA"
docker push "$REGISTRO/$REPO:$ETIQUETA"

echo "3/5 Copia (snapshot) de RDS antes de migrar"
if [[ -n "${RDS_INSTANCIA:-}" ]]; then
  aws rds create-db-snapshot --region "$REGION" --db-instance-identifier "$RDS_INSTANCIA" \
    --db-snapshot-identifier "sigweb-$ETIQUETA-$(date +%Y%m%d%H%M)" >/dev/null
  echo "   snapshot pedido para $RDS_INSTANCIA"
else
  echo "   RDS_INSTANCIA no definida: haga el snapshot a mano antes de seguir."
fi

echo "4/5 Definición de tarea"
# La plantilla trae <CUENTA>, <REGION> y <ETIQUETA>; el resto de marcadores (<DOMINIO>, <EFS_ID>…) se reemplaza
# una vez en una copia propia: TAREA=mi-tarea.json ./deploy/aws/desplegar.sh
TAREA="${TAREA:-$RAIZ/deploy/aws/ecs-task-definition.json}"
if grep -q '<DOMINIO>\|<EFS_ID>\|<CORREO_ADMIN>' "$TAREA"; then
  echo "   $TAREA aún tiene marcadores sin reemplazar (<DOMINIO>, <EFS_ID>…)." >&2; exit 1
fi
DEF="$(sed -e "s/<CUENTA>/$CUENTA/g" -e "s/<REGION>/$REGION/g" -e "s/<ETIQUETA>/$ETIQUETA/g" "$TAREA")"
ARN="$(aws ecs register-task-definition --region "$REGION" --cli-input-json "$DEF" | jq -r '.taskDefinition.taskDefinitionArn')"
echo "   $ARN"

echo "5/5 Servicio $CLUSTER/$SERVICIO"
aws ecs update-service --region "$REGION" --cluster "$CLUSTER" --service "$SERVICIO" --task-definition "$ARN" --force-new-deployment >/dev/null
aws ecs wait services-stable --region "$REGION" --cluster "$CLUSTER" --services "$SERVICIO"
echo "Listo: $REGISTRO/$REPO:$ETIQUETA en servicio."
