# Roadmap técnico — execução incremental

**Branch sugerida:** `feature/chat-sessions-providers`. Se a branch já existir, verificar `git status` e alterações pendentes antes de prosseguir. Nunca commitar diretamente em `main` nem fazer merge automático.

**Situação em 01/11/2026:** etapas 0/1 aprovadas e mergeadas em `main` (`5a3fd01`). Etapa 2 implementada na branch de trabalho após autorização do proprietário, com títulos e consumo auxiliar separado. Etapas 3–5 ainda não iniciadas. Resultados, limitações e aplicação local no diário.

| Etapa | Entrega | Critérios de aceite | Bloqueio |
| --- | --- | --- | --- |
| 0 | Auditoria e proposta | `AS_IS_AUDIT.md` preenchido, inventário real e plano de migração revisado | **Parar para revisão** |
| 1 | Sessões por usuário | CRUD, mensagens persistidas, histórico após restart, isolamento A/B testado | Auth e migração segura |
| 2 | Títulos automáticos | Título após 1ª resposta, fallback, edição manual preservada, uso contabilizável | Etapa 1 |
| 3 | Métricas de tokens | Usage por chamada/sessão/usuário/provedor, fonte declarada, sem dados falsos | Etapa 1 |
| 4 | Providers | DeepSeek, OpenAI, Ollama via adapters existentes, modelo selecionável, erros claros | Auditoria dos adapters |
| 5 | Configurações e dashboard | Admin configura modelos sem expor chaves, usuário escolhe modelo permitido, métricas reais | Auth + etapas 3/4 |

## Por etapa

Revisar [o diagnóstico](../architecture/AS_IS_AUDIT.md), [o modelo de dados](../architecture/DATA_MODEL.md) e [os contratos](../architecture/API_CONTRACTS.md) antes da etapa 1. A etapa 2 deve incluir o registro mínimo do consumo da chamada de título; agregações e métricas completas continuam na etapa 3. Antes de habilitar adapters alternativos, resolver os bloqueios de credencial literal/logs apontados na auditoria.

1. Apresentar diagnóstico, arquivos previstos e impacto em dados/contratos.
2. Alterar apenas o escopo acordado.
3. Executar build .NET e frontend; testes unitários/integração e smoke local quando possível.
4. Registrar resultados e limitações no `IMPLEMENTATION_LOG.md`.
5. Solicitar revisão antes de começar a etapa seguinte.

## Não fazer

- Não apagar SQLite nem converter registros legados silenciosamente.
- Não criar telas de demonstração com números inventados.
- Não duplicar implementações de DeepSeek/OpenAI/Ollama.
- Não expor credenciais em código, browser, commits ou logs.
- Não publicar dados ou código do empregador anterior.
