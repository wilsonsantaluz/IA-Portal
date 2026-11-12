# Atlas local: entrega incremental

## Padrão de IA da demonstração com Groq

A configuração local pode usar **LiteLLM → fast → Groq `openai/gpt-oss-20b`**
como padrão Atlas. Configure `GROQ_API_KEY`, `LITELLM_MASTER_KEY`,
`LITELLM_ENABLED=true`, `LITELLM_GROQ_FREE_ACCOUNT=true`,
`ATLAS_AI_DEFAULT_PROVIDER=litellm` e `ATLAS_AI_DEFAULT_MODEL=fast`.
Inclua `docker-compose.litellm.yml` em `COMPOSE_FILE` e `litellm` em
`COMPOSE_PROFILES`. Consulte o [guia LiteLLM](../features/LITELLM.md).

O seed cria esse padrão somente quando Atlas ainda não tem um padrão salvo.
Em instalações existentes, altere o padrão na organização Atlas pela interface/API
autorizada. Preferências pessoais têm precedência; conversas existentes mantêm
seu modelo até uma troca explícita em Configurações da conversa. Outras
organizações e adapters diretos não têm seus padrões alterados por esse seed.
As chaves ficam apenas no ambiente do servidor; nunca são enviadas ao navegador.
O uso na franquia Groq continua sujeito à elegibilidade e cotas da conta.

Todos os documentos e registros Atlas são fictícios. Não conectar dados reais.

## Instalação local ativada em 07/11/2026

Após a entrega inicial, o proprietário autorizou apagar os dados locais e
dispensou backup. Os volumes `ia-portal_portal_data` e `ia-portal_qdrant_data`
foram removidos, e a instalação foi recriada com o schema v6 e dados Atlas.
Não existe cópia dos dados anteriores criada por essa operação.

Estado validado: um administrador recriado pelas credenciais do `.env`, dez
documentos Atlas, um conector habilitado e sete tabelas Trino com 500 clientes,
5.896 faturas e 5.561 eventos de pagamento/estorno. A organização padrão continua
como estrutura técnica de administração global, sem base de conhecimento ou
conectores de negócio; o login e requests sem header escolhem Atlas por padrão.
As memberships e as permissões continuam sendo validadas normalmente.

O override Atlas agora usa `Demo:Atlas:OnlyKnowledge=true` para não semear o
documento genérico, `Demo:Atlas:PreferOrganization=true` para preferir Atlas no
login, e RAG textual local (`Chat:UseAdvancedRag=false`). Esses flags não apagam
dados por conta própria e não alteram o comportamento genérico quando ausentes.
As migrations comuns continuam com seus mecanismos de backup; a limpeza
autorizada criou uma instalação vazia, sem precisar de backup de upgrade.

Neste ambiente Windows, o `.env` local passou a selecionar os dois arquivos
Compose por `COMPOSE_FILE` e `COMPOSE_PATH_SEPARATOR`. Assim, `docker compose up
-d` mantém o modo Atlas. Valores de credenciais foram preservados e não foram
incluídos em commits ou relatórios. Atualize a página e entre novamente com o
administrador configurado no `.env`; as sessões de acesso antigas são inválidas.

Abra `http://127.0.0.1:9080`. Para novas perguntas analíticas, selecione **Atlas -
dados financeiros** e a referência **2026-11-01** no chat. Uma conversa de
validação real foi mantida: Trino + DeepSeek (`deepseek-flash` efetivo), política
citada, valores conferidos e gráfico renderizado. As duas chamadas de IA
(resposta e título) registraram 3.610 tokens. Não são métricas antigas ou simuladas.

O Trino Memory continua descartável. O serviço `atlas-loader` recarrega e valida
automaticamente o dataset ao subir e após reinícios do Trino. A carga manual
continua disponível para diagnóstico. A captura local da validação está em
`artifacts/atlas-local/chat-live.png`.

## Base de conhecimento (Fase A)

No `.env` do Compose, configure `ATLAS_SEED_KNOWLEDGE=true` e inicie o gateway
com `docker compose up --build`. Fora do Docker, defina
`$env:Demo__Atlas__SeedKnowledge='true'` antes de `dotnet run --project ai-gateway`.
O padrão é desativado. O snapshot acompanha o assembly, inclusive em publish/Docker.

Entre com uma conta autorizada e abra `/kb`. Os dez IDs começam com `atlas-`;
`Section` é a categoria e `Subsection` identifica `synthetic-demo / v1`. O aviso
no corpo acompanha a recuperação textual. A consulta exige `kb.view`, a manutenção
`kb.manage`; o seed não concede permissões e não altera `organization_base`.

Reiniciar não duplica nem atualiza documentos existentes, mesmo editados ou
inativos. Desligar o seed não remove registros. Para retirar a demonstração,
desative os dez documentos na tela existente e mantenha o seed desligado.
A migration v5 adiciona organizações sem recriar as tabelas existentes. A transação evita seed parcial.
O snapshot v1 é imutável; versões futuras exigem política explícita, sem atualizar
conteúdo humano automaticamente. Não renomeie IDs se precisar preservar a
identidade do documento no seed.

O RAG local encontra documentos ativos usando o mecanismo textual existente.
Com RAG avançado, configure ONNX/Qdrant e execute `POST /api/kb/reindex-all` com
JWT autorizado. Confira `falhas=0`; o seed não promete índices vetoriais prontos.
Sem modelos, o modo local continua disponível. Selecione Atlas no controle
Organização ativa no topo. O servidor valida a membership e o RBAC em cada
request, inclusive SSE, e filtra a KB e as conversas por organização.

## Dataset offline (Fase B)

```powershell
python scripts/atlas_dataset.py --output artifacts/atlas-v1
python -m unittest discover -s tests -p test_atlas_dataset.py -v
```

O destino deve ser novo: o gerador recusa sobrescrever arquivos. Seed padrão 42,
referência fixa 2026-11-01, competências 2025-11 até 2026-10. Para reproduzir,
gere em outro diretório e compare `manifest.json` e SHA-256 de cada CSV.
Não usa rede, pacotes extras ou o SQLite do portal. Arquivos ficam em `artifacts/`,
ignorado pelo Git. Não exibir estes valores como indicadores reais de empresas.

| CSV | Chaves e significado |
| --- | --- |
| customers | id; nome explicitamente fictício e região, sem documentos ou contatos |
| plans | id; nome, mensalidade em centavos |
| contracts | id; customer_id, plan_id; início, cancelamento opcional, estado |
| invoices | id; customer_id, contract_id; competência, emissão, vencimento, bruto/desconto/líquido em centavos, estado |
| payments | id; invoice_id; data, valor em centavos, confirmed ou reversed |
| disputes | id; invoice_id; abertura, motivo sintético, estado |
| collection_actions | id; invoice_id; data, ação proposta, estado; não executa cobrança |

São 500 clientes; cancelamentos encerram emissões futuras. Há sazonalidade
em dezembro, concentração de receita, atrasos e pagamentos parciais. Estornos
são eventos positivos com status `reversed`, subtraídos dos pagamentos confirmados;
não somar ambos como caixa. Negociações são propostas sem mudança do saldo.

Fórmulas: líquido = bruto - desconto; pagamento efetivo = confirmed - reversed;
saldo = máximo(líquido - pagamento efetivo, 0), excluindo faturas canceladas;
vencido = saldo com vencimento anterior à referência; taxa = vencido / saldo,
indefinida se denominador zero. Caixa usa data de pagamento, faturamento usa
competência. Converta centavos para DECIMAL/100, nunca use ponto flutuante para
conciliação. A data de referência não é o dia atual.

Os CSV UTF-8 com cabeçalho, IDs inteiros e datas ISO são a camada de intercâmbio
para outros catálogos. O carregador local descrito abaixo usa Trino Memory.
Defina montantes como BIGINT, datas como DATE, demais campos como VARCHAR e
trate cancelled_on vazio como NULL. CSV não é um catálogo Trino. O carregador
reproduz o mesmo gerador em tabelas tipadas, separadas do SQLite do portal.
O manifest contém contagens e hashes; os testes verificam todas as FKs,
conciliação, datas, cinco faixas de atraso e reprodutibilidade byte a byte.

Consultas de conferência: `trino/examples/atlas-verification.sql`. Resultado
esperado de aging calculado pelo gerador seed 42:

| Faixa | Faturas com saldo | Saldo em centavos BRL |
| --- | ---: | ---: |
| 01–15 | 281 | 15605108 |
| 16–30 | 32 | 1140108 |
| 31–60 | 214 | 11006177 |
| 61–90 | 188 | 9354212 |
| >90 | 1356 | 60303330 |

## Módulos integrados

[Data Connectors](DATA_CONNECTORS.md) configura fontes aprovadas no servidor,
concede grupos e seleciona tabelas. O [chat Atlas](ATLAS_CHAT.md) combina documentos
e planos analíticos fechados, com tabela, proveniência e gráficos numéricos.
Mermaid tem saída isolada e fallback textual. As rotas antigas de SQL livre
estão bloqueadas; consultas novas passam pela política do conector.

## Migração v5: organizações e acesso

A inicialização faz backup consistente pre-chat-v5 antes de atualizar um banco
v4, usa transação e checksum. As tabelas existentes recebem OrganizationId, com
FK e proteção contra NULL; não são apagadas ou recriadas. Usuários atuais ganham
membership na organização padrão (id 1); KB, grupos, sessões e avaliações atuais
continuam nela. Mensagens e consumo herdam o escopo da sessão. IDs, senhas,
conteúdo e propriedade de conversas são preservados.

Atlas é outra organização, criada apenas com o seed opt-in. Administradores
existentes da organização padrão recebem membership/grupo admin de Atlas no
primeiro provisionamento. Usuários comuns não recebem acesso automático. Novas
contas criadas na tela de usuários pertencem à organização ativa; grupos e suas
permissões são locais. Memberships desativadas e grupos removidos não são
restaurados pelo seed. Contas compartilhadas permitem alterar grupos locais,
mas não senha, e-mail, nome ou estado global por um administrador de tenant.
Este incremento não tem UI de criação de organizações ou associação de contas
existentes a outros tenants.

O login escolhe a primeira membership ativa; o seletor envia X-Organization-Id,
validado no banco, e recarrega a interface para limpar o estado anterior. Header
ausente seleciona a primeira membership ativa; inválido/não autorizado é rejeitado.
Configurações globais de IA ficam restritas à organização padrão. Preferências
pessoais de modelo continuam sendo da conta e não expõem dados de outra org.

Registros Atlas v1 anteriores à v5 permanecem na organização padrão, como todos
os dados legados, sem mover conteúdo humano. Atlas recebe seed independente.
Desative cópias legadas na KB padrão se não quiser usá-las ali. Reindexe a KB
vetorial explicitamente: pontos antigos sem organizationId ficam fora da busca;
os IDs ativos também são revalidados no SQLite antes do reranking.

Rollback operacional: pare o gateway e restaure o backup completo pre-v5 para
um caminho novo, com a versão anterior da aplicação. Não execute downgrade de
colunas no banco em uso; mudanças posteriores ao backup exigem reconciliação.
Testes cobrem restauração e novo upgrade, além de rollback de DDL em falha.

## Executar a demonstração completa com Docker Compose

Use o `.env` já configurado conforme o README (JWT, administrador e provedor de
IA). O override abaixo é **opt-in**: ativa o seed Atlas e acrescenta um Trino de
demonstração, com conta de leitura e memória separada. O volume SQLite existente
é preservado e recebe as migrations com backup automático.

```powershell
docker compose -f docker-compose.yml -f docker-compose.atlas.yml config --quiet
docker compose -f docker-compose.yml -f docker-compose.atlas.yml up --build -d --wait --wait-timeout 180
docker compose -f docker-compose.yml -f docker-compose.atlas.yml ps
# A API aguarda atlas-loader saudável (carga e conferência automáticas).
docker compose -f docker-compose.yml -f docker-compose.atlas.yml logs --tail 20 atlas-loader
# Conferência manual opcional:
python scripts/atlas_trino_load.py --verify
```

Acesse `http://127.0.0.1:9080`, escolha **Atlas Serviços Digitais (demonstração)**
no topo e abra **Data Connectors**. Cadastre `atlas_local`, selecione ao menos
`invoices` e `payments`, conceda o grupo `admin` (ou grupo com `data.query`),
habilite e teste. No chat, selecione a fonte e `2026-11-01`; abra as perguntas
sugeridas e confira os resultados e as fontes. Não há cadastro automático de
conexão nem concessão automática de consulta a todos os usuários.

O perfil interno fixa o IP `172.29.88.10` da rede dedicada `172.29.88.0/24`.
Se houver conflito de subnet no host, ajuste a rede e a allowlist em conjunto.
O Trino só publica `127.0.0.1:18081`. A conta `atlas_loader` é usada apenas pelo
carregador local; o gateway sempre usa `atlas_reader`. Esses nomes sem senha são
exclusivos de uma demonstração isolada em loopback; não são autenticação para
fontes empresariais. Enterprise exige TLS e credencial real read-only no servidor.

Trino Memory perde os dados quando seu container reinicia. O serviço
`atlas-loader` verifica a base a cada 30 segundos e recupera o dataset sintético
automaticamente; durante a recuperação, consultas podem ficar indisponíveis.
Ele executa como usuário sem privilégios, filesystem somente leitura e acesso
apenas à rede da demonstração. A API aguarda seu healthcheck na subida via
Compose. Reinícios isolados do Trino não param a API já em execução.
Uma carga parcial ou divergente deixa o loader sem prontidão e exige diagnóstico;
ele nunca apaga tabelas para tentar corrigir a situação.
Isso restaura o dataset fixo de demonstração, não persiste alterações manuais
no Trino. Dados editáveis exigem outro catálogo com armazenamento durável.
A carga é determinística e idempotente: se encontrar o
mesmo manifest/contagens, não escreve. Se encontrar um schema diferente ou
parcial, recusa sobrescrevê-lo. Nunca executa DROP/TRUNCATE nem altera o SQLite.
O gerador e o carregador não dependem de dados externos; o runtime Trino exige
a imagem Docker especificada. Nenhum serviço proprietário é necessário.

## Gateway nativo com Trino isolado

Para validar sem iniciar os serviços da instalação existente:

```powershell
docker compose -f trino/atlas/compose.yml -p ia-atlas-validation up --build -d --wait --wait-timeout 180
python scripts/atlas_trino_load.py --verify
$env:Demo__Atlas__SeedKnowledge='true'
$env:TRINO_CONNECTORS_FILE='config/trino-sources.example.json'
# Defina Jwt__Key, Bootstrap e provedor conforme README; .NET não lê .env sozinho.
dotnet run --project ai-gateway --urls http://localhost:9089
```

Inicie o frontend com `yarn dev` em `front`. Não execute os dois modos na mesma
porta `18081`. Para parar apenas o Trino isolado de validação, use
`docker compose -f trino/atlas/compose.yml -p ia-atlas-validation down`.
Isso não toca volumes da aplicação; a memória sintética será recarregada no
próximo teste. Sem `down -v` ou reset do banco do portal.

## Testes adicionais

```powershell
dotnet run --project tests/Connectors -- --real-trino
python scripts/smoke_local.py --atlas
python scripts/smoke_local.py --atlas --real-trino
```

O primeiro e o terceiro comandos usam exclusivamente o Trino isolado em
`127.0.0.1:18081`, já carregado. O smoke cria gateway/SQLite temporários e usa
doubles de LLM/OCR, sem inferência paga. O comando sem `--real-trino` também
simula Trino para injetar falhas e respostas vazias. Resultados efetivos,
Docker smoke e testes visuais estão no diário de implementação.
