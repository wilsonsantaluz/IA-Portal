# Chat empresarial Atlas

O chat existente recebe fonte e data opcionais; não há segunda interface de
conversa. Selecione uma conexão concedida ao seu grupo. Em Atlas, a referência
sugerida é **01/11/2026**, compatível com o dataset fixo, não o dia atual.

## Perguntas demonstráveis

1. Qual é a política de cobrança da Atlas? — recuperação documental existente.
2. Mostre o aging por faixa de atraso. — snapshot na referência selecionada.
3. Compare faturamento e recebimentos nos últimos seis meses. — emissão versus
   caixa, com estornos subtraídos e faturas canceladas excluídas.
4. Mostre a inadimplência dos últimos seis meses e explique quais ações a
   política de cobrança recomenda. — fechamentos dos seis meses completos
   anteriores ao mês da referência, combinados com a política recuperada.
5. Mostre os dez maiores saldos em aberto. — IDs, valores e concentração,
   sem nomes ou contatos de clientes.
6. Gere um fluxograma Mermaid para contestação de faturas. — documentos e LLM;
   o renderizador não inventa um diagrama substituto em caso de erro.

Perguntas sugeridas ficam em um controle expansível, preservando espaço para a
conversa. Indicadores são resultados de consultas; não existem cartões com
números pré-fabricados. O dashboard de qualidade anterior foi preservado.

## Planejamento e limites

O classificador inicial é determinístico em pt-BR: documental, analítico,
híbrido ou esclarecimento. Não é um classificador LLM genérico. Os planos
permitidos são `aging`, `aging_trend`, `cashflow` e `top_balances`; a autorização
e compilação acontecem no servidor, antes da interpretação pelo modelo.
Simulações, projeções e análises fora desses planos pedem esclarecimento.
O executor não aceita SQL livre, mesmo quando o texto começa com SELECT/WITH.
As rotas antigas `/api/sql/ask` e `/api/sql/ask-rag` rejeitam a operação antes
de chamar qualquer modelo; a rota JSON documental `/api/chatBot/ask` pede uso do
fluxo de fonte/data para perguntas analíticas, sem inventar indicadores.

O período numérico vem dos controles explícitos de fonte/data. O classificador
não interpreta livremente qualquer expressão de data do texto. A resposta sempre
declara a referência efetiva, para que o usuário confira. Não usar esse protótipo
para consultas financeiras sem revisar período, filtros e semântica.

A taxa de inadimplência é vencido dividido pelo **saldo em aberto**, não pelo
faturamento ou número de clientes. Neste dataset todas as faturas de um mês
vencem antes do fechamento mensal; a taxa no fechamento pode ser 100% do saldo
aberto, sem significar que todos os clientes estão inadimplentes. Denominador
zero produz valor ausente. Cashflow usa intervalo de seis meses a partir do
primeiro dia, com fim exclusivo na referência; uma referência no meio do mês
inclui o mês parcial. Os meses completos do trend são declarados separadamente.

Fonte ausente, sem grant, desabilitada, indisponível, vazia ou sem tabelas
compatíveis não produz números inventados. O servidor entrega esclarecimento
local, sem inferência ou tokens fictícios, e usa título de fallback sem chamada
paga. Em chamadas ao modelo, os adapters e a contabilização existentes continuam
ativos; a tabela, o gráfico e a proveniência anexados pelo servidor são conferíveis
independentemente do texto do modelo. Modelos ainda podem errar a interpretação:
não há alegação de validação jurídica/financeira ou de qualidade de um LLM real.

Resultados estruturados de fontes enterprise/public só entram no prompt com
`AllowExternalLlm=true` no perfil aprovado do servidor. Sem isso, a tabela
autorizada é exibida localmente, sem enviar esses resultados ao LLM. A regra
também é conservadora para Ollama local. O comportamento documental anterior
continua usando o provedor configurado; este flag controla os resultados SQL.

Respostas com resultados SQL têm marcador persistido pelo servidor. Elas ficam
visíveis no histórico, mas são omitidas do contexto automático de futuras
chamadas de IA, inclusive caminhos legados/OCR. Isso impede reenvio implícito
após revogação de grant ou alteração de política. Para analisar números novamente,
especifique a fonte e consulte outra vez. Conteúdo recuperado e resultados não
têm autoridade para mudar políticas, perfis, permissões ou os planos executáveis.

## Visualizações

Gráficos financeiros usam Recharts já instalado, dados numéricos finitos e
validação de estrutura/tamanho; a tabela exata fica na resposta e há alternativa
tabular acessível em BRL. O servidor converte centavos apenas para apresentação.
Diagramas Mermaid aceitam tipos conhecidos, têm limites, parsing estrito e
saída em iframe sem scripts/same-origin, com CSP sem rede. Diretivas de configuração,
callbacks e scripts são rejeitados. Um diagrama inválido mostra aviso e código
original. Imagens externas no Markdown não são carregadas automaticamente.

Testes: `tests/BusinessChat`, `tests/Connectors`, `scripts/smoke_local.py --atlas`,
`node tests/ui/atlas-security.cjs` e `tests/ui/atlas.cjs`. Consulte o diário para
os comandos e resultados realmente executados, distinguindo doubles e Trino real.
