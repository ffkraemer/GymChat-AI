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

        SeedLoyaltyDemoData(store, gym);
    }

    /// <summary>
    /// Seeds a few members and one campaign per automatic type, so the loyalty engine
    /// (see LoyaltyEngineHandler / LoyaltyEngineBackgroundService) has something to act on
    /// without needing real member data. Note: a freshly-seeded member has never checked in
    /// (LastCheckInAtUtc is null), so - by Member.IsInactiveFor's own definition - it also
    /// qualifies for the Reactivation campaign; that's an intentional simplification for the demo,
    /// not a bug.
    /// </summary>
    private static void SeedLoyaltyDemoData(InMemoryDataStore store, Gym gym)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var newMember = new Member(gym.Id, "Ana Silva", "351922000001");
        var birthdayMember = new Member(gym.Id, "Bruno Costa", "351922000002", new DateOnly(today.Year - 30, today.Month, today.Day));
        var inactiveMember = new Member(gym.Id, "Carla Mendes", "351922000003");

        foreach (var member in new[] { newMember, birthdayMember, inactiveMember })
            store.Members[member.Id] = member;

        var campaigns = new[]
        {
            new Campaign(gym.Id, "Boas-vindas", CampaignType.Welcome,
                "Olá {FirstName}! Bem-vindo(a) ao {GymName} 🎉 Qualquer dúvida sobre horários, planos ou aulas, escreve-nos aqui a qualquer momento!",
                triggerDayOffset: 0),
            new Campaign(gym.Id, "Aniversário", CampaignType.Birthday,
                "Parabéns, {FirstName}! 🎂 A equipa do {GymName} deseja-te um excelente dia. Aparece esta semana para umas boas vindas especiais!"),
            new Campaign(gym.Id, "Reativação", CampaignType.Reactivation,
                "Sentimos a tua falta, {FirstName}! Já lá vão uns dias desde a tua última visita ao {GymName}. Queres que te ajudemos a planear o teu regresso?",
                triggerDayOffset: 30),
        };

        foreach (var campaign in campaigns)
            store.Campaigns[campaign.Id] = campaign;
    }
}
