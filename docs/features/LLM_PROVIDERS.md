# Provedores de IA — DeepSeek, OpenAI e Ollama

## Princípios

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
