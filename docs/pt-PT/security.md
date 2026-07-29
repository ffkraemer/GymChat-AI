# Security

## Autenticação

ASP.NET Core Identity, usando o seu esquema nativo de **token Bearer opaco** — não um JWT escrito à mão. Os tokens são encriptados através do Data Protection do ASP.NET Core, em vez de JWTs assinados/descodificáveis, e só fazem sentido para esta instância específica da aplicação.

- `POST /api/auth/login` — emite um token de acesso + um token de renovação.
- `POST /api/auth/refresh` — renova o token de acesso sem pedir a password outra vez.
- `GET /api/auth/me` — devolve a identidade autenticada (email, nome, `gymId`, papéis), para o frontend nunca ter de descodificar um token sozinho.

A autenticação só está disponível no **modo de persistência SQL Server** — o Identity exige um repositório real e durável de utilizadores, que o modo em memória deliberadamente não fornece (ver `solution-architecture.md`). Correr em memória significa que todos os endpoints ficam abertos, o que é aceitável para desenvolvimento local/demonstrações, mas nunca pode ser como uma implementação real funciona.

## Autorização

Duas políticas, ambas baseadas em papéis:

- **`Policies.Admin`** — qualquer utilizador autenticado com o papel `Admin` ou `PlatformAdmin`. Usado em quase todos os endpoints virados para o Portal.
- **`Policies.PlatformAdmin`** — só `PlatformAdmin`. Reservado para operações ao nível da plataforma: dar início a um gym novo, registar a sua primeira conta de operador, e qualquer ação que precise de atravessar tenants (definir o ID de WABA ou a chave de encriptação de um gym em seu nome, sem precisar do login próprio desse gym).

## Isolamento Multi-Tenant

Todos os endpoints restritos a um gym trazem um parâmetro de rota `{gymId}`, e o `GymScopeFilter` (um `IEndpointFilter`) verifica-o contra a claim `gym_id` de quem chama, antes do pedido chegar ao handler. Um `Admin` normal cuja claim não bata certo com o `gymId` da rota é rejeitado de imediato — não há forma de o admin de um gym ler ou escrever dados de outro gym manipulando um URL. O `PlatformAdmin` contorna esta verificação deliberadamente, já que todo o seu propósito é gestão transversal entre tenants.

Para a mão-cheia de endpoints de escrita que não têm `{gymId}` na rota (ex: ações identificadas pelo ID de um recurso, como publicar um Flow específico), é feita uma verificação de posse equivalente diretamente no handler, comparando o `GymId` próprio do recurso com a claim de quem chama.

## Gestão de Segredos

- Os segredos de desenvolvimento local (token de acesso do WhatsApp, chaves dos fornecedores de IA, connection string do SQL Server) vivem em `dotnet user-secrets`, nunca no `appsettings.json` nem no controlo de versões.
- A chave privada RSA usada para desencriptar os pedidos de Data Exchange dos WhatsApp Flows é gerada com `openssl` e guardada **completamente fora do repositório** (não só num `.gitignore`) — uma escolha estrutural deliberada, feita depois de um quase-incidente anterior em que uma chave de API quase foi submetida ao repositório. Se a chave privada se perder alguma vez sem cópia de segurança, tem de se gerar um par novo e registá-lo outra vez na Meta; as sessões de Flow existentes simplesmente falham até lá (sem dano duradouro, já que as sessões de Flow são de curta duração).
- Numa implementação de produção real, ambas as categorias de segredo acima viveriam num cofre de segredos gerido (ex: Azure Key Vault), não em `user-secrets` — ver `deployment-guide.md`.

## Encriptação dos WhatsApp Flows

O endpoint de Data Exchange (`POST /webhooks/whatsapp/flow-data-exchange`) é, em si, uma superfície sensível à segurança: é um endpoint público, sem autenticação (a Meta chama-o diretamente), protegido inteiramente pelo protocolo de encriptação em vez de um token Bearer. Todo o pedido é encriptado de ponta a ponta com RSA-OAEP + AES-128-GCM; um pedido que falhe a desencriptação é rejeitado com `421` em vez de processado, e nunca se tenta uma desencriptação parcial ou "melhor esforço".

## Acesso entre Origens (CORS)

O Portal de Administração é uma origem de frontend separada (um servidor de desenvolvimento Vite, ou um build estático alojado à parte em produção) a chamar a API pela rede. O `AddCors`/`UseCors` está configurado explicitamente para essa origem — sem isso, o browser bloqueia todos os pedidos antes sequer de chegarem à API, por mais correto que o token Bearer esteja.

## Endpoints de Teste Nunca Alcançáveis em Produção

O `POST /api/conversations/reset-for-testing` (e qualquer outro endpoint de conveniência semelhante, adicionado mais tarde) é mapeado condicionalmente, protegido por `!app.Environment.IsProduction()`. Isto é aplicado ao nível do encaminhamento, não só por não estar documentado — o endpoint genuinamente não existe como rota quando `ASPNETCORE_ENVIRONMENT=Production`.

## Retenção de Dados como Propriedade de Segurança

A política de "soft delete" descrita em `solution-architecture.md` funciona também como salvaguarda de auditoria: nada do que a equipa de um gym faz (desativar uma FAQ, desligar o template de uma campanha) destrói o registo subjacente. Combinado com chaves estrangeiras `NoAction` em todo o lado, uma tentativa de eliminação acidental ou maliciosa falha ruidosamente, em vez de se propagar silenciosamente por dados relacionados.
