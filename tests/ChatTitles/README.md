# Títulos automáticos — etapa 2

Na raiz do repositório:

```powershell
dotnet run --project tests/ChatTitles
dotnet run --project tests/ChatSessions
dotnet run --project tests/access/SeedSmoke
python scripts/smoke_local.py
npm --prefix front run typecheck
npm --prefix front run build
```

`ChatTitles` usa SQLite temporário, identidades sintéticas, mocks de geração e handler HTTP em memória. Testa upgrade v1→v2, backup, rollback/checksum, contexto mínimo/modelo, títulos automáticos, ausência de regeneração, edição manual antes/durante a chamada, exclusão concorrente, isolamento A/B, fallback, timeout simulado, Unicode, consumo reportado/indisponível e parsing do adapter ativo. Inclui fixture de tentativa `started` para simular interrupção após reserva. Não chama serviços externos nem consome créditos.

O smoke Python inicia o gateway com banco temporário e doubles HTTP de LLM/OCR; verifica título por SSE, consumo real da fixture em tabela separada, fallback sem falhar o chat e persistência após reinício.

Para navegador, siga o setup descartável de [ChatSessions](../ChatSessions/README.md), usando a imagem do gateway construída a partir deste incremento e o frontend existente. Com `IA_SESSION_CREDENTIALS`, `IA_PLAYWRIGHT_MODULE` e `IA_CHROME_PATH` definidos:

```powershell
node tests/ChatTitles/ui.cjs
```

O teste exige uma conta `@example.test`, cria conversas sintéticas e verifica atualização automática do título/lista após resposta e reload, edição manual seguida de nova interação e falha de título com resposta preservada. Não executar sobre o ambiente diário. Captura em `docs/design/screenshots/titles/`. Limpar os containers/processo/arquivo de credenciais conforme o roteiro anterior.
