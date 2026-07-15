using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;
using GymChatAI.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace GymChatAI.Infrastructure.Persistence;

/// <summary>Seeds one demo gym + a starter FAQ knowledge base for local/POC testing.</summary>
public static class DemoDataSeeder
{
    public static void Seed(InMemoryDataStore store, IOptions<WhatsAppOptions> whatsAppOptions)
    {
        if (store.Gyms.Any()) return;

        var gym = new Gym(
            name: "GymChat Demo Fitness Club",
            whatsAppPhoneNumberId: whatsAppOptions.Value.DemoPhoneNumberId,
            whatsAppDisplayPhoneNumber: "+351 000 000 000",
            defaultLanguage: Language.Portuguese);

        store.Gyms[gym.Id] = gym;

        var faqs = new[]
        {
            new Faq(gym.Id, "Qual o horário de funcionamento?", "Estamos abertos de segunda a sexta das 07h00 às 22h00, e aos sábados das 09h00 às 14h00.", "Horários"),
            new Faq(gym.Id, "Quais os planos disponíveis?", "Temos o plano Mensal, Trimestral e Anual, todos com acesso a musculação e aulas de grupo. Posso enviar os preços?", "Planos"),
            new Faq(gym.Id, "Como cancelo a minha inscrição?", "Podes cancelar a qualquer momento na receção ou através deste chat, com 30 dias de aviso prévio.", "Planos"),
            new Faq(gym.Id, "Têm aulas de grupo?", "Sim! Temos Yoga, Spinning, CrossTraining e Zumba. O horário completo está disponível na receção e no nosso site.", "Aulas"),
            new Faq(gym.Id, "Existe alguma promoção atual?", "Sim, este mês temos 20% de desconto na inscrição para novos membros que se inscrevam no plano Anual.", "Promoções"),
        };

        foreach (var faq in faqs)
            store.Faqs[faq.Id] = faq;
    }
}
