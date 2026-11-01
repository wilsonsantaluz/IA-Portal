# Telemetria de uso, tokens e custos

**Estado após etapa 3:** `LlmUsage` registra as chamadas dos fluxos persistentes de chat SSE, chat sem streaming, análise de documentos e títulos, com tokens reportados opcionais, cache, duração, modelo e status. Há consultas autenticadas, agrupamentos e estimativa de custo quando preços explícitos estiverem configurados. Não há ainda gráficos ou tela de consumo (etapa 5). Provedores alternativos continuam para a etapa 4.

O parsing do adapter usa os campos `prompt_tokens`, `completion_tokens`, `total_tokens`, `prompt_cache_hit_tokens` (ou `prompt_tokens_details.cached_tokens`), conforme [a referência oficial DeepSeek](https://api-docs.deepseek.com/api/create-chat-completion/). Não recalcula nem soma cache ao total retornado. O stream solicita `include_usage`, aceita métricas junto ao conteúdo ou em chunk de choices vazio e exige `[DONE]` para reconhecer conclusão. Um EOF antecipado não transforma resposta parcial em sucesso.

## Cobertura implementada

- `purpose=chat`: `/api/ia/ask/stream` e `/api/chatBot/ask`.
- `purpose=document`: chamada ao LLM de `/api/ocr/analisar`, após a extração de texto. Não mede tokens/custo do serviço de OCR.
- `purpose=title`: chamada auxiliar já existente, agora também elegível a estimativa com preço conhecido.
- Uma linha por chamada, vinculada ao usuário JWT, sessão e mensagem. Chat/documento usam a pergunta persistida como referência, para incluir falhas que não possuem resposta. Títulos usam a primeira resposta. O ID da chamada é independente do ID da sessão/mensagem.
- Cada chamada reserva o registro antes de acessar o provedor e finaliza os metadados após a geração. Falha de reserva impede o consumo externo. Replay concluído não chama nem contabiliza novamente. Se o processo morrer ou a persistência final falhar, `started` significa resultado desconhecido; não é sucesso, erro comprovado nem consumo zero. Não há reconciliação automática com a cobrança do provedor.
- Contadores ausentes permanecem NULL, com `source=unavailable`; nenhum estimador heurístico foi introduzido. `reported` pode conter apenas parte dos campos. Status: `started`, `completed`, `failed`, `timeout`, `cancelled`, `invalid_response`. Erros/cancelamentos podem conservar tokens efetivamente recebidos.
- `Model` de uso captura o modelo retornado pelo provedor quando disponível. As mensagens ainda conservam o identificador solicitado pelo caminho atual; seleção e unificação completa de modelos serão tratadas na etapa 4.
- Rotas experimentais SQL/Trino e serviços legados fora do fluxo persistente não estão instrumentados neste incremento. Não declarar esses totais como a fatura integral de todos os recursos/credenciais da conta externa. OpenAI/Ollama não foram ativados. Dados anteriores não recebem tokens ou preços inventados retroativamente.

## Consultas disponíveis

| Rota GET | Acesso | Conteúdo |
| --- | --- | --- |
| `/api/usage/summary` | `chat.access` | Resumo do próprio usuário; filtro opcional por sua conversa |
| `/api/usage/groups` | `chat.access` | Agrupa os próprios dados por `day`, `month`, `model`, `provider` ou `purpose` |
| `/api/usage/calls` | `chat.access` | Chamadas próprias paginadas, sem prompts, respostas, dados de conta ou segredos |
| `/api/usage/admin/summary` | `usage.admin` | Resumo global ou de um usuário por ID |
| `/api/usage/admin/groups` | `usage.admin` | Agrupamentos administrativos, incluindo `by=user` |

Filtros comuns: `from` inclusivo, `to` exclusivo (ISO-8601 com offset, convertido a UTC), `provider`, `model`, `purpose`. Sem datas usa início do mês UTC até agora. Limite de 366 dias. Endpoints pessoais aceitam `sessionId`, sempre validado contra dono JWT e exclusão. Até administradores recebem 404 ao tentar filtrar conversa de outra pessoa. Endpoints administrativos não expõem detalhes/chamadas/conteúdo de sessões alheias; aceitam `userId` apenas para agregados. `dashboard.view` e `access.manage` não substituem `usage.admin`. O catálogo é inicializado de forma idempotente: administradores existentes recebem a nova permissão; grupos comuns precisam de concessão explícita.

Listas e agrupamentos usam `page` (1–10.000), `limit` (1–100, padrão 30) e retornam `items`/`nextPage`. Chamadas ordenadas por horário descendente + ID; grupos por chave. Fixar o mesmo `from/to` ao paginar evita deslocamento causado por novas chamadas. Campos de chave não utilizados são strings vazias/zero; `day=0` no agrupamento mensal. Agregação e paginação são executadas no SQLite, sem carregar todos os registros em memória.

Resumo e cada grupo incluem `requests`, `errors`, `unknown`, `reportedRequests`, `totalTokenRequests`, `pricedRequests`, somas opcionais de tokens/cache, `estimatedCostUsd` e `currency=USD`. `tokensComplete` se refere à disponibilidade do total de tokens para todas as tentativas; não garante que cada contador detalhado exista. `costComplete` indica que todas têm estimativa. Somas parciais não representam consumo/custo completo; quando nenhum valor é conhecido, soma é NULL. Sem registros, requisições são zero e tokens/custo permanecem NULL. `errors` inclui falhas, timeout, cancelamento e resposta inválida; `unknown` separa tentativas `started`.

Exclusão lógica da conversa não apaga o consumo do período. A consulta detalhada por `sessionId` excluído responde 404; resumo e chamadas próprias continuam preservando a trilha contábil. Não existe purga física/exportação nesta etapa.

## Configuração real de preços

`LLM_PRICES_FILE` aponta para JSON local do gateway, padrão `config/model-prices.json`, versionado no repositório e inicialmente `[]`. Não há preços iniciais presumidos. Em Docker, o caminho é dentro do container: incluir o arquivo na imagem ou montá-lo em volume somente leitura e configurar o caminho. Não colocar chaves nessa configuração. O catálogo é validado/carregado no startup; mudanças exigem reinício.

Cada entrada requer `version` (identificador único), `provider`, `model` (identificador efetivo retornado), `effectiveFrom` (ISO-8601), `inputUsdPerMillion` e `outputUsdPerMillion`. `cachedInputUsdPerMillion` é opcional e, quando ausente, usa o preço normal de entrada. Valores precisam ser explícitos e não negativos. Campos obrigatórios ausentes, JSON inválido, versões duplicadas ou duas entradas com mesma vigência/provider/model impedem startup, em vez de virarem custo zero silencioso. Usar valores confirmados pelo responsável, com origem e data revisadas; não inserir números demonstrativos como se fossem preços de produção.

Aplica-se a entrada mais recente cuja vigência seja anterior ao início da chamada. Cálculo: `(entrada − cache) × preço de entrada + cache × preço de cache + saída × preço de saída`, com unidade por milhão. Se cache tiver preço diferente, seu contador também é obrigatório. Sem preço exato para o modelo, tokens suficientes ou cache válido, custo fica NULL. Internamente, o valor é arredondado por chamada a nanodólares (10⁻⁹ USD) e armazenado como inteiro para somas exatas no SQLite, com `PriceVersion`; a API oferece USD decimal. Não é fatura real. Mudanças no arquivo não recalculam registros históricos.

Migração `003_ChatUsage.sql`: acrescenta custo/versão opcionais e índices de unicidade por mensagem/finalidade, sessão/data, finalidade/data e data/ID. Mantém os scripts anteriores intactos; backup consistente v3 antes de evoluir banco v2. Sem apagar mensagens, usage, usuários ou inferir donos antigos. Testes em `tests/Usage`; testes HTTP de isolamento, totalização e paginação em `scripts/smoke_local.py`.

## Dimensões obrigatórias

- Usuário autenticado, conversa, mensagem/chamada, provedor, modelo, finalidade (`chat`, `title`, etc.), timestamp UTC e status.
- Tokens de entrada, saída, cache e total quando reportados; latência; custo estimado em USD somente com preço conhecido.
- Agregações por dia, mês, usuário, conversa, modelo e provedor; administrador vê agregados autorizados; usuário vê apenas os próprios.

## Qualidade dos dados

| Situação | Registrar |
| --- | --- |
| API retorna `usage` | `source=reported`, valores efetivos |
| Provedor não retorna `usage`, estimador confiável disponível | `source=estimated`, metodologia documentada |
| Sem uso nem estimador | `source=unavailable`, campos de tokens nulos (não zero) |
| Falha/timeout | status e latência; tokens apenas se comprovadamente disponíveis |

- Não somar tokens de cache duas vezes: respeitar semântica do provedor.
- Não confundir token de entrada com token total; documentar diferenças por API.
- Evitar dupla contabilização por retries; registrar identificador único da chamada.
- Chamadas de título também consomem tokens e devem ser contabilizadas.

## Custos

- Configurar preços versionados por provedor/modelo e data, unidade explícita (ex.: USD por milhão de tokens).
- Custo = componentes de tokens × preço aplicável; se não houver preço, mostrar `não configurado`, nunca `$0` fictício.
- Ollama: sem cobrança de API externa no modo local; custo de infraestrutura não incluído.
- Guardar custo como decimal e moeda; distinguir estimativa de cobrança real.

## Segurança e privacidade

- Não registrar chaves, tokens de acesso, prompts completos ou conteúdo sensível na telemetria.
- Restringir consultas agregadas por papel/permissão no servidor.
- Criar política de retenção e eventual anonimização antes de disponibilizar exportações.

## Critérios de aceite

- Mock com usage conhecido produz totais corretos por usuário/conversa/provedor.
- Usuário B não consegue consultar consumo de A.
- Dados ausentes não são exibidos como zero medido.
- Requisição auxiliar de título aparece em categoria separada.
