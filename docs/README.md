# IA-Portal — Índice técnico e decisões

Este conjunto documenta **o objetivo e os contratos propostos**, não uma afirmação de que tudo já está implementado. O Codex deve confrontá-lo com o repositório antes de editar código.

## Estado verificado por auditoria em 01/11/2026

- IA-Portal executa localmente via Docker Compose, com frontend acessível em `127.0.0.1:9080`.
- Login e uma resposta via API DeepSeek foram observados no ambiente local.
- A interface foi renovada e exibe navegação para chat, dashboard, base de conhecimento, usuários e grupos/permissões.
- A leitura de código confirmou persistência em `ChatSession`/`ChatMessage`, dono derivado do JWT na criação e filtro de dono no histórico. A verificação de propriedade ainda precisa ser aplicada a cada escrita e antes de chamadas externas; não é uma garantia completa de isolamento de todos os fluxos.
- Frontend guarda uma sessão por conta/navegador; não existe CRUD/lista de conversas persistentes, títulos automáticos, uso/custo ou migração versionada. Os adapters OpenAI/Ollama existem, mas não estão habilitados no fluxo principal.
- Auditoria e proposta de dados/API registradas; **etapa 0 aguarda revisão**, conforme o roadmap. Código, esquema e containers não foram alterados neste incremento.

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
