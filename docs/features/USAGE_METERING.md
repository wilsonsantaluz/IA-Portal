# Telemetria de uso, tokens e custos

**Estado após etapa 2:** `LlmUsage` já registra exclusivamente tentativas automáticas de título (`purpose=title`), separadas de sessões/mensagens, com tokens reportados opcionais, cache, duração, modelo e status. Não há ainda contabilização completa de chat, preços, custo estimado ou endpoints/gráficos de agregação. `started` é uma tentativa reservada cujo resultado pode ser desconhecido após interrupção; não tratar como sucesso ou consumo zero. Ver [comportamento implementado](CHAT_SESSIONS.md).

O parsing do adapter usa os campos `prompt_tokens`, `completion_tokens`, `total_tokens`, `prompt_cache_hit_tokens` (ou `prompt_tokens_details.cached_tokens`), conforme [a referência oficial DeepSeek](https://api-docs.deepseek.com/api/create-chat-completion/). Não recalcula nem soma cache ao total retornado. Nenhuma tabela de preços foi preenchida neste incremento.

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
