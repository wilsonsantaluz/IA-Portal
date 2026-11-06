# Prompt para Codex local — IA-Portal: Atlas Billing Intelligence

Você está trabalhando no repositório IA-Portal. Implemente incrementalmente uma demonstração empresarial chamada **Atlas Serviços Digitais**, totalmente fictícia, combinando base de conhecimento e dados analíticos via Trino.

## Antes de editar
1. Leia AGENTS.md, README.md, docs/README.md, docs/architecture/AS_IS_AUDIT.md, docs/architecture/DATA_MODEL.md, docs/security/SECURITY_REQUIREMENTS.md e docs/demo/ATLAS_KNOWLEDGE_BASE.md.
2. Inspecione o código atual de EF Core, DatabaseInitializer, KB, chat, TrinoRestClient, TrinoQueryService, TrinoMetadataService, SQL planner e frontend. O README pode estar desatualizado: prevalece o código real.
3. Faça um inventário do que existe, do que falta, dos riscos e da estratégia de migração. Não afirme funcionalidades não implementadas.
4. Não utilize dados, regras, relatórios, nomes ou códigos de qualquer empregador ou cliente. Todo dataset será gerado sinteticamente.
5. Preserve login, RBAC, histórico, SSE, OCR e configurações de provedores. Não introduza chaves ou credenciais em código, logs, commits ou browser.

## Fase A — Base de conhecimento
- Transforme as 10 seções de ATLAS_KNOWLEDGE_BASE.md em entradas versionadas, com IDs estáveis, categoria, origem 'synthetic-demo' e indicação explícita de conteúdo fictício.
- Faça seed **opt-in**, idempotente, por configuração, não ativado por padrão em instalações existentes.
- Inspecione a estrutura do SQLite e o mecanismo de evolução já utilizado. Se forem necessárias mudanças, use migration versionada, backup consistente e rollback testado; não recrie o banco nem use EnsureCreated como substituto de migração.
- Não sobrescreva edições humanas. Teste seed inicial, reexecução, banco legado e isolamento por organização/usuário, conforme o modelo de autorização disponível.
- Se o RAG vetorial estiver ativo, documente o reindex e trate ausência de modelos sem impedir o modo local.

## Fase B — Dataset sintético
- Gere dataset determinístico (seed fixa) de aproximadamente 500 clientes fictícios, 12 meses de contratos/faturas, pagamentos parciais, atrasos, estornos e contestações.
- Modelo sugerido: customers, plans, contracts, invoices, payments, disputes, collection_actions; relações e chaves consistentes; datas coerentes; nenhuma PII real.
- Inclua cenários contrastantes: clientes em dia, atrasos 1–15/16–30/31–60/61–90/>90 dias, pagamento parcial, cancelamento, concentração de receita e sazonalidade.
- Produza tabelas Parquet/CSV ou outro formato compatível com um catálogo Trino local de demonstração. Não exigir infraestrutura proprietária ou acesso a dados externos.
- Documente dicionário de dados, fórmulas, consultas de verificação e resultados esperados. Valores demonstrativos não são números reais de empresa alguma.

## Fase C — Módulo de conectores Trino
- Reutilize os serviços Trino existentes; evite segundo cliente HTTP/SQL concorrente.
- Crie registro de conexões por organização, com nome, endpoint, catálogo, schema, método de autenticação suportado e política de acesso. Armazene segredos cifrados em serviço apropriado ou referencie secret manager; nunca envie credenciais ao frontend.
- Inclua tipos: 'Atlas demo' (local), 'Organizational Trino' (administrador) e 'Public dataset' (somente fontes verificadas, com licença e atribuição).
- Permita testar conexão, descobrir catálogos/schemas/tabelas permitidos, inspecionar metadados e selecionar fontes habilitadas para cada grupo/permissão.
- Consultas apenas SELECT (incluindo WITH ... SELECT quando validado); não confiar só em regex. Bloquear múltiplas instruções, funções perigosas e acesso a catálogos fora da allowlist. Usar credencial de banco estritamente read-only, limites de linhas, timeout curto, cancelamento, controle de concorrência e auditoria sanitizada.
- Restringir SSRF: destinos de rede permitidos, TLS e validação de endpoint; impedir conexões arbitrárias a serviços internos. Isolar tenants no backend e no Trino.
- Não permitir que texto recuperado, prompts ou resultados de consulta alterem políticas de autorização.

## Fase D — Orquestração RAG + SQL
- Classifique perguntas em: conhecimento documental, consulta analítica, híbrida ou insuficiente.
- Para pergunta híbrida, obtenha políticas no KB e agregações autorizadas no Trino; entregue ao LLM somente dados mínimos necessários.
- Resposta deve mostrar origem, período, filtros, citações documentais e proveniência da consulta; indicar claramente estimativas e simulações.
- Mermaid: gere flowchart para processos, sequenceDiagram para integrações e gráficos Mermaid somente onde o renderizador suportar; para séries financeiras prefira gráficos numéricos nativos quando existirem.
- Renderize Mermaid em sandbox segura, com proteção contra conteúdo malicioso; ofereça fallback textual. Não aceite HTML/script arbitrário.
- Nunca executar SQL sugerido pelo LLM sem validação e política de execução. Não transmitir dados sensíveis a provedores externos sem configuração explícita.

## Fase E — Demonstração e validação
- Criar tela de seleção da base 'Atlas Demo', lista de conectores autorizados, status da conexão e perguntas sugeridas.
- Criar cenários guiados: (1) política de cobrança, (2) aging de recebíveis, (3) faturamento versus caixa, (4) análise híbrida, (5) fluxograma de contestação.
- Incluir testes unitários, integração com SQLite temporário, testes de autorização cross-tenant, SQL adversarial, seed idempotente e smoke Docker; não alegar testes reais que não rodaram.
- Registrar alterações e limitações no README e no diário de implementação.

## Entrega
Trabalhe por fases e commits pequenos. Primeiro apresente diagnóstico e plano; depois implemente Fase A e valide antes de avançar. Não publicar na internet nem executar deploy sem autorização. Não apagar volumes nem migrar dados de produção sem backup e aprovação. Gere ao final checklist com arquivos alterados, comandos executados, resultados de testes, limitações e próximos passos.
