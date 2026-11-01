# Conversas, sessões e títulos

## Experiência desejada

- Botão **Nova conversa** cria um contexto separado para o usuário autenticado.
- Conversas recentes agrupadas visualmente: Hoje, Ontem, Últimos 7 dias, Anteriores (fuso do usuário para exibição; timestamps UTC persistidos).
- Ao selecionar uma conversa, carregar mensagens paginadas e permitir continuar o contexto.
- Buscar por título; renomear; excluir com confirmação; manter estado correto após reload.
- Mostrar provedor/modelo da conversa e da resposta quando houver troca explícita.
- Nunca mostrar dados de outro usuário nem por manipulação de UUID.

## Ciclo de vida

1. Criar conversa autenticada com título provisório (`Nova conversa`).
2. Enviar mensagem e registrar a requisição com idempotência quando possível.
3. Persistir resposta, status e timestamps; lidar com erro sem duplicar mensagem.
4. Após a primeira resposta bem-sucedida, gerar título curto (ideal 3–8 palavras) com chamada econômica e contexto mínimo.
5. Se geração de título falhar, usar trecho seguro da primeira pergunta; marcar origem `fallback`.
6. Se usuário renomear, marcar `manual`; nunca sobrescrever automaticamente.
7. Registrar consumo do título como `purpose=title`, separado do chat.

## API — contratos sugeridos (adaptar aos existentes)

| Operação | Método/rota ilustrativa | Regra |
| --- | --- | --- |
| Listar minhas conversas | `GET /api/conversations?cursor=...` | Somente usuário autenticado; paginação |
| Criar | `POST /api/conversations` | Dono obtido do JWT |
| Ler conversa | `GET /api/conversations/{id}` | 404/403 sem revelar dados de terceiros |
| Listar mensagens | `GET /api/conversations/{id}/messages` | Propriedade validada; paginação |
| Enviar | `POST /api/conversations/{id}/messages` | Validação, limites, histórico contextual |
| Renomear | `PATCH /api/conversations/{id}` | Marcar título manual |
| Excluir | `DELETE /api/conversations/{id}` | Autorização + semântica documentada |

**Não criar estes endpoints se já houver equivalentes**; preservar compatibilidade ou documentar migração do frontend.

## Testes mínimos

- Usuários A e B: A não lista, lê, altera, exclui nem envia mensagens na conversa de B.
- Histórico preservado após reiniciar API e frontend.
- Título gerado uma vez; título manual preservado; falha usa fallback.
- Paginação estável; ausência de mensagens duplicadas ao repetir requisição.
- Estado vazio, erro de rede e conversa excluída são tratados no frontend.
