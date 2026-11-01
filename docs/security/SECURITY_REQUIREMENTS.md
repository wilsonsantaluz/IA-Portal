# Segurança — requisitos de bloqueio

## Autenticação e autorização

- Exigir JWT validado para histórico, conversas, preferências e métricas privadas.
- Obter ID do usuário das claims validadas, não de corpo/query/rota como fonte de autoridade.
- Validar propriedade da conversa **em cada endpoint e em cada query de persistência**.
- Exigir política administrativa no servidor para usuários, papéis, permissões, provedores e métricas globais.
- Revisar serialização de entidades de usuário: nunca retornar `PasswordHash` ou dados internos.
- Proteger endpoints existentes antes de habilitar botões de administração.

## Segredos

- Manter `.env` fora do Git; `.env.example` só com placeholders.
- API keys somente no backend; nunca em localStorage, JavaScript entregue ao browser, logs ou JSON de configuração.
- Não armazenar segredos em texto puro no SQLite. Inicialmente usar ambiente; eventual gestão via UI requer criptografia autenticada e chave mestra externa.
- Rotacionar credenciais suspeitas de exposição e documentar procedimento sem registrar valores.

## Proteções de integração

- Validar endpoint do Ollama e URLs configuráveis; mitigar SSRF.
- Limitar tamanho de entrada, uploads e contexto; aplicar timeouts e rate limiting apropriados.
- Tratar respostas de LLM, OCR e documentos como conteúdo não confiável; sanitizar renderização.
- Revisar CORS, Swagger e HTTPS para cada ambiente; não manter política permissiva em produção.
- SQL gerado por IA não deve executar operações destrutivas; exigir enforcement no servidor e credenciais read-only quando aplicável.

## Testes de segurança (bloqueadores)

1. Requisição anônima não acessa histórico nem administração.
2. Usuário A não acessa conversas, mensagens ou consumo de B.
3. Usuário comum não configura provedores nem gerencia outros usuários.
4. Respostas da API não incluem hashes de senha nem chaves.
5. Nenhuma chave aparece no bundle do frontend ou logs de testes.
6. Migração não torna dados legados visíveis para todos.

Não declarar a aplicação pronta para produção sem revisão e execução desses testes.

## Auditoria de 01/11/2026 — lacunas específicas

- `OllamaService.cs`, adapter inativo do chat, contém uma credencial literal. Não copiar/testar/divulgar o valor; exigir revogação/rotação pelo responsável e remover o uso antes de habilitar essa integração. Remover o literal de um novo commit não revoga o segredo nem elimina seu histórico.
- `OpenAiService.cs` grava o payload completo da requisição em arquivo; o adapter Ollama também loga resposta bruta. Não habilitar esses caminhos antes de remover esses registros e sanitizar erros públicos.
- Autorização de sessão deve acontecer antes de SSE/OCR/LLM; o repositório deve revalidar dono em cada escrita, não apenas na abertura da sessão.
- Métricas globais de consumo exigem permissão administrativa específica; não reutilizar automaticamente `dashboard.view`, que atualmente libera métricas globais de avaliações.
- Plano e evidências completos: [AS_IS_AUDIT.md](../architecture/AS_IS_AUDIT.md). Este registro não representa correção já aplicada.
