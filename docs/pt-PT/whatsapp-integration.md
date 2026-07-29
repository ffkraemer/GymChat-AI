# WhatsApp Integration

## Plataforma

WhatsApp Business Cloud API — uma integração HTTP direta com a Graph API da Meta, sem SDK de terceiros nem camada de BSP (Business Solution Provider) pelo meio.

## Entrada: O Webhook

`POST /webhooks/whatsapp/` é o ponto de entrada único para tudo o que a Meta nos envia. O `WhatsAppWebhookMapper` traduz o JSON em bruto da Meta em registos independentes do transporte antes de mais nada lhe tocar, para que o resto do sistema nunca dependa dos formatos específicos de payload da Meta. Quatro coisas distintas chegam pelo mesmo webhook:

1. **Mensagens de texto simples** — o caso comum, encaminhado para o assistente de IA (fundamentado por FAQs) a menos que um passo de menu/Flow/onboarding o intercete primeiro.
2. **Respostas interativas** — um toque num botão ou numa linha de lista. Não traz texto livre nenhum, só o id da opção escolhida; `Message.Content` nunca pode estar vazio (uma invariante de domínio), por isso usa-se um texto de substituição legível (`[Menu: option_id]`) para estes casos no histórico da conversa.
3. **Submissões de Flow** (`interactive.type == "nfm_reply"`) — o conjunto final de respostas de um WhatsApp Flow concluído, entregue como uma string JSON dentro do próprio payload do webhook (um mecanismo separado do endpoint encriptado de Data Exchange — ver abaixo).
4. **Relatórios de estado de entrega** (`statuses`) — a Meta a confirmar, depois do facto, se uma mensagem que enviámos foi mesmo entregue. Esta foi uma lacuna real durante algum tempo: uma mensagem enviada com sucesso podia mesmo assim falhar silenciosamente a chegar ao destinatário (número errado, negócio bloqueado), e não tínhamos visibilidade nenhuma sobre isso até isto ficar ligado — agora alimenta o `WhatsAppDeliveryFailure`, uma das três categorias de falha do Dashboard de Conformidade.

Toda a mensagem recebida é verificada contra o `WhatsAppMessageId` antes de ser processada, para que um reenvio do webhook (a Meta reenvia mesmo) nunca processe a mesma mensagem duas vezes.

## Saída: Tipos de Mensagem

O `WhatsAppCloudApiClient` envia cinco formatos distintos de mensagem, todos através do mesmo endpoint `messages`, só a mudar o campo `type`: texto simples, botões interativos (até 3 opções), listas interativas (até 10 linhas), mensagens de WhatsApp Flow (abre um formulário nativo), e mensagens de template (ver abaixo). Um controlo de mensagens repetidas bloqueia o envio de texto idêntico ao mesmo destinatário dentro de uma janela curta, protegendo o *quality rating* do número contra envios repetidos acidentais.

## A Janela de 24 Horas, e Porque Determina Quase Tudo

O WhatsApp só permite texto livre quando há uma **janela de atendimento ao cliente** aberta — ou seja, o destinatário escreveu ao negócio nas últimas 24 horas. Fora dessa janela, uma mensagem iniciada pelo negócio *tem* de usar um template de mensagem pré-aprovado, ou a Meta rejeita-a (ou, pior, deixa passar silenciosamente enquanto vai danificando o *quality rating* do número ao longo do tempo). Esta única regra é o motivo de existirem duas funcionalidades inteiras:

- **Templates de Mensagem** (abaixo) — para as campanhas de fidelização e outras mensagens iniciadas pelo negócio conseguirem ser enviadas em conformidade fora dessa janela.
- **O Dashboard de Conformidade** — assinalando, pelo nome, qualquer campanha ativa que ainda envie texto livre em vez de um template aprovado.

## Templates de Mensagem

Geridos inteiramente a partir do Portal de Administração em vez do Meta Business Manager: cria-se um rascunho (usando a mesma sintaxe de placeholders `{Variável}` que as campanhas de fidelização), submete-se para revisão da Meta, e acompanha-se o estado (Draft → PendingApproval → Approved/Rejected/Paused/Disabled). Dois detalhes que só surgiram ao testar contra a Meta a sério:

- A Meta pode **reclassificar silenciosamente** a categoria de um template depois da revisão (um template submetido como "Utility" pode voltar como "Marketing") — a `ActualCategory` é sincronizada separadamente do que submetemos, para isto ficar visível em vez de ser uma surpresa silenciosa.
- Depois de submetido, o corpo de um template torna-se imutável — editar significa criar um template novo, não modificar o existente. A eliminação, por isso, só é permitida para rascunhos que nunca chegaram à Meta.

## WhatsApp Flows

O formulário nativo multi-ecrã da Meta — a alternativa mais rica a uma cadeia de menus de botões/listas, suportando seleção múltipla real (`CheckboxGroup`), listas suspensas, e campos de texto livre num único ecrã nativo.

### Porque os Flows existem a par do menu antigo de botões/listas

O menu original de onboarding/preferências (ainda em funcionamento, `OnboardingFlowHandler`) é uma máquina de estados escrita à mão, guiada por botões e listas encadeados — funcional, mas limitado a passos de escolha única e sem seleção múltipla real (contornar isso exigia um ciclo de "queres adicionar mais uma?"). Os Flows resolvem isso como deve ser, ao custo de dois pré-requisitos reais: Verificação de Negócio da Meta, e um protocolo de encriptação nada trivial (abaixo).

### Encriptação

Flows com dados dinâmicos exigem um **endpoint de Data Exchange**, e a Meta só fala com ele encriptado: RSA-OAEP (SHA-256) para desencriptar uma chave AES-128, depois AES-128-GCM para o payload em si, com todos os bits do IV do pedido invertidos para derivar o IV da resposta. O `WhatsAppFlowEncryptionService` implementa isto usando **BouncyCastle**, não o `AesGcm` nativo do .NET — confirmado por teste direto que a classe do .NET só aceita um *nonce* de 12 bytes, enquanto o IV da própria Meta nem sempre respeita essa restrição.

Duas outras lições específicas de encriptação, ao testar contra a Meta a sério:
- O endpoint de **registo da chave de encriptação** (`POST /{phone-number-id}/whatsapp_business_encryption`) está escopado ao **número de telefone**, não à WABA — o único endpoint relacionado com Flows que difere de todos os outros.
- Uma resposta `421` do endpoint de Data Exchange é o sinal da própria Meta de que a desencriptação falhou (chave desatualizada/errada) — não é um código de erro genérico, é assim que o protocolo espera que as falhas sejam comunicadas.

### Navegação multi-ecrã sem estado

Um Flow com vários ecrãs precisa que as respostas de cada ecrã sejam transportadas para o seguinte. Em vez de guardar estado de sessão do lado do servidor, o `FlowJsonCompiler` (em tempo de desenho) liga o Rodapé de cada ecrã para reencaminhar *todas* as respostas recolhidas até ali como parte do seu payload de `navigate` (`{form.X}` para os campos próprios do ecrã, `{data.X}` para tudo transportado de ecrãs anteriores). Isto significa que a lógica em tempo de execução do endpoint de Data Exchange é simples e sem estado: o que quer que a Meta nos envie num pedido `data_exchange` já contém todas as respostas anteriores — só acrescentamos os dados dinâmicos próprios do ecrã seguinte (ex: a lista de aulas do gym) e deixamos o resto passar.

### O Flow Designer

Uma única página unificada do Portal substituiu três páginas anteriores e separadas (lista / editor estruturado / editor de JSON em bruto), depois de testes iniciais com o utilizador mostrarem que a divisão era confusa. Agora oferece:
- Um **modo de Desenho**: construir ecrãs e componentes (título, texto, campo, lista suspensa, grupo de checkboxes, grupo de opções, rodapé) sem código, com uma pré-visualização ilustrativa ao vivo de qualquer elemento selecionado.
- Um **modo JSON**: editar o Flow JSON compilado diretamente, ou carregar um a partir de um ficheiro `.json` — para quem prefere trabalhar mais próximo da própria ferramenta da Meta.
- Um painel de pré-visualização partilhado, que renderiza o ecrã *real* que está a ser construído, independentemente de qual modo o produziu.
- Configuração do *endpoint* e publicação no mesmo ecrã, já que na prática são sempre feitas juntas.

Duas regras de validação específicas da Meta, só descobertas ao testar ao vivo (agora aplicadas do lado do cliente antes de gravar, para evitar uma ida e volta confusa através do próprio validador da Meta): os IDs de ecrã só podem conter letras e underscores (nunca dígitos), e pelo menos um ecrã tem de estar marcado com `terminal: true` **e** `success: true` — duas propriedades separadas que a Meta exige juntas.

## Gestão da WABA (WhatsApp Business Account)

Definir a WABA de um gym (`POST /api/gyms/{gymId}/whatsapp-business-account`) chama automaticamente `POST /{waba-id}/subscribed_apps` em nome da Meta — o "elo em falta" que, de outra forma, teria de ser feito à mão no Graph API Explorer para cada gym novo, e era uma fonte recorrente de problemas do tipo "as mensagens simplesmente não chegam", antes de isto ser automatizado.

## Dashboard de Conformidade

Reúne tudo o que foi descrito acima numa única vista: o *quality rating* ao vivo do número e o nível de mensagens (consultado diretamente à Meta), o nosso próprio histórico de erros em três categorias separadas (falhas de entrega reportadas pela Meta, falhas nossas ao chamar a API, falhas de resposta da IA), e avisos de risco calculados — sobretudo, qualquer campanha de fidelização ativa que ainda envie texto livre em vez de um template aprovado.
