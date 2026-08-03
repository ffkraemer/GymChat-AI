# Glossário de Conceitos — Campanhas, Templates, e Flows

Estes três conceitos confundem-se com frequência porque todos tocam em "enviar algo a um contacto via WhatsApp" — mas cada um resolve um problema diferente, e combinam-se em vez de se substituírem.

## A Versão de Uma Linha

| | O que é | A que pergunta responde |
|---|---|---|
| **Template** | O *texto* aprovado que uma mensagem iniciada pelo negócio pode usar | *"O que é que posso dizer?"* |
| **Campanha** | Uma mensagem + uma regra de **quando** enviar | *"Quando é que digo?"* |
| **Flow** | Um **formulário** nativo multi-ecrã para recolher respostas estruturadas | *"Como recolho uma resposta estruturada, em vez de só mandar texto?"* |

Nenhum funciona sozinho em todas as situações — cada um depende de outra coisa para conseguir chegar a um contacto de forma útil.

## Template de Mensagem

**Para que serve:** A Meta só permite texto livre dentro de uma janela aberta de 24h de atendimento (o contacto escreveu-te recentemente). Fora dessa janela, uma mensagem **iniciada pelo negócio** tem de usar um template pré-aprovado, ou arrisca rejeição / dano ao *quality rating* do número. Um Template é esse texto pré-aprovado, gerido a partir do Portal em vez do Meta Business Manager.

**Sozinho:** Um Template parado, sem uso, não faz nada — é só texto aprovado à espera de ser enviado por algo.

**Ciclo de vida:** Rascunho → submeter para revisão da Meta → Aprovado/Rejeitado/Pausado. Depois de submetido, o corpo não pode ser editado — tem de se criar um Template novo.

## Campanha

**Para que serve:** Automatizar **quando** uma mensagem sai, sem um humano ter de se lembrar e agir. Quatro tipos: `Welcome` (X dias depois da inscrição), `Birthday` (todos os anos), `Reactivation` (depois de X dias inativo), `Manual` (um operador dispara à mão, para destinatários escolhidos).

**Sozinha:** Uma Campanha consegue enviar texto livre — mas só é seguro se for provável que caia dentro de uma janela aberta de 24h, o que mensagens de fidelização (iniciadas pelo negócio, não uma resposta a algo que o contacto acabou de dizer) geralmente não são.

**O que significa "ligar a um Template":** Em vez de enviar o seu próprio corpo de texto livre, a Campanha passa a enviar **através** de um Template Aprovado — o Template fornece o texto conforme e pré-aprovado; a Campanha continua a fornecer a regra de disparo e os valores por destinatário (`{FirstName}`, `{GymName}`, etc.). Uma Campanha sem Template ligado continua a funcionar, mas é exatamente a situação que o Dashboard de Conformidade assinala como risco.

## WhatsApp Flow

**Para que serve:** Recolher uma resposta **estruturada** — escolha múltipla, seleção múltipla, um formulário com vários campos — como um ecrã nativo do WhatsApp, em vez de uma cadeia de toques em botões/listas ou texto livre que a IA teria de interpretar. Usado aqui para as preferências de notificação (que aulas, que dia, que horário).

**Sozinho:** Um Flow tem de ser **disparado** por algo — uma mensagem com um botão de "abrir este Flow", enviada manualmente (a partir do Portal, para testar) ou como parte de alguma outra lógica que construas (ex: uma Campanha ou o menu de onboarding podiam enviar uma mensagem de disparo de Flow em vez de um menu de botões simples, embora essa ligação não seja automática — tem de ser construída para o caso específico).

**Estático vs Dinâmico:** Um Flow cujas perguntas/opções estão inteiramente fixas no desenho é **estático** — sem configuração extra necessária. Um Flow que precisa de dados ao vivo (ex: a lista atual de aulas do gym) é **dinâmico** — tem de ser marcado como tal, e precisa de um URL de endpoint de Data Exchange configurado antes de poder publicar (ver `whatsapp-integration.md`).

## Como se Combinam na Prática

Um exemplo realista: uma **Campanha de Reativação** (dispara depois de 30 dias inativo) está ligada a um **Template Aprovado** ("Sentimos a tua falta, {FirstName}!") para conseguir chegar a alguém em conformidade fora da janela de 24h. Essa mensagem podia, por sua vez, conter uma chamada à ação que leva o contacto a abrir um **Flow** para atualizar as suas preferências de aulas. Três conceitos diferentes, três funções diferentes, a trabalhar juntos — não três nomes para a mesma coisa.
