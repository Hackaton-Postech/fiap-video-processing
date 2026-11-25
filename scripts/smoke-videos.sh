#!/usr/bin/env bash
# Teste de ponta a ponta: Auth + Video API + RabbitMQ + storage.
# Uso (com o compose no ar): bash scripts/smoke-videos.sh
set -u
cd "$(dirname "$0")/.."
set -a; . ./.env; set +a

AUTH=${AUTH_URL:-http://localhost:5001}
API=${VIDEOS_URL:-http://localhost:5002}
MQ=${RABBITMQ_URL:-http://localhost:15672}
MQAUTH="$RABBITMQ_USER:$RABBITMQ_PASSWORD"
EMAIL='smoke@teste.com'
SENHA='Senha12345'
FILA='smoke.video.enviado'
FALHAS=0

ok()    { echo "  OK     - $1"; }
falha() { echo "  FALHA  - $1"; FALHAS=$((FALHAS + 1)); }
checa() { if [ "$2" = "$3" ]; then ok "$1"; else falha "$1 (esperado $2, obtido $3)"; fi; }
fim()   { rm -f smoke-upload.mp4 smoke-upload.txt; echo "RESULTADO: $FALHAS falha(s)"; [ "$FALHAS" -eq 0 ]; exit $?; }

echo '1) Autenticacao'
curl -s -o /dev/null -X POST "$AUTH/auth/register" -H 'Content-Type: application/json' \
  -d "{\"nome\":\"Smoke\",\"email\":\"$EMAIL\",\"senha\":\"$SENHA\"}"
TOKEN=$(curl -s -X POST "$AUTH/auth/login" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"senha\":\"$SENHA\"}" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
if [ -n "$TOKEN" ]; then ok 'login retornou token'; else falha 'login sem token'; fim; fi

echo '2) Fila de teste no RabbitMQ'
for _ in $(seq 1 30); do   # aguardando painel do rabbitmq
  [ "$(curl -s -o /dev/null -w '%{http_code}' -u "$MQAUTH" "$MQ/api/overview")" = "200" ] && break
  sleep 1
done
curl -s -o /dev/null -u "$MQAUTH" -X DELETE "$MQ/api/queues/%2F/$FILA"
c1=$(curl -s -o /dev/null -w '%{http_code}' -u "$MQAUTH" -X PUT "$MQ/api/exchanges/%2F/video.events" \
  -H 'content-type: application/json' -d '{"type":"topic","durable":true}')
c2=$(curl -s -o /dev/null -w '%{http_code}' -u "$MQAUTH" -X PUT "$MQ/api/queues/%2F/$FILA" \
  -H 'content-type: application/json' -d '{"durable":true}')
c3=$(curl -s -o /dev/null -w '%{http_code}' -u "$MQAUTH" -X POST "$MQ/api/bindings/%2F/e/video.events/q/$FILA" \
  -H 'content-type: application/json' -d '{"routing_key":"video.enviado"}')
case "$c1$c2$c3" in
  20[14]20[14]201) ok 'exchange, fila e binding criados' ;;
  *) falha "falha ao preparar a fila (codigos $c1 $c2 $c3)" ;;
esac

echo '3) Upload e consultas'
head -c 4096 /dev/urandom > smoke-upload.mp4
RESP=$(curl -s -w '\n%{http_code}' -X POST "$API/videos" -H "Authorization: Bearer $TOKEN" \
  -F "arquivo=@smoke-upload.mp4;type=video/mp4")
CODE=$(echo "$RESP" | tail -n 1)
BODY=$(echo "$RESP" | sed '$d')
checa 'POST /videos retorna 202' 202 "$CODE"
ID=$(echo "$BODY" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
if [ -n "$ID" ]; then ok "video criado: $ID"; else falha "resposta sem id: $BODY"; fim; fi
echo "$BODY" | grep -q '"status":"Pendente"' && ok 'status inicial Pendente' || falha 'status inicial diferente de Pendente'

curl -s -H "Authorization: Bearer $TOKEN" "$API/videos" | grep -q "$ID" \
  && ok 'GET /videos lista o video' || falha 'GET /videos nao lista o video'
checa 'GET /videos sem token retorna 401' 401 "$(curl -s -o /dev/null -w '%{http_code}' "$API/videos")"

echo texto > smoke-upload.txt
checa 'upload de .txt retorna 400' 400 "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/videos" \
  -H "Authorization: Bearer $TOKEN" -F 'arquivo=@smoke-upload.txt;type=text/plain')"
checa 'download antes de concluir retorna 409' 409 "$(curl -s -o /dev/null -w '%{http_code}' \
  -H "Authorization: Bearer $TOKEN" "$API/videos/$ID/download")"

echo '4) Mensagem na fila'
ACHOU=0
for _ in $(seq 1 15); do
  MSG=$(curl -s -u "$MQAUTH" -X POST "$MQ/api/queues/%2F/$FILA/get" -H 'content-type: application/json' \
    -d '{"count":1,"ackmode":"ack_requeue_false","encoding":"auto"}')
  if echo "$MSG" | grep -q "$ID"; then ACHOU=1; break; fi
  sleep 1
done
if [ "$ACHOU" -eq 1 ]; then ok 'mensagem video.enviado chegou na fila'; else falha 'mensagem nao chegou na fila em 15s'; fi

curl -s -o /dev/null -u "$MQAUTH" -X DELETE "$MQ/api/queues/%2F/$FILA"
fim
