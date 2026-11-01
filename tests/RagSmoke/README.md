# Teste integrado do RAG

Requer .NET 8 SDK e os modelos locais de `docs/RAG_LOCAL.md`. O teste usa SQLite temporário, um Qdrant descartável e um servidor HTTP local no lugar do DeepSeek. Embeddings e reranking usam os modelos ONNX reais. Não conecta ao Trino, não usa o `.env` e não lê o banco da aplicação.

Execute na raiz, em PowerShell:

```powershell
docker run -d --name portal-rag-test --tmpfs /qdrant/storage -p 127.0.0.1:16333:6333 qdrant/qdrant:v1.20.0
$env:RAG_TEST_QDRANT = "http://127.0.0.1:16333"
dotnet run --project tests/RagSmoke -- (Get-Location).Path
docker rm -f portal-rag-test
```

O endpoint de teste deve apontar a uma instância descartável e vazia. O teste recusa uma instância que já contém coleções e não apaga coleções existentes. Remova o container de teste mesmo se o teste falhar.

Verifica login sintético, chat com índice vazio, indexação e recuperação de um documento sintético, reranking e entrega do trecho ao prompt da resposta em streaming. Os logs incluem apenas conteúdo sintético. O banco temporário é removido ao finalizar.

Também confirma que a sessão é atribuída à conta do JWT, ignorando um `userId` enviado pelo cliente, e que outra conta autenticada não consegue ler o histórico dessa conversa.
