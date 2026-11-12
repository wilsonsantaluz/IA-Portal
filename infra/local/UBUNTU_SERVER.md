# Migração para Ubuntu Server

Aplicações permanecem em containers. O host usa Docker Engine/Compose,
OpenSSH e systemd. Instalação pelo repositório oficial Docker para a versão real
do Ubuntu; não requer Docker Desktop.
[Documentação Docker](https://docs.docker.com/engine/install/ubuntu/).

`ubuntu-provision.sh` instala Docker em host Ubuntu sem runtime conflitante,
sem apagar dados, trocar firewall ou adicionar acesso público aos serviços.
Gera no servidor uma chave Ed25519 exclusiva para o túnel. A chave privada da
estação de desenvolvimento não deve ser transferida.

`ia-portal-stack.service` inicia o Compose em `/opt/ia-portal` com imagens já
validadas, sem builds/pulls durante boot. `ia-portal-tunnel.service` depende da
stack e mantém os forwards remotos exclusivamente em loopback na VM Oracle.
Configuração privada em `/etc/ia-portal/tunnel`, acesso por usuário de serviço
sem shell e verificação estrita da chave do host. O frontend/API continuam
escutando somente no loopback do Ubuntu; o domínio/TLS permanecem na Oracle.

Antes do corte, testar a chave nova em portas remotas temporárias 29080/29089.
Somente após validar a stack e dados no destino, desativar o autostart Windows e
parar o runner antigo com seu helper de verificação de identidade; iniciar o
túnel definitivo em 19080/19089. Nunca manter dois bancos recebendo gravações.

Dados necessários: `portal_data`, `litellm_audit`, `whatsapp_sessions`, `.env`
privado e ativos ONNX quando presentes. Preservar JWT, usuários, organizações,
conversas, documentos, consumo e perfis de sessão. Usar backup SQLite consistente;
no corte final, interromper escritores e bridge antes de copiar os volumes.
Não executar `down -v`, reset, limpeza de históricos ou exclusão de volumes.

O `.env` transferido deve usar separador `:` no Linux, projeto `ia-portal`,
perfis/overrides existentes e permissões 0600. Não precisa conter credenciais
SSH de provisionamento da estação. Manter o estado habilitado/desabilitado da
bridge, independentemente de preservar o container/perfil de sessão.

Imagens exportadas devem ser conferidas por hash e ID de configuração. Docker
load pode não preservar referências de registry por digest: gerar no destino
um override de imagens locais com tags dos IDs verificados, mantendo manifesto
dos digests originais, em vez de provocar downloads duplicados ou usar `latest`.

Transporte de arquivos grandes pode usar blocos retomáveis sobre SSH, com hash
SHA-256 do arquivo final antes de instalar. Transferir segredos e dados privados
somente por canal criptografado; não incluí-los no pacote público de código.

Validação antes de declarar conclusão: serviços/healthchecks, integridade e
contagens do SQLite, login, preferência Atlas/Groq, uma conversa com streaming,
ledger, leitura dos documentos, sessão da bridge preservada, túnel definitivo,
HTTPS público e ausência de escritores antigos. Só depois parar os demais
containers da estação, preservando imagens/volumes para rollback deliberado.

Estado em 12/11/2026: Docker instalado, código e configuração privada transferidos,
unidades verificadas e túnel de teste em loopback funcionando. Transferência das
imagens/ativos ainda em andamento. Nenhuma declaração de corte concluído.
O host informou aproximadamente 7,3 GiB de RAM utilizável; dimensionar pelo valor
verificado. A conexão Wi-Fi apresentou sinal fraco e retransmissões; cabo de rede
é preferível para a transferência inicial.
