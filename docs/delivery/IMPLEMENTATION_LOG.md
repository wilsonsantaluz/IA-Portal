# Diário de implementação

> Documento vivo. Atualizar após cada etapa; **não declarar testes não executados**.

## Modelo de registro

### YYYY-MM-DD — Etapa X — Título

- **Branch/commit:**
- **Objetivo:**
- **Estado anterior / evidência:**
- **Arquivos alterados:**
- **Mudanças de API/banco:**
- **Migração/backup/rollback:**
- **Comandos executados:**
- **Testes aprovados:**
- **Testes falhos ou não executados:**
- **Riscos e pendências:**
- **Decisão/revisão do proprietário:**

## Estado inicial conhecido

- O proprietário confirmou execução local via Docker e resposta do DeepSeek.
- A interface atual foi refatorada pelo Codex; menus administrativos aparecem.
- Histórico por usuário, título automático, medição de tokens e múltiplos provedores **ainda exigem verificação técnica**.

### 2026-11-01 — Etapa 0 — Auditoria e proposta para revisão

- **Branch/base:** `feature/chat-sessions-providers`, criada a partir de `ec9d14e`. A implementação de usuários/grupos já está commitada em `f44b803` e incorporada à main pelo PR #4; nenhum commit duplicado dessa entrega foi criado.
- **Objetivo:** confrontar a documentação fornecida pelo proprietário com controllers, entidades, repositórios, frontend, adapters e inicialização reais. Respeitar a parada de revisão explícita do roadmap.
- **Estado anterior:** pacote técnico novo e atualização de AGENTS ainda não rastreados/commitados; código de usuários limpo. Preservados e incorporados ao registro técnico.
- **Diagnóstico:** mensagens persistem em SQLite, mas há uma sessão por navegador/conta, sem CRUD/lista/títulos/uso. Existe vínculo JWT para novas sessões e filtro de leitura, porém faltam verificações em cada escrita e autorização antecipada de OCR/SSE. DeepSeek ativo; OpenAI/Ollama existem, mas não estão selecionados no chat. Credencial literal no Ollama e logging de payloads são bloqueios registrados, sem transcrição/teste do segredo.
- **Arquivos alterados:** `AGENTS.md`, README raiz e índice em `docs/README.md`; auditoria e proposta em `docs/architecture/{AS_IS_AUDIT,DATA_MODEL,API_CONTRACTS}.md`; roadmap/diário; complemento de segurança; notas do design system e marcação da documentação visual antiga como histórico. Documentos conceituais de features e referências visuais fornecidos foram incluídos, sem alterar o frontend.
- **Mudanças de API/banco:** nenhuma. Contratos, tabelas/campos, índices, registro de chamadas e runner SQL versionado são propostas, não implementação. `.env.example` permanece como está porque nenhuma variável foi introduzida neste incremento.
- **Migração/backup/rollback:** somente plano. Não foi aberto/copiado/migrado/restaurado o SQLite diário. Nenhum container foi reconstruído ou recriado; testes usam dados sintéticos temporários.

Comandos executados e resultados:

| Comando | Resultado efetivo |
| --- | --- |
| `git status --short`, `git log -3 --oneline`, leitura dos manifests/fontes/docs | Auditoria realizada; último merge da etapa anterior confirmado |
| `git switch -c feature/chat-sessions-providers` | Branch criada; sem alteração direta na main |
| `dotnet build ai-gateway/AiGateway.csproj --no-restore -v quiet` | Passou, 0 erros e 14 avisos preexistentes |
| `node front/node_modules/typescript/bin/tsc --noEmit -p front/tsconfig.json` | Passou |
| `node node_modules/vite/bin/vite.js build` em `front/` | Passou; aviso preexistente de chunk maior que 500 kB |
| `python scripts/smoke_local.py` | Passou: criação SQLite, bootstrap/login, KB, streaming/histórico, avaliações, rota OCR e persistência após reinício; LLM/OCR simulados |
| `dotnet run --project tests/access/SeedSmoke` | Passou: inicialização idempotente preserva fixture de usuários/senhas/grupos/KB e não eleva não administradores |

- **Não executado:** nova suíte de CRUD de conversas (não implementado); títulos/usage/providers; migração de esquema e restauração; testes reais de OpenAI/Ollama/DeepSeek; suíte de navegador, RAG/ONNX, Docker builds ou builds OCR/ML. Nenhuma chamada paga foi realizada. Builds demonstram compilação, não validação de integração externa.
- **Fontes externas:** documentação oficial de usage DeepSeek, OpenAI e Ollama e `EnsureCreated`/EF Core, vinculada na auditoria; sem adoção de preços ou troca de modelo. Skill OpenAI Docs usada para a consulta de contrato OpenAI; nenhuma chave solicitada ou necessária para leitura de documentação.
- **Riscos e pendências:** resolver segredo literal/logging antes de habilitar adapters alternativos; endurecer posse em OCR/SSE/repositório; revisão do plano de quarentena dos legados, exclusão lógica, backup e migração antes de implementação. Remover segredo do código não revoga seu valor nem o histórico; revogação exige ação do responsável no provedor.
- **Decisão/revisão do proprietário:** pendente. `ROADMAP.md` etapa 0 manda **Parar para revisão**; `CODEX_START_HERE.md` determina primeira tarefa somente auditoria. Não iniciadas etapas 1–5, nem criadas entidades/APIs de fase posterior.
