# Data Connectors - primeira versao

Acesso administrativo em /data-connectors requer connectors.manage no tenant
ativo. Consulta requer data.query E um grupo explicitamente concedido na conexao.
As categorias sao Demo Dataset, Enterprise Connector e Public Data Connector.
Nenhuma conexao nem grupo recebe acesso de dados automaticamente.

## Configuracao no servidor

TRINO_CONNECTORS_FILE aponta para um JSON com DataConnectors.Sources. Exemplo
sem segredos: ai-gateway/config/trino-sources.example.json (gateway nativo,
Trino local em 127.0.0.1:18081). Fora do Docker:

```powershell
$env:TRINO_CONNECTORS_FILE='config/trino-sources.example.json'
$env:Demo__Atlas__SeedKnowledge='true'
dotnet run --project ai-gateway
```

O caminho relativo parte de ai-gateway. A UI escolhe um perfil aprovado, incluindo
endpoint/catalogo/schema, em vez de permitir destinos arbitrarios. Cada perfil
pertence a uma unica OrganizationSlug. Catalogo/schema fisicos nao podem ser
compartilhados por perfis de tenants diferentes. Configure ACLs/conta read-only
na origem: ReadOnlyAccount declara o requisito, nao inspeciona privilegios reais.
Nao conectar fontes reais sem testar a negacao de escrita e de outros schemas.

AllowedAddresses fixa IPs aprovados do servidor. O socket resolve DNS, conecta
apenas a um IP aprovado e valida TLS normalmente; proxy e redirects desativados.
Link-local, metadados cloud conhecidos, multicast e enderecos indefinidos sao
bloqueados. IPs privados/loopback so funcionam se explicitamente aprovados no
servidor; HTTP sem senha apenas na categoria demo de atlas-demo. Empresa/publico
exigem HTTPS. Perfis do mesmo endpoint devem fixar os mesmos IPs.

PasswordEnvironment e uma referencia opcional TRINO_CONNECTOR_<NOME>_PASSWORD.
Valor secreto vem exclusivamente do ambiente do backend e exige HTTPS. User e
identidade da conta read-only. Nao ha senha/chave/ref de segredo no SQLite nem
nos DTOs. O arquivo de perfis fica no servidor, carregado no startup; alteracoes
exigem restart. A conexao conserva endpoint/catalogo/schema: se o perfil mudar,
precisa ser revisada/salva antes de consultar, sem redirecionamento silencioso.
Publico exige SourceUrl HTTPS e License revisados pelo operador; nenhuma fonte
publica foi verificada ou cadastrada automaticamente nesta entrega.

## Cadastro e uso

Escolha organizacao, perfil e nome. Selecione tabelas da allowlist e grupos
locais, habilite e salve. Teste conexao (SELECT 1) e descubra tabelas/colunas
somente do catalogo/schema e tabelas aprovados. Estado/horario do teste e historico
de disponibilidade, nao garantia da proxima consulta. Edicao usa versao CAS;
desabilitar revoga uso de IA na proxima autorizacao. Credenciais nunca entram
na interface. A lista de fontes do usuario omite conexoes sem grant.

SQL v6 cria DataConnectors/ConnectorGrants com FK e trigger anti-grant entre
organizacoes. Backup/checksum/transacao seguem o runner existente. Rollback
operacional restaura backup completo com a versao anterior, em caminho novo.

## SQL permitido e limites

Nao existe campo de SQL livre. O compilador fechado ApprovedTrinoQuery aceita
somente probe, metadados e tres planos: aging, cashflow, top_balances. Os planos
financeiros exigem invoices/payments selecionadas, montantes em centavos BRL e
colunas do dicionario Atlas. Outras tabelas podem ser descobertas/selecionadas,
mas nao ganham interpretacao financeira generica automaticamente. SQL arbitrario,
CTEs arbitrarias, funcoes externas e texto gerado por LLM nunca sao executados.
Identificadores sao validados e totalmente qualificados; parametros de plano e
data sao tipados. WITH existe apenas nos templates conhecidos. As rotas SQL
legadas falham fechadas no cliente Trino (409), preservando checks de sessao.

Aging e top representam a data de referencia. Cashflow compara seis meses desde
o primeiro dia seis meses antes ate a referencia exclusiva (se a referencia nao
for o dia 1, inclui o mes parcial). Valores de estorno sao subtraidos. Nao calcular
indicadores com outras semanticas/colunas sem um novo plano revisado e testado.

Limites: 2 consultas simultaneas por tenant, 8 globais, 20s no cliente/15s no
servidor, 100 paginas, 2MiB por pagina/8MiB total. Maximo 1000 linhas para
metadados; 6/7/10 nos planos financeiros. nextUri deve manter origem e prefixo
/v1/statement/, sem redirects. Cancelamento envia DELETE ao ultimo nextUri
validado; query_max_run_time limita mesmo se o cancelamento falhar. Logs usam
traceId, tenant, plano, estado e contagem, sem SQL/payload/credenciais.

Referencias oficiais consultadas em 07/11/2026:
- https://trino.io/docs/current/develop/client-protocol.html
- https://trino.io/docs/current/admin/properties-query-management.html

## Validacoes e limites desta entrega

Testes unitarios: tests/Connectors; smoke HTTP: scripts/smoke_local.py --atlas.
Incluem migracao v5->v6 com backup/restauracao/rollback, planos/identificadores
adversariais, nextUri malicioso, limites de linhas/bytes, cancelamento e politicas
de perfis. Smoke usa Trino HTTP double com socket real e IP fixado: cadastro,
grants/tabelas/perfis, descoberta/teste, consulta, CAS, desabilitacao e isolamento.
Isso nao representa ainda uma consulta validada contra um servidor Trino real.
