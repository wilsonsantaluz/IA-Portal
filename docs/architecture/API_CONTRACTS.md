# Contratos de API — conversas, consumo e provedores

Data: 01/11/2026. **Etapas 1–3 implementadas; contratos das etapas 4–5 continuam propostos.** Os endpoints `/api/ia/ask/stream`, `/api/chatBot/ask`, `/api/chatBot/history/{sessionId}` e `/api/ocr/analisar` foram evoluídos, sem uma segunda pilha de chat. `/api/chatkb/rate` permanece existente.

**Atualização da etapa 1 aprovada:** rotas de sessões/metadados/renomeação/exclusão e mensagens da tabela abaixo estão implementadas. A criação aceita body vazio e gera o título provisório; título inicial customizado não é aceito (usar PATCH). Envios sem `requestId` continuam aceitos por compatibilidade e recebem ID do servidor, mas só um ID explícito permite replay idempotente. Todas as sessões devem ser criadas antes do envio. O cursor `before` de mensagens é o ID da âncora, validado na própria sessão; a ordenação usa horário + ID. Títulos automáticos e consumo auxiliar estão implementados na etapa 2; a etapa 3 acrescenta consultas de consumo dos fluxos persistentes; seleção/configuração continuam pendentes. O índice da documentação e o diário registram os testes executados.

## Segurança comum

Todas as operações de conversa exigem JWT válido, conta ativa e `chat.access`. Dono vem exclusivamente da claim validada. ID de usuário não é aceito como autoridade em body/query/cursor. Consulta à sessão inclui proprietário e ausência de exclusão na própria query. Sessão inexistente, excluída ou de outra conta responde o mesmo HTTP 404 genérico; não revelar título, contagem ou existência. Administração do sistema não implica poder abrir conversas alheias.

Erros públicos possuem código estável e mensagem pt-BR; não conter payload bruto do provedor, endpoint interno, API key ou stack trace. Correlation/request ID permite diagnóstico no servidor sem armazenar prompts em logs.

## Etapa 1 — sessões e persistência

| Operação | Rota proposta | Entrada e resposta |
| --- | --- | --- |
| Listar próprias sessões | `GET /api/chatBot/sessions?limit=30&cursor=...&search=...` | `{items:[SessionDto],nextCursor}`; máximo 100; busca por título limitada. Ordenação `(updatedAt DESC,id DESC)`. |
| Nova conversa | `POST /api/chatBot/sessions` | Body vazio ou título opcional; servidor gera UUID/dono. `201`, `Location` e SessionDto. Inicialmente usa somente provider/model efetivamente configurados no backend. |
| Metadados | `GET /api/chatBot/sessions/{id}` | SessionDto sem mensagens completas, segredos ou IDs de outro usuário. |
| Mensagens anteriores | `GET /api/chatBot/sessions/{id}/messages?limit=50&before=...` | `{items:[MessageDto],nextCursor}`; até 100; página mais recente inicialmente, itens em ordem cronológica e cursor para os anteriores. |
| Renomear | `PATCH /api/chatBot/sessions/{id}` | `{title,version}`; 1–120 caracteres úteis, marca origem manual. `200` SessionDto; versão antiga retorna `409`. |
| Excluir | `DELETE /api/chatBot/sessions/{id}` | Exclusão lógica; `204`. Retira também das APIs de mensagens/envio/edição; não declara apagamento físico. Sem rota de restauração neste incremento. |
| Enviar interação | `POST /api/ia/ask/stream` (existente) | Acrescentar `{requestId}`; manter `{pergunta,sessionId,documentContext?}`. Validar propriedade antes de iniciar SSE/rede. Sem `userId` de autoridade. |
| Analisar documento | `POST /api/ocr/analisar` (existente) | Multipart atual + request ID; verificar sessão antes de OCR/LLM. Persistir pergunta/análise no mesmo ciclo de interação, respeitando limite atual de documento. |

`SessionDto`: `{id,title,titleSource,createdAt,updatedAt,provider,model,version}`. Provider/model podem ser nulos para dados antigos sem evidência. `MessageDto`: `{id,sessionId,requestId,role,content,createdAt,status,provider,model}`. Papel é definido pelo servidor, não escolhido pelo cliente para injetar mensagens de sistema.

Cursores opacos contêm a posição composta e contexto de busca; serão limitados/validados. Eles não conferem autorização, que continua em cada query. Usar limite de tempo/posição da listagem para reduzir deslocamentos por novas atividades; não prometer snapshot transacional permanente entre páginas.

### Compatibilidade e envio

- O frontend passa a criar UUIDs de conversa pelo servidor e salva apenas a última seleção por usuário, não toda a fonte do histórico. Sem seleção válida, lista suas conversas ou oferece Nova conversa.
- `GET /api/chatBot/history/{id}` permanece como adaptador limitado durante transição, com a mesma regra de proprietário e sem novas sessões implícitas. A UI nova usa mensagens com IDs/cursores.
- `POST /api/chatBot/ask` continua como caminho sem streaming, delegando a mesma verificação de propriedade/persistência. Não manter autorização diferente nesse caminho.
- Clientes que inventam um GUID sem criar sessão deverão ser migrados para `POST /sessions`; a alteração de comportamento será explícita, junto com testes e atualização do frontend. O envio não adota sessões desconhecidas/alheias.
- O `requestId` identifica uma interação. Uma repetição concluída reutiliza IDs/texto persistidos; em andamento ou com payload diferente responde `409`. Não repetir a cobrança como retry automático. Texto/pergunta têm limites definidos no DTO antes de rede.
- O servidor registra pergunta/estado antes da rede. SSE preserva os eventos atuais, acrescentando identificadores persistidos e estado final. `done` só indica sucesso se acompanhado de estado concluído persistido; eventos de erro/cancelamento não viram resposta bem-sucedida.
- Operações concorrentes em uma sessão são controladas por reserva/lease curta no banco, sem transação longa durante geração. Falha/reinício libera geração interrompida conforme estado persistido.
- Desconexão interrompe a geração quando possível e deixa estado recuperável. Não há promessa de capturar usage final não entregue pelo provedor.

## Etapa 2 — títulos

Não exige endpoint novo de escrita pública além do PATCH manual. Automação após primeira interação bem-sucedida usa modelo da conversa, contexto mínimo e limite curto. O frontend recarrega os metadados após `done`; nenhum evento SSE novo é exigido. Falha preserva resposta e produz título truncado `fallback`; edição manual concorrente vence por update condicional. Toda chamada auxiliar recebe request ID e finalidade `title` no registro de uso.

## Etapa 3 — consumo implementado

| Contrato implementado | Autorização e semântica |
| --- | --- |
| `GET /api/usage/summary` | `chat.access`; resumo próprio, filtro opcional `sessionId` validado contra o dono. |
| `GET /api/usage/groups?by=day` | `chat.access`; agrupamentos próprios `day`, `month`, `model`, `provider`, `purpose`. |
| `GET /api/usage/calls` | `chat.access`; chamadas próprias paginadas, sem conteúdo/prompts/segredos. |
| `GET /api/usage/admin/summary` | `usage.admin`; resumo agregado global ou filtro `userId`. |
| `GET /api/usage/admin/groups?by=user` | `usage.admin`; agrupamentos administrativos, sem mensagens nem acesso às conversas alheias. |

Filtros `from` inclusivo/`to` exclusivo (normalizados UTC), provider/model/purpose; padrão início do mês até agora; intervalo máximo 366 dias. Listas usam `page`/`limit` e `nextPage`. Endpoints pessoais por sessão inexistente/alheia/excluída retornam 404. `userId` é rejeitado no resumo pessoal; nunca substitui a identidade JWT. `tokensComplete`/`costComplete` distinguem somas parciais; ausência de consumo medido/preço retorna NULL. `started` entra em `unknown`, separado de erros. Formatos e exemplos de configuração em [USAGE_METERING.md](../features/USAGE_METERING.md).

As antigas rotas ilustrativas `/api/usage/me`, `/api/chatBot/sessions/{id}/usage` e `/api/admin/usage` não foram criadas: usar os contratos acima. O consumo de conversa usa o filtro `sessionId` nas APIs pessoais. Gráficos/UI continuam pendentes.

## Etapas 4–5 — configuração proposta

| Contrato proposto | Autorização e semântica |
| --- | --- |
| `GET /api/ai/providers` | Usuário autorizado: somente provedores/modelos habilitados e capacidades; sem segredos. |
| `PATCH /api/chatBot/sessions/{id}/model` | Proprietário; troca explícita com versão, validada na lista habilitada, bloqueada durante geração; preserva mensagens e registra modelo em cada resposta. |
| `GET/PUT /api/me/ai-preference` | Preferência do próprio usuário; não permite habilitar modelo proibido. |
| `GET/PUT /api/admin/ai/providers/{provider}` | Permissão administrativa específica; enable/endpoint validado/modelos/padrão; sem campos de valor de API key nesta primeira versão. |
| `POST /api/admin/ai/providers/{provider}/test` | Admin; teste limitado/timeout/custo declarado e registrado; nunca retorna raw ou segredo. |

A permissão `usage.admin` foi adicionada na etapa 3; permissões de configuração continuam futuras. Não dar poderes administrativos por acesso ao dashboard. A chave permanece no ambiente; a resposta futura mostra apenas `configured: true/false`. Endpoints de runtime não aceitam URL arbitrária ou chave enviada pelo cliente. Toda configuração de endpoint precisa de política de destinos permitidos contra SSRF e exfiltração da credencial.

Uso retornado deve incluir números opcionais, fonte (`reported`, `estimated`, `unavailable`), unidade, finalidade, provedor/modelo, duração, status e custo estimado somente com preço configurado. Custo parcial e chamadas sem preço devem ter contagens próprias; não apresentar soma parcial como custo total conhecido. Para Ollama local, informar ausência de cobrança externa separada do custo de infraestrutura desconhecido.

## Testes correspondentes

- Identidades A/B: mesmas operações por GUID/cursor adulterado, inclusive OCR e fluxo sem streaming; zero chamadas ao mock externo quando não autorizado.
- Persistência após reiniciar com mesmo banco; nova instância do browser consegue recuperar lista sem localStorage anterior.
- Paginação com horários repetidos, limit inválido, busca, cursor inválido e exclusão durante navegação.
- Reenvio de request ID não duplica mensagens/chamadas; falha e cancelamento preservam estado; lease vencida não bloqueia para sempre.
- Migração/baseline/backup/rollback com dados sintéticos existentes, conforme DATA_MODEL.
- Mais adiante: título manual concorrente, fallback, usage real de cada fixture, preços/ausência de preço, SSRF e ausência de chaves em JSON/bundle/log.
