#!/usr/bin/env bash
# Roda todos os testes com cobertura e gera o relatorio (precisa do Docker aberto).
# Uso: bash scripts/coverage.sh
set -u
cd "$(dirname "$0")/.."
rm -rf coverage-raw coverage-report

dotnet tool restore > /dev/null 2>&1 || { echo 'FALHA: dotnet tool restore'; exit 1; }

echo 'Rodando os testes com cobertura...'
if ! dotnet test FiapVideo.sln --settings coverlet.runsettings --collect:'XPlat Code Coverage' --results-directory coverage-raw > saida-coverage.txt 2>&1; then
  echo 'TESTES FALHARAM'
  grep -E 'error|Failed|Falha|Com falha' saida-coverage.txt | sort -u | head -n 20
  exit 1
fi

echo 'Gerando o relatorio...'
if ! dotnet reportgenerator -reports:'coverage-raw/**/coverage.cobertura.xml' -targetdir:coverage-report -reporttypes:'TextSummary;Html;Cobertura' > saida-reportgenerator.txt 2>&1; then
  echo 'FALHA: reportgenerator'
  tail -n 15 saida-reportgenerator.txt
  exit 1
fi

grep -E 'Line coverage|Branch coverage|Method coverage' coverage-report/Summary.txt
echo '--- projetos'
grep -E '^FiapVideo' coverage-report/Summary.txt
echo '--- classes abaixo de 80%'
grep -E '^  FiapVideo' coverage-report/Summary.txt | awk '{ p = $NF; sub("%", "", p); if (p + 0 < 80) print }'
echo 'Relatorio completo: coverage-report/index.html'
