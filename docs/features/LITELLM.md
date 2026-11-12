# LiteLLM no IA-Portal

Entrega opt-in. Os adapters DeepSeek, OpenAI e Ollama continuam independentes.
LiteLLM usa as mesmas interfaces de geração, SSE, títulos e métricas. Não há
troca automática de adapter direto para proxy, nem o inverso. Uma conversa que
selecionou LiteLLM falha explicitamente se o proxy estiver indisponível; escolha
um adapter direto autorizado para continuar. O Compose normal não inicia nem
aguarda o proxy.

## Subida e configuração

1. Configure no `.env` uma `LITELLM_MASTER_KEY` aleatória com prefixo `sk-` e pelo
   menos 32 caracteres adicionais. O gateway recebe a mesma chave como
   `LITELLM_API_KEY`; nenhuma credencial aparece na API administrativa.
2. Para Groq, configure `GROQ_API_KEY` e os `GROQ_*_MODEL`. Confirme que a conta
   está restrita à franquia gratuita antes de definir
   `LITELLM_GROQ_FREE_ACCOUNT=true`. Ter uma chave Groq não prova gratuidade.
   Para OpenRouter, configure `OPENROUTER_API_KEY` e um modelo `:free` ou
   `openrouter/free`. Cotas, disponibilidade e termos continuam externos.
3. Revise `infra/litellm/routes.json`. Os nomes `fast`, `balanced` e `reasoning`
   representam capacidades pretendidas. Sua qualidade depende dos deployments
   escolhidos: não há avaliação automática que certifique essa capacidade.
4. Suba apenas o proxy, sem reiniciar o restante da instalação:

```powershell
docker compose -f docker-compose.yml -f docker-compose.litellm.yml --profile litellm up -d litellm
# Se usa Atlas, inclua também -f docker-compose.atlas.yml antes do override LiteLLM.
```

5. Para disponibilizar o novo adapter no gateway em uma implantação atualizada,
   defina `LITELLM_ENABLED=true` e reconstrua/recrie o gateway com esses overrides.
   Configurações administrativas já salvas no SQLite têm precedência sobre
   defaults de ambiente. Em **Configurações → Inteligência Artificial**, habilite
   LiteLLM, endpoint `http://litellm:4000/v1/`, modelos `fast, balanced, reasoning`
   e um padrão autorizado. O teste administrativo consulta `/v1/models`, sem
   gerar texto. Escolha LiteLLM na conversa ou preferência pessoal/global.

Sem credenciais/modelos autorizados, o proxy recusa iniciar. Isso não afeta
adapters diretos. O healthcheck verifica o processo HTTP; não chama provedores
nem comprova saldo, instalação local ou inferência. Não há porta pública do
proxy nem UI administrativa exposta. Proteja acesso à rede Docker/master key.

## Alterar destinos sem mudar C#

`routes.json` é a allowlist do servidor. `destinations` define provider LiteLLM,
nome da variável do modelo, nome da variável da chave e classificação de cobrança.
`routes` define, por capacidade, uma lista ordenada de destinos autorizados,
política de privacidade e limite de saída. A primeira opção configurada é a
primária; as seguintes são fallbacks. Sem chave/modelo, um destino não é carregado.
As rotas não precisam apontar para os mesmos modelos em instalações diferentes.
O destino `groq-fast` configura `parameters.reasoning_effort=low` para reduzir
raciocínio auxiliar e permitir títulos curtos com o GPT-OSS. Esse ajuste pertence
ao deployment: remova-o ou adapte-o se substituir por um modelo sem suporte.
O compilador impede que esse bloco sobrescreva credenciais ou limites de tokens.

- Substituir modelo: altere a variável referenciada por `model_env` e reinicie
  apenas o proxy. Não é necessário alterar o catálogo C# nem conversas existentes.
- Remover: retire o destino da rota; para remover um provider, retire todas as
  referências e sua credencial do ambiente do proxy.
- Adicionar: acrescente uma definição e referência na rota. O prefixo de
  `provider` deve ser suportado pela versão fixada do LiteLLM. Declare chave em
  `docker-compose.litellm.yml`/ambiente, nunca no JSON. Adicione somente modelos
  disponíveis à conta e reveja compatibilidade de streaming/tools/contexto.
- Local: instale o modelo no Ollama do host, defina `LITELLM_OLLAMA_MODEL` e
  referencie `local` na rota. Para dados locais, use `privacy: local-only` e
  remova os destinos externos. O compilador rejeita fallback externo nessa rota.
- Pago: DeepSeek está definido, mas não participa das rotas padrão. Para incluí-lo,
  é necessário alterar a allowlist, autorizar a capacidade em
  `LITELLM_PAID_ROUTES` e definir `LITELLM_PAID_DAILY_BUDGET_USD` positivo.
  Essas três ações constituem autorização administrativa explícita. O limite
  do router é operacional e depende do estado do processo; não substitui um
  teto financeiro no provedor. Não habilite cobrança automática com orçamento
  financeiro rígido sem limite no provedor ou serviço de orçamento persistente.

O compilador nunca transforma automaticamente um modelo OpenRouter gratuito em
pago. Groq exige declaração da conta gratuita; não altera billing do provedor.
`openrouter/free` pode selecionar diferentes modelos gratuitos internamente;
para privacidade com allowlist de upstream estrita, use um modelo `:free` fixo
e restrinja os providers também na conta OpenRouter. Todos os destinos de uma
rota externa precisam estar aprovados para os dados envolvidos. O proxy não
substitui grants, isolamento por organização ou `AllowExternalLlm` no C#.

## Fallback, cancelamento e erros

O router configura uma repetição por grupo, no máximo dois fallbacks,
timeout/stream timeout de 25 segundos, cooldown de 30 segundos após falhas
repetidas. SDK retries internos estão desligados para não multiplicar tentativas.
As rotas geradas têm apenas os fallbacks explicitamente declarados. Limites 429,
timeouts e falhas temporárias são exercitados com upstreams sintéticos no teste
Docker. C# não faz retries adicionais nem reinicia streams interrompidos.
Cancelamento do navegador propaga `CancellationToken`; uma operação já aceita
pelo provedor pode continuar consumindo recursos mesmo após desconexão.

## Consumo entregue e execução do gateway

Existem dois registros com finalidades diferentes; **não some seus tokens**:

| Registro | Significado | Persistência |
| --- | --- | --- |
| `LlmUsage` | Uma chamada entregue/tentada pelo portal, tokens do resultado final; título separado | SQLite existente |
| `LlmDelivered` / `LlmTitle` | Correlação, organização, deployment, retries/fallbacks quando reportados | Logs estruturados .NET |
| `gateway_attempt` | Execuções de upstream, inclusive falhas/retries que podem consumir tokens | JSONL em volume `litellm_audit` |

`LlmUsage.Provider=litellm` identifica a integração selecionada. O modelo efetivo
é preservado pelo body ou pelo deployment ID; o provedor upstream está nos logs
correlacionados. A sessão mantém a capacidade pedida, inclusive para gerar títulos.
O gateway usa IDs de deployment `provider|model|alias`, lidos do header
`x-litellm-model-id`; mantenha esse formato ao criar configurações alternativas.
Se o upstream faz roteamento próprio, o ID identifica o serviço configurado e o
modelo retornado identifica o resultado quando o provedor o informa.

O middleware JWT valida identidade/membership e popula `OrganizationScope`.
Somente esse escopo fornece `organization_id`; o adapter acrescenta `request_id`
do servidor e `portal_call_id` novo por chamada. Não aceita tenant informado pelo
prompt ou corpo como autoridade. Chat e título compartilham correlação HTTP,
mas têm calls/consumo separados.

O ledger contém eventos started/completed/failed e fallback_completed/fallback_failed,
modelo, provider, tokens,
latência, tipo/status de erro e custo estimado quando informado pelo LiteLLM.
Eventos started não se somam aos terminais; um started sem terminal é resultado
desconhecido. Não atribua zero a falhas sem usage: podem ter custo no upstream.
Preços LiteLLM são estimativas de tabela e podem divergir da franquia/fatura real.
Custos desconhecidos ficam ausentes. O portal só estima a resposta com sua tabela
versionada existente; não importa custo total do proxy para `LlmUsage`.
Callbacks assíncronos não são um ledger financeiro transacional: perda do processo,
disco cheio e interrupção abrupta podem deixar eventos incompletos. Preserve o
volume, monitore disco, configure retenção e reconcilie com a fatura externa.
Nenhum prompt, resposta, chave ou argumento de ferramenta é gravado pelo callback.

## Testes e rollback

```powershell
dotnet run --project tests/Providers
dotnet run --project tests/LiteLlm
dotnet run --project tests/Usage
python -m unittest discover -s tests -p test_litellm_policy.py -v
python tests/litellm_proxy_smoke.py
```

O smoke sobe proxy real da imagem fixada e upstreams falsos, sem chaves reais,
inferência paga ou volumes do portal. Testa protocolo Groq, fallback, SSE,
tool calls diretamente no proxy e ledger. Não existe tool calling no contrato
atual `IOllamaService`; nada foi adicionado a esse contrato. Não há executor de
ferramentas, agentes ou LangGraph nesta entrega.

Rollback: selecione explicitamente um adapter direto habilitado para padrões e
conversas, desabilite LiteLLM na administração e pare apenas seu serviço. Não
apague SQLite/volume de auditoria. Não há migração de banco nesta integração.

Referências: [configuração LiteLLM](https://docs.litellm.ai/docs/proxy/configs),
[fallbacks](https://docs.litellm.ai/docs/proxy/reliability),
[headers](https://docs.litellm.ai/docs/proxy/response_headers),
[callbacks](https://docs.litellm.ai/docs/observability/custom_callback),
[Groq](https://console.groq.com/docs/models),
[OpenRouter gratuito](https://openrouter.ai/collections/free-models/).
