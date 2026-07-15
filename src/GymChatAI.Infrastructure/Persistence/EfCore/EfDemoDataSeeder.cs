using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;
using GymChatAI.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymChatAI.Infrastructure.Persistence.EfCore;

public static class EfDemoDataSeeder
{
    public static async Task<Gym> SeedAsync(GymChatDbContext context, IOptions<WhatsAppOptions> whatsAppOptions, CancellationToken cancellationToken = default)
    {
        var existingGym = await context.Gyms.FirstOrDefaultAsync(cancellationToken);
        if (existingGym is not null) return existingGym;

        var gym = new Gym(
            name: "GymChat Demo Fitness Club",
            whatsAppPhoneNumberId: whatsAppOptions.Value.DemoPhoneNumberId,
            whatsAppDisplayPhoneNumber: "+351 000 000 000",
            defaultLanguage: Language.Portuguese);

        context.Gyms.Add(gym);

        context.Faqs.AddRange(
            new Faq(gym.Id, "Qual o horário de funcionamento?", "Estamos abertos de segunda a sexta das 07h00 às 22h00, e aos sábados das 09h00 às 14h00.", "Horários"),
            new Faq(gym.Id, "Quais os planos disponíveis?", "Temos o plano Mensal, Trimestral e Anual, todos com acesso a musculação e aulas de grupo. Posso enviar os preços?", "Planos"),
            new Faq(gym.Id, "Como cancelo a minha inscrição?", "Podes cancelar a qualquer momento na receção ou através deste chat, com 30 dias de aviso prévio.", "Planos"),
            new Faq(gym.Id, "Têm aulas de grupo?", "Sim! Temos Yoga, Spinning, CrossTraining e Zumba. O horário completo está disponível na receção e no nosso site.", "Aulas"),
            new Faq(gym.Id, "Existe alguma promoção atual?", "Sim, este mês temos 20% de desconto na inscrição para novos membros que se inscrevam no plano Anual.", "Promoções"));

        context.Plans.AddRange(
            new Plan(gym.Id, "Mensal", "Acesso total à sala de musculação e aulas de grupo, renovação mensal.", 39.90m),
            new Plan(gym.Id, "Trimestral", "Igual ao plano Mensal, com desconto por pagamento trimestral.", 34.90m),
            new Plan(gym.Id, "Anual", "Igual ao plano Mensal, com o melhor preço por pagamento anual.", 29.90m));

        context.Promotions.Add(
            new Promotion(gym.Id, "20% de desconto na inscrição - Plano Anual",
                "Novos membros que se inscrevam no plano Anual este mês têm 20% de desconto na taxa de inscrição.",
                DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1))));

        await context.SaveChangesAsync(cancellationToken);
        return gym;
    }
}
