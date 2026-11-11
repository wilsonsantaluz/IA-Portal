# Cadastro público na demonstração Atlas

Fluxo opt-in em `/signup`: nome, e-mail e senha; a conta só é criada após
confirmação explícita do link enviado por SMTP. O destino é exclusivamente a
organização ativa `atlas-demo`, diferente da organização 1, resolvida no servidor.
IDs, grupos ou permissões enviados pelo cliente não concedem privilégios.

## Grupo Atlas — Colaboração

Permissões: `chat.access`, `kb.view`, `kb.edit`, `dashboard.view`, `data.query`.
Pode usar o próprio chat, consultar base/dashboard/fontes concedidas e criar ou
editar documentos compartilhados. Não recebe administração de usuários, grupos,
canais, provedores, conectores ou organizações; não pode desativar/reativar
documentos nem reindexar toda a base. Não existe endpoint de exclusão de
documentos atualmente. O `upsert` preserva o estado ativo existente e cria novos
documentos ativos para quem só tem `kb.edit`, mesmo com `isActive` forjado.
Administradores e grupos com `kb.manage` mantêm o comportamento anterior.
Histórico e exclusão do próprio chat continuam; chats de terceiros são privados.

Na confirmação, fontes Atlas habilitadas e marcadas `demo` recebem concessão ao
grupo. Outras categorias e fontes criadas posteriormente exigem concessão
administrativa. O modo mínimo continua bloqueando consultas analíticas.
O grupo é criado na primeira confirmação; se suas permissões forem alteradas,
novas confirmações falham até revisão. Cadastro nunca altera contas existentes,
inclusive desativadas, nem concede membership em outras organizações.

## Ativação local

Preencher no `.env` privado:

```dotenv
ATLAS_PUBLIC_SIGNUP=true
PORTAL_PUBLIC_URL=https://seu-dominio-publico.example
SMTP_USERNAME=seu-remetente@gmail.com
SMTP_APP_PASSWORD=
```

A URL deve ser a origem HTTPS pública do túnel, sem caminho, query ou fragmento.
Ela não é inferida de headers do cliente. Configure a senha de app do Google
diretamente no ambiente privado, fora do Git; nunca a senha principal da conta.
SMTP usa `smtp.gmail.com:587` com STARTTLS. Consulte
[SMTP do Google](https://support.google.com/mail/answer/7104828) e
[senhas de app](https://support.google.com/accounts/answer/185833).

Com o mesmo projeto/volumes e overrides já usados:

```powershell
docker compose config --quiet
docker compose up -d --build ai-gateway portal-front
```

Não remover volumes nem recriar SQLite. A migração v10 adiciona a tabela de
solicitações/índices com backup consistente prévio; o diretório de backups deve
ser privado, persistente e gravável. Rollback: binário anterior com uma cópia
completa do backup pré-v10 em outro caminho; reconciliar gravações posteriores.
O binário anterior rejeita ledger 10. Sem Compose, configurar `Signup__Enabled`,
`Signup__PublicUrl`, `Smtp__Username`, `Smtp__AppPassword`. A Atlas deve existir
previamente; o cadastro não cria organizações nem ativa o seed.

## Segurança e limites

- Tokens aleatórios de 256 bits, validade de 30 minutos, consumo transacional.
  SQLite guarda SHA-256 do token e BCrypt da senha, nunca seus valores originais.
- O link usa fragmento, removido da barra/histórico na página. Só o POST explícito
  confirma o cadastro; não registrar corpos de requests/responses em telemetria.
- Resposta genérica para contas existentes; intervalo de dois minutos por e-mail
  pendente. Falha SMTP reconhecida remove a solicitação daquele envio. Novas
  tentativas limpam solicitações expiradas. Recuperação de senha não foi incluída.
- Cinco POSTs por IP a cada dez minutos; status tem limite separado de 60/minuto.
  Limitador em memória, reiniciado com o processo. Não há CAPTCHA nem limitador
  distribuído. Se o túnel apresenta um único IP, o limite será compartilhado;
  não confiar indiscriminadamente em headers para contornar isso.
- Cadastro desligado por padrão. O status informa a flag; somente o POST valida
  existência da Atlas e configuração de URL/remetente. Entrega real pelo Gmail
  depende da configuração do operador e permanece pendente de validação.

## Testes isolados

`dotnet run --project tests/Signup` cobre migração v9→v10, preservação,
backup/restauração, rollback DDL, confirmação, expiração, replay, grupos, fontes e
falha SMTP por double. `python scripts/smoke_signup.py` inicia gateway e banco
temporários para testar confirmação/login/autorização HTTP reais. Ele insere um
token sintético para representar o e-mail entregue; não testa SMTP real.

`node tests/ui/signup.cjs` usa Chromium e fixtures HTTP para formulário,
confirmação explícita em StrictMode, remoção do fragmento, temas e tela estreita.
Exige Playwright (`IA_PLAYWRIGHT_MODULE`) e Chromium (`IA_CHROME_PATH`). Inicia e
encerra Vite em loopback 15174; screenshots em `artifacts/signup-ui/`, fora do
Git. Nenhum teste usa a base em execução, envio real ou LLM pago.
