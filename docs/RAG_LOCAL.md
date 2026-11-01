# RAG local com SQLite, Qdrant e ONNX

O chat vetorial usa a base SQLite como fonte dos documentos, Qdrant para recuperar vetores, ONNX para embeddings e reranking, e DeepSeek para gerar a resposta. O fluxo do chat não usa o planejador SQL nem consulta Trino.

## Arquivos locais

Coloque os modelos nesta estrutura dentro da pasta do projeto:

```text
models/
  embeddings/nomic-v15/
    model.onnx
    tokenizer.json
  reranking/
    model.int8.onnx
    tokenizer.json
```

Preserve também eventuais arquivos de pesos externos referenciados pelo ONNX, na mesma estrutura relativa original. Apenas renomear outro modelo para esses nomes não garante compatibilidade. O embedding configurado tem 768 dimensões; entradas, saídas, tokenizer e reranker precisam ser validados com os arquivos efetivamente fornecidos.

A pasta `models/` fica dentro do projeto para execução e é montada como somente leitura em `/app/models` pelo Compose. Ela está ignorada pelo Git: os binários grandes não entram em commits, imagens ou exportações públicas. O código e estas instruções ficam versionados. Não copie bases vetoriais, bancos SQLite ou documentos da aplicação corporativa anterior.

## Ativação após validar os modelos

No `.env` local, configure `USE_ADVANCED_RAG=true`. Inicie Qdrant e o gateway com o perfil RAG:

```powershell
docker compose --profile rag up -d --build
```

O perfil `rag` inicia Qdrant, mas sozinho não altera `USE_ADVANCED_RAG`. Mantenha as duas configurações coerentes. Não é necessário instalar ou iniciar Trino.

Depois de inserir conhecimento sintético no SQLite, faça `POST /api/kb/reindex-all` para gerar e enviar os vetores. A indexação de itens novos também ocorre por `POST /api/kb/upsert` quando o RAG está ativo. Os endpoints `save` e `toggle` existentes não sincronizam automaticamente o índice: execute a reindexação após usá-los. O volume `qdrant_data` guarda o índice entre reinicializações; não o remova para solucionar falhas sem avaliar a perda dos vetores.

Valide inicialmente com documentos sintéticos em um banco e uma coleção isolados. Não reindexe automaticamente dados existentes nem envie conteúdo real ao provedor de IA. Com modelos ausentes ou incompatíveis, mantenha `USE_ADVANCED_RAG=false` até terminar a preparação.

## Modelos e validação

Os modelos fornecidos pelo usuário foram copiados para `models/`, incluindo o embedding `nomic-v15`, o reranker quantizado e os respectivos tokenizers. Arquivos de geração Python da aplicação anterior não foram copiados. Os tamanhos e hashes SHA-256 estão em [MODELS_MANIFEST.json](MODELS_MANIFEST.json); nenhum binário foi adicionado ao Git.

Qdrant foi fixado na versão `v1.20.0`, validada nesta sessão. O teste integrado em [tests/RagSmoke](../tests/RagSmoke/README.md) usa inferência ONNX e busca reais, SQLite temporário e provedor LLM simulado. O fluxo do chat vetorial foi separado do fluxo legado de SQL/Trino. Não foi medida a qualidade das respostas do DeepSeek.

A coleção é criada vazia na primeira consulta, quando necessário; a ausência de documentos gera um contexto explicitamente vazio em vez de provocar falha do chat. As gravações e exclusões de pontos aguardam confirmação do Qdrant antes de retornar.


## Exemplo disponível nesta instalação

Foi criado e indexado um único item identificado como sintético: `portal_rag_synthetic_demo`, título “Exemplo sintetico de recuperacao RAG”. Para testar no chat, pergunte “Qual é o código de validação do RAG?”. O trecho de demonstração contém `PORTAL-RAG-2026`. O gateway e os pontos desse exemplo foram verificados no ambiente Docker ativo, sem chamar o DeepSeek.

A base pré-existente não foi reindexada automaticamente. Novos itens salvos pela tela de conhecimento usam `upsert` e são indexados com o RAG ativo.
