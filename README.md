# Portal de Conhecimento IA

Portal de conhecimento corporativo com chat, documentos e inteligência artificial, adaptável a consultoria financeira, auditoria de recebíveis e outros setores.

A persistência padrão é **SQLite local**. Usuários, base de conhecimento, sessões, mensagens e avaliações ficam em um arquivo, sem servidor SQL separado. O projeto ainda é um protótipo para uso local; consulte as pendências em [Revisão para publicação](docs/REVISAO_PUBLICACAO.md).

## O que funciona no modo local

- Criação automática do banco e das tabelas na primeira execução.
- Criação do primeiro administrador por configuração, sem senha padrão no código.
- Login JWT, cadastro da base de conhecimento, chat em streaming e histórico persistente.
- Consulta textual à base local para compor o contexto do chat. Seleciona até oito trechos entre os 200 itens ativos mais recentes; não é busca semântica.
- Envio de PDFs e imagens pelo chat, extração via serviço OCR e análise pelo provedor de IA. Texto e análise são gravados no histórico para perguntas posteriores.
- Avaliações de respostas e painel de métricas.

O modo local dispensa Qdrant, Trino e modelos ONNX. As respostas de IA ainda usam o provedor DeepSeek: banco local não significa inferência offline. O conteúdo selecionado da base, histórico e documentos é enviado ao provedor configurado.

## Componentes

| Componente | Responsabilidade |
| --- | --- |
| `front/` | React, TypeScript, Vite, Material UI; Nginx no container. |
| `ai-gateway/` | .NET 8, Entity Framework Core SQLite, chat, conhecimento, login e integração com OCR/IA. |
| SQLite | Arquivo `ai-gateway/data/portal.db` fora do Docker; `/app/data/portal.db` no container. |
| `ocr-service/` | .NET 8 com Tesseract e Poppler para imagens/PDFs. |
| `ml-service/` | Simulação de projeção de carteira; não contém modelo preditivo treinado. |
| RAG avançado opcional | Busca vetorial Qdrant, modelos ONNX, reranking e consultas Trino. |

## Executar com Docker

Requisito: Docker Compose e credencial própria do provedor de IA.

1. Execute `Copy-Item .env.example .env` uma única vez.
2. Preencha o `.env`:

| Variável | Uso |
| --- | --- |
| `DEEPSEEK_API_KEY` | Chave do provedor para chat e análise documental. |
| `JWT_KEY` | Segredo aleatório próprio de pelo menos 32 bytes para assinar tokens. |
| `ADMIN_EMAIL` | E-mail do administrador inicial. |
| `ADMIN_PASSWORD` | Senha inicial com pelo menos 12 caracteres. |
| `USE_ADVANCED_RAG` | Mantenha `false` para o modo local. |

3. Suba os serviços:

```powershell
docker compose config --quiet
docker compose up --build
```

Acesse `http://localhost:9080/login`. Swagger: `http://localhost:9089/swagger`.

O volume Docker `portal_data` preserva o SQLite entre reinicializações. `docker compose down` preserva volumes; não use `down -v` se quiser manter os dados. O bootstrap só cria administrador quando não há usuários: alterar `ADMIN_PASSWORD` depois não redefine uma senha existente.

O OCR é executado no container com os utilitários necessários. No chat, use o botão de documento para enviar PDF, PNG ou JPEG, até 20 MB. O serviço de OCR deve estar disponível e a análise depende do provedor de IA.

## Desenvolvimento fora do Docker

Requisitos: SDK .NET 8 compatível, Node.js 22.12 ou superior e Yarn Classic.

Configure no ambiente do processo do gateway:

- `Jwt__Key`: segredo de assinatura.
- `Bootstrap__AdminEmail` e `Bootstrap__AdminPassword`: conta inicial.
- `DEEPSEEK_API_KEY`: chave do provedor.
- `ConnectionStrings__PortalConnection`: opcional, padrão `Data Source=data/portal.db;Foreign Keys=True`.
- `OCR_SERVICE_URL`: endereço do serviço OCR; padrão `http://localhost:9082`.

O `.env` da raiz é lido pelo Compose, não automaticamente pelo .NET. Caminhos relativos do arquivo SQLite são resolvidos a partir da raiz de conteúdo do gateway.

```powershell
cd ai-gateway
dotnet run --urls http://localhost:9089
```

Em outro terminal:

```powershell
cd front
yarn install --frozen-lockfile
yarn dev
```

Vite usa a porta 5173 e encaminha `/api` para o gateway. `front/.env.example` permite configurar outra origem de API, sem o sufixo `/api`. Nunca coloque segredos em variáveis Vite.

Para subir somente o OCR com Docker:

```powershell
docker compose run --rm -p 127.0.0.1:9082:8080 ocr-service
```

A criação inicial usa `EnsureCreatedAsync`; ela não atualiza o esquema de bancos já existentes. Mudanças futuras no modelo exigem uma estratégia de migrations com preservação de dados. Consulte a [documentação do EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created). A mudança de provedor não importa dados ou backups do SQL Server anterior: a instalação SQLite começa com dados novos.

## RAG avançado opcional

Para retomar o fluxo original de planejamento SQL, busca vetorial e reranking:

1. Disponibilize modelos/tokenizers compatíveis (embeddings de dimensão 768):

```text
models/embeddings/nomic-v15/model.onnx
models/embeddings/nomic-v15/tokenizer.json
models/reranking/model.int8.onnx
models/reranking/tokenizer.json
```

2. Configure `USE_ADVANCED_RAG=true` e execute `docker compose --profile rag up --build`.
3. Configure Trino se usar consultas tabulares; ele é externo ao Compose. `trino/examples/` contém apenas dados sintéticos de demonstração.
4. Reindexe a base pelo endpoint `/api/kb/reindex-all`. Itens salvos em modo local não têm índice vetorial automaticamente.

SQLite continua responsável pela persistência do portal nesse modo. O banco analítico consultado pelo Trino é uma integração separada. Modelos, licenças e configuração do provedor são responsabilidade da implantação.

## Verificação

```powershell
dotnet build ai-gateway/AiGateway.csproj
dotnet build ocr-service/OcrService.csproj
dotnet build ml-service/MlService.csproj
python scripts/smoke_local.py
cd front
yarn build
```

O teste de integração inicia a API real e um SQLite temporário, verifica login, base, chat, histórico, avaliações, encaminhamento OCR e persistência após reiniciar. Provedor de IA e OCR são substitutos HTTP de teste: isso não valida a qualidade do modelo nem a extração real pelo Tesseract. Nenhum banco ou documento real é usado.

## Personalização e publicação

Identidade: `front/src/` e `front/index.html`. Instruções do assistente: `ai-gateway/ragbase/ragbaseinput.txt`. Base inicial: `KnowledgeBaseSeed.cs`. Planejador avançado: `ai-gateway/ragbase/planservices.txt`.

```powershell
python scripts/export_public.py
```

Revise e distribua somente a nova pasta criada em `.publication/`, com manifesto SHA-256. Bancos SQLite, arquivos `.env`, modelos e materiais privados ficam excluídos.

**Não compacte a pasta inteira:** `.private-originals/` contém backups e materiais antigos não anonimizados. As credenciais anteriormente expostas precisam ser trocadas. A autorização de várias APIs ainda precisa ser completada antes de disponibilizar uma instalação na internet.

## Licença

Ainda não definida. Confirme a titularidade e escolha a licença antes de distribuir publicamente. Dependências e modelos mantêm suas respectivas licenças.
