# Provedores — etapas 4/5

```powershell
dotnet run --project tests/Providers
python scripts/smoke_local.py
npm --prefix front run typecheck
npm --prefix front run build
```

`Providers` usa SQLite temporário e handlers HTTP em memória. Exercita os três adapters reais com modelos/credenciais sintéticos: seleção e preferência, resposta/modelo efetivo, título limitado, contadores, Ollama local/remoto, ausência de thinking no texto, troca explícita preservando histórico, bloqueio durante geração e isolamento A/B. Valida allowlist, proibição de userinfo, erros sanitizados e falha sem fallback. Injeta erro na migração v4 para testar backup/rollback/idempotência/preservação/checksum.

O smoke inicia gateway real e doubles HTTP locais; verifica provedores, preferências após restart, OpenAI/Ollama simulados, consumo e APIs de isolamento. Nenhum teste acessa o banco diário, endpoint/credencial antigos ou provedor pago. Resultados/limitações estão no diário de implementação.
