# AI Architecture

## Componentes

- **Três fornecedores intercambiáveis**: Gemini, OpenAI, e Azure OpenAI (não um modelo único fixo)
- **Grounding por FAQ** (não um pipeline formal de RAG)
- **Sem tool calling / function calling**
- **Multi-idioma**, através de um detetor de idioma leve

## Fornecedores Intercambiáveis, Não um Modelo Único

O assistente de IA vive atrás de uma única porta, `IAIAssistantService`, com três implementações: `GeminiAIAssistantService`, `OpenAIAssistantService`, `AzureOpenAIAssistantService`. Qual deles responde a uma dada mensagem é decidido inteiramente por configuração — ou uma definição explícita `AiProvider`, ou detetado automaticamente consoante qual chave de API está preenchida (verificado por ordem: Gemini, depois OpenAI, com Azure OpenAI como recurso final). Nenhuma parte da lógica de conversa, do *grounding* por FAQ, ou do tratamento de idioma sabe ou precisa de saber qual fornecedor está ativo.

Isto é uma resposta direta a duas coisas encontradas durante o desenvolvimento, não uma preferência teórica:
- **Instabilidade do lado do fornecedor**: os nomes dos modelos do Gemini mudaram a meio do projeto (`gemini-pro` foi descontinuado a favor do `gemini-flash-latest`), e Google, OpenAI e Microsoft já fizeram, cada um, alterações de rutura nas suas APIs segundo o seu próprio calendário, fora do controlo deste projeto.
- **Limites de taxa e indisponibilidades**: um problema pontual de um fornecedor não devia obrigar a um deploy de emergência — só uma alteração de configuração, ou uma nova tentativa automática (ver "Fiabilidade" abaixo).

## Grounding: Pesquisa por FAQ, Não RAG

**Não** há base de dados vetorial, nem *embeddings*, nem um pipeline de geração aumentada por recuperação (RAG) no sentido formal. O *grounding* funciona assim, em vez disso:

1. `IFaqRepository.SearchAsync` corre uma pesquisa básica de relevância textual sobre as FAQs do gym, para a mensagem recebida.
2. Os melhores resultados (limitados a 5) são injetados diretamente no prompt como pares `(pergunta, resposta)`, junto com o nome do gym e o histórico recente da conversa (últimas 10 mensagens).
3. A IA responde usando esse contexto — nunca consulta nada por conta própria.

Isto é uma simplificação deliberada, não um esquecimento: a base de conhecimento de um gym são umas quantas FAQs, planos e promoções — pequena o suficiente para uma simples pesquisa de relevância ser barata e suficientemente precisa, sem precisar do esforço operacional de uma base vetorial. Se a base de conhecimento crescer substancialmente (ex: centenas de documentos por gym), um RAG formal passaria a valer a pena reconsiderar.

## Sem Tool Calling

A IA nunca tem capacidade de chamar funções/ferramentas, e nunca decide *o que o nosso sistema faz* — só decide *o que dizer*. Toda a lógica de fluxo (arrancar o onboarding, avançar um menu de botões/listas, correr um WhatsApp Flow, disparar uma campanha de fidelização) é decidida pelo nosso próprio código da camada de Application, ou antes de a IA sequer ser consultada (o caso mais comum: onboarding, menus, e Flows interceptam todos a mensagem antes de chegar à IA) ou é totalmente independente dela (o serviço em segundo plano do motor de fidelização). Isto mantém o "raio de ação" da IA limitado a "gerar uma resposta de texto fundamentada em factos conhecidos" — não consegue disparar um efeito colateral por acidente.

## Multi-Idioma

O `ILanguageDetector` (implementado por `HeuristicLanguageDetector`) analisa o texto de cada mensagem recebida e classifica-o como Português, Inglês, Espanhol, ou Desconhecido. O idioma detetado é guardado na `Conversation` (`PreferredLanguage`) e passado à IA como parte do seu contexto, para as respostas se manterem no idioma em que o contacto está mesmo a escrever, mesmo que mude a meio da conversa.

## Fiabilidade

Se o fornecedor ativo falhar por completo (indisponibilidade, limite de taxa, chave inválida), a falha é capturada, a conversa é marcada com `EscalateToHuman()`, e a pergunta original é guardada como `PendingAIReply`. O `PendingAIReplyBackgroundService` tenta outra vez automaticamente a cada 3 minutos, até 5 tentativas, antes de desistir e deixar a conversa escalada. Isto significa que um problema pontual do fornecedor atrasa uma resposta, em vez de perder a pergunta por completo.

O `GET /health/ai` reporta se as credenciais do fornecedor ativo ainda são válidas, mostrando diretamente problemas de expiração/revogação de chave, em vez de exigir uma sessão de depuração ao vivo sempre que uma resposta falha silenciosamente a ser gerada.
