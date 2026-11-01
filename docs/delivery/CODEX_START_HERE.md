# Instrução inicial para Codex

**Estado atualizado em 01/11/2026:** a auditoria inicial abaixo já foi concluída e aprovada pelo proprietário, incluindo o refinamento do diagrama de entidades. A etapa 1 foi mergeada; a etapa 2 de títulos foi mergeada e a etapa 3 de consumo foi autorizada/implementada. Consultar o diário/roadmap antes de iniciar provedores. Não reiniciar a auditoria nem presumir que as etapas posteriores foram implementadas. A instrução de primeira tarefa abaixo registra o início do trabalho.

Leia `AGENTS.md` e `docs/README.md`. Leia também todos os documentos de `docs/architecture`, `docs/features`, `docs/security` e `docs/delivery/ROADMAP.md`.

**Primeira tarefa: apenas auditoria.** Inspecione o código real e preencha `docs/architecture/AS_IS_AUDIT.md` com referências de arquivos, controllers, serviços, entidades, contratos, rotas e testes. Compare a implementação atual com as propostas documentadas. Identifique riscos de autorização, migração do SQLite e exposição de segredos. Não implemente entidades ou APIs antes de apresentar o diagnóstico para revisão.

Respeite a identidade visual já aplicada. Trabalhe em branch separada, preserve o banco e não faça merge. Registre somente testes efetivamente executados. Não inclua dados da Conveste.
