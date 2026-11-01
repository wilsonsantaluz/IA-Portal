# Provedores de IA — DeepSeek, OpenAI e Ollama

## Estado implementado — etapa 4

Os três adapters existentes agora implementam `IAiProviderAdapter`, reutilizando `IOllamaService`, `ILlmMetrics` e `IChatTitleGenerator`. DeepSeek/OpenAI compartilham apenas o protocolo Chat Completions em `ChatCompletionAdapter`; Ollama mantém `/api/generate`/NDJSON. `AiProviderRouter` escolhe explicitamente o adapter por conversa, sem fallback ou retry pago automático. Não foi criada outra pilha de mensagens/repositórios. Conteúdo/contexto continuam preparados pelos serviços existentes.

`GET /api/ai/providers` retorna somente modelos habilitados/configurados e capacidades básicas, sem endpoint ou chave. `GET/PUT /api/me/ai-preference` persiste a escolha do próprio JWT. Novas conversas usam preferência pessoal, depois padrão administrativo e depois configuração do ambiente. Preferência existente que ficou indisponível falha explicitamente: não muda de provedor silenciosamente. `PATCH /api/chatBot/sessions/{id}/model` exige dono, versão atual, modelo permitido e ausência de resposta ativa; preserva mensagens. O seletor no cabeçalho do chat pede confirmação. Cada resposta exibe provedor/modelo efetivo, preservando os identificadores das anteriores.

Modelo escolhido permanece em `ChatSession`; pergunta guarda a escolha usada na interação e resposta/telemetria guardam o modelo efetivo retornado. Título usa o mesmo provedor e modelo solicitado na primeira interação bem-sucedida, não um alias efetivo potencialmente indisponível. Não envia todo o histórico para titular. OpenAI usa `max_completion_tokens` para título; DeepSeek usa `max_tokens`; Ollama usa `options.num_predict`. Cache/tokens ausentes continuam nulos. Ollama soma `prompt_eval_count` e `eval_count` quando ambos existem e não expõe `thinking` como resposta.

Migração v4 cria `AiProviderSettings`, `AiUserPreferences`, `AiSystemDefault` e acrescenta `LlmUsage.ApiBilling`. Configurações persistidas não contêm chaves e inicialmente ficam vazias, preservando variáveis de ambiente como fallback. Padrões existentes DeepSeek/OpenAI são mantidos; nenhum modelo Ollama instalado é presumido. OpenAI e Ollama começam desabilitados no ambiente padrão, e habilitar um modelo não garante que a conta/servidor o disponibilize.

`*_API_KEY` fica somente no servidor. A credencial literal do adapter Ollama foi removida sem ser reutilizada. O responsável ainda precisa revogar/rotacionar a credencial antiga no emissor: remover do código não revoga nem elimina o histórico Git. Foram removidos logs de prompts/payloads/respostas e erros com corpo bruto dos adapters ativos. Não houve uso do endpoint/credencial antigos nem chamada paga para testar a mudança.

Endpoints editáveis precisam pertencer a `DEEPSEEK_ALLOWED_ORIGINS`, `OPENAI_ALLOWED_ORIGINS` ou `OLLAMA_ALLOWED_ORIGINS`, definidos pelo responsável no servidor. Sem allowlist explícita, os remotos usam a origem de seu `BASE_URL`, e Ollama aceita localhost/host.docker.internal/ollama na porta 11434. URLs com usuário/senha, query e fragmento são rejeitadas; redirecionamentos HTTP do cliente de IA ficam desabilitados. Autorizar destinos extras é uma ação de configuração no servidor, não um poder concedido à tela. Não autorizar proxies abertos ou destinos de metadados internos. Alterar uma origem autorizada no ambiente pode invalidar configuração salva, exigindo revisão.

`OLLAMA_LOCAL_ORIGINS` declara quais origens são de execução local: somente essas recebem `ApiBilling=local` e custo de API externa zero, com infraestrutura explicitamente fora do cálculo. Ollama em outra origem é `unknown`, sem custo zero presumido; preço explícito pode ser configurado. Resumos incluem `localApiRequests`; chamadas detalhadas incluem `apiBilling`. Os preços externos permanecem vazios por padrão.

Variáveis em `.env.example`/Compose: `AI_DEFAULT_PROVIDER`, `DEEPSEEK_*`, `OPENAI_*`, `OLLAMA_*` (chave, habilitação, endpoint, modelo padrão, lista de modelos e origens). O Compose permite iniciar sem chave DeepSeek para usar outro provedor, mas não fabrica disponibilidade: sem configuração válida, o chat pede uma escolha/configuração habilitada. Modelos são permitidos explicitamente, sem descoberta remota automática. Recursos avançados (visão, ferramentas, structured output) não são anunciados como universais.

Referências de implementação: [OpenAI Chat Completions](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create), [Ollama generate](https://docs.ollama.com/api/generate). Tests em `tests/Providers` e smoke HTTP local usam handlers/doubles e modelos sintéticos, não verificam a disponibilidade de contas/modelos reais. A interface administrativa e indicadores serão entregues no incremento seguinte, já autorizado na mesma branch pelo proprietário.

## Princípios

**Etapa 5 concluída:** administração, preferência pessoal e indicadores estão disponíveis em `/settings/ai` e `/usage`, protegidos pelas permissões documentadas em [AI_SETTINGS.md](AI_SETTINGS.md). O destino e a cobrança são mantidos no mesmo snapshot da chamada; alterações administrativas entram na próxima seleção, sem redirecionar uma requisição aceita.

- Auditar implementações já presentes; **não duplicar adapters**.
- Expor interface comum com provider, model, request, response, usage, duration, error e capabilities.
- Separar configuração global de preferências do usuário e escolha por conversa.
- Não presumir que todos os provedores suportam streaming, ferramentas, visão, JSON estruturado ou contagem de tokens idêntica.
- Troca de modelo em conversa existente deve ser explícita, registrada por mensagem e não descartar histórico.

## Configuração inicial segura

| Provedor | Fonte de credencial | Observações |
| --- | --- | --- |
| DeepSeek | variável de ambiente existente `DEEPSEEK_API_KEY` | Preservar compatibilidade |
| OpenAI | variável de ambiente, nome confirmado na implementação | Não expor ao browser |
| Ollama | endpoint local configurável | Sem chave obrigatória em instalação local típica; validar host/rede Docker |

A tela administrativa deve mostrar **configurado/não configurado**, endpoint permitido, modelos habilitados, teste de conectividade e seleção de padrão. Nunca retornar valor de segredo. Armazenamento de novas chaves via UI é **fase posterior**, condicionado a criptografia autenticada e gestão da chave mestra fora do SQLite.

## Contrato de adapter (conceitual)

- `ListModels` / modelos permitidos (configuração explícita ou descoberta validada).
- `Generate` com mensagens, modelo, parâmetros suportados, cancellation token.
- Resultado: conteúdo, provider, model efetivo, status, usage reportado/ausente, duração.
- Erros tipados: configuração, autenticação, rate limit, timeout, rede, resposta inválida.

## Regras de runtime

- Timeout, cancelamento, retry limitado somente quando seguro; evitar duplicar cobranças.
- Não fazer fallback silencioso de DeepSeek para OpenAI/Ollama.
- Para Ollama no Docker, `localhost` dentro do container não é o host Windows; verificar conectividade e endpoint apropriado ao ambiente.
- Validar endpoints configuráveis para mitigar SSRF; permitir destinos autorizados e proibir acesso arbitrário a metadados internos.
- Testar cada adapter com mocks; integração real opcional, com custo controlado.

## Estado após a etapa 1

A injeção do chat continua selecionando DeepSeek; OpenAI/Ollama não foram habilitados nem duplicados. A única correção do adapter DeepSeek neste incremento foi tornar a leitura do stream cancelável sem sondagem síncrona e descartar corretamente request/response HTTP. Contagem de tokens, identificação do modelo resolvido pelo provedor, normalização de erros entre adapters e seleção/configuração continuam para as próximas etapas.

## Etapa 2 — capacidade auxiliar de título

O mesmo adapter DeepSeek implementa `IChatTitleGenerator`, com chamada JSON sem streaming limitada a 64 tokens. Respeita o modelo registrado na primeira resposta e captura, nessa chamada auxiliar, modelo retornado e tokens reais quando disponíveis. Não muda a seleção nem o contrato textual do chat existente. O consumo completo de chat e a normalização entre provedores continuam pendentes; não habilita OpenAI/Ollama. Detalhes de timeout/fallback e limites em [CHAT_SESSIONS.md](CHAT_SESSIONS.md).
