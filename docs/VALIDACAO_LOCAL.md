# Validação de build e execução local — 30/10/2026

Branch: `fix/local-sqlite-builds`. Nenhum merge ou push realizado.

## Escopo e estado inicial

O gateway usa ASP.NET Core e EF Core SQLite; OCR e ML são serviços .NET 8 separados. O frontend usa React 18, TypeScript e Vite 7. O ML retorna projeções simuladas, sem modelo treinado.

Havia 148 arquivos modificados antes deste trabalho. `git diff --ignore-space-at-eol --stat` inicialmente não mostrou diferenças de conteúdo: eram diferenças de fim de linha. Essas alterações foram preservadas. As únicas alterações de conteúdo desta etapa são este relatório, o README e os perfis de inicialização de OCR e ML.

Não foram acessados bancos existentes, documentos reais, credenciais locais, originais privados ou provedores externos de IA. Qdrant, Trino e inferência ONNX não foram iniciados. Os pacotes ONNX existentes foram apenas restaurados como dependências do build.

## Ambiente necessário

- .NET 8 SDK e runtime ASP.NET Core 8.
- Node.js 22.12+ e Yarn Classic; Vite instalado exige `^20.19.0 || >=22.12.0`.
- Linux/WSL para o OCR atual, que chama Bash, Tesseract CLI com idiomas `por` e `eng`, e Poppler (`pdftoppm`).
- Python 3 para `scripts/smoke_local.py`.
- Acesso aos registros NuGet e Yarn para restaurar dependências.
- Gateway: `Jwt__Key` com pelo menos 32 bytes; `Bootstrap__AdminEmail` e `Bootstrap__AdminPassword` (mínimo 12 caracteres) para banco novo. `Chat__UseAdvancedRag=false`. SQLite padrão: `data/portal.db`; não há migração de schemas anteriores.
- IA real exige chave DeepSeek; testes locais usaram exclusivamente um simulador HTTP e chave fictícia. O `.env` da raiz é carregado pelo Compose, não pelo processo .NET.

Ambiente usado: Ubuntu 24.04 em WSL, SDK .NET 8.0.427, Node 22.22.0, Yarn 1.22.22, Python 3.12, Tesseract 5.3.4 e Poppler 24.02.0. As ferramentas Linux foram preparadas em `/tmp`, sem alterar instalações Windows ou exigir sudo. Para reproduzir normalmente, instale os requisitos no PATH; os caminhos `/tmp/ia-*` são temporários desta sessão.

## Erros encontrados e correções

1. `dotnet` e `node` não estavam no PATH Linux; havia somente executáveis Windows e wrappers npm/Yarn. Preparados SDK e Node Linux temporários, além do Yarn Classic.
2. Tesseract e Poppler ausentes. Preparados binários e idiomas temporários com bibliotecas nativas. O índice apt local apontava para algumas versões já indisponíveis (HTTP 404); foram usados pacotes disponíveis do Ubuntu Noble, extraídos sem instalação global. Em instalação normal, atualizar o índice apt antes de instalar os requisitos.
3. Primeira tentativa de build React, antes de terminar a reinstalação das dependências, falhou com `Cannot find module @rollup/rollup-linux-x64-gnu`. A pasta `node_modules` existente não estava pronta para Linux; outra tentativa antes de concluir a instalação também apontou ausência do executável Linux do esbuild. Reinstalação com `yarn install --frozen-lockfile`; sem alterar versões ou o lockfile. Para evitar a lentidão da montagem Windows, também foi preparada uma cópia limpa de `package.json`, `yarn.lock`, `src`, `index.html` e `vite.config.js` em `/tmp/ia-front-clean`, instalada com o mesmo lockfile e cache local (`--offline`). Não compartilhe `node_modules` entre Windows e Linux/WSL. A instalação na montagem Windows foi interrompida devido à lentidão, depois de restaurar os binários nativos necessários; a instalação limpa Linux terminou com sucesso. Uma tentativa de renomear a pasta na montagem Windows retornou `Permission denied`; nenhum backup ou link simbólico foi mantido. O build final e o servidor de desenvolvimento também passaram no diretório original, com HTTP 200 para `/login` e `/src/main.tsx`. O Yarn também registrou tentativas por falha de rede durante download.
4. Os perfis de execução local apontavam OCR para 62977 e ML para 54225, enquanto o gateway espera 9082 e 9081. Corrigidos os dois `launchSettings.json` para HTTP local nessas portas, com abertura automática do navegador desabilitada.
5. README não explicava os requisitos nativos nem os comandos de execução dos dois serviços. Acrescentadas essas informações e substituída a observação de builds ainda não executados pelo link para este registro.

Não foram necessárias alterações de código C# para compilar. Nenhum componente visual, tema, marca, página ou estilo foi alterado.

## Builds e testes

| Verificação | Resultado |
| --- | --- |
| `dotnet build ai-gateway/AiGateway.csproj` | Passou, zero erros e 14 avisos |
| `dotnet build ocr-service/OcrService.csproj` | Passou, zero erros e zero avisos |
| `dotnet build ml-service/MlService.csproj` | Passou, zero erros e zero avisos |
| `cd front && yarn build` no diretório original | Passou, 1 min 21 s; mesmo bundle da cópia limpa Linux |
| `yarn build` na cópia Linux com lockfile original | Passou (Vite 7.3.0), 4.725 módulos; 1 min 24 s; aviso de chunks maiores que 500 kB |
| `python3 scripts/smoke_local.py` | Passou com gateway real, SQLite temporário e simuladores HTTP |
| OCR real: `POST /ocr`, PDF criado programaticamente, sem dados reais | HTTP 200; texto extraído: `SYNTHETIC PORTAL TEST 125` |
| ML real: `POST /predict` com carteira `synthetic` e três meses | HTTP 200; três projeções simuladas, offsets 0, 1 e 2 |
| Vite dev: `GET /login` e `GET /src/main.tsx` na cópia Linux | HTTP 200 para a página e para o módulo transformado; sem validação visual em navegador |
| Perfis corrigidos: `dotnet run --no-build --project ...` | OCR e ML iniciaram nas portas padrão esperadas pelo gateway |

O smoke test cobre criação e integridade do SQLite, bootstrap e login, criação/leitura de conhecimento, contexto no prompt, streaming e histórico do chat, avaliações e métricas, upload encaminhado ao simulador OCR, rejeição de sessão inválida e persistência após reinício. Usa diretório temporário e segredos aleatórios descartáveis.

O Yarn registrou avisos de peers para `@mui/system` e `react-is`, além do aviso de workspaces em dependências. Nenhum ajuste de versão foi realizado. O Vite avisou sobre chunks maiores que 500 kB (principal aproximadamente 1,77 MB); otimização de bundle ficou fora desta etapa.

Avisos C# existentes: CS8424 (atributo de cancelamento na interface), CS8618/CS8600/CS8601/CS8602/CS8604 (nulabilidade), CS0219/CS0168 (variáveis não usadas) e CS1998 (async sem await). Não impedem o build e foram mantidos para respeitar o escopo.

## Reprodução

Na raiz, execute os três comandos `dotnet build` da tabela e `python3 scripts/smoke_local.py`. Para o frontend: `cd front`, `yarn install --frozen-lockfile`, `yarn build` e `yarn dev`. O build existente usa Vite/SWC e não executa uma checagem completa de tipos TypeScript.

Para executar os serviços, use terminais separados com `dotnet run --project ocr-service/OcrService.csproj` e `dotnet run --project ml-service/MlService.csproj`. Configure as variáveis do gateway acima e execute `dotnet run --project ai-gateway/AiGateway.csproj --urls http://localhost:9089`. Vite atende em 5173 e encaminha `/api` ao gateway em 9089.

Os processos de teste foram encerrados após as verificações. O SQLite do smoke test foi descartado automaticamente. As saídas detalhadas desta sessão estão em `/tmp/ia-ai-gateway-build.log`, `/tmp/ia-ocr-service-build.log`, `/tmp/ia-ml-service-build.log`, `/tmp/ia-smoke.log`, `/tmp/ia-front-clean-build.log` e `/tmp/ia-front-root-build.log`; esses arquivos temporários não fazem parte do repositório.

Docker/Compose não estavam disponíveis no PATH desta sessão; não foram executados builds de imagens. Não foram validados navegação em navegador, qualidade de IA externa, integrações avançadas, migração de banco ou uso em produção.

## Correção de login nos containers — 30/10/2026

O login falhava ao assinar o JWT com `IDX10720`: a chave configurada tinha 192 bits (24 bytes), abaixo dos 256 bits (32 bytes) exigidos pelo HS256. Substituída apenas a variável `JWT_KEY` no `.env` local, ignorado pelo Git, por segredo aleatório de 64 caracteres hexadecimais. Nenhum segredo foi registrado neste relatório. Acrescentado lembrete de tamanho em `.env.example`.

Recriado somente o gateway com `docker.exe compose up -d --no-deps --force-recreate ai-gateway`; os volumes e contas existentes foram preservados. Confirmado que a configuração efetiva usa 64 bytes. Login validado com HTTP 200 e emissão de JWT em um container temporário da mesma imagem, usando a chave corrigida, conta sintética e SQLite em tmpfs. O container de teste foi removido. O proxy do frontend respondeu HTTP 400 esperado para JSON sem credenciais; uma primeira sondagem sem Content-Type JSON retornou HTTP 415, corrigida na sondagem seguinte. Nenhuma conta real foi consultada para o teste e nenhuma integração de IA foi acionada. A substituição da chave invalida tokens assinados com a chave anterior; é necessário entrar novamente.

## Correção do HTTP 500 no chat dos containers — 30/10/2026

Os logs de `POST /api/ia/ask/stream` mostraram falha na construção de `NomicEmbeddingService`: o modelo `models/embeddings/nomic-v15/model.onnx` não existe. A inspeção da configuração efetiva confirmou `Chat__UseAdvancedRag=true`, proveniente de `USE_ADVANCED_RAG=true` no `.env` local. Isso divergia do modo local solicitado.

Alterada somente essa opção local para `false` e recriado o gateway com `docker.exe compose up -d --no-deps --force-recreate ai-gateway`, preservando SQLite e contas. Confirmada a configuração efetiva `Chat__UseAdvancedRag=false`. Uma requisição sintética com sessão inválida retornou o HTTP 400 esperado tanto diretamente na porta 9089 quanto pelo proxy na porta 9080, demonstrando que o controller é construído sem tentar carregar ONNX; antes a falha ocorria antes da execução da ação. Os logs do gateway recém-criado não apresentaram a exceção. Não foi feita chamada ao provedor de IA nem lido histórico ou conhecimento real nesta verificação; a resposta completa do modelo externo permanece dependente da configuração e disponibilidade do provedor. Nenhum modelo foi instalado e Qdrant/Trino não foram ativados.

## Mudança de escopo: RAG vetorial autorizado — 30/10/2026

Após a validação do modo local, o usuário autorizou explicitamente Qdrant e ONNX e forneceu a pasta dos modelos. Copiados somente os modelos, tokenizers e metadados associados para `models/`, dentro do projeto e ignorada pelo Git. Nenhum banco, documento corporativo ou script de geração anterior foi importado. Tamanhos e hashes SHA-256 foram registrados em `docs/MODELS_MANIFEST.json`.

Alterações: o chat avançado passou a usar `VectorChatContextService`, que recupera conhecimento vetorial, incorpora histórico e contexto documental e não usa o planejador SQL/Trino. A coleção Qdrant é criada quando vazia/inexistente e as gravações/exclusões aguardam confirmação (`wait=true`). Qdrant foi fixado em `v1.20.0`. Acrescentados guia `docs/RAG_LOCAL.md` e teste integrado `tests/RagSmoke`. O modo textual com `USE_ADVANCED_RAG=false` continua disponível.

Validação: build/publish Release e imagem Docker do gateway concluídos. O teste integrado passou com login sintético, SQLite temporário, Qdrant descartável, criação de coleção vazia, embeddings reais de 768 dimensões, indexação/recuperação real, reranking real e streaming com servidor LLM simulado. Foi confirmada a presença do marcador sintético no prompt final. Nenhuma chamada ao Trino ou provedor real foi feita. A primeira execução do Compose build via Docker Windows/WSL encontrou erro de permissão do Buildx Bake para caminho relativo; o build foi executado diretamente com caminhos Windows absolutos. Os avisos C# preexistentes permanecem. Logs temporários: `/tmp/ia-rag-build-release.log` e `/tmp/ia-rag-smoke-final.log`.

Ativação local: `USE_ADVANCED_RAG=true` no `.env`; gateway e Qdrant recriados com `docker.exe compose --profile rag up -d --no-deps --no-build qdrant ai-gateway`. Volumes existentes preservados; instâncias descartáveis de teste removidas. O Trino não foi iniciado. Nenhuma alteração visual, merge ou push realizado.

No ambiente ativo, foi salvo o item sintético `portal_rag_synthetic_demo` (id 2) pela API do frontend; a resposta foi HTTP 200 e os pontos correspondentes foram confirmados no Qdrant por filtro no id criado. O controller de chat carregou ambos os modelos e retornou HTTP 400 esperado para sessão de teste inválida, sem chamar o DeepSeek. O código de demonstração é `PORTAL-RAG-2026`. Não foi feita reindexação em massa de conhecimento pré-existente.

## Auditoria da composição do prompt do chat vetorial — 30/10/2026

Confirmado por comparação SHA-256 que `/app/ragbase/ragbaseinput.txt` e `/app/ragbase/planservices.txt` no gateway ativo são idênticos aos arquivos do repositório. O prompt comportamental ativo é `ragbaseinput.txt`, atualmente com 12 linhas e 992 bytes. `VectorChatContextService` o lê a cada pergunta e acrescenta os trechos recuperados pelo RAG, até 20 mensagens de histórico (máximo de 4.000 caracteres por mensagem), contexto documental quando fornecido (até 12.000 caracteres) e a pergunta atual. Não há varredura automática de pastas para incorporar outros documentos de instruções.

`planservices.txt` pertence ao planejador SQL legado e não participa do fluxo atual do chat vetorial. Em `DeepSeekService.GenerateStreamAsync`, o conteúdo consolidado é enviado como uma única mensagem com `role=user`; ainda não há separação das instruções comportamentais em uma mensagem `system`. Esta etapa foi somente uma inspeção, sem mudar o comportamento ou fazer chamadas ao provedor.
