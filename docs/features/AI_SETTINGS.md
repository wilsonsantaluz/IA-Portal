# Configurações e consumo de Inteligência Artificial

## Telas implementadas — etapa 5

Navegação **Inteligência Artificial**, rota `/settings/ai`, mantém o shell e os temas existentes. Usuários com `chat.access` escolhem seu modelo padrão entre opções habilitadas/configuradas. A preferência vale para novas conversas; conversas existentes só mudam pelo seletor com confirmação em Chat IA. Opção previamente escolhida e desabilitada aparece indisponível, sem fallback silencioso.

Com `ai.manage`, a mesma tela também mostra configuração de DeepSeek/OpenAI/Ollama: habilitação, endpoint permitido, modelos separados por vírgula, modelo padrão do provedor, padrão global versionado e teste de conectividade. Salvar um provedor não apaga rascunhos dos demais. Atualizar recarrega as configurações salvas. Versão concorrente rejeitada retorna erro e exige atualização explícita.

Chaves são configuradas exclusivamente no servidor por `DEEPSEEK_API_KEY`, `OPENAI_API_KEY` e opcionalmente `OLLAMA_API_KEY`. A tela mostra apenas configurada/ausente (ou opcional para Ollama), sem campo de chave, endpoint de leitura de segredo ou armazenamento em texto puro no SQLite. Variáveis existentes continuam funcionando quando não existe configuração persistida. Alterações de variáveis exigem reiniciar/recriar o gateway com o ambiente atualizado; não regravar chaves via browser.

URLs precisam satisfazer as allowlists do servidor descritas em [LLM_PROVIDERS.md](LLM_PROVIDERS.md). Configuração que ficou inválida após mudança no ambiente é exibida com diagnóstico sanitizado, permitindo correção por um endpoint permitido; não vaza credenciais embutidas em URL inválida. Estado configurado não significa conectividade ou disponibilidade do modelo.

Teste de conexão é uma solicitação GET a `models` (DeepSeek/OpenAI) ou `api/tags` (Ollama), limitada a 10 segundos e corpo de diagnóstico de até 256 KiB. Não faz inferência, título, instalação de modelo ou download. Verifica formato da resposta de metadados; mostra sucesso/falha, categoria e horário do teste nesta sessão. Não garante que cada modelo configurado esteja instalado/autorizado. Status é diagnóstico atual da tela, não monitoramento periódico nem SLA persistido.

## Consumo

Navegação **Consumo de IA**, rota `/usage`, oferece período UTC, provider/modelo, finalidade e filtro de conversa própria com carregamento progressivo da lista. O botão **Consumo desta conversa** no chat já abre o filtro correto. Indicadores e tabelas vêm somente das APIs de consumo, com erros, estados de carregamento e estado vazio reais.

Há total de chamadas, erros, resultados desconhecidos, tokens reportados e cobertura, custo externo estimado e quantidade com custo conhecido. Agrupamentos por modelo, provedor, finalidade, dia/mês e detalhes paginados das próprias chamadas incluem duração/status. Cache é identificado como parte da entrada. Falta de métricas ou preço não é apresentada como zero; somas parciais são rotuladas. API local e custo de infraestrutura são separados explicitamente. Sem preços reais configurados no arquivo, custos externos de chamadas remotas continuam indisponíveis.

`usage.admin` permite a aba de todos os usuários, com filtro por ID e agregados por usuário. Não fornece detalhes de mensagens, prompts, respostas ou lista de chamadas de outras pessoas. `chat.access`, `dashboard.view`, `access.manage` e `ai.manage` não substituem essa permissão. Grupos com somente `usage.admin` ou `ai.manage` têm rota inicial e navegação para suas capacidades, sem precisar liberar chat/administração de contas.

Seleções de filtros são rascunhos até **Aplicar filtros**; o período efetivamente aplicado é exibido. Paginação reutiliza os mesmos limites temporais. Consultas inválidas ou erros não deixam indicadores antigos sob filtros novos. As tabelas podem rolar horizontalmente dentro do card em telas estreitas. Não há dados de demonstração no produto, gráficos fictícios ou cobrança de infraestrutura inventada.

## Contratos administrativos

| Método/rota | Regra |
| --- | --- |
| `GET /api/admin/ai/providers` | `ai.manage`; configurações sem segredo |
| `PUT /api/admin/ai/providers/{id}` | `ai.manage`; enabled, endpoint, models, defaultModel, version |
| `POST /api/admin/ai/providers/{id}/test` | `ai.manage`; teste limitado de metadados sem inferência |
| `GET/PUT /api/admin/ai/default` | `ai.manage`; provider/model/version do padrão global |

Não é possível autorizar nova origem por estas APIs, consultar uma chave, forçar modelo não habilitado ou escolher usuário em preferências pessoais. O gateway verifica permissões em cada requisição a partir do estado atual dos grupos.

## Validação e operação

Testes HTTP em `scripts/smoke_local.py`; testes de adapters/migração em `tests/Providers`; navegador em `tests/Providers/ui.cjs`, executado por `python scripts/test_ai_ui.py`. O runner cria rede/containers com SQLite temporário em tmpfs e credenciais `@example.test`; exige instalação descartável e remove somente recursos que criou. Nenhuma chamada vai a serviço pago. Capturas sintéticas em `docs/design/screenshots/ai-settings/`. Comandos e resultados efetivos no diário de implementação.

Antes de habilitar um provedor real, configurar seu ambiente e modelos válidos e fazer teste de metadados. Inferência externa real, créditos/disponibilidade, custos de infraestrutura e gestão criptografada de novas chaves pela interface não foram validados/implementados. A credencial literal antiga removida do adapter Ollama requer revogação/rotação pelo responsável no emissor; remoção do código não limpa o histórico Git nem revoga o segredo.
