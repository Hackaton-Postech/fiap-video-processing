#!/usr/bin/env bash
# Teste do Notification Service: um video invalido deve gerar um e-mail de falha (visto no Mailpit).
# Uso (com o compose no ar): bash scripts/smoke-notification.sh
set -u
cd "$(dirname "$0")/.."
set -a; . ./.env; set +a

AUTH=${AUTH_URL:-http://localhost:5001}
API=${VIDEOS_URL:-http://localhost:5002}
MAIL=${MAILPIT_URL:-http://localhost:8025}
EMAIL='smoke@teste.com'
SENHA='Senha12345'
FALHAS=0
ARQUIVO="invalido-$RANDOM.mp4"

ok()    { echo "  OK     - $1"; }
falha() { echo "  FALHA  - $1"; FALHAS=$((FALHAS + 1)); }
fim()   { rm -f "$ARQUIVO"; echo "RESULTADO: $FALHAS falha(s)"; [ "$FALHAS" -eq 0 ]; exit $?; }

echo '1) Mailpit e autenticacao'
if [ "$(curl -s -o /dev/null -w '%{http_code}' "$MAIL/api/v1/messages")" = "200" ]; then ok 'Mailpit respondendo'; else falha "Mailpit nao respondeu em $MAIL"; fim; fi
curl -s -o /dev/null -X DELETE "$MAIL/api/v1/messages"
curl -s -o /dev/null -X POST "$AUTH/auth/register" -H 'Content-Type: application/json' \
  -d "{\"nome\":\"Smoke\",\"email\":\"$EMAIL\",\"senha\":\"$SENHA\"}"
TOKEN=$(curl -s -X POST "$AUTH/auth/login" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"senha\":\"$SENHA\"}" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
if [ -n "$TOKEN" ]; then ok 'login retornou token'; else falha 'login sem token'; fim; fi

echo '2) Upload de arquivo invalido'
head -c 4096 /dev/urandom > "$ARQUIVO"
RESP=$(curl -s -X POST "$API/videos" -H "Authorization: Bearer $TOKEN" -F "arquivo=@$ARQUIVO;type=video/mp4")
ID=$(echo "$RESP" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
if [ -n "$ID" ]; then ok "upload aceito: $ID"; else falha "upload recusado: $RESP"; fim; fi

echo '3) E-mail de falha'
ACHOU=0
for _ in $(seq 1 45); do
  LISTA=$(curl -s "$MAIL/api/v1/messages")
  if echo "$LISTA" | grep -q "$ARQUIVO"; then ACHOU=1; break; fi
  sleep 2
done
if [ "$ACHOU" -eq 1 ]; then ok 'e-mail chegou no Mailpit'; else falha 'e-mail nao chegou em 90s'; fim; fi
if echo "$LISTA" | grep -q 'Falha ao processar'; then ok 'assunto indica falha no processamento'; else falha 'assunto diferente do esperado'; fi
if echo "$LISTA" | grep -q "$EMAIL"; then ok "destinatario correto ($EMAIL)"; else falha 'destinatario diferente do esperado'; fi

fim
