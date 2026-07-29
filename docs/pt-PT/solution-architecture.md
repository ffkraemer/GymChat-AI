# Solution Architecture

## Arquitetura

A solução segue **Clean Architecture** e **Domain-Driven Design**, construída sobre **ASP.NET Core Minimal APIs**. A ideia central: as regras de negócio (Domain) nunca dependem de frameworks, bases de dados ou APIs externas — as dependências apontam sempre para dentro.

```
GymChatAI.Api            → Endpoints HTTP, mapeamento de pedidos/respostas, políticas de autenticação
GymChatAI.Application     → Casos de uso (handlers), orquestração, portas (interfaces)
GymChatAI.Domain          → Entidades, objetos de valor, regras e invariantes de domínio
GymChatAI.Infrastructure  → EF Core, WhatsApp Cloud API, fornecedores de IA, serviços em segundo plano
```

O `Domain` não tem nenhuma referência a outros projetos. O `Application` depende só do `Domain`. O `Infrastructure` e a `Api` dependem do `Application` e implementam as suas portas (repositórios, `IWhatsAppMessageSender`, `IAIAssistantService`, etc.). Isto significa que toda a lógica de negócio (regras de fidelização, fluxo de onboarding, cálculo de risco de conformidade) pode ser testada isoladamente sem base de dados, sem conta de WhatsApp e sem fornecedor de IA.

## Stack Tecnológica

| Camada | Escolha | Porquê |
|---|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs | Definição de endpoints leve, sem o "boilerplate" de controllers; suporte assíncrono de primeira classe |
| Persistência | SQL Server via EF Core, **ou** em memória | Ver "Modo de Persistência Duplo" abaixo |
| Frontend | React + TypeScript (Vite) | SPA separada, comunica com a API via REST + tokens Bearer |
| Mensagens | WhatsApp Business Cloud API | Integração HTTP direta, sem SDK de terceiros |
| IA | Gemini, OpenAI, ou Azure OpenAI (intercambiáveis) | Ver "Abstração de Fornecedor de IA" abaixo |
| Autenticação | ASP.NET Core Identity (tokens Bearer opacos) | Nativo, tokens encriptados via Data Protection — sem código de JWT escrito à mão |

## Modo de Persistência Duplo

A aplicação consegue correr em dois modos, decididos apenas por a `ConnectionStrings:GymChatDb` estar configurada ou não:

- **Modo SQL Server**: o `MigrateAsync()` aplica migrações EF Core pendentes no arranque; o ASP.NET Core Identity fica ativo (a autenticação exige um repositório real de utilizadores); os dados sobrevivem a reinícios.
- **Modo em memória**: um repositório em memória do processo (`InMemoryDataStore` e alguns repositórios dedicados para funcionalidades mais recentes) serve de base a todos os repositórios; a autenticação fica completamente desativada (o Identity não tem onde persistir utilizadores); os dados perdem-se a cada reinício.

Esta foi uma escolha deliberada desde o primeiro POC: um developer (ou uma demonstração) consegue clonar o repositório e correr a aplicação imediatamente, sem dependências externas, e depois evoluir para SQL Server sem tocar numa única linha de lógica de negócio — cada interface de repositório tem uma implementação `Ef*Repository` e uma `InMemory*Repository`, escolhida pelo `ServiceCollectionExtensions.AddGymChatInfrastructure`.

## Abstração de Fornecedor de IA

`IAIAssistantService` é uma única porta com três implementações intercambiáveis: `GeminiAIAssistantService`, `OpenAIAssistantService`, `AzureOpenAIAssistantService`. O fornecedor ativo é escolhido por configuração (`AiProvider`, ou detetado automaticamente consoante qual chave de API está preenchida). Nenhuma parte da lógica de negócio (grounding por FAQ, histórico de conversa, deteção de idioma) sabe ou precisa de saber qual fornecedor respondeu.

Isto existe por dois motivos concretos encontrados durante o desenvolvimento, não só por flexibilidade teórica:
1. **Descontinuações do lado do fornecedor** — os nomes dos modelos Gemini mudaram a meio do projeto (`gemini-pro` → `gemini-flash-latest`), e ter o fornecedor isolado atrás de uma única interface significou que a correção ficou contida a uma única classe.
2. **Fiabilidade** — uma indisponibilidade ou limite de taxa de um fornecedor não exige uma alteração de código de emergência, só uma troca de configuração.

A IA **não** tem capacidade de *tool calling*/*function calling*, nem está ligada a um pipeline formal de RAG (geração aumentada por recuperação) com *embeddings*/pesquisa vetorial. O *grounding* é mais simples: as FAQs relevantes são encontradas por uma pesquisa básica de relevância textual e injetadas diretamente no prompt, junto com o histórico recente da conversa. Toda a lógica de fluxo (onboarding, preferências de notificação, WhatsApp Flows) é decidida pelo nosso próprio código antes ou depois da chamada à IA, nunca pela própria IA.

## Camada de Integração com o WhatsApp

- `WhatsAppCloudApiClient` — envia todos os tipos de mensagem de saída (texto simples, botões interativos, listas interativas, mensagens de WhatsApp Flow, mensagens de template), comunicando diretamente com a Graph API via `HttpClient`.
- `WhatsAppWebhookMapper` — traduz o JSON em bruto do webhook da Meta em `IncomingWhatsAppMessage`, um formato independente do transporte, para que a camada de Application nunca veja formatos específicos da Meta.
- `WhatsAppFlowEncryptionService` — implementa o protocolo de encriptação do Data Exchange dos WhatsApp Flows (RSA-OAEP para desencriptar a chave AES, AES-128-GCM para o payload, com a inversão obrigatória de bits do IV nas respostas), usando BouncyCastle em vez do `AesGcm` nativo do .NET (que só suporta *nonces* de 12 bytes — o IV da Meta nem sempre respeita essa restrição).
- Um controlo de mensagens repetidas dentro do `WhatsAppCloudApiClient` bloqueia o envio de texto idêntico ao mesmo destinatário dentro de uma janela curta, protegendo o *quality rating* do número contra envios repetidos acidentais (ex: um bug a causar um ciclo de repetição).

## Serviços em Segundo Plano

Duas implementações de `IHostedService` correm continuamente junto com a API:

- `LoyaltyEngineBackgroundService` — corre 15 segundos depois do arranque, depois a cada 24h; avalia as campanhas automáticas de cada gym ativo (Boas-vindas, Aniversário, Reativação) e dispara as mensagens em falta.
- `PendingAIReplyBackgroundService` — corre a cada 3 minutos; tenta outra vez as mensagens que falharam a obter resposta da IA (indisponibilidade do fornecedor, limite de taxa), até 5 tentativas antes de desistir e deixar a conversa escalada para um humano.

## Política de "Soft Delete"

Nada no domínio é apagado à força, com duas exceções restritas:
1. Rascunhos de templates/Flows do WhatsApp que nunca foram submetidos (ou, no caso dos Flows, que só existem localmente sem nunca terem sido criados do lado da Meta) — não há nenhum registo externo a preservar.
2. Coleções filhas próprias, substituídas explicitamente pelo seu agregado raiz (ex: `NotificationPreference.Slots` em `ResetSelections()`, ou `WhatsAppFlow.Screens` em `ReplaceScreens()`) — é uma substituição deliberada, não uma perda acidental em cascata.

Tudo o resto (`Gym`, `Faq`, `Plan`, `Promotion`, `Campaign`, `ClassType`, `Member`, `Lead`, `Conversation`, `WhatsAppMessageTemplate` depois de submetido) usa uma flag `IsActive`/estado com métodos `Activate()`/`Deactivate()`, e todas as chaves estrangeiras entre agregados usam `DeleteBehavior.NoAction` ao nível da base de dados, para prevenir cascatas não intencionais.

## Facilidades de Desenvolvimento e Teste

- `dotnet ef migrations has-pending-model-changes` — apanha uma migração esquecida *antes* de correr a aplicação, em vez de falhar no arranque.
- `POST /api/conversations/reset-for-testing` — fecha a conversa aberta de um contacto de teste (e, opcionalmente, limpa as suas preferências de notificação), só mapeado quando `!Environment.IsProduction()`, para que testar o fluxo de onboarding outra vez não exija SQL manual.
- `GET /health`, `/health/whatsapp`, `/health/ai` — mostram diretamente problemas de expiração de credenciais/tokens, em vez de forçar uma sessão de diagnóstico sempre que uma mensagem falha silenciosamente ao ser enviada.

## Observabilidade e Conformidade

Três registos de auditoria dedicados alimentam o Dashboard de Conformidade do Portal de Administração:
- `WhatsAppApiError` — todas as chamadas *nossas* falhadas à Graph API (autenticação, limites de taxa, pedidos malformados).
- `WhatsAppDeliveryFailure` — falhas de entrega que a própria Meta reporta, depois do facto, via o campo `statuses` do webhook (uma mensagem que enviámos com sucesso mas que mesmo assim não chegou ao destinatário).
- `PendingAIReply` — todos os casos em que a IA falhou a responder de todo.

O Dashboard de Conformidade também consulta diretamente a Graph API da Meta para o `quality_rating` ao vivo do número e o `whatsapp_business_manager_messaging_limit`, e calcula avisos de risco (ex: campanhas de fidelização ativas ainda não ligadas a um template de mensagem Aprovado — a única forma legítima de contactar alguém fora da janela de 24h de atendimento).
