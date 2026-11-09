# IA-Portal — Technical documentation

New contributors: [current state](architecture/CURRENT_STATE.md),
[target architecture](architecture/TARGET_ARCHITECTURE.md),
[product roadmap](ROADMAP.md), [self-hosting](SELF_HOSTING.md),
[organization invitations](features/ORGANIZATION_INVITATIONS.md),
[IA-Portal Service / channels](service/README.md),
[setup specification](features/SETUP_WIZARD.md) and
[license decision](LICENSE_OPTIONS.md). These English documents describe the
Community/shared-core direction. Historical delivery records below remain in
Portuguese and must not be confused with a claim that all SaaS features exist.

Este conjunto documenta **o objetivo e os contratos propostos**, não uma afirmação de que tudo já está implementado. O Codex deve confrontá-lo com o repositório antes de editar código.

## Estado verificado por auditoria em 01/11/2026

- IA-Portal executa localmente via Docker Compose, com frontend acessível em `127.0.0.1:9080`.
- Login e uma resposta via API DeepSeek foram observados no ambiente local.
- A interface foi renovada e exibe navegação para chat, dashboard, base de conhecimento, usuários e grupos/permissões.
- A auditoria inicial confirmou persistência em `ChatSession`/`ChatMessage` e identificou verificações de propriedade incompletas. A etapa 1 acrescentou filtros por proprietário em leituras/escritas e rejeição antes de OCR/LLM/SSE; os testes A/B estão registrados no diário. Isso não equivale a uma auditoria de segurança completa do produto.
- Após aprovação, a etapa 1 acrescentou sessões criadas pelo servidor, lista/busca, CRUD, recuperação entre navegadores e mensagens paginadas, com dono validado em cada operação. A migração SQL v1 preserva dados e faz backup consistente antes de alterar um esquema legado.
- A etapa 2 acrescentou títulos automáticos, fallback e consumo auxiliar separado em `LlmUsage`. A etapa 3 estendeu o registro para chat/documentos persistentes, consultas por período e agregados administrativos, com preços opcionais versionados. As etapas 4/5 integram os adapters existentes, seleção explícita, preferências e telas de configuração/consumo. OpenAI/Ollama precisam ser configurados e habilitados; não há fallback silencioso.
- A auditoria descreve a base anterior ao incremento; consulte [o estado implementado de conversas](features/CHAT_SESSIONS.md) e o diário para testes, aplicação local e limitações. A etapa 0 foi aprovada; as etapas 1/2 foram mergeadas; o proprietário autorizou concluir as etapas restantes na mesma branch, com merge ao final.

## Leitura recomendada

1. `../AGENTS.md` — limites e normas permanentes dos agentes.
2. `architecture/AS_IS_AUDIT.md` — roteiro e registro da auditoria do código real.
3. `architecture/DATA_MODEL.md` — modelo conceitual e migração segura.
4. `features/CHAT_SESSIONS.md` — sessões, mensagens, títulos e histórico.
5. `features/LLM_PROVIDERS.md` — DeepSeek, OpenAI, Ollama e configuração.
6. `features/USAGE_METERING.md` — tokens, custos e telemetria.
7. `security/SECURITY_REQUIREMENTS.md` — isolamento, segredos e autorização.
8. `design/DESIGN_SYSTEM.md` — identidade visual e componentes.
9. `delivery/ROADMAP.md` — fases e critérios de aceite.
10. `delivery/IMPLEMENTATION_LOG.md` — decisões, testes e pendências por entrega.

Contratos detalhados após inspeção: [architecture/API_CONTRACTS.md](architecture/API_CONTRACTS.md). Os resultados atuais de build e smoke estão no diário; testes de funcionalidades ainda propostas não foram declarados como aprovados.

## Convenções

- Requisitos: **MUST** obrigatório; **SHOULD** recomendado; **MAY** opcional.
- Exemplos de endpoints e nomes de tabelas são **propostas**. Não criar contratos paralelos sem verificar os existentes.
- Não usar dados reais de terceiros em testes, documentação ou screenshots.
- Toda mudança estrutural exige testes e plano de migração sem perda de dados.

Estado atual de configurações e consumo visual: [AI_SETTINGS.md](features/AI_SETTINGS.md). Resultados efetivos de testes e aplicação local no diário.


Atlas BI: [execucao local e migracao](demo/ATLAS_LOCAL.md),
[Data Connectors e seguranca SQL/rede](demo/DATA_CONNECTORS.md),
[chat hibrido e visualizacoes](demo/ATLAS_CHAT.md). Implementacao demonstrativa
com dados inteiramente sinteticos e isolamento por organizacao; consulte o diario
para distinguir testes de Trino real, doubles de LLM/OCR e fixtures de navegador.
