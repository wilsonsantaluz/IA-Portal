# Provedores — etapas 4/5

```powershell
dotnet run --project tests/Providers
python scripts/smoke_local.py
npm --prefix front run typecheck
npm --prefix front run build
```

`Providers` usa SQLite temporário e handlers HTTP em memória. Exercita os três adapters reais com modelos/credenciais sintéticos: seleção e preferência, resposta/modelo efetivo, título limitado, contadores, Ollama local/remoto, ausência de thinking no texto, troca explícita preservando histórico, bloqueio durante geração e isolamento A/B. Valida allowlist, proibição de userinfo, erros sanitizados e falha sem fallback. Injeta erro na migração v4 para testar backup/rollback/idempotência/preservação/checksum.

O smoke inicia gateway real e doubles HTTP locais; verifica provedores, preferências após restart, OpenAI/Ollama simulados, consumo e APIs de isolamento. Nenhum teste acessa o banco diário, endpoint/credencial antigos ou provedor pago. Resultados/limitações estão no diário de implementação.

## Navegador e imagens Docker

Requer Docker, Chrome e Playwright/axe-core disponíveis em `%TEMP%/ia-portal-ui-tools/node_modules`, ou caminhos informados em `IA_PLAYWRIGHT_MODULE`, `IA_AXE_MODULE`, `IA_CHROME_PATH`.

```powershell
python scripts/test_ai_ui.py
# Reutilizar as imagens finais já construídas:
python scripts/test_ai_ui.py --skip-build
```

O runner cria containers/rede próprios, SQLite tmpfs, helper HTTP sem encaminhamento e arquivo temporário com credenciais sintéticas; remove os recursos criados mesmo se o teste falhar. Recusa instalação de teste com conversas preexistentes. Não usar dados/rede/serviço de inferência diários como fixture. O helper usa porta 19091, API 19089 e frontend 19080. As chamadas são simuladas: geração de 34 registros, falha posterior, métricas parciais e paginação representam dados realmente persistidos no banco descartável.

Verifica configurações, preferência/global, conectividade sem consumo de inferência, troca preservando respostas/modelos, consumo próprio/admin, negação A/B, estados vazios, cinco grupos de verificações visuais, axe-core e overflow em claro/escuro a 1440/390/320 px. Capturas/resumo em `docs/design/screenshots/ai-settings/`. O runner usa resposta HTTP 401 de uma API protegida como readiness; a rota Swagger via frontend seria apenas fallback SPA e não serve como prova de API pronta.
