using System.Text.Json.Serialization;

namespace GymChatAI.Infrastructure.WhatsApp;

internal record SendFlowMessageRequest(
    [property: JsonPropertyName("messaging_product")] string MessagingProduct,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("interactive")] FlowInteractive Interactive)
{
    public static SendFlowMessageRequest Create(string to, string bodyText, string flowCtaButtonText, string metaFlowId, string flowToken, string screenId) =>
        new("whatsapp", to, "interactive", new FlowInteractive(
            "flow",
            new InteractiveBody(bodyText),
            new FlowAction("flow", new FlowActionParameters(
                "3", flowToken, metaFlowId, flowCtaButtonText, "navigate",
                new FlowActionPayload(screenId, new object())))));
}

internal record FlowInteractive(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("body")] InteractiveBody Body,
    [property: JsonPropertyName("action")] FlowAction Action);

internal record FlowAction(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parameters")] FlowActionParameters Parameters);

internal record FlowActionParameters(
    [property: JsonPropertyName("flow_message_version")] string FlowMessageVersion,
    [property: JsonPropertyName("flow_token")] string FlowToken,
    [property: JsonPropertyName("flow_id")] string FlowId,
    [property: JsonPropertyName("flow_cta")] string FlowCta,
    [property: JsonPropertyName("flow_action")] string FlowActionName,
    [property: JsonPropertyName("flow_action_payload")] FlowActionPayload FlowActionPayload);

internal record FlowActionPayload(
    [property: JsonPropertyName("screen")] string Screen,
    [property: JsonPropertyName("data")] object Data);