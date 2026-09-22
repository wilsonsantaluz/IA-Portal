# Revisão para publicação

Data: 22/09/2026. Escopo: fontes da interface, gateway, OCR, previsão, configuração, prompts, exemplos SQL e inventário dos materiais antigos.

## Estado atual

O portal foi generalizado e sua persistência mudou de SQL Server para SQLite. O modo local inclui base de conhecimento, chat e integração OCR, sem exigir Qdrant, Trino ou ONNX. A geração de respostas continua usando um provedor externo de IA.

| Área | Tratamento |
| --- | --- |
| Identidade | Nome genérico, retirada de logos, renomeação de tema, namespaces, DbContext, chave da base e sessão do navegador. |
| Conhecimento | Prompts e seed genéricos para conhecimento corporativo, consultoria financeira e auditoria de recebíveis. |
| Integrações | Credenciais e endereços internos retirados da configuração ativa; segredos vêm do ambiente. |
| Dados setoriais | Catálogos e esquemas antigos isolados; três recebíveis inteiramente fictícios no exemplo Trino. |
| Materiais antigos | Backups, arquivos compactados, versões, páginas HTML antigas, imagens, respostas e binários isolados em `.private-originals/`. |
| Persistência | EF Core SQLite, arquivo local, criação inicial das tabelas e seed genérico; bootstrap de administrador somente em instalação sem usuários. |
| Chat | Modo local com busca textual e histórico SQLite; RAG avançado preservado como opção. Tratamento de HTTP/erros de streaming e envio do token de login corrigidos. |
| OCR | Botão de documento conectado ao serviço, sessão vinculada por formulário, limite de 20 MB e persistência do texto/análise. |
| Execução | Proxy de API, configuração OCR e volume Docker `portal_data` para persistência. |
| Distribuição | Exclusões em Git/Docker e exportação de fontes com verificação e manifesto SHA-256; arquivos SQLite não entram no pacote. |

## Pendências antes de produção

1. **Credenciais anteriormente expostas:** revogar/trocar as chaves de IA, senhas de bancos e chave JWT dos originais. Remover valores dos arquivos não os revoga.
2. **Autorização incompleta:** vários controllers ainda não possuem `[Authorize]`; endpoints administrativos e históricos precisam validar identidade, permissões e propriedade dos dados no servidor. O token enviado pelo frontend não substitui essas verificações.
3. **SQL por IA no modo avançado:** a limpeza de comentários em `TrinoRestClient` não garante somente leitura. Exigir usuário com leitura limitada e validar instruções, catálogos e limites antes de conectar fontes reais.
4. **Evolução do SQLite:** a criação inicial usa `EnsureCreatedAsync`, sem migrations. Alterações no esquema precisam de estratégia explícita de atualização e backup. SQLite é a escolha temporária local, não uma implantação multi-instância validada. Não houve importação do banco SQL Server antigo.
5. **RAG avançado:** arquivos ONNX/tokenizers são externos e precisam de compatibilidade e revisão de licença. Dados cadastrados no modo local devem ser reindexados ao ativar esse modo.
6. **Previsão simulada:** `ml-service/Program.cs` usa números fixos derivados do índice de mês, não um modelo treinado.
7. **Conteúdo e documentos:** revisar HTML em Markdown, configuração Mermaid, execução/limites do OCR, limpeza de temporários em falhas, logs e mensagens de erro antes de aceitar conteúdo não confiável. Os documentos são enviados ao provedor para análise.
8. **Implantação:** revisar CORS, Swagger, HTTPS, isolamento entre clientes, retenção e backup. Portas publicadas no Compose estão em localhost. A conta de bootstrap não é mecanismo de recuperação de senha.
9. **Licença:** ainda não atribuída. Confirmar titularidade e condições de distribuição.

## Compatibilidade

- `ConnectionStrings__PortalConnection` agora aceita uma conexão SQLite, como `Data Source=data/portal.db;Foreign Keys=True`. Conexões antigas SQL Server não são compatíveis.
- No Docker, `/app/data/portal.db` fica no volume `portal_data`; fora dele, caminhos relativos são resolvidos a partir da raiz de conteúdo do gateway.
- `Bootstrap__AdminEmail` e `Bootstrap__AdminPassword` criam o primeiro administrador. Não alteram usuários existentes. Não há senha padrão.
- `Chat__UseAdvancedRag=false` é o modo padrão. No Compose, use `USE_ADVANCED_RAG=true` com o perfil `rag` para retomar o fluxo vetorial e SQL.
- SQLite armazena o portal; Trino continua sendo uma integração analítica separada.
- A chave de conhecimento genérico é `organization_base`. A API de previsão usa `carteiraId`.
- As páginas HTML antigas foram retiradas; a interface mantida é `front/`.

## Limites da anonimização

A pasta original não possuía `.git`; não foi possível auditar commits ou histórico remoto. A limpeza local não limpa cópias externas. Backups e arquivos compactados foram inventariados e isolados, não restaurados nem certificados como anônimos. Nenhum banco ou serviço da organização original foi consultado.

`.private-originals/` ainda contém material sensível. `.gitignore` não protege um ZIP manual. Distribua somente o resultado de `python scripts/export_public.py`, após revisão. A varredura verifica padrões conhecidos e não prova ausência de toda informação identificável.

## Validação atual

- API com SQLite: compilação aprovada, com 14 avisos existentes de nulabilidade e código não utilizado/assíncrono.
- Frontend: build aprovado; permanece aviso de chunks acima de 500 kB.
- OCR e previsão: compilaram na revisão anterior; seus motores não foram alterados nesta etapa.
- Compose: configuração validada estruturalmente, sem construir ou iniciar containers. O ambiente emitiu aviso de acesso ao arquivo de configuração local do Docker.
- `python scripts/smoke_local.py`: aprovado com API real e SQLite temporário. Verifica criação das tabelas, bootstrap/login, documentação Swagger, cadastro/leitura de conhecimento, streaming, histórico, avaliações, métricas mensais, formulário OCR, rejeição de sessão inválida e persistência após reiniciar. O bootstrap não sobrescreve a senha existente.
- IA e OCR no teste usam substitutos HTTP. Não foram validados inferência real, extração Tesseract, modelos ONNX, Trino ou navegação automatizada no navegador.
- A exportação exclui dados locais, credenciais, dependências, binários e originais privados.
