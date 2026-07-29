# Loyalty Engine

## Objetivo

Melhorar a retenção e o envolvimento dos membros, contactando-os automaticamente nos momentos que importam (inscrição, aniversários, ausência prolongada), sem exigir que um operador do ginásio se lembre de o fazer manualmente.

## Modelo Central: Mensagem + Regra de Disparo

Uma `Campaign` é deliberadamente só duas coisas juntas: um template de mensagem e uma regra sobre quando dispara. Quatro tipos, cada um interpretando o campo partilhado `TriggerDayOffset` de forma diferente:

| Tipo | Regra de disparo |
|---|---|
| `Welcome` | `TriggerDayOffset` dias depois de o membro se inscrever (uma vez por membro) |
| `Birthday` | No aniversário do membro, todos os anos (`TriggerDayOffset` não usado) |
| `Reactivation` | Assim que um membro fica inativo há `TriggerDayOffset` dias |
| `Manual` | Nunca dispara sozinha — um operador escolhe os destinatários e dispara à mão a partir do Portal |

O `MessageTemplate` usa placeholders `{FirstName}`, `{FullName}`, `{GymName}`, resolvidos por destinatário pelo `MessageTemplateRenderer` no momento do envio.

## Idempotência

O problema mais difícil, sozinho, num sistema de mensagens agendadas é enviar a mesma mensagem duas vezes por acidente. O `CampaignMessage` existe só para prevenir isso: um registo imutável por cada combinação `(campanha, membro, período)`. Antes de disparar, o `LoyaltyEngineHandler` verifica se já existe um `CampaignMessage` para essa combinação exata; se existir, ignora silenciosamente. O "período" é trivial para Welcome (dispara uma única vez, para sempre, por membro) e para Birthday (uma vez por ano civil), e é a própria data de avaliação no caso de Reactivation.

## Execução

O `LoyaltyEngineBackgroundService` (um `IHostedService`) corre 15 segundos depois da API arrancar, e depois a cada 24 horas. A cada execução, percorre todos os gyms ativos, avalia cada uma das suas campanhas automáticas ativas (`Welcome`/`Birthday`/`Reactivation`) contra os membros do gym, e dispara o que estiver em falta. As campanhas `Manual` nunca são tocadas por este ciclo — só disparam via `POST /api/campaigns/{id}/trigger`, chamado a partir da página de Campanhas do Portal, depois de um operador escolher membros específicos.

## Envio em Conformidade: Templates em Vez de Texto Livre

Esta é a área onde o motor de fidelização se cruza diretamente com as regras de política do WhatsApp (ver `whatsapp-integration.md`). Uma mensagem de fidelização é **iniciada pelo negócio** — nada garante que o destinatário escreveu ao ginásio nas últimas 24 horas, que é a única janela em que a Meta permite texto livre. Enviar texto livre fora dessa janela arrisca rejeição ou, pior, danificar o *quality rating* do número.

O `Campaign.WhatsAppMessageTemplateId` é como isto se resolve: liga uma campanha a um `WhatsAppMessageTemplate` **Aprovado**, e o `LoyaltyEngineHandler` passa a enviar via `SendTemplateMessageAsync` em vez de texto livre — resolvendo as variáveis declaradas do template (por ordem) a partir dos mesmos valores `{FirstName}`/`{FullName}`/`{GymName}` que o renderizador de texto livre usaria. Se uma campanha ainda não estiver ligada, ou estiver ligada a um template ainda não Aprovado, o handler regista um aviso e recorre a texto livre — a campanha continua a enviar (nada falha silenciosamente), mas os avisos de risco do Dashboard de Conformidade continuam a nomear essa campanha até ficar corretamente ligada.

## Visibilidade

A página de Campanhas do Portal mostra cada campanha com o seu estado de ligação a template, permite a um operador ativar/desativar uma campanha sem a apagar, e (para campanhas Manuais) permite escolher destinatários da lista de membros do gym e disparar um envio a pedido. O Dashboard de Conformidade cruza as campanhas ativas com os templates aprovados e nomeia, explicitamente, qualquer campanha que ainda envie texto livre.
