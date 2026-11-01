# Segurança — requisitos de bloqueio

## Autenticação e autorização

- Exigir JWT validado para histórico, conversas, preferências e métricas privadas.
- Obter ID do usuário das claims validadas, não de corpo/query/rota como fonte de autoridade.
- Validar propriedade da conversa **em cada endpoint e em cada query de persistência**.
- Exigir política administrativa no servidor para usuários, papéis, permissões, provedores e métricas globais.
- Revisar serialização de entidades de usuário: nunca retornar `PasswordHash` ou dados internos.
- Proteger endpoints existentes antes de habilitar botões de administração.

## Segredos

- Manter `.env` fora do Git; `.env.example` só com placeholders.
- API keys somente no backend; nunca em localStorage, JavaScript entregue ao browser, logs ou JSON de configuração.
- Não armazenar segredos em texto puro no SQLite. Inicialmente usar ambiente; eventual gestão via UI requer criptografia autenticada e chave mestra externa.
- Rotacionar credenciais suspeitas de exposição e documentar procedimento sem registrar valores.

## Proteções de integração

- Validar endpoint do Ollama e URLs configuráveis; mitigar SSRF.
- Limitar tamanho de entrada, uploads e contexto; aplicar timeouts e rate limiting apropriados.
- Tratar respostas de LLM, OCR e documentos como conteúdo não confiável; sanitizar renderização.
- Revisar CORS, Swagger e HTTPS para cada ambiente; não manter política permissiva em produção.
- SQL gerado por IA não deve executar operações destrutivas; exigir enforcement no servidor e credenciais read-only quando aplicável.

## Testes de segurança (bloqueadores)

1. Requisição anônima não acessa histórico nem administração.
2. Usuário A não acessa conversas, mensagens ou consumo de B.
3. Usuário comum não configura provedores nem gerencia outros usuários.
4. Respostas da API não incluem hashes de senha nem chaves.
5. Nenhuma chave aparece no bundle do frontend ou logs de testes.
6. Migração não torna dados legados visíveis para todos.

Não declarar a aplicação pronta para produção sem revisão e execução desses testes.

## Auditoria de 01/11/2026 — lacunas específicas

- `OllamaService.cs`, adapter inativo do chat, contém uma credencial literal. Não copiar/testar/divulgar o valor; exigir revogação/rotação pelo responsável e remover o uso antes de habilitar essa integração. Remover o literal de um novo commit não revoga o segredo nem elimina seu histórico.
- `OpenAiService.cs` grava o payload completo da requisição em arquivo; o adapter Ollama também loga resposta bruta. Não habilitar esses caminhos antes de remover esses registros e sanitizar erros públicos.
- Autorização de sessão deve acontecer antes de SSE/OCR/LLM; o repositório deve revalidar dono em cada escrita, não apenas na abertura da sessão.
- Métricas globais de consumo exigem permissão administrativa específica; não reutilizar automaticamente `dashboard.view`, que atualmente libera métricas globais de avaliações.
- Plano e evidências completos: [AS_IS_AUDIT.md](../architecture/AS_IS_AUDIT.md). Este registro não representa correção já aplicada.

## Atualização da etapa 1

As verificações de propriedade nas operações de sessão/mensagem e a rejeição antes de OCR/LLM/SSE foram implementadas e testadas, inclusive chamadas diretas por outra conta e sessões inexistentes/excluídas. Erros do fluxo de chat são sanitizados. Backups e migração v1 foram testados em fixtures sintéticas. Isso atualiza esses itens do diagnóstico inicial; não encerra os requisitos de segurança do produto inteiro.

Credencial literal e logs dos adapters alternativos inativos continuam pendentes antes de sua habilitação. Não houve configuração de novas chaves, SSRF configurável, gestão de preços ou tela administrativa de IA nesta etapa.

## Atualização da etapa 3

Consultas de consumo pessoal filtram o usuário JWT; filtro de conversa revalida posse/exclusão. Consumo administrativo agregado exige `usage.admin`, inclusive com JWT válido de um usuário que só possui `chat.access`/`dashboard.view`. Não há endpoint administrativo de prompts, mensagens ou chamadas detalhadas de terceiros. Testes HTTP A/B, endpoints administrativos, parâmetros e paginação executados no smoke. Novos registros não incluem prompts, respostas, bodies de erro ou segredos; erros brutos também foram removidos do caminho não streaming do adapter DeepSeek ativo. Preços são configuração local sem credenciais, com arquivo vazio por padrão. Esses ajustes não habilitam nem resolvem automaticamente os bloqueios dos adapters alternativos.

## Atualização das etapas 4/5

Ollama/OpenAI foram corrigidos e integrados: removidos credencial literal/endpoint antigo e logs de payload/resposta dos adapters. O valor antigo não foi reutilizado nem testado. Revogação/rotação no emissor continua pendente de confirmação pelo responsável; remover código não revoga uma chave nem limpa o histórico Git. Esta observação substitui o estado histórico de “literal ainda presente” acima.

Configurações via `ai.manage` armazenam apenas metadados; chaves continuam no ambiente, com status booleano na interface. Não foi criado mecanismo inseguro para gravar chaves em texto puro. A allowlist de origens só pode ser alterada no servidor; redirecionamentos HTTP ficam desabilitados e userinfo/query/fragmento são rejeitados. O destino validado e sua classificação de cobrança são congelados juntos por chamada, impedindo que mudança administrativa concorrente redirecione uma inferência aceita ou classifique um endpoint remoto como local por uma corrida.

Rotas de seleção/preferência revalidam dono, disponibilidade e versão. Administração de provedores usa `ai.manage`, sem depender de menus ou do acesso ao dashboard. Testes HTTP e navegador verificam negação para usuário comum, ausência de segredos nas respostas/estrutura SQLite, preferências próprias, modelo/histórico e consumo alheio. Teste de metadados não chama inferência. Isso não equivale a auditoria completa de produção, nem valida modelos/credenciais/fatura reais ou elimina logs legados fora dos adapters alterados.
