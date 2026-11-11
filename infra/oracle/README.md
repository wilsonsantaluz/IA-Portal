# IA-Portal na OCI — provisionamento Always Free

**Atualização em 08/11/2026:** após autorização posterior, a VM Micro criada pelo
proprietário recebeu a implantação mínima. Operação, acesso, medições e limites:
[MICRO_DEPLOYMENT.md](MICRO_DEPLOYMENT.md). O histórico abaixo descreve a etapa
anterior de provisionamento A1 e não substitui esse relatório.

Esta etapa prepara **uma VM**, sem instalar ou publicar o IA-Portal. O padrão do
script é `DryRun`: apenas consultas OCI e arquivos locais. Não altera rede,
conta, `.env`, bancos, VCNs ou recursos existentes. Não usa Terraform, scheduler,
load balancer, NAT Gateway, disco adicional, Marketplace ou shape alternativo.

## Diagnóstico realizado em 07/11/2026

OCI CLI 3.96.0 autenticada por API Signing Key; Home e única região assinada:
`sa-saopaulo-1`, AD `XpPw:SA-SAOPAULO-1-AD-1`. Inventário da tenancy, incluindo
consulta de subcompartments: nenhuma instância ativa, nenhum boot/block volume
e nenhuma quota customizada encontrados. O relatório mensal de uso retornou
`items: []`; dados de billing podem atrasar, portanto isso não garante custo zero.

| Item | Resultado observado |
| --- | --- |
| A1.Flex | `billing-type: LIMITED_FREE`, compatível com 2 OCPUs/12 GB |
| Limite técnico AD | 41 OCPUs/277 GB disponíveis, uso 0 |
| Limite técnico regional | 16 OCPUs/96 GB (Free Trial; **não** é franquia gratuita) |
| Armazenamento gratuito | 200 GB disponíveis, uso 0 |
| Ubuntu | `Canonical-Ubuntu-24.04-aarch64-2026.10.27-0`, plataforma oficial; compatibilidade A1 confirmada pela API |
| Boot padrão da imagem | 47.694 MiB, aproximadamente 46,6 GiB; não solicitar tamanho customizado |
| VCN 1 | Subnet pública, mas tabela de rotas vazia; não selecionada |
| VCN 2 | Subnet pública; rota `0.0.0.0/0` para IGW da mesma VCN, habilitado |
| Segurança VCN 2 | SSH 22 de `0.0.0.0/0`; HTTP/HTTPS ausentes; egress stateful permitido; sem NSGs existentes |
| SSH | Um candidato adequado em `Downloads/ssh-key-2026-11-07.key.pub`; RSA 2048 validada com `ssh-keygen -l`. Nenhuma chave privada lida/copiada |

O dry-run inicial terminou **Blocked**, com quatro motivos: SSH amplo, ausência de
SSH para o /32 do administrador, ausência de 80 e ausência de 443. Não houve
tentativa real de criação; disponibilidade física continua desconhecida. Uma
consulta de limites bem-sucedida não demonstra capacidade do host.

### Alteração de rede proposta — autorizada e aplicada posteriormente

Somente na security list da segunda subnet:

1. Substituir TCP 22 de `0.0.0.0/0` pelo IPv4 público atual do administrador `/32`.
2. Adicionar TCP 80 e 443 de `0.0.0.0/0`, stateful.
3. Preservar as regras ICMP limitadas e o egress atual.
4. Não abrir portas de PostgreSQL, Qdrant, Trino, gateway, ML/OCR ou Ollama.

Não é necessário alterar a rota, IGW ou atribuição de IP público da VCN 2.
Security Lists e NSGs são **aditivos**; um NSG restrito não corrige SSH amplo
na security list. A automação não associa NSGs à VM nova. Subnets com IPv6 ou
roteamento avançado são recusadas até uma revisão específica.

Foi preparado um payload local em `artifacts/oci-audit/network-ingress.proposed.json`
e um resumo com OCID/ETag em `network-change-review.json` no mesmo diretório.
Esses artefatos são ignorados pelo Git e não são parte necessária do script.
Não enviar o inventário bruto a repositórios públicos: pode conter identificadores
e tags pessoais. Antes de aplicar, reler a security list, conferir o IP atual,
revisar o JSON e usar `--if-match` com ETag atual, sem sobrescrever mudanças
concorrentes. O comando remoto a seguir é apenas um modelo; **não executá-lo sem
aprovação explícita da alteração concreta**:

```powershell
# Somente após autorização; os valores abaixo vêm do review recém-validado.
$review = Get-Content artifacts/oci-audit/network-change-review.json -Raw | ConvertFrom-Json
oci network security-list update `
  --security-list-id $review.securityListId `
  --ingress-security-rules file://artifacts/oci-audit/network-ingress.proposed.json `
  --if-match $review.ifMatch --region sa-saopaulo-1 --force
```

`--force` confirma a substituição da coleção ingress; não remove a security list
nem modifica egress. Uma alteração de rede autorizada não é, por si só,
autorização para criar VM. O código de provisionamento nunca faz essa alteração.

## Operação no Windows

### Atualização após autorização em 07/11/2026

O proprietário autorizou a alteração de ingress proposta e o início do
provisionamento. A security list da VCN 2 foi atualizada com ETag/If-Match:
SSH somente do /32 aprovado, 80/443 públicos, ICMP e egress preservados. Subnet,
rota e IGW foram relidos antes da mudança. O novo dry-run retornou `Ready`,
sem bloqueios. Não houve alteração da VCN 1, conta, `.env` ou aplicação.

O processo de provisionamento foi iniciado em PowerShell oculto no Windows,
com imagem fixada ao ID do dry-run aprovado. Isso não instala a aplicação e
não implica que a VM tenha sido criada: conferir resultado no journal/log.
Não foi criada tarefa agendada nem serviço; o computador deve permanecer ligado.

A primeira tentativa real retornou falta de capacidade explícita. O journal
registrou `Capacity`, o runner permaneceu ativo na espera de 30 minutos, e uma
nova leitura confirmou zero instâncias/boot/block volumes após essa tentativa.
Isso valida o ramo real de capacity/retry, não a criação bem-sucedida de VM.
O estado atual deve sempre ser consultado nos arquivos locais abaixo.

Estado do runner e acompanhamento (sem ler credenciais):

```powershell
$state = Join-Path $env:LOCALAPPDATA 'IA-Portal/oci-always-free'
$runner = Get-Content (Join-Path $state 'runner.json') -Raw | ConvertFrom-Json
Get-Process -Id $runner.ProcessId -ErrorAction SilentlyContinue
Get-Content (Join-Path $state 'operations.log') -Tail 15
Get-Content (Join-Path $state 'journal.json') -Raw | ConvertFrom-Json |
  Select-Object Outcome, At, InstanceId
```

Para interromper o runner oculto, primeiro validar PID e início para evitar
atingir um processo diferente caso o PID tenha sido reutilizado:

```powershell
$process = Get-Process -Id $runner.ProcessId -ErrorAction Stop
if ($process.ProcessName -ne 'powershell' -or
    $process.StartTime.ToUniversalTime() -ne [DateTime]::Parse($runner.StartedUtc).ToUniversalTime()) {
  throw 'Identidade do processo mudou; não interromper por este PID.'
}
Stop-Process -Id $process.Id
```

Interrupção durante POST pode deixar criação em andamento e `Pending` no journal;
sempre reconciliar antes da retomada. No uso em primeiro plano, Ctrl+C continua
disponível. Não iniciar outro runner enquanto este existir. Após encerrar, a
retomada é feita pelo comando `-Mode Provision -ProvisionApproved` abaixo,
preservando o mesmo diretório de estado e a imagem aprovada.

Requer Windows PowerShell 5.1+, OCI CLI no PATH, configuração
`%USERPROFILE%\.oci\config` e permissão de leitura de inventário/limites/usage
em toda a tenancy. O modo real também precisa de permissão de criação de VM e
uso de subnet/imagem/volumes. Um inventário que falhe por IAM bloqueia o processo.

```powershell
cd C:\source\IA-Portal
# Padrão: apenas validação, inclusive com a rede ainda bloqueada.
powershell -NoProfile -ExecutionPolicy Bypass -File infra/oracle/provision-always-free.ps1 -Mode DryRun

# Seleção explícita para ambientes com mais de uma chave pública:
.\infra\oracle\provision-always-free.ps1 -Mode DryRun `
  -SshPublicKeyPath "$env:USERPROFILE\Downloads\ssh-key-2026-11-07.key.pub" `
  -AdminCidr '<IPv4-publico-atual>/32'
```

Sem `-AdminCidr`, consulta HTTPS a `checkip.amazonaws.com` identifica o IP de
saída atual. VPN/NAT podem produzir outro IP; confira o /32 antes da alteração
da rede. Só `.pub` em formato OpenSSH é lida; não selecione a API Signing Key,
chaves ADB ou chaves internas do Ollama. A descoberta automática procura
`id_*.pub`/`ssh-key*.pub` em `.ssh` e `Downloads`; ambiguidade exige caminho explícito.

Parâmetros adicionais: `-ConfigFile`, `-Profile`, `-CompartmentId` (padrão:
tenancy), `-SubnetId` (padrão: segunda subnet informada), `-ImageId` e
`-StateDirectory`. Região, nome, shape, 2 OCPUs, 12 GB e intervalo de 1.800 s são
fixos no código. Não há fallback para shapes/regiões pagos. É possível fixar o
`ImageId` mostrado no dry-run; uma imagem que deixe de ser compatível bloqueia.

**Somente após autorização para a criação real e rede validada:**

```powershell
.\infra\oracle\provision-always-free.ps1 -Mode Provision -ProvisionApproved
```

O switch é uma trava operacional, não substitui a confirmação do proprietário.
Manter o computador ligado e o processo aberto; não foi criada tarefa agendada.
Ctrl+C interrompe o loop. Se for usado durante uma requisição de criação,
a OCI pode concluir a operação: conferir inventário antes de qualquer retomada.

### Estados, logs e proteção contra duplicação

Arquivos locais ficam por padrão em
`%LOCALAPPDATA%\IA-Portal\oci-always-free`:

- `dry-run.json`: plano resumido e bloqueios; nunca contém credenciais/chave.
- `operations.log`: horário UTC, estados e resultado; sem configuração, conteúdo
  de chave, corpo de erro da OCI ou tokens.
- `provision.lock`: lock exclusivo mantido pelo processo; impede duas execuções
  usando o mesmo diretório, liberado inclusive ao sair por erro.
- `journal.json`: gravado antes do POST. `Pending`, `Accepted`, `Fatal` ou
  `Ambiguous` impedem novo POST se a VM não for localizada. `Capacity` permite
  retomar apenas o mesmo alvo, após o intervalo restante.
- `launch-request.json`: payload temporário somente com chave **pública** e
  parâmetros da VM; removido no `finally`. Após encerramento forçado do Windows,
  pode restar localmente; nunca contém chave privada.

Antes de cada POST: reler toda a infraestrutura e limites. A busca considera
nome e tag `ia-portal-provision=ia-portal-prod`, inclui instâncias paradas e em
provisionamento e todos os compartments inventariados. Shape/configuração
divergente ou múltiplos resultados interrompem. VM já existente não é iniciada,
redimensionada nem recriada. Para Provision, aguarda RUNNING até 20 minutos e
confere subnet/IP da VNIC primária. Não executa SSH ou cloud-init de aplicação.

Somente `ServiceError` OCI `InternalError`/500 com mensagem explícita
`Out of [host] capacity` gera espera de 15 minutos, conforme alteração autorizada
em 09/11/2026. O parâmetro `-IntervalMinutes` permite configurar de 15 a 1440
minutos (padrão: 15). SDK/CLI retries desativados. Ao reiniciar, o journal mantém
o intervalo restante desde a última tentativa; não é apagado para antecipar POSTs.
Erros de autenticação, permissão, limite, quota, billing ou configuração não
geram outra tentativa. Timeout/resposta vazia ambígua provoca até três leituras
de reconciliação; se nenhuma confirmar VM, encerra com journal travado. Uma VM
encontrada na reconciliação é observada, sem reenviar o POST.

Não execute de outro computador ou com diretórios de estado distintos. O lock é
local; OCI não impõe unicidade de display name. Evite criações manuais paralelas
e mantenha o journal. Não existe garantia distribuída contra outro operador.

Saídas: `0` validado/existente/sucesso, `2` dry-run bloqueado, `1` erro. Se o
PowerShell externo reportar apenas falha genérica, use o JSON/operations.log.

### Recuperação

| Situação | Ação |
| --- | --- |
| Falta de capacidade explícita | Deixar o loop ativo ou retomar o mesmo comando/diretório; preserva intervalo |
| SSH amplo/HTTP ausente | Obter autorização, aplicar proposta revisada e repetir DryRun; nunca ignorar o bloqueio |
| IP do administrador mudou | Revalidar `/32` e solicitar autorização para ajuste da regra |
| Falha IAM, chave OCI, quota ou limite | Corrigir a causa; não trocar região/shape nem fazer upgrade |
| Pending/Ambiguous/Fatal sem VM visível | Consultar instâncias de toda a tenancy, audit/work requests e boot volumes órfãos; aguardar consistência. Não apagar journal automaticamente |
| VM localizada após timeout | Retomar para observá-la; não criar outra |
| VM parada/terminando | Interrompe; resolver o estado com autorização separada |
| Processo caiu após POST | Journal anterior impede duplicação; mesmo procedimento de reconciliação |
| Pedido realmente não criou recurso | Só após investigação, arquivar o journal fora do repo e autorizar nova tentativa |

## Custos e limites

Em 07/11/2026 a documentação oficial informa A1 gratuito até **1.500 OCPU-h e
9.000 GB-h/mês**, equivalentes a **2 OCPUs/12 GB**, na Home Region. Uma VM assim
por 31 dias consome até 1.488 OCPU-h/8.928 GB-h. Boot e block volumes compartilham
200 GB gratuitos. O script usa boot padrão com 10 VPU/GB, sem autoscaling de
desempenho ou volumes adicionais. Reserva conservadora de 50 GB na validação
não altera o tamanho solicitado à OCI.

Fontes: [Always Free Resources](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm),
[Free Tier](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier.htm),
[CLI launch](https://docs.oracle.com/en-us/iaas/tools/oci-cli/latest/oci_cli_docs/cmdref/compute/instance/launch.html).

Limite técnico de Free Trial não é franquia Always Free. O script recusa outra
VM A1 ativa/parada e uso histórico A1 positivo já reportado neste mês, exigindo
revisão; também soma volumes na tenancy e consulta disponibilidade efetiva.
Billing tem atraso e a verificação é uma fotografia, não um teto de cobrança
imposto na conta. Sem upgrade. Sem interpretação de preço de tabela como fatura,
e sem interpretar estimativa zero como garantia. Antes da execução autorizada,
revisar a elegibilidade no Console e evitar operações paralelas; repetir revisão
ao acabar o Trial. Instâncias ociosas podem ser retomadas pela Oracle.

Não há promessa de SLA/capacidade. Backups futuros, tráfego, DNS/domínio ou
outros recursos devem ter orçamento/eligibilidade próprios revisados. Não são
criados aqui. OpenAI/DeepSeek podem cobrar por inferência fora da OCI; hospedar a
VM gratuitamente não torna essas chamadas gratuitas. Nenhuma inferência foi feita
nesta tarefa.

## Análise do Compose para implantação futura

Não foi executado deploy nem alterado o Compose. `docker buildx imagetools inspect
--raw` confirmou `linux/arm64` nos manifests dos tags/digest utilizados em
07/11/2026. Isso **não** comprova execução da aplicação em ARM64.

| Componente real | Imagem/dependência | Avaliação |
| --- | --- | --- |
| Gateway .NET 8 | `mcr.microsoft.com/dotnet/aspnet:8.0`, `sdk:8.0` | Bases ARM64; EF SQLite local, volume persistente. ONNX Runtime 1.23.2 e tokenizer 2.21.4 possuem runtimes linux-arm64 no cache NuGet inspecionado, mas exigem teste dos modelos/nativos |
| Frontend React/Vite | `node:22-alpine` → `nginx:1.25-alpine` | Ambos ARM64; build deve ser para ARM64. Atualizar/revisar versões de imagens antes da publicação |
| OCR | Serviço **.NET**, Tesseract/Poppler via apt/CLI | Base ARM64. Pacote NuGet Tesseract 5.2.0 só traz binários x86/x64; fluxo atual usa executável CLI, sem TesseractEngine. Testar PDF/imagem e bibliotecas no Ubuntu ARM64 |
| ML | Serviço **.NET**, Microsoft.ML 4.0 e MKL | Microsoft.ML contém linux-arm64; `Microsoft.ML.Mkl.Redist` 4.0 só contém linux-x64/osx-x64/Windows. Uso nativo MKL não está pronto para ARM64; serviço atual retorna fórmulas fixas, não previsão treinada. Não apresentar essas fórmulas como BI real |
| Qdrant | `qdrant/qdrant:v1.20.0`, profile `rag` | Manifest ARM64; opcional e volume persistente. Atlas atual usa recuperação textual |
| Trino Atlas | Digest fixado `trinodb/trino:484@sha256:2704eb53935af5e582d63d6033e9bf99d20c325e9b0e30b1016ef59a4773979f` | Manifest ARM64; limite Compose 2 GiB, consulta 512 MB, dataset Memory 128 MB. Medir JVM/heap em ARM64; Memory perde dados no restart e exige recarga |
| PostgreSQL | Ausente | Não é dependência do Compose atual. Não provisionar banco nem disco adicional por suposição |
| Python/ReRAG | Ausente como container | Python aparece nos scripts locais de geração/carga/smoke; RAG e reranking estão no gateway .NET, não em um serviço Python separado |
| OpenAI/DeepSeek | HTTPS externo | Configuração/segredos só no servidor, restrições de origem existentes; custos externos separados |
| Ollama | Externo ao Compose | `host.docker.internal` não deve ser presumido no Linux. Sem GPU nesta VM; inferência/modelos locais disputariam CPU/RAM e não têm capacidade validada |

Não há limite CPU/memória explícito para gateway/ML/OCR/Qdrant/frontend no Compose
base; apenas Trino tem limite de 2 GiB no override Atlas. Para 12 GB, definir e
medir orçamento incluindo SO, Docker, picos de OCR, JVM e modelos antes de
dimensionar produção. Compilar frontend/.NET na própria VM compete com o runtime;
preferir imagens ARM64 construídas/testadas previamente, depois de autorização.
Não foi feita medição de desempenho ARM64 nem um build integral nessa arquitetura.

O Compose publica frontend `9080`, API `9089`, Qdrant `6333` e Trino `18081`
somente em loopback; ML/OCR não publicam porta do host. Manter isso. A rede OCI
permitir 80/443 não basta: futuramente um reverse proxy com TLS deve receber
essas portas e encaminhar ao frontend, que já encaminha `/api` ao gateway.
Revisar firewall do host e regras Docker em conjunto, sem publicar APIs internas.
Não instalar proxy/certificados nem criar DNS nesta etapa.

### Sequência de execução

1. Corrigir apenas a security list revisada e repetir o dry-run até `Ready`
   (concluído após autorização; ver atualização operacional acima).
2. Confirmar custos/eligibilidade e autorizar o loop real de provisionamento
   (autorizado e iniciado; sucesso depende da capacidade OCI e do journal).
3. Após RUNNING, revisar hardening/SSH/patches e armazenamento, sem copiar chaves
   privadas para a VM; usar autenticação pela chave pública já cadastrada.
4. Resolver dependências ARM64, testar build e smoke completo na arquitetura,
   definir orçamento de memória e validar persistência/backup/restauração.
5. Planejar domínio, TLS, proxy, firewall e segredos do servidor. Deploy público
   só em etapa separada, com autorização. Não presumir que a demo equivale a uma
   auditoria de segurança para produção.

## Testes da automação

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File infra/oracle/provision-always-free.tests.ps1
```

Testes offline substituem OCI e espera: política de portas, classificação de
erros, dry-run sem POST, trava de aprovação, falha de inventário, VM existente,
intervalo de 30 minutos, sucesso, erro fatal sem retry, reconciliação de timeout,
journal após restart e concorrência por lock. O teste real feito nesta tarefa
é DryRun contra OCI, bloqueado corretamente pela rede; launch real não testado.
