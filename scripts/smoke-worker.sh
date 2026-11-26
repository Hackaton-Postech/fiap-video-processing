#!/usr/bin/env bash
# Teste do Worker: envia um video real e um arquivo invalido e confere os eventos e o zip no storage.
# Uso (com o compose no ar): bash scripts/smoke-worker.sh
set -u
cd "$(dirname "$0")/.."
set -a; . ./.env; set +a

AUTH=${AUTH_URL:-http://localhost:5001}
API=${VIDEOS_URL:-http://localhost:5002}
MQ=${RABBITMQ_URL:-http://localhost:15672}
MQAUTH="$RABBITMQ_USER:$RABBITMQ_PASSWORD"
EMAIL='smoke@teste.com'
SENHA='Senha12345'
FILA_OK='smoke.video.processado'
FILA_ERRO='smoke.video.falhou'
AMOSTRA=scripts/amostra.mp4
FALHAS=0

ok()    { echo "  OK     - $1"; }
falha() { echo "  FALHA  - $1"; FALHAS=$((FALHAS + 1)); }
fim()   {
  curl -s -o /dev/null -u "$MQAUTH" -X DELETE "$MQ/api/queues/%2F/$FILA_OK"
  curl -s -o /dev/null -u "$MQAUTH" -X DELETE "$MQ/api/queues/%2F/$FILA_ERRO"
  rm -f smoke-invalido.mp4
  echo "RESULTADO: $FALHAS falha(s)"; [ "$FALHAS" -eq 0 ]; exit $?
}

prepara_fila() {  # $1 = fila, $2 = routing key
  curl -s -o /dev/null -u "$MQAUTH" -X DELETE "$MQ/api/queues/%2F/$1"
  curl -s -o /dev/null -u "$MQAUTH" -X PUT "$MQ/api/queues/%2F/$1" -H 'content-type: application/json' -d '{"durable":true}'
  curl -s -o /dev/null -u "$MQAUTH" -X POST "$MQ/api/bindings/%2F/e/video.events/q/$1" \
    -H 'content-type: application/json' -d "{\"routing_key\":\"$2\"}"
}

espera_msg() {  # $1 = fila, $2 = id do video
  local i msg
  for i in $(seq 1 45); do
    msg=$(curl -s -u "$MQAUTH" -X POST "$MQ/api/queues/%2F/$1/get" -H 'content-type: application/json' \
      -d '{"count":50,"ackmode":"ack_requeue_true","encoding":"auto"}')
    if echo "$msg" | grep -q "$2"; then echo "$msg"; return 0; fi
    sleep 2
  done
  return 1
}

[ -f "$AMOSTRA" ] || { echo "Falta $AMOSTRA"; exit 1; }

echo '1) Autenticacao'
curl -s -o /dev/null -X POST "$AUTH/auth/register" -H 'Content-Type: application/json' \
  -d "{\"nome\":\"Smoke\",\"email\":\"$EMAIL\",\"senha\":\"$SENHA\"}"
TOKEN=$(curl -s -X POST "$AUTH/auth/login" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"senha\":\"$SENHA\"}" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
if [ -n "$TOKEN" ]; then ok 'login retornou token'; else falha 'login sem token'; fim; fi

echo '2) Filas de teste'
prepara_fila "$FILA_OK" video.processado
prepara_fila "$FILA_ERRO" video.falhou
ok 'filas de resultado preparadas'

echo '3) Video valido'
RESP=$(curl -s -X POST "$API/videos" -H "Authorization: Bearer $TOKEN" -F "arquivo=@$AMOSTRA;type=video/mp4")
ID=$(echo "$RESP" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
if [ -n "$ID" ]; then ok "upload aceito: $ID"; else falha "upload recusado: $RESP"; fim; fi

USERID=''
if MSG=$(espera_msg "$FILA_OK" "$ID"); then
  ok 'evento video.processado publicado'
  FRAMES=$(echo "$MSG" | sed -n 's/.*totalFrames\\":\([0-9]*\).*/\1/p' | head -n 1)
  USERID=$(echo "$MSG" | sed -n 's/.*usuarioId\\":\\"\([^\\]*\)\\".*/\1/p' | head -n 1)
  if [ "${FRAMES:-0}" -ge 1 ]; then ok "frames extraidos: $FRAMES"; else falha 'nenhum frame informado no evento'; fi
else
  falha 'evento video.processado nao chegou em 90s'
fi

if command -v aws > /dev/null && [ -n "$USERID" ]; then
  LISTA=$(AWS_ACCESS_KEY_ID="$STORAGE_ACCESS_KEY" AWS_SECRET_ACCESS_KEY="$STORAGE_SECRET_KEY" AWS_DEFAULT_REGION=us-east-1 \
    aws --endpoint-url http://localhost:8333 s3 ls "s3://$STORAGE_BUCKET/$USERID/$ID/")
  if echo "$LISTA" | grep -q 'frames.zip'; then
    ok "frames.zip no storage ($(echo "$LISTA" | grep frames.zip | tr -s ' ' | cut -d' ' -f3) bytes)"
  else
    falha 'frames.zip nao encontrado no storage'
  fi
else
  echo '  (verificacao do storage pulada: aws cli ausente ou usuario nao identificado)'
fi

echo '4) Arquivo invalido (deve gerar video.falhou)'
head -c 4096 /dev/urandom > smoke-invalido.mp4
RESP=$(curl -s -X POST "$API/videos" -H "Authorization: Bearer $TOKEN" -F 'arquivo=@smoke-invalido.mp4;type=video/mp4')
ID2=$(echo "$RESP" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
if [ -n "$ID2" ]; then ok "upload aceito: $ID2"; else falha "upload recusado: $RESP"; fim; fi

if MSG=$(espera_msg "$FILA_ERRO" "$ID2"); then
  ok 'evento video.falhou publicado'
  echo "$MSG" | sed -n 's/.*mensagem\\":\\"\([^\\]*\)\\".*/         motivo: \1/p' | head -n 1
else
  falha 'evento video.falhou nao chegou em 90s'
fi

fim
