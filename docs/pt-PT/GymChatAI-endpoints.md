# GymChat AI — Documentação de Endpoints

Lista completa de todos os endpoints da API, organizados por área funcional, com o que cada um resolve. Serve de base para a documentação mais ampla do projeto (arquitetura, decisões de design, etc.) que vamos construir a seguir.

> Nota: todos os endpoints (exceto os explicitamente marcados como públicos) exigem autenticação (`Policies.Admin`), e são restritos ao gym do utilizador autenticado via `GymScopeFilter` — um Admin normal nunca consegue aceder a dados de outro gym; o `PlatformAdmin` contorna essa restrição deliberadamente.

---

## 1. Webhook do WhatsApp (público — chamado pela Meta)

| Método | Rota | O que resolve |
|---|---|---|
| `POST` | `/webhooks/whatsapp/` | Ponto de entrada único para tudo o que a Meta envia: mensagens de texto, respostas a botões/listas, submissões de Flows (`nfm_reply`), e relatórios de estado de entrega (`statuses`). Processa cada mensagem através do `ProcessIncomingMessageHandler`, com idempotência (uma mensagem com o mesmo `WhatsAppMessageId` nunca é processada duas vezes, mesmo que a Meta reenvie o webhook). |

## 2. Data Exchange de Flows (público — chamado pela Meta, protegido por encriptação)

| Método | Rota | O que resolve |
|---|---|---|
| `POST` | `/webhooks/whatsapp/flow-data-exchange` | Recebe pedidos **encriptados** (RSA-OAEP + AES-128-GCM) sempre que um WhatsApp Flow precisa de dados dinâmicos (ex: a lista de aulas do gym) ou quando a Meta faz o *health check* periódico (`ping`). Devolve sempre uma resposta encriptada em texto simples (nunca JSON). Devolve `421` se não conseguir desencriptar — sinal para a Meta de que a chave pode estar desatualizada. |

## 3. Autenticação (ASP.NET Core Identity)

| Método | Rota | O que resolve |
|---|---|---|
| `POST` | `/api/auth/login` | Login — devolve um token Bearer opaco (encriptado via Data Protection), não um JWT. |
| `POST` | `/api/auth/refresh` | Renova o token sem pedir password outra vez. |
| `GET` | `/api/auth/me` | Devolve email, nome, `gymId` e papéis do utilizador autenticado — usado pelo frontend para saber quem está logado e que gym gerir. |
| `POST` | `/api/auth/register-operator` | Só `PlatformAdmin`: cria uma conta Admin para um gym específico. É assim que a plataforma cria acessos para novos clientes. |

## 4. Gyms

| Método | Rota | O que resolve |
|---|---|---|
| `GET` | `/api/gyms/{whatsAppPhoneNumberId}` | Resolve qual gym é dono de um número de telefone — usado internamente pelo processamento de mensagens. |
| `GET` | `/api/gyms/by-id/{gymId}` | Obtém os dados de um gym pelo seu ID — usado pelo Portal para mostrar/pré-preencher configurações (WABA, etc.) sem depender do número de telefone. |
| `GET` | `/api/gyms/` | Só `PlatformAdmin`: lista todos os gyms — necessário para o seletor de gym nas páginas de Definições/Templates/Flows. |
| `POST` | `/api/gyms/` | Só `PlatformAdmin`: cria um novo gym (onboarding de um cliente novo na plataforma). |
| `POST` | `/api/gyms/{gymId}/whatsapp-business-account` | Define/atualiza a WABA (WhatsApp Business Account) de um gym. Também dispara automaticamente a subscrição do webhook (`POST {WABA}/subscribed_apps`), eliminando um passo manual no Graph API Explorer que causava problemas recorrentes de mensagens não chegarem. |

## 5. FAQs

| Método | Rota | O que resolve |
|---|---|---|
| `GET` | `/api/faqs/{gymId}` | Lista todas as FAQs de um gym (incluindo inativas, para gestão no Portal). |
| `POST` | `/api/faqs/` | Cria uma FAQ nova. |
| `PUT` | `/api/faqs/{id}` | Edita uma FAQ existente. |
| `POST` | `/api/faqs/{id}/activate` / `/deactivate` | Ativa/desativa uma FAQ — nunca apagamos FAQs (política de "soft delete" em toda a base). |

*(Endpoints internos de pesquisa/relevância usados pela IA não são expostos publicamente — a IA consulta o repositório diretamente.)*

## 6. Tipos de Aula (Class Types)

| Método | Rota | O que resolve |
|---|---|---|
| `GET` | `/api/class-types/{gymId}` | Lista os tipos de aula do gym — alimentam tanto o menu antigo (botões/listas) como as opções dinâmicas dos WhatsApp Flows (`GymClassTypes`). |
| `POST` | `/api/class-types/` | Cria um tipo de aula. Nota especial: se quem chama for `PlatformAdmin`, o `gymId` vem explícito no pedido (a WABA de um `PlatformAdmin` não tem gym "próprio"); um Admin normal usa sempre o seu próprio `gymId`, ignorando o que vier no corpo do pedido (protege contra um Admin tentar criar dados para outro gym). |
| `PUT` | `/api/class-types/{id}` | Renomeia. |
| `POST` | `/api/class-types/{id}/activate` / `/deactivate` | Soft delete, como nas FAQs. |

## 7. Membros

| Método | Rota | O que resolve |
|---|---|---|
| `GET` | `/api/members/gym/{gymId}` | Lista os membros de um gym — usado para escolher destinatários ao disparar uma campanha manual. |

## 8. Conversas

| Método | Rota | O que resolve |
|---|---|---|
| `GET` | `/api/conversations/gym/{gymId}` | Lista as conversas de um gym (histórico de mensagens). |
| `GET` | `/api/conversations/gym/{gymId}/history` | Histórico completo, incluindo mensagens antigas. |
| `POST` | `/api/conversations/reset-for-testing` | **Só fora de Produção** (`!IsProduction()`). Fecha a conversa aberta de um número de teste e (opcionalmente) limpa as suas preferências de notificação — elimina a necessidade de repetir SQL manual sempre que se quer testar o fluxo de onboarding do zero. |

## 9. Campanhas (motor de fidelização)

| Método | Rota | O que resolve |
|---|---|---|
| `GET` | `/api/campaigns/gym/{gymId}` | Lista as campanhas de um gym. |
| `POST` | `/api/campaigns/` | Cria uma campanha — mensagem + regra de disparo (`Welcome`, `Birthday`, `Reactivation`, `Manual`). |
| `POST` | `/api/campaigns/{id}/trigger` | Dispara uma campanha manual para uma lista de membros escolhidos — a única forma de enviar uma campanha `Manual` (as outras três disparam sozinhas, por agendamento). |
| `GET` | `/api/campaigns/gym/{gymId}/history` | Histórico de envios (`CampaignMessage`) — idempotência: nunca envia a mesma campanha duas vezes ao mesmo membro no mesmo período. |
| `POST` | `/api/campaigns/{campaignId}/link-template` | Liga (ou desliga, se `templateId` for `null`) uma campanha a um **template aprovado** da Meta. Sem isto, a campanha envia texto livre — que só é permitido dentro da janela de 24h de atendimento; fora dela, arrisca ser rejeitado ou penalizar o *quality rating* do número. É a correção real do aviso permanente que o Dashboard de Conformidade mostrava. |
| `POST` | `/api/campaigns/{campaignId}/activate` / `/deactivate` | Liga/desliga uma campanha sem a apagar. |

## 10. Conformidade (Compliance Dashboard)

| Método | Rota | O que resolve |
|---|---|---|
| `GET` | `/api/compliance/{gymId}` | Devolve o *quality rating* ao vivo (consultado diretamente à Meta), o limite de mensagens atual, e uma lista de **avisos de risco** calculados (quality rating em risco, erros de limite de frequência, volume de erros, campanhas sem template aprovado ligado). |
| `GET` | `/api/compliance/{gymId}/failures` | As três categorias de falha separadas: falhas reportadas pela própria Meta (via webhook de estado de entrega), falhas nossas ao chamar a API, e falhas da IA a gerar resposta. |

## 11. Templates de Mensagem

| Método | Rota | O que resolve |
|---|---|---|
| `GET` | `/api/templates/{gymId}` | Lista os templates do gym, filtrados pela **WABA atual** (esconde templates de uma WABA antiga, ex: depois de trocar de conta de teste para produção, sem apagar o histórico). |
| `POST` | `/api/templates/` | Cria um rascunho local (ainda não existe do lado da Meta). |
| `POST` | `/api/templates/{id}/submit` | Submete o rascunho para aprovação da Meta — a partir daqui, o template deixa de poder ser editado (é assim que a Meta funciona: corpo imutável depois de submetido). |
| `DELETE` | `/api/templates/{id}` | Só permite eliminar **rascunhos** — um template já submetido tem de ficar, faz parte do histórico de qualidade da Meta. |
| `POST` | `/api/templates/{gymId}/refresh-statuses` | Sincroniza o estado (Aprovado/Rejeitado/Pausado) e a **categoria real** atribuída pela Meta — que pode ser diferente da que submetemos (ex: um template pensado como "Utility" pode ser reclassificado como "Marketing"). |

## 12. WhatsApp Flows

| Método | Rota | O que resolve |
|---|---|---|
| `GET` | `/api/flows/{gymId}` | Lista os Flows do gym, também filtrados pela WABA atual. |
| `POST` | `/api/flows/` | Cria um Flow novo na Meta, com um ecrã "placeholder" válido (a Meta exige pelo menos um ecrã com estrutura correta). |
| `POST` | `/api/flows/{id}/publish` | Publica o Flow (deixa de poder ser rascunho). |
| `DELETE` | `/api/flows/{id}` | Elimina um Flow em rascunho — ao contrário dos templates, um Flow existe do lado da Meta desde que é criado, por isso isto chama a Meta para apagar lá também, antes de apagar aqui. Publicados só podem ser descontinuados, nunca apagados. |
| `POST` | `/api/flows/{gymId}/refresh-statuses` | Sincroniza os estados de todos os Flows do gym. |
| `POST` | `/api/flows/{gymId}/encryption-key` | Regista a chave pública RSA — necessário antes de publicar qualquer Flow. Nota importante: este é o único endpoint de Flows escopado ao **número de telefone**, não à WABA (diferente de todos os outros — confirmado só depois de testar contra a Meta a sério). |
| `POST` | `/api/flows/{id}/trigger` | Envia a mensagem que abre o Flow a um número de teste. |
| `GET` / `POST` | `/api/flows/{id}/screens` | Lê/grava o **modelo estruturado** de ecrãs e componentes (o "Desenho" no editor visual) — sem código, sem risco de JSON inválido. |
| `GET` / `PUT` | `/api/flows/{id}/json` | Lê/grava o **JSON em bruto** do Flow — modo alternativo ao editor estruturado, para quem prefere editar/colar diretamente (ou carregar um `.json` de outro sítio). |
| `POST` | `/api/flows/{id}/endpoint` | Define o URL do endpoint de *data exchange* deste Flow — obrigatório antes de publicar, sempre que o Flow tem dados dinâmicos. |

## 13. Saúde das Credenciais

| Método | Rota | O que resolve |
|---|---|---|
| `GET` | `/health` | Confirma se a app está no ar e em que modo de persistência (SQL Server ou em memória). |
| `GET` | `/health/whatsapp` | Confirma se o token de acesso ao WhatsApp ainda é válido — evita perder tempo a diagnosticar "porque é que a IA não responde" quando na verdade é o token que expirou. |
| `GET` | `/health/ai` | O mesmo, mas para a chave do fornecedor de IA ativo (Gemini/OpenAI/Azure). |

---

*Este documento cobre só os endpoints. Os próximos documentos vão explicar as decisões de arquitetura por trás de cada área (porque SQL Server e não só em memória, porque motor de fidelização com idempotência, porque templates e Flows em vez de texto livre, etc.) — diz-me por onde queres continuar.*
