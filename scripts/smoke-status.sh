#!/usr/bin/env bash
# Teste de ponta a ponta do status: upload -> Worker -> Video API (Concluido/Erro) -> download do zip.
# Uso (com o compose no ar): bash scripts/smoke-status.sh
set -u
cd "$(dirname "$0")/.."
set -a; . ./.env; set +a

AUTH=${AUTH_URL:-http://localhost:5001}
API=${VIDEOS_URL:-http://localhost:5002}
SENHA='Senha12345'
AMOSTRA=scripts/amostra.mp4
FALHAS=0

ok()    { echo "  OK     - $1"; }
falha() { echo "  FALHA  - $1"; FALHAS=$((FALHAS + 1)); }
fim()   { rm -f smoke-invalido.mp4 smoke-frames.zip; echo "RESULTADO: $FALHAS falha(s)"; [ "$FALHAS" -eq 0 ]; exit $?; }

login() {  # $1 = email (registra se preciso e devolve o token)
  curl -s -o /dev/null -X POST "$AUTH/auth/register" -H 'Content-Type: application/json' \
    -d "{\"nome\":\"Smoke\",\"email\":\"$1\",\"senha\":\"$SENHA\"}"
  curl -s -X POST "$AUTH/auth/login" -H 'Content-Type: application/json' \
    -d "{\"email\":\"$1\",\"senha\":\"$SENHA\"}" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p'
}

upload() {  # $1 = token, $2 = arquivo
  curl -s -X POST "$API/videos" -H "Authorization: Bearer $1" -F "arquivo=@$2;type=video/mp4"
}

espera_status() {  # $1 = id, $2 = status esperado, $3 = token
  local i corpo
  for i in $(seq 1 45); do
    corpo=$(curl -s -H "Authorization: Bearer $3" "$API/videos/$1")
    if echo "$corpo" | grep -q "\"status\":\"$2\""; then echo "$corpo"; return 0; fi
    sleep 2
  done
  echo "$corpo"
  return 1
}

echo '1) Autenticacao'
TOKEN=$(login smoke@teste.com)
if [ -n "$TOKEN" ]; then ok 'login do usuario 1'; else falha 'login sem token'; fim; fi
[ -f "$AMOSTRA" ] || { echo "Falta $AMOSTRA"; exit 1; }

echo '2) Video valido: Pendente -> Concluido'
ID=$(upload "$TOKEN" "$AMOSTRA" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
if [ -n "$ID" ]; then ok "upload aceito: $ID"; else falha 'upload recusado'; fim; fi
if CORPO=$(espera_status "$ID" Concluido "$TOKEN"); then
  ok 'status mudou para Concluido'
  if echo "$CORPO" | grep -q '"zipDisponivel":true'; then ok 'zipDisponivel = true'; else falha 'zipDisponivel diferente de true'; fi
else
  falha "status nao chegou a Concluido em 90s (ultimo: $CORPO)"
fi

echo '3) Download do zip'
CODIGO=$(curl -s -o smoke-frames.zip -w '%{http_code}' -H "Authorization: Bearer $TOKEN" "$API/videos/$ID/download")
if [ "$CODIGO" = "200" ]; then ok 'download retornou 200'; else falha "download retornou $CODIGO"; fi
TAM=$(wc -c < smoke-frames.zip 2>/dev/null | tr -d ' ')
if [ "${TAM:-0}" -gt 1000 ]; then ok "zip com $TAM bytes"; else falha "zip pequeno demais (${TAM:-0} bytes)"; fi
if [ "$(head -c 2 smoke-frames.zip 2>/dev/null)" = "PK" ]; then ok 'arquivo tem assinatura de zip (PK)'; else falha 'arquivo nao parece um zip'; fi

echo '4) Arquivo invalido: Pendente -> Erro'
head -c 4096 /dev/urandom > smoke-invalido.mp4
ID2=$(upload "$TOKEN" smoke-invalido.mp4 | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
if [ -n "$ID2" ]; then ok "upload aceito: $ID2"; else falha 'upload recusado'; fim; fi
if CORPO=$(espera_status "$ID2" Erro "$TOKEN"); then
  ok 'status mudou para Erro'
  MOTIVO=$(echo "$CORPO" | sed -n 's/.*"mensagemErro":"\([^"]*\)".*/\1/p')
  if [ -n "$MOTIVO" ]; then ok "motivo registrado: $MOTIVO"; else falha 'mensagemErro vazia'; fi
else
  falha "status nao chegou a Erro em 90s (ultimo: $CORPO)"
fi
CODIGO=$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $TOKEN" "$API/videos/$ID2/download")
if [ "$CODIGO" = "409" ]; then ok 'download de video com erro retorna 409'; else falha "download de video com erro retornou $CODIGO"; fi

echo '5) Privacidade entre usuarios'
TOKEN2=$(login smoke2@teste.com)
CODIGO=$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $TOKEN2" "$API/videos/$ID")
if [ "$CODIGO" = "404" ]; then ok 'outro usuario recebe 404 no video alheio'; else falha "outro usuario recebeu $CODIGO no video alheio"; fi
if curl -s -H "Authorization: Bearer $TOKEN2" "$API/videos" | grep -q "$ID"; then
  falha 'listagem do usuario 2 mostra video do usuario 1'
else
  ok 'listagem do usuario 2 nao mostra video alheio'
fi

fim
