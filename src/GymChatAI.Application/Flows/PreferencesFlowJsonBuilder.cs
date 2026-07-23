namespace GymChatAI.Application.Flows;

/// <summary>
/// Builds the Flow JSON for the notification-preferences form: one screen, with a real
/// multi-select checkbox group for class types, a dropdown for the day, and a radio group
/// for the time window - the native-UI equivalent of what OnboardingFlowHandler does with
/// chained buttons/lists, but as a single WhatsApp Flow screen.
///
/// This is a dynamic flow (requires an endpoint): the CheckboxGroup's options come from the
/// gym's ClassTypes, injected by our Data Exchange endpoint in response to the "INIT" action
/// - see WhatsAppFlowDataExchangeHandler.
/// </summary>
public static class PreferencesFlowJsonBuilder
{
    public const string ScreenId = "PREFERENCES";

    public static string Build() => """
    {
      "version": "7.2",
      "data_api_version": "3.0",
      "routing_model": {
        "PREFERENCES": []
      },
      "screens": [
        {
          "id": "PREFERENCES",
          "title": "Preferências de notificações",
          "terminal": true,
          "data": {
            "class_types": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "id": { "type": "string" },
                  "title": { "type": "string" }
                }
              },
              "__example__": [
                { "id": "example-id", "title": "Exemplo" }
              ]
            }
          },
          "layout": {
            "type": "SingleColumnLayout",
            "children": [
              {
                "type": "TextHeading",
                "text": "Sugestões de aulas por WhatsApp"
              },
              {
                "type": "TextBody",
                "text": "Escolhe as aulas e a altura do dia em que preferires receber sugestões. Deixa tudo por marcar se não quiseres receber notificações."
              },
              {
                "type": "CheckboxGroup",
                "name": "selected_classes",
                "label": "Tipos de aula",
                "data-source": "${data.class_types}",
                "required": false
              },
              {
                "type": "Dropdown",
                "name": "preferred_day",
                "label": "Dia da semana preferido",
                "required": false,
                "data-source": [
                  { "id": "1", "title": "Segunda-feira" },
                  { "id": "2", "title": "Terça-feira" },
                  { "id": "3", "title": "Quarta-feira" },
                  { "id": "4", "title": "Quinta-feira" },
                  { "id": "5", "title": "Sexta-feira" },
                  { "id": "6", "title": "Sábado" },
                  { "id": "0", "title": "Domingo" }
                ]
              },
              {
                "type": "RadioButtonsGroup",
                "name": "preferred_window",
                "label": "Altura do dia preferida",
                "required": false,
                "data-source": [
                  { "id": "morning", "title": "Manhã" },
                  { "id": "afternoon", "title": "Tarde" },
                  { "id": "evening", "title": "Noite" }
                ]
              },
              {
                "type": "Footer",
                "label": "Guardar preferências",
                "on-click-action": {
                  "name": "complete",
                  "payload": {
                    "selected_classes": "${form.selected_classes}",
                    "preferred_day": "${form.preferred_day}",
                    "preferred_window": "${form.preferred_window}"
                  }
                }
              }
            ]
          }
        }
      ]
    }
    """;
}
