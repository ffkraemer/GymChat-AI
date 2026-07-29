# Domain Model

## Entidades Principais

### Gym
Representa um ginásio (o "tenant" da plataforma). É dono de exatamente um número de telefone do WhatsApp (`WhatsAppPhoneNumberId`) e, opcionalmente, de uma WhatsApp Business Account (`WhatsAppBusinessAccountId`) — necessária antes de se poderem gerir templates ou Flows para esse gym. `IsActive` permite desativação sem apagar dados históricos.

### Member
Um membro registado do ginásio. Guarda `FullName`, `PhoneNumber`, `BirthDate` (dispara a campanha de Aniversário), e `Status` (Active/Inactive/Suspended/Cancelled). `FirstName` é uma propriedade calculada (a primeira palavra do `FullName`), nunca guardada separadamente.

### Lead
Um contacto que já escreveu ao ginásio mas ainda não é membro. Capturado automaticamente da primeira vez que um número desconhecido escreve, com um funil de `Status` (New → Contacted → Qualified → Converted/Lost).

### Conversation
Uma conversa de WhatsApp com um contacto (lead ou membro). Guarda `Status` (Open/WaitingForHuman/Closed), `PreferredLanguage`, e o histórico completo de `Messages`. `EscalateToHuman()`/`ResolveEscalation()` marcam quando a IA falhou e um humano precisa de intervir.

### Message
Uma mensagem, recebida ou enviada, dentro de uma Conversation. `Direction` (Inbound/Outbound), `Origin` (Human/AiAssistant/System), e `Status` (Received/Processing/Sent/Delivered/Read/Failed). `Content` nunca pode estar vazio — uma invariante de domínio que revelou um bug real logo no início: um toque num botão/lista do WhatsApp não traz texto livre nenhum, só o id da resposta interativa, por isso usa-se um texto de substituição legível para esses casos, em vez do texto (vazio) em bruto.

### Campaign
Uma mensagem de fidelização + a sua regra de disparo. `Type` (Welcome/Birthday/Reactivation/Manual) decide como `TriggerDayOffset` é interpretado (dias depois da inscrição, dias de inatividade, ou não usado em Birthday/Manual). `MessageTemplate` usa placeholders `{FirstName}`/`{FullName}`/`{GymName}`. Opcionalmente ligada (`WhatsAppMessageTemplateId`) a um `WhatsAppMessageTemplate` **Aprovado** — necessário para a campanha enviar de forma conforme fora da janela de 24h de atendimento; sem isso, o `LoyaltyEngineHandler` recorre a texto livre (e o Dashboard de Conformidade assinala isso).

### CampaignMessage
Um registo imutável de disparo — uma linha por combinação (campanha, membro, período), existindo só para o `LoyaltyEngineHandler` nunca enviar a mesma campanha ao mesmo membro duas vezes no mesmo período. `Status` (Pending/Sent/Failed).

### Faq
Um par pergunta/resposta na base de conhecimento com que a IA fundamenta as respostas, com uma `Category`. Soft-deletável (`IsActive`).

### Plan
Um plano de adesão (nome, descrição, preço) mostrado a quem pergunta sobre preços.

### Promotion
Uma promoção com prazo limitado (`StartDate`/`EndDate`) que a IA pode referir quando relevante.

### ClassType
Uma categoria de aula que um ginásio oferece (ex: "Yoga", "Spinning") — configurável pelo admin, por gym. Alimenta as opções dinâmicas tanto no menu antigo de botões/listas como nos WhatsApp Flows (`GymClassTypes` como origem de opções de um Flow).

### NotificationPreference
As preferências de notificação de aulas de um contacto, por gym: se já completou o onboarding, se optou por receber, que `ClassType`s escolheu, e uma lista de `NotificationTimeSlot`s (dia + janela horária). `ResetSelections()` limpa tudo para recomeçar o fluxo de preferências do zero.

### NotificationTimeSlot
Um par (dia da semana, janela horária) para o qual um contacto quer notificações. Pertence exclusivamente a uma `NotificationPreference` — nunca é referenciado de forma independente.

### PendingAIReply
Regista uma mensagem que a IA falhou a responder (indisponibilidade do fornecedor, limite de taxa), para o `PendingAIReplyBackgroundService` tentar outra vez mais tarde, em vez da pergunta desaparecer silenciosamente. `Status` (Pending/Resolved/Abandoned), limitado a 5 tentativas.

### WhatsAppApiError
Um registo de auditoria das *nossas* chamadas falhadas à Graph API (falhas de autenticação, pedidos malformados, limites de taxa) — alimenta o painel "falhas ao chamar a API" do Dashboard de Conformidade.

### WhatsAppDeliveryFailure
Um registo de auditoria de uma falha de entrega que a própria Meta reportou, depois do facto, via o campo `statuses` do webhook — distinto do `WhatsAppApiError` porque representa uma mensagem que enviámos com sucesso mas que mesmo assim não chegou ao destinatário (ex: o número não está no WhatsApp, ou bloqueou o negócio).

### WhatsAppMessageTemplate
Um template de mensagem da Meta, gerível a partir do Portal de Administração em vez do Meta Business Manager. Usa a mesma sintaxe de placeholders `{VariávelExemplo}` que `Campaign.MessageTemplate`, traduzida para a sintaxe posicional `{{1}}`, `{{2}}`... da Meta só no momento da submissão. Guarda tanto a `Category` que submetemos como a `ActualCategory` que a Meta atribuiu depois da revisão (que pode ser diferente — a Meta reclassifica templates silenciosamente). `Status` (Draft/PendingApproval/Approved/Rejected/Paused/Disabled); depois de submetido, o corpo torna-se imutável (é preciso criar um template novo em vez de editar).

### WhatsAppFlow
Um WhatsApp Flow (o formulário nativo multi-ecrã da Meta). É dono de uma coleção de `FlowScreen`s (a fonte de verdade editável, construída pelo Flow Designer do Portal) mais o `FlowJson` compilado, realmente enviado à Meta. Guarda uma "fotografia" da `WhatsAppBusinessAccountId` do gym no momento da criação, para o Portal conseguir esconder Flows deixados para trás de uma WABA da qual o gym já mudou, sem os apagar.

### FlowScreen
Um ecrã de um Flow — um contentor de `FlowComponent`s, terminando sempre com exatamente um Rodapé. O `ScreenId` só pode conter letras e underscores (uma restrição da Meta, aplicada tanto do lado do cliente como no construtor do domínio).

### FlowComponent
Um campo/elemento num ecrã: título, texto, campo de texto, lista suspensa, grupo de checkboxes, grupo de botões de opção, ou rodapé. Os componentes de entrada guardam um `VariableName` (a chave sob a qual a resposta aparece na submissão final) e, para componentes de escolha, uma `OptionsSource` (lista fixa escrita pelo admin, ou uma origem dinâmica: `GymClassTypes`/`DaysOfWeek`, resolvida ao vivo pelo endpoint de Data Exchange do Flow). A `FooterAction` (Navigate/Complete) de um componente de Rodapé determina se avança para outro ecrã ou termina o Flow.

### ApplicationUser (Identity)
Um login do Portal de Administração (`IdentityUser<Guid>` + `GymId` + `FullName`). Dois papéis: `Admin` (restrito a um gym via `GymScopeFilter`) e `PlatformAdmin` (um papel transversal, sem gym próprio — `GymId = Guid.Empty` como sentinela — usado para dar acesso a novos gyms e gerir configuração transversal, como IDs de WABA e chaves de encriptação, em nome de qualquer gym).
